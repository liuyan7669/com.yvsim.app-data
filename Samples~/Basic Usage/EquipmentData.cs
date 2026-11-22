using System;
using System.Collections.Generic;

namespace Yvsim.AppData.Samples
{
    /// <summary>普通数据类可以包含数值列表、文字列表和其他普通数据类的列表。</summary>
    [Serializable]
    [AppDataMenu]
    public sealed class EquipmentData
    {
        [UnityEngine.Tooltip("实验使用的设备名称。")]
        public string equipmentName = "教学设备";

        [UnityEngine.Tooltip("勾选表示已经完成设备检查。")]
        public bool inspectionCompleted;

        /// <summary>List&lt;int&gt;：列表的每一项是整数。</summary>
        [UnityEngine.Tooltip("整数列表，初始为 1、2；点击 Get 按钮会添加一个整数。")]
        public List<int> listint = new List<int> { 1, 2 };

        /// <summary>List&lt;string&gt;：列表的每一项是文字。</summary>
        [UnityEngine.Tooltip("文字列表，初始为设备 A、设备 B；点击 Get 按钮会添加新设备。")]
        public List<string> liststring = new List<string> { "设备 A", "设备 B" };

        /// <summary>List&lt;QuizData&gt;：列表的每一项是一个可序列化的普通数据对象。</summary>
        [UnityEngine.Tooltip("普通数据类列表，可展开编辑每项的字段；点击 Get 按钮会添加一份答题数据。")]
        public List<QuizData> listQuizData = new List<QuizData>
        {
            new QuizData { questionCount = 5, correctAnswerCount = 1 }
        };
    }
}
