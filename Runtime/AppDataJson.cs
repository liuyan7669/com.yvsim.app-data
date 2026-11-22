using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Yvsim.AppData
{
    /// <summary>检查 JSON 支持范围和实际值，并按路径恢复 null 字段。</summary>
    internal static class AppDataJson
    {
        /// <summary>检查数据类和字段类型是否在当前 JSON 数据的支持范围内。</summary>
        /// <param name="type">要检查的数据类或字段类型。</param>
        /// <param name="visited">已经检查过的类型，首次调用传入空集合，避免反复检查同一类型。</param>
        internal static void ValidateType(Type type, HashSet<Type> visited)
        {
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
                throw new NotSupportedException("JSON 数据字段不支持运行时指针。");
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                return;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) || type == typeof(object)
                || type.IsInterface || typeof(Delegate).IsAssignableFrom(type))
                throw new NotSupportedException("JSON 数据字段需要具体的普通数据类型：" + type.FullName);
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    throw new NotSupportedException("JSON 数据暂不支持多维数组。");
                Type element = type.GetElementType();
                if (element.IsArray || element.IsGenericType && element.GetGenericTypeDefinition() == typeof(List<>))
                    throw new NotSupportedException("嵌套容器请使用可序列化的数据类包裹。");
                ValidateType(element, visited);
                return;
            }
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() != typeof(List<>))
                    throw new NotSupportedException("JSON 数据泛型字段目前支持 List<T>：" + type.FullName);
                Type element = type.GetGenericArguments()[0];
                if (element.IsArray || element.IsGenericType && element.GetGenericTypeDefinition() == typeof(List<>))
                    throw new NotSupportedException("嵌套容器请使用可序列化的数据类包裹。");
                ValidateType(element, visited);
                return;
            }
            if (!type.IsSerializable || type.Namespace == "System"
                || type.IsClass && type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                throw new NotSupportedException("字段类型不在当前 JSON 数据支持范围：" + type.FullName);
            if (!visited.Add(type))
                return;
            foreach (FieldInfo field in Fields(type))
            {
                if (field.IsDefined(typeof(SerializeReference), true))
                    throw new NotSupportedException("JSON 数据字段暂不支持 SerializeReference 多态，请使用专用的普通数据类：" + field.Name);
                ValidateType(field.FieldType, visited);
            }
        }

        /// <summary>分步检查实际数据中的循环引用、派生实例和非有限数字。</summary>
        /// <param name="value">当前正在检查的数据对象或字段值。</param>
        /// <param name="ancestors">遍历到当前位置时经过的父对象，首次传入空列表，用来判断是否回到了同一个对象。</param>
        /// <returns>检查步骤，调用方可逐步执行并在批次之间让出帧。</returns>
        internal static IEnumerable<int> ValidateValue(object value, List<object> ancestors)
        {
            yield return 0;
            if (value == null)
            {
                yield break;
            }
            if (value is float single && (float.IsNaN(single) || float.IsInfinity(single))
                || value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                throw new NotSupportedException("JSON 数据不支持 NaN 或 Infinity。");
            if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                yield break;
            if (ancestors.Exists(item => ReferenceEquals(item, value)))
                throw new NotSupportedException("JSON 数据存在循环引用，请转换为标识关联。");
            ancestors.Add(value);
            if (value is IList list)
            {
                Type element = value.GetType().IsArray ? value.GetType().GetElementType() : value.GetType().GetGenericArguments()[0];
                for (int index = 0; index < list.Count; index++)
                {
                    object item = list[index];
                    if (item != null && item.GetType() != element)
                        throw new NotSupportedException("JSON 数据容器包含派生实例，请使用专用的普通数据类。");
                    foreach (int step in ValidateValue(item, ancestors)) yield return step;
                }
            }
            else
            {
                foreach (FieldInfo field in Fields(value.GetType()))
                {
                    object child = field.GetValue(value);
                    if (child != null && !field.FieldType.IsValueType && field.FieldType != typeof(string)
                        && !field.FieldType.IsArray && !field.FieldType.IsGenericType && child.GetType() != field.FieldType)
                        throw new NotSupportedException("JSON 数据字段包含派生实例，请整理为普通数据类：" + field.Name);
                    foreach (int step in ValidateValue(child, ancestors)) yield return step;
                }
            }
            ancestors.RemoveAt(ancestors.Count - 1);
        }

        /// <summary>沿着一条字段路径找到目标位置，并将它设为 null。</summary>
        /// <param name="value">当前遍历到的对象或容器。</param>
        /// <param name="segments">用斜杠拆分后的路径，例如 items、0、name。</param>
        /// <param name="depth">当前处理到第几段路径，从 0 开始。</param>
        internal static void RestoreNullAt(object value, string[] segments, int depth)
        {
            if (value == null)
                throw new System.IO.InvalidDataException("JSON 数据空值路径的父对象不存在。");
            bool last = depth == segments.Length - 1;
            if (value is IList list)
            {
                if (!int.TryParse(segments[depth], out int index) || index < 0 || index >= list.Count)
                    throw new System.IO.InvalidDataException("JSON 数据空值路径的数组下标不合法。");
                Type element = value.GetType().IsArray ? value.GetType().GetElementType() : value.GetType().GetGenericArguments()[0];
                if (last)
                {
                    if (element.IsValueType)
                        throw new System.IO.InvalidDataException("值类型不能恢复为空。");
                    list[index] = null;
                }
                else
                {
                    object child = list[index];
                    RestoreNullAt(child, segments, depth + 1);
                    list[index] = child;
                }
                return;
            }

            FieldInfo found = null;
            foreach (FieldInfo field in Fields(value.GetType()))
            {
                if (field.Name == segments[depth])
                {
                    found = field;
                    break;
                }
            }
            if (found == null)
                throw new System.IO.InvalidDataException("JSON 数据空值字段不存在，迁移时需同步更新空值路径：" + segments[depth]);
            if (last)
            {
                if (found.FieldType.IsValueType)
                    throw new System.IO.InvalidDataException("值类型字段不能恢复为空。");
                found.SetValue(value, null);
            }
            else
            {
                object child = found.GetValue(value);
                RestoreNullAt(child, segments, depth + 1);
                // 将嵌套结构体的装箱副本写回父字段或容器。
                found.SetValue(value, child);
            }
        }

        /// <summary>列出当前数据类及基类中需要进行JSON 数据检查的实例字段。</summary>
        /// <param name="type">需要列出字段的数据类型。</param>
        /// <returns>public 或带序列化标记的字段，排除 readonly 和带 NonSerialized 的字段；SerializeReference 留给类型检查明确拒绝。</returns>
        internal static IEnumerable<FieldInfo> Fields(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsInitOnly && !field.IsNotSerialized
                        && (field.IsPublic || field.IsDefined(typeof(SerializeField), true)
                            || field.IsDefined(typeof(SerializeReference), true)))
                        yield return field;
                }
            }
        }
    }
}
