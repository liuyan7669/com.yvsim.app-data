using System;

namespace Yvsim.AppData.Samples
{
    /// <summary>答题实验的数据示例，放入 AppData 后可在 Inspector 查看。</summary>
    [Serializable]
    [AppDataMenu]
    public sealed class QuizData
    {
        /// <summary>本次实验共有多少道题，示例默认是 10 道。</summary>
        [UnityEngine.Tooltip("本次实验的题目总数，初始为 10 道。")]
        public int questionCount = 10;

        /// <summary>当前已经答对多少道题，示例修改按钮每次最多加一。</summary>
        [UnityEngine.Tooltip("本次实验已经答对的题数。")]
        public int correctAnswerCount;
    }
}
