using System;
using System.Collections.Generic;

namespace Yvsim.AppData
{
    /// <summary>仅处理运行数据的 JSON 格式，不接触存储。</summary>
    internal static class AppDataSnapshot
    {
        internal static string TypeId(Type type)
        {
            return type.Assembly.GetName().Name + ":" + type.FullName;
        }

        internal static string Serialize(List<object> values)
        {
            var writer = new AppDataJsonWriter();
            // 同步导出一次跑完同一套写入步骤，不还原、不比较。
            foreach (int step in writer.Write(values, null)) { }
            return writer.Output.ToString();
        }

        internal static List<object> Deserialize(string json, List<object> current)
        {
            var reader = new AppDataJsonReader();
            foreach (int step in reader.Read(json, current, null)) { }
            return reader.Result;
        }
    }
}
