using System;

namespace Yvsim.AppData
{
    /// <summary>让这个数据类出现在 AppData Inspector 的“添加数据”菜单中。</summary>
    /// <remarks>与 Serializable 一起使用。仅控制菜单显示，不影响 Set；子类需要单独添加此标记。</remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AppDataMenuAttribute : Attribute
    {
    }
}
