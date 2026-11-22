using System;
using UnityEngine;

namespace Yvsim.AppData
{
    /// <summary>一次分帧 JSON 导出或还原，可用 yield return 等待，也可每帧读取进度。</summary>
    public sealed class AppDataJsonOperation : CustomYieldInstruction
    {
        /// <summary>是否已经结束；成功、失败或容器停用都会结束。</summary>
        public bool IsDone { get; private set; }

        /// <summary>参考进度，范围 0 到 1；导出按完成的数据类型推进，导入结合已读取的字符与字段处理推进，部分阶段可能暂时不变。结束时为 1，不代表成功，也不是剩余时间估算。</summary>
        public float Progress { get; internal set; }

        /// <summary>导出成功后生成的 JSON；尚未结束、失败或执行导入时为 null。请先检查 Error。</summary>
        public string Json { get; private set; }

        /// <summary>失败或容器停用的原因；成功时为 null。调度参数错误会在调用 ToJsonAsync 或 FromJsonAsync 时直接抛出。</summary>
        public Exception Error { get; private set; }

        /// <summary>尚未结束时继续等待，供 Unity 协程使用。</summary>
        public override bool keepWaiting { get { return !IsDone; } }

        internal AppDataJsonOperation() { }

        internal void Complete(string json, Exception error)
        {
            if (IsDone)
                return;
            Json = json;
            Error = error;
            Progress = 1f;
            IsDone = true;
        }
    }
}
