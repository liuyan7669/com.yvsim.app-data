using System;
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
        /// <returns>包含数据类型信息和可序列化字段的 JSON；数据无法完整转换时会抛出异常。</returns>
        /// <remarks>只生成字符串，不写入文件或浏览器。返回的 JSON 可以交给 FromJson 还原。</remarks>
        public static string ToJson()
        {
            return AppDataSnapshot.Serialize(RequireInstance().data);
        }

        /// <summary>把 JSON 中的数据还原到当前容器；全部检查通过后才替换，失败时原数据保持不变。</summary>
        /// <param name="json">由 ToJson 生成的 JSON 内容，不是文件路径；读取文件需要由保存脚本处理。</param>
        /// <remarks>先用 Set 存入各类初始数据，再调用本方法；JSON 中不能出现当前容器没有的数据类型。JSON 中有的类型会替换当前对象，没有的类型保留当前值，成功后请重新 Get。JSON 格式错误或类型不匹配时会抛出异常，不会自动读取文件。</remarks>
        public static void FromJson(string json)
        {
            AppData owner = RequireInstance();
            owner.data = AppDataSnapshot.Deserialize(json, owner.data);
        }

        /// <summary>取得已初始化的 AppData；如果还没有，就抛出异常提示先创建并初始化组件。</summary>
        /// <returns>当前正在使用的 AppData 组件。</returns>
        private static AppData RequireInstance()
        {
            if (instance == null)
                throw new InvalidOperationException("请先在启动场景的专用根对象上挂载唯一 AppData，并等待 Awake 完成。");
            return instance;
        }

        /// <summary>当前使用的组件被销毁时清除记录；销毁重复组件不会影响正在使用的组件。</summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this))
                instance = null;
        }
    }
}
