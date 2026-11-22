using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Yvsim.AppData
{
    /// <summary>逐项写入 JSON；每次 yield 是一份小工作，外层协程决定何时让出一帧。</summary>
    internal sealed class AppDataJsonWriter
    {
        internal readonly StringBuilder Output = new StringBuilder();
        private readonly List<object> ancestors = new List<object>();
        private readonly List<string> nullPaths = new List<string>();

        internal IEnumerable<int> Write(List<object> values, AppDataJsonOperation operation)
        {
            Output.Append("{\"formatVersion\":1,\"entries\":[");
            var ids = new HashSet<string>();
            var checkedTypes = new HashSet<Type>();
            for (int index = 0; index < values.Count; index++)
            {
                object value = values[index];
                if (value == null)
                    throw new InvalidDataException("数据列表存在空项，请先移除或重新注入。");
                Type type = value.GetType();
                AppDataJson.ValidateType(type, checkedTypes);
                string id = AppDataSnapshot.TypeId(type);
                if (!ids.Add(id))
                    throw new InvalidDataException("数据类型重复：" + id);
                yield return 0;

                if (index > 0) Output.Append(',');
                Output.Append("{\"type\":");
                foreach (int step in WriteString(new JsonText(Output, false), id)) yield return step;
                Output.Append(",\"json\":");
                Output.Append('"');
                var dataJson = new JsonText(Output, true);
                nullPaths.Clear();
                foreach (int step in WriteValue(dataJson, value, type, "")) yield return step;
                // 直接把内层 JSON 转义写入最终缓冲区，不复制整个数据类的 JSON。
                Output.Append('"');
                Output.Append(",\"nullPaths\":[");
                for (int item = 0; item < nullPaths.Count; item++)
                {
                    if (item > 0) Output.Append(',');
                    foreach (int step in WriteString(new JsonText(Output, false), nullPaths[item])) yield return step;
                }
                Output.Append("]}");
                if (operation != null)
                    operation.Progress = (index + 1f) / (values.Count + 1f);
                yield return 0;
            }
            Output.Append("]}");
        }

        private IEnumerable<int> WriteValue(JsonText output, object value, Type declaredType, string path)
        {
            yield return 0;
            if (value == null)
            {
                nullPaths.Add(path);
                output.Append("null");
                yield break;
            }
            Type type = value.GetType();
            if (type != declaredType)
                throw new NotSupportedException("JSON 数据包含派生实例：" + path);
            if (value is string text)
            {
                foreach (int step in WriteString(output, text)) yield return step;
                yield break;
            }
            if (value is float single && (float.IsNaN(single) || float.IsInfinity(single))
                || value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                throw new NotSupportedException("JSON 数据不支持 NaN 或 Infinity：" + path);
            if (type.IsPrimitive || type.IsEnum)
            {
                if (value is bool flag) output.Append(flag ? "true" : "false");
                else if (value is char character) output.Append(((int)character).ToString(CultureInfo.InvariantCulture));
                else if (type.IsEnum) output.Append(Convert.ToString(Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                else if (value is float f) output.Append(f.ToString("R", CultureInfo.InvariantCulture));
                else if (value is double d) output.Append(d.ToString("R", CultureInfo.InvariantCulture));
                else output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                yield break;
            }
            if (ancestors.Exists(item => ReferenceEquals(item, value)))
                throw new NotSupportedException("JSON 数据存在循环引用：" + path);
            ancestors.Add(value);
            if (value is IList list)
            {
                output.Append('[');
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                for (int index = 0; index < list.Count; index++)
                {
                    if (index > 0) output.Append(',');
                    string childPath = path.Length == 0 ? index.ToString() : path + "/" + index;
                    foreach (int step in WriteValue(output, list[index], element, childPath)) yield return step;
                }
                output.Append(']');
            }
            else
            {
                output.Append('{');
                bool first = true;
                foreach (var field in AppDataJson.Fields(type))
                {
                    if (!first) output.Append(',');
                    first = false;
                    foreach (int step in WriteString(output, field.Name)) yield return step;
                    output.Append(':');
                    string childPath = path.Length == 0 ? field.Name : path + "/" + field.Name;
                    foreach (int step in WriteValue(output, field.GetValue(value), field.FieldType, childPath)) yield return step;
                }
                output.Append('}');
            }
            ancestors.RemoveAt(ancestors.Count - 1);
        }

        // 长文本每写入最多 128 字符就交还控制权。
        private static IEnumerable<int> WriteString(JsonText output, string text)
        {
            int length = text.Length;
            output.Append('"');
            for (int index = 0; index < length; index++)
            {
                char character = text[index];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32 || char.IsSurrogate(character))
                            output.Append("\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
                if ((index + 1) % 128 == 0) yield return 0;
            }
            output.Append('"');
            yield return 0;
        }

        /// <summary>内层 JSON 作为字符串写入外层时，顺手转义，避免再遍历和复制整份 JSON。</summary>
        private sealed class JsonText
        {
            private readonly StringBuilder buffer;
            private readonly bool escaped;

            internal JsonText(StringBuilder buffer, bool escaped)
            {
                this.buffer = buffer;
                this.escaped = escaped;
            }

            internal void Append(string text)
            {
                foreach (char character in text) Append(character);
            }

            internal void Append(char character)
            {
                if (escaped && (character == '\\' || character == '"')) buffer.Append('\\');
                buffer.Append(character);
            }
        }
    }
}
