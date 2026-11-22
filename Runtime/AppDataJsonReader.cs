using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Yvsim.AppData
{
    /// <summary>分步解析数据快照，只生成临时对象，成功前不修改 AppData 当前保管的对象。</summary>
    internal sealed class AppDataJsonReader
    {
        internal List<object> Result { get; private set; }
        private object value;
        private int builtNodes;

        internal IEnumerable<int> Read(string json, List<object> current, AppDataJsonOperation operation)
        {
            var parser = new AppDataJsonParser(json);
            foreach (int step in parser.Read())
            {
                Progress(operation, 0.2f * parser.Position / json.Length);
                yield return step;
            }
            var document = parser.Value as Dictionary<string, object>;
            if (document == null || !(Get(document, "formatVersion") is AppDataJsonParser.Number version)
                || version.Text != "1" || !(Get(document, "entries") is List<object> entries))
                throw new InvalidDataException("不支持的运行数据 JSON 格式。");

            var known = new Dictionary<string, int>();
            for (int index = 0; index < current.Count; index++)
            {
                if (current[index] == null) throw new InvalidDataException("当前数据列表存在空项。");
                string id = AppDataSnapshot.TypeId(current[index].GetType());
                if (known.ContainsKey(id)) throw new InvalidDataException("当前数据类型重复：" + id);
                known.Add(id, index);
                yield return 0;
            }
            var replacement = new List<object>(current);
            var ids = new HashSet<string>();
            var checkedTypes = new HashSet<Type>();
            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index] as Dictionary<string, object>;
                string id = entry == null ? null : Get(entry, "type") as string;
                if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidDataException("JSON 数据类型缺失或重复。");
                if (!known.TryGetValue(id, out int target)) throw new InvalidDataException("请先注入 JSON 对应的数据类型：" + id);
                Type type = current[target].GetType();
                AppDataJson.ValidateType(type, checkedTypes);
                string dataJson = Get(entry, "json") as string;
                var dataParser = new AppDataJsonParser(dataJson);
                foreach (int step in dataParser.Read())
                {
                    Progress(operation, 0.2f + 0.8f * (index + 0.4f * dataParser.Position / dataJson.Length) / entries.Count);
                    yield return step;
                }
                if (!(dataParser.Value is Dictionary<string, object>)) throw new InvalidDataException("运行数据必须是 JSON 对象：" + id);
                builtNodes = 0;
                foreach (int step in Build(type, dataParser.Value))
                {
                    Progress(operation, 0.2f + 0.8f * (index + 0.4f + 0.4f * Math.Min(1f, (float)builtNodes / dataParser.NodeCount)) / entries.Count);
                    yield return step;
                }
                object restored = value;
                object paths = Get(entry, "nullPaths");
                if (paths != null)
                {
                    if (!(paths is List<object> nullPaths)) throw new InvalidDataException("空值位置需要是字符串数组。");
                    foreach (object item in nullPaths)
                    {
                        if (!(item is string path) || string.IsNullOrEmpty(path)) throw new InvalidDataException("JSON 数据中的空值路径不合法。");
                        string[] segments = path.Split('/');
                        if (segments.Length > 128) throw new InvalidDataException("空值路径的嵌套层数超过 128。");
                        AppDataJson.RestoreNullAt(restored, segments, 0);
                        yield return 0;
                    }
                }
                // 同时检查缺失字段的初始值，防止它们带入循环引用、派生实例或非有限数字。
                foreach (int step in AppDataJson.ValidateValue(restored, new List<object>())) yield return step;
                replacement[target] = restored;
                Progress(operation, 0.2f + 0.8f * (index + 1f) / entries.Count);
                yield return 0;
            }
            Result = replacement;
        }

        private IEnumerable<int> Build(Type type, object node)
        {
            builtNodes++;
            yield return 0;
            if (node == null)
            {
                if (type.IsValueType) throw new InvalidDataException("值类型字段不能为 null：" + type.FullName);
                value = null;
                yield break;
            }
            if (type == typeof(string))
            {
                if (!(node is string)) throw new InvalidDataException("字符串字段的 JSON 类型不匹配。");
                value = node;
                yield break;
            }
            if (type == typeof(bool))
            {
                if (!(node is bool)) throw new InvalidDataException("布尔字段的 JSON 类型不匹配。");
                value = node;
                yield break;
            }
            if (type.IsPrimitive || type.IsEnum)
            {
                if (!(node is AppDataJsonParser.Number number)) throw new InvalidDataException("数字字段的 JSON 类型不匹配：" + type.FullName);
                Type target = type.IsEnum ? Enum.GetUnderlyingType(type) : type == typeof(char) ? typeof(ushort) : type;
                try
                {
                    object converted = Convert.ChangeType(number.Text, target, CultureInfo.InvariantCulture);
                    if (converted is float single && (float.IsNaN(single) || float.IsInfinity(single))
                        || converted is double real && (double.IsNaN(real) || double.IsInfinity(real)))
                        throw new OverflowException();
                    value = type.IsEnum ? Enum.ToObject(type, converted) : type == typeof(char) ? (object)(char)(ushort)converted : converted;
                }
                catch (Exception error) when (error is FormatException || error is OverflowException)
                {
                    throw new InvalidDataException("JSON 数字超出范围或不能转换为 " + type.FullName + "。", error);
                }
                yield break;
            }
            if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (!(node is List<object> items)) throw new InvalidDataException("容器字段需要 JSON 数组：" + type.FullName);
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                IList list = type.IsArray ? Array.CreateInstance(element, items.Count) : (IList)Activator.CreateInstance(type);
                for (int index = 0; index < items.Count; index++)
                {
                    foreach (int step in Build(element, items[index])) yield return step;
                    if (type.IsArray) list[index] = value;
                    else list.Add(value);
                }
                value = list;
                yield break;
            }
            if (!(node is Dictionary<string, object> members)) throw new InvalidDataException("数据对象需要 JSON 对象：" + type.FullName);
            // 只让 Unity 创建固定的空对象，保留缺失字段的既有初始化规则及无参构造函数非必需的行为。
            // 实际 JSON 的解析和字段填充都由上面的分步逻辑完成，不交给原生解析器一次处理。
            object result = JsonUtility.FromJson("{}", type);
            if (result == null) throw new InvalidDataException("无法创建数据对象：" + type.FullName);
            foreach (var field in AppDataJson.Fields(type))
            {
                if (members.TryGetValue(field.Name, out object child))
                {
                    foreach (int step in Build(field.FieldType, child)) yield return step;
                    field.SetValue(result, value);
                }
                yield return 0;
            }
            value = result;
        }

        private static object Get(Dictionary<string, object> members, string name)
        {
            members.TryGetValue(name, out object result);
            return result;
        }

        private static void Progress(AppDataJsonOperation operation, float progress)
        {
            if (operation != null) operation.Progress = Math.Max(operation.Progress, Math.Min(progress, 0.99f));
        }
    }
}
