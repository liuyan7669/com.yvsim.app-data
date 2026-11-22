using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Yvsim.AppData
{
    /// <summary>仅处理运行数据的 JSON 格式与往返完整性，不接触存储。</summary>
    internal static class AppDataSnapshot
    {
        [Serializable]
        private sealed class Document
        {
            public int formatVersion;
            public List<Entry> entries;
        }

        [Serializable]
        private sealed class Entry
        {
            public string type;
            public string json;
            public List<string> nullPaths;
        }

        private static string TypeId(Type type)
        {
            return type.Assembly.GetName().Name + ":" + type.FullName;
        }

        internal static string Serialize(List<object> values)
        {
            var document = new Document { formatVersion = 1, entries = new List<Entry>() };
            var ids = new HashSet<string>();
            foreach (object value in values)
            {
                if (value == null)
                    throw new InvalidDataException("数据列表存在空项，请先移除或重新注入。");
                Type type = value.GetType();
                AppDataJson.ValidateType(type, new HashSet<Type>());
                string id = TypeId(type);
                if (!ids.Add(id))
                    throw new InvalidDataException("数据类型重复：" + id);
                var nulls = new List<string>();
                AppDataJson.CaptureNulls(value, new List<object>(), nulls, "");
                string json = JsonUtility.ToJson(value);
                object probe = JsonUtility.FromJson(json, type);
                AppDataJson.RestoreNulls(probe, nulls);
                if (!AppDataJson.Equivalent(value, probe))
                    throw new NotSupportedException("数据无法完整转换为 JSON：" + id);
                document.entries.Add(new Entry { type = id, json = json, nullPaths = nulls });
            }
            return JsonUtility.ToJson(document, true);
        }

        internal static List<object> Deserialize(string json, List<object> current)
        {
            RequireObject(json);
            Document document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.formatVersion != 1 || document.entries == null)
                throw new InvalidDataException("不支持的运行数据 JSON 格式。");
            var known = new Dictionary<string, int>();
            for (int index = 0; index < current.Count; index++)
            {
                if (current[index] == null)
                    throw new InvalidDataException("当前数据列表存在空项。");
                string id = TypeId(current[index].GetType());
                if (known.ContainsKey(id))
                    throw new InvalidDataException("当前数据类型重复：" + id);
                known.Add(id, index);
            }
            var replacement = new List<object>(current);
            var ids = new HashSet<string>();
            foreach (Entry entry in document.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.type) || !ids.Add(entry.type))
                    throw new InvalidDataException("JSON 数据类型缺失或重复。");
                if (!known.TryGetValue(entry.type, out int index))
                    throw new InvalidDataException("请先注入 JSON 对应的数据类型：" + entry.type);
                Type type = current[index].GetType();
                AppDataJson.ValidateType(type, new HashSet<Type>());
                RequireObject(entry.json);
                object value = JsonUtility.FromJson(entry.json, type);
                if (value == null)
                    throw new InvalidDataException("数据还原为空：" + entry.type);
                AppDataJson.RestoreNulls(value, entry.nullPaths);
                var nulls = new List<string>();
                AppDataJson.CaptureNulls(value, new List<object>(), nulls, "");
                replacement[index] = value;
            }
            return replacement;
        }

        private static void RequireObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") || !json.TrimEnd().EndsWith("}"))
                throw new InvalidDataException("运行数据必须是 JSON 对象。");
        }
    }
}
