using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yvsim.AppData
{
    /// <summary>统一保管运行中的数据，提供存入、取出、重置和 JSON 转换，不负责保存文件。</summary>
    /// <remarks>把组件放在启动场景的独立根物体上，全局只保留一个，切换场景后仍可使用。业务在 Start 或之后从主线程调用；AppData 未初始化时调用会抛出异常。数据类是普通 C# 类，不需要挂到物体上。</remarks>
    [AddComponentMenu("Yvsim/框架/AppData")]
    [Yvsim.InspectorGroups.HideScript]
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AppData : MonoBehaviour
    {
        /// <summary>当前正在使用的唯一 AppData 组件。</summary>
        private static AppData instance;

        /// <summary>当前保管的数据，每个具体类型只保留一份，可通过 Set 存入或在 Inspector 中添加。</summary>
        [Tooltip("本次运行的数据，每种具体类型一份；业务可通过 Set 主动注入。")]
        [SerializeReference] private List<object> data = new List<object>();

        private readonly List<AppDataJsonOperation> jsonOperations = new List<AppDataJsonOperation>();

        /// <summary>登记当前组件并让物体跨场景保留；如果已有 AppData，就停用并销毁这个重复物体。</summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>存入一份数据；已有同类型数据时，用传入的新对象整体替换。</summary>
        /// <typeparam name="T">带 Serializable 标记的具体普通数据类，例如 QuizData；不要用 object 或父类代替。</typeparam>
        /// <param name="value">要存入的数据对象，例如 new QuizData()，不能为 null，实际类型必须与 T 一致。</param>
        /// <remarks>保管的是传入的对象本身，不会复制一份，也不会自动保存文件。整体替换后，之前取出的旧对象不会变成新对象，需要重新 Get。</remarks>
        public static void Set<T>(T value) where T : class
        {
            AppData owner = RequireInstance();
            const string typeHelp = "支持：带 [Serializable] 的具体、非泛型普通数据类，例如 Set(new QuizData())；T 必须与对象实际类型一致。\n"
                + "不支持直接存入：null、字符串、数组、List/Dictionary 等泛型类、GameObject/Component/ScriptableObject 等 Unity 对象、委托，以及未标记 [Serializable] 的类。\n"
                + "List 不能直接 Set（如 Set(new List<int>())），但普通数据类中的 List<int>、List<string>、List<普通数据类> 等字段受支持，元素类型也须可序列化。\n"
                + "int、float、bool、字符串、一维数组等也可作为数据类字段；JSON 转换不支持字典、直接嵌套容器、Unity 对象引用、多态字段和循环引用。\n"
                + "Set 不要求 [AppDataMenu]，也不要求公开无参构造函数；后者用于面板创建和重置。";
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"Set 失败：T = {typeof(T).FullName}，传入对象 = null。请先创建数据对象再存入。\n{typeHelp}");
            Type type = typeof(T);
            Type actualType = value.GetType();
            if (actualType != type || !type.IsSerializable || type == typeof(string)
                || type.IsArray || type.IsGenericType || value is UnityEngine.Object || value is Delegate)
            {
                string reason;
                if (actualType != type)
                    reason = "泛型 T 与对象实际类型不一致，请使用具体数据类型，不要用 object、接口或父类代替。";
                else if (value is UnityEngine.Object)
                    reason = "传入的是 Unity 对象，请先把需要的数据整理到普通数据类中。";
                else if (value is Delegate)
                    reason = "传入的是委托，不能作为数据对象存入。";
                else if (type == typeof(string) || type.IsArray || type.IsGenericType)
                    reason = "传入的是字符串、数组或泛型对象，请用一个普通数据类包裹这些字段。";
                else
                    reason = "数据类没有标记 [Serializable]，请在类声明上添加此标记。";
                throw new ArgumentException($"Set 失败：T = {type.FullName}，对象实际类型 = {actualType.FullName}。\n原因：{reason}\n{typeHelp}", nameof(value));
            }
            int index = owner.data.FindIndex(item => item != null && item.GetType() == typeof(T));
            if (index < 0)
                owner.data.Add(value);
            else
                owner.data[index] = value;
        }

        /// <summary>检查是否已经存入某一类数据，不会创建数据。</summary>
        /// <typeparam name="T">要检查的具体数据类，例如 QuizData。</typeparam>
        /// <returns>已经存入返回 true，没有存入返回 false。</returns>
        public static bool Contains<T>() where T : class
        {
            return RequireInstance().data.Exists(item => item != null && item.GetType() == typeof(T));
        }

        /// <summary>取出当前数据对象，可以直接修改它的字段；没有这类数据时返回 null。</summary>
        /// <typeparam name="T">要取出的具体数据类，与 Set 时使用的类型一致。</typeparam>
        /// <returns>当前数据对象本身，不是副本；没有存入过该类型时返回 null，访问字段前请先判空。</returns>
        /// <remarks>修改字段不需要再调用 Set；要换成另一个新对象时，才调用 Set。</remarks>
        public static T Get<T>() where T : class
        {
            object value = RequireInstance().data.Find(item => item != null && item.GetType() == typeof(T));
            return (T)value;
        }

        /// <summary>把一类已有数据换成 new T() 创建的新对象；没有这类数据时直接跳过。</summary>
        /// <typeparam name="T">要重置的具体数据类，必须能通过公开的无参构造函数创建对象。</typeparam>
        /// <remarks>初始值由数据类的字段初始值和无参构造函数决定，不是读取之前的存档。重置后请重新 Get，之前取出的对象仍是旧对象。不会保存或删除文件。</remarks>
        public static void Reset<T>() where T : class, new()
        {
            if (Contains<T>())
                Set(new T());
        }

        /// <summary>重置已经存入的全部数据，每个类型重新创建一份对象，保留原有的类型。</summary>
        /// <remarks>每个数据类都需要公开的无参构造函数，初始值由字段初始值和构造函数决定。全部创建成功后才替换；如果其中一个创建失败，会抛出异常，原数据保持不变。重置后请重新 Get；不会保存或删除文件，也不会还原 Inspector 中之前手动填写的值。</remarks>
        public static void ResetAll()
        {
            AppData owner = RequireInstance();
            var reset = new List<object>();
            foreach (object value in owner.data)
            {
                if (value == null)
                    throw new InvalidOperationException("数据列表存在空项，请先移除或重新注入。");
                reset.Add(Activator.CreateInstance(value.GetType()));
            }
            owner.data = reset;
        }

        /// <summary>把已经存入的全部数据转成 JSON 字符串，供外部保存或发送。</summary>
        /// <returns>包含数据类型信息和可序列化字段的 JSON；遇到不支持的类型、循环引用等情况时会抛出异常。</returns>
        /// <remarks>在当前帧完成，只检查并导出，不做还原和比较。不写入文件或浏览器；大量数据建议使用 ToJsonAsync。返回的 JSON 可以交给 FromJson 还原。</remarks>
        public static string ToJson()
        {
            return AppDataSnapshot.Serialize(RequireInstance().data);
        }

        /// <summary>用协程分批导出全部数据，返回可等待的操作对象，不使用线程或 async/await。</summary>
        /// <param name="frameInterval">两批处理之间相隔的帧数，至少为 1，默认 1：下一帧继续；2：隔两帧继续。不是秒数，也不能降低单批本身的耗时。</param>
        /// <param name="chunkSize">每批最多执行多少份小工作，至少为 1，默认 32。字段、容器元素的遍历和每段最多 128 字符的文本写入都参与计数；不是字节数、根数据类数量或毫秒预算。同一元素可能需要多份工作。</param>
        /// <returns>可 yield return 等待的操作对象；结束后检查 Error，为 null 时读取 Json。Progress 按已完成的数据类型推进。</returns>
        /// <remarks>由唯一 AppData 组件运行，从下一帧开始，不受 timeScale 影响。导出期间不要修改、替换、重置或导入数据，也不要重复启动多个导出来降低流畅度。块越小单批负担越轻，间隔越大总耗时越长。最后生成完整字符串仍需一次分配，不能保证所有设备完全无卡顿。容器停用或销毁会结束操作并记录 Error。未初始化、容器未启用或参数小于 1 时直接抛出异常；处理数据时的异常记录在 Error 中。</remarks>
        public static AppDataJsonOperation ToJsonAsync(int frameInterval = 1, int chunkSize = 32)
        {
            AppData owner = RequireJsonOwner(frameInterval, chunkSize);
            var operation = new AppDataJsonOperation();
            var writer = new AppDataJsonWriter();
            owner.jsonOperations.Add(operation);
            owner.StartCoroutine(owner.RunJson(operation, writer.Write(new List<object>(owner.data), operation).GetEnumerator(),
                () => operation.Complete(writer.Output.ToString(), null), frameInterval, chunkSize));
            return operation;
        }

        private static AppData RequireJsonOwner(int frameInterval, int chunkSize)
        {
            if (frameInterval < 1)
                throw new ArgumentOutOfRangeException(nameof(frameInterval), "分帧间隔至少为 1，单位是帧。");
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "每批处理量至少为 1。");
            AppData owner = RequireInstance();
            if (!owner.isActiveAndEnabled)
                throw new InvalidOperationException("分帧 JSON 转换需要已启用且处于活动物体上的 AppData 组件。");
            return owner;
        }

        private IEnumerator RunJson(AppDataJsonOperation operation, IEnumerator<int> steps, Action finish, int frameInterval, int chunkSize)
        {
            yield return null;
            using (steps)
            {
                while (!operation.IsDone)
                {
                    try
                    {
                        for (int count = 0; count < chunkSize; count++)
                        {
                            if (!steps.MoveNext())
                            {
                                finish();
                                break;
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        operation.Complete(null, error);
                    }
                    if (!operation.IsDone)
                        for (int frame = 0; frame < frameInterval; frame++)
                            yield return null;
                }
            }
            jsonOperations.Remove(operation);
        }

        /// <summary>把 JSON 中的数据还原到当前容器；全部检查通过后才替换，失败时原数据保持不变。</summary>
        /// <param name="json">由 ToJson 或 ToJsonAsync 生成的 JSON 内容，不是文件路径；读取文件需要由保存脚本处理。</param>
        /// <remarks>先用 Set 存入各类初始数据，再调用本方法；JSON 中不能出现当前容器没有的数据类型。JSON 中有的类型会替换当前对象，没有的类型保留当前值，成功后请重新 Get。JSON 格式错误或类型不匹配时会抛出异常，不会自动读取文件。</remarks>
        public static void FromJson(string json)
        {
            AppData owner = RequireInstance();
            owner.data = AppDataSnapshot.Deserialize(json, owner.data);
        }

        /// <summary>用协程分批还原 JSON；全部成功后一起替换，失败时保留当前数据。</summary>
        /// <param name="json">由 ToJson 或 ToJsonAsync 生成的 JSON 内容，不是文件路径；格式错误记录在返回操作的 Error 中。</param>
        /// <param name="frameInterval">两批处理之间相隔的帧数，至少为 1，默认 1：下一帧继续；2：隔两帧继续。不是秒数，不受 timeScale 影响。</param>
        /// <param name="chunkSize">每批最多执行多少份小工作，至少为 1，默认 32，与 ToJsonAsync 一致。每段最多 128 字符的读取、字段和列表元素的解析与填充、空值恢复和检查均参与计数；不是字节数、数据类数量或毫秒预算，同一元素可能需要多份工作。</param>
        /// <returns>可 yield return 等待的操作对象，提供 IsDone、Progress 和 Error；导入不生成 JSON，Json 始终为 null。成功后重新 Get 数据。</returns>
        /// <remarks>先 Set 各类初始数据，只还原已知类型；JSON 缺少的类型保留原数据。从下一帧开始，完成前 Get 仍返回原对象。期间业务不要修改、替换、重置数据或同时导入导出。停用或销毁 AppData 会结束操作并记录 Error，原数据不变。未初始化、容器未启用或两个调度参数小于 1 时直接抛出异常。字符串、数组和容器扩容的分配、对象创建与类型检查仍有同步开销，不能保证所有设备完全无卡顿。</remarks>
        public static AppDataJsonOperation FromJsonAsync(string json, int frameInterval = 1, int chunkSize = 32)
        {
            AppData owner = RequireJsonOwner(frameInterval, chunkSize);
            var operation = new AppDataJsonOperation();
            var reader = new AppDataJsonReader();
            owner.jsonOperations.Add(operation);
            owner.StartCoroutine(owner.RunJson(operation, reader.Read(json, new List<object>(owner.data), operation).GetEnumerator(),
                () => { owner.data = reader.Result; operation.Complete(null, null); }, frameInterval, chunkSize));
            return operation;
        }

        /// <summary>取得已初始化的 AppData；如果还没有，就抛出异常提示先创建并初始化组件。</summary>
        /// <returns>当前正在使用的 AppData 组件。</returns>
        private static AppData RequireInstance()
        {
            if (instance == null)
                throw new InvalidOperationException("请先在启动场景的专用根对象上挂载唯一 AppData，并等待 Awake 完成。");
            return instance;
        }

        private void OnDisable()
        {
            foreach (AppDataJsonOperation operation in jsonOperations)
                operation.Complete(null, new OperationCanceledException("AppData 已停用或销毁，JSON 转换已结束。"));
            StopAllCoroutines();
            jsonOperations.Clear();
        }

        /// <summary>当前使用的组件被销毁时清除记录；销毁重复组件不会影响正在使用的组件。</summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this))
                instance = null;
        }
    }
}
