using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yvsim.AppData.Editor
{
    /// <summary>通过 Yvsim 框架菜单在当前场景创建全局数据根对象。</summary>
    public static class AppDataHierarchyCreator
    {
        /// <summary>创建带 AppData 组件的根对象，选中后即可添加数据类型。</summary>
        [MenuItem("Yvsim/框架/创建 AppData", false, -10000)]
        [MenuItem("GameObject/Yvsim/框架/创建 AppData", false, -10000)]
        public static void Create()
        {
            if (!CanCreate())
                return;
            var root = new GameObject("AppData", typeof(AppData));
            Undo.RegisterCreatedObjectUndo(root, "创建 AppData");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>编辑模式且已加载的场景中没有本包的 AppData 时，允许创建。</summary>
        /// <returns>可以创建时返回 true。</returns>
        [MenuItem("Yvsim/框架/创建 AppData", true)]
        [MenuItem("GameObject/Yvsim/框架/创建 AppData", true)]
        public static bool CanCreate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !SceneManager.GetActiveScene().IsValid())
                return false;
            foreach (AppData candidate in Resources.FindObjectsOfTypeAll<AppData>())
            {
                if (!EditorUtility.IsPersistent(candidate) && candidate.gameObject.scene.IsValid())
                    return false;
            }
            return true;
        }
    }
}
