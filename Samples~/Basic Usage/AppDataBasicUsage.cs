using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Yvsim.AppData.Samples
{
    /// <summary>每个按钮直接演示一个用法；AppData 单独放在另一个物体上。</summary>
    public sealed class AppDataBasicUsage : MonoBehaviour
    {
        [Tooltip("显示两类数据当前的字段值。")]
        [SerializeField] private Text dataText;
        [Tooltip("显示刚刚执行了什么操作。")]
        [SerializeField] private Text resultText;
        [Tooltip("显示同步或分帧导出的 JSON 字符串，不写入文件。")]
        [SerializeField] private Text jsonText;
        [Tooltip("按钮区域；分帧导出或还原期间暂时禁用，避免中途修改数据或重复操作。")]
        [SerializeField] private CanvasGroup buttons;
        private string json;

        private void Start()
        {
            SetData();
        }

        /// <summary>Set 存入两类初始数据，已有同类型数据时整体替换。</summary>
        public void SetData()
        {
            AppData.Set(new QuizData());
            AppData.Set(new EquipmentData());
            ShowState("Set：已存入两类初始数据。");
        }

        /// <summary>Contains 只检查有没有这类数据，不创建或修改数据。</summary>
        public void CheckData()
        {
            bool quizExists = AppData.Contains<QuizData>();
            bool equipmentExists = AppData.Contains<EquipmentData>();
            ShowState($"Contains：QuizData = {quizExists}，EquipmentData = {equipmentExists}");
        }

        /// <summary>Get 取得当前对象，直接修改字段，不需要再次 Set。</summary>
        public void ModifyData()
        {
            QuizData quiz = AppData.Get<QuizData>();
            quiz.correctAnswerCount = Mathf.Min(quiz.questionCount, quiz.correctAnswerCount + 1);
            EquipmentData equipment = AppData.Get<EquipmentData>();
            equipment.inspectionCompleted = true;
            equipment.listint.Add(equipment.listint.Count + 1);
            equipment.liststring.Add("新设备");
            equipment.listQuizData.Add(new QuizData { questionCount = 5, correctAnswerCount = 2 });
            ShowState("Get：修改字段，并向三种 List 各添加一项。");
        }

        /// <summary>Set 传入一个新对象，整体替换 QuizData，EquipmentData 不变。</summary>
        public void ReplaceData()
        {
            AppData.Set(new QuizData { questionCount = 20, correctAnswerCount = 10 });
            ShowState("Set：用新对象替换答题数据，总题数 20，答对 10。");
        }

        /// <summary>Reset 指定类型，只重置答题数据。</summary>
        public void ResetQuiz()
        {
            AppData.Reset<QuizData>();
            ShowState("Reset<QuizData>：答题数据恢复初始值，设备数据不变。");
        }

        /// <summary>ResetAll 重置全部已存入的数据。</summary>
        public void ResetAll()
        {
            AppData.ResetAll();
            ShowState("ResetAll：两类数据都恢复初始值。");
        }

        /// <summary>ToJson 导出当前数据，字符串只放在本示例内存中。</summary>
        public void ExportJson()
        {
            json = AppData.ToJson();
            jsonText.text = json;
            ShowState("ToJson：已记录当前数据；继续修改，再点 FromJson 可还原。");
        }

        /// <summary>ToJsonAsync 返回操作对象，协程可等待它完成。</summary>
        public void ExportJsonAsync()
        {
            StartCoroutine(ExportInFrames());
        }

        private IEnumerator ExportInFrames()
        {
            buttons.interactable = false;
            // 每批最多 32 份小工作，两批相隔 1 帧。大 List 的元素和长文本也会分批。
            AppDataJsonOperation operation = AppData.ToJsonAsync(frameInterval: 1, chunkSize: 32);
            while (!operation.IsDone)
            {
                resultText.text = $"ToJsonAsync：正在导出 {operation.Progress:P0}，请等待。";
                yield return null;
            }
            // 不需要显示进度时，上面的循环可以直接换成 yield return operation。
            buttons.interactable = true;
            if (operation.Error != null)
            {
                resultText.text = "导出失败：" + operation.Error.Message;
                yield break;
            }
            json = operation.Json;
            jsonText.text = json;
            ShowState("ToJsonAsync：分帧导出完成，可用 FromJson 还原。");
        }

        /// <summary>FromJson 用之前导出的字符串还原数据，成功后重新 Get。</summary>
        public void ImportJson()
        {
            if (string.IsNullOrEmpty(json))
            {
                resultText.text = "请先点击 ToJson 或 ToJsonAsync，生成一份 JSON。";
                return;
            }
            AppData.FromJson(json);
            ShowState("FromJson：已还原为上次导出 JSON 时的数据。");
        }

        /// <summary>FromJsonAsync 分帧还原，等待成功后重新 Get 更新画面。</summary>
        public void ImportJsonAsync()
        {
            if (string.IsNullOrEmpty(json))
            {
                resultText.text = "请先点击 ToJson 或 ToJsonAsync，生成一份 JSON。";
                return;
            }
            StartCoroutine(ImportInFrames());
        }

        private IEnumerator ImportInFrames()
        {
            buttons.interactable = false;
            resultText.text = "FromJsonAsync：正在还原，完成前保留原数据。";
            // 与导出的参数含义相同：两批相隔 1 帧，每批最多执行 32 份小工作。
            AppDataJsonOperation operation = AppData.FromJsonAsync(json, frameInterval: 1, chunkSize: 32);
            yield return operation;
            buttons.interactable = true;
            if (operation.Error != null)
            {
                resultText.text = "还原失败：" + operation.Error.Message;
                yield break;
            }
            ShowState("FromJsonAsync：分帧还原完成，已重新 Get 当前数据。");
        }

        // 以下只负责更新场景文字，与 AppData 的使用无关。
        private void ShowState(string message)
        {
            QuizData quiz = AppData.Get<QuizData>();
            EquipmentData equipment = AppData.Get<EquipmentData>();
            dataText.fontSize = 18;
            dataText.text = $"QuizData：题目总数 {quiz.questionCount}，答对 {quiz.correctAnswerCount}\n"
                + $"EquipmentData：{equipment.equipmentName}，检查完成 {equipment.inspectionCompleted}\n"
                + $"List<int>：{string.Join(", ", equipment.listint)}；List<string>：{string.Join(", ", equipment.liststring)}\n"
                + $"List<QuizData>（答对/总数）：{string.Join(", ", equipment.listQuizData.ConvertAll(item => item.correctAnswerCount + "/" + item.questionCount))}";
            resultText.text = message;
        }
    }
}
