using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Yvsim.InspectorGroups;
using Yvsim.InspectorGroups.Editor;

namespace Yvsim.AppData.Editor
{
    /// <summary>声明全局数据的字段和操作，由 Inspector Groups 统一绘制面板。</summary>
    [CustomEditor(typeof(AppData))]
    public sealed class AppDataEditor : InspectorGroupsEditor<AppData>
    {
        /// <summary>列出已添加的数据及其字段，提供添加和移除操作。</summary>
        /// <param name="content">Inspector Groups 提供的面板内容声明对象。</param>
        protected override void BuildInspector(InspectorContent content)
        {
            content.Exclude("data");
            SerializedProperty data = serializedObject.FindProperty("data");
            content.Group("app-data", "全局数据", group =>
            {
                group.Message("支持：带 [Serializable] 的具体、非泛型普通数据类，每种类型保留一份。\n"
                    + "不能直接 Set：GameObject、Component、ScriptableObject 等 Unity 对象，以及字符串、数组或 List 本身。\n"
                    + "支持数据类内的字段：int、string、一维数组、List<T> 等；数组和 List 的元素也需为支持的类型。\n"
                    + "面板添加：还需 [AppDataMenu] 和公开无参构造函数；代码 Set 不需要此标记。\n"
                    + "JSON 转换另有限制：不支持字典、Unity 对象引用、多态字段、嵌套容器和循环引用。");
                for (int index = 0; index < data.arraySize; index++)
                {
                    int itemIndex = index;
                    SerializedProperty item = data.GetArrayElementAtIndex(index);
                    string typeName = item.managedReferenceFullTypename;
                    string title = string.IsNullOrEmpty(typeName) ? "空数据或类型丢失" : typeName.Substring(typeName.IndexOf(' ') + 1);
                    string key = string.IsNullOrEmpty(typeName) ? "empty-" + index : typeName;
                    group.Group(key, title, entry =>
                    {
                        SerializedProperty child = item.Copy();
                        SerializedProperty end = item.GetEndProperty();
                        bool enterChildren = true;
                        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                        {
                            entry.Property(child.propertyPath);
                            enterChildren = false;
                        }
                        entry.Action("移除这份数据", () => RemoveData(itemIndex),
                            tooltip: "从当前 AppData 中移除这份数据，支持撤销。编辑模式下请保存场景或 Prefab。");
                    });
                }
                group.Action("添加数据", ShowTypeMenu,
                    tooltip: "只列出带 AppDataMenu 和 Serializable 标记、可以直接创建的数据类；此标记不影响 Set。");
            }, InspectorGroupStyle.Framework);
        }

        /// <summary>只列出带 AppDataMenu 标记且可以创建的数据类，已添加的类型保持可见但不可重复选择。</summary>
        private void ShowTypeMenu()
        {
            var registered = new HashSet<string>();
            SerializedProperty data = serializedObject.FindProperty("data");
            for (int index = 0; index < data.arraySize; index++)
                registered.Add(data.GetArrayElementAtIndex(index).managedReferenceFullTypename);

            var menu = new GenericMenu();
            foreach (Type type in TypeCache.GetTypesWithAttribute<AppDataMenuAttribute>()
                .Where(type => type.IsSerializable && type.IsClass && !type.IsAbstract && !type.IsGenericType
                    && type.IsVisible && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                    && !typeof(Delegate).IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName))
            {
                var label = new UnityEngine.GUIContent(type.FullName.Replace('.', '/'));
                if (registered.Contains(GetSerializedTypeName(type)))
                    menu.AddDisabledItem(label);
                else
                    menu.AddItem(label, false, () => AddData(type));
            }
            menu.ShowAsContext();
        }

        /// <summary>取得 Unity 为这个数据类保存的类型名称。</summary>
        /// <param name="type">需要检查的数据类。</param>
        /// <returns>程序集名称与数据类完整名称组成的字符串。</returns>
        private static string GetSerializedTypeName(Type type)
        {
            return type.Assembly.GetName().Name + " " + type.FullName.Replace('+', '/');
        }

        /// <summary>创建所选类型的数据，并将它加入当前组件。</summary>
        /// <param name="type">用户在添加菜单中选择的数据类。</param>
        private void AddData(Type type)
        {
            if (this == null || target == null)
                return;
            object value;
            try
            {
                value = Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                return;
            }
            serializedObject.Update();
            SerializedProperty data = serializedObject.FindProperty("data");
            for (int index = 0; index < data.arraySize; index++)
            {
                if (data.GetArrayElementAtIndex(index).managedReferenceFullTypename == GetSerializedTypeName(type))
                    return;
            }
            int newIndex = data.arraySize;
            data.arraySize++;
            data.GetArrayElementAtIndex(newIndex).managedReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        /// <summary>移除面板中指定位置的数据，修改由 Unity 记录撤销和 Prefab 覆盖。</summary>
        /// <param name="index">需要移除的数据位置，从 0 开始。</param>
        private void RemoveData(int index)
        {
            serializedObject.Update();
            SerializedProperty data = serializedObject.FindProperty("data");
            if (index < 0 || index >= data.arraySize)
                return;
            data.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
