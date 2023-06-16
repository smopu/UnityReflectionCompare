#region Intro
// Purpose: Reflection Optimize Performance
// Author: ZhangYu
// LastModifiedDate: 2022-06-11
#endregion

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace EmitReflection 
{
    public class EmitWrapperTypeFieldAndProperty
    {
        public Func<object, object> getValue;
        public Action<object, object> setValue;
        public Delegate getValueDelegate;
        public Delegate setValueDelegate;
    }

    public class EmitWrapperType
    {
        Dictionary<string, EmitWrapperTypeFieldAndProperty> all = new Dictionary<string, EmitWrapperTypeFieldAndProperty>();
        public Type type { get; private set; }
        public EmitWrapperTypeFieldAndProperty GetEmitWarp(string name)
        {
            EmitWrapperTypeFieldAndProperty cache;
            if (all.TryGetValue(name, out cache))
            {
                return cache;
            }
            return null;
        }

        public EmitWrapperType(Type type)
        {
            this.type = type;
            FieldInfo[] typeAddrFieldsNow = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var item in typeAddrFieldsNow)
            {
                if (all.ContainsKey(item.Name))
                {
                    
                }
                else
                {
                    EmitWrapperTypeFieldAndProperty d = all[item.Name] = new EmitWrapperTypeFieldAndProperty();
                    d.getValue = ILUtil.CreateGetValue(type, item.Name);
                    d.setValue = ILUtil.CreateSetValue(type, item.Name);
                    d.getValueDelegate = ILUtil.CreateGetValueDelegate(type, item.Name);
                    d.setValueDelegate = ILUtil.CreateSetValueDelegate(type, item.Name);
                }
            }

            var loopType = type;
            while (loopType.BaseType != typeof(object))
            {
                foreach (var item in loopType.BaseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (item.Attributes == FieldAttributes.Private)
                    {
                        if (!all.ContainsKey(item.Name))
                        {
                            EmitWrapperTypeFieldAndProperty d = all[item.Name] = new EmitWrapperTypeFieldAndProperty();
                            d.getValue = ILUtil.CreateGetValue(type, item.Name);
                            d.setValue = ILUtil.CreateSetValue(type, item.Name);
                            d.getValueDelegate = ILUtil.CreateGetValueDelegate(type, item.Name);
                            d.setValueDelegate = ILUtil.CreateSetValueDelegate(type, item.Name);
                        }
                    }
                }
                loopType = loopType.BaseType;
            }

            //获得所有属性 get set
            //如果属性是值类型且不是基本数据类型
            //额外处理
            //计算属性数量 构造非托管内存
            //讲属性方法设置到非托管内存
            //int propertySize = 0;
            PropertyInfo[] propertyInfosNow = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var item in propertyInfosNow)
            {
                if (!all.ContainsKey(item.Name))
                {
                    if (item.Name != "Item")
                    {
                        EmitWrapperTypeFieldAndProperty d = all[item.Name] = new EmitWrapperTypeFieldAndProperty();
                        if (item.GetMethod != null)
                        {
                            d.getValue = ILUtil.CreateGetValue(type, item.Name);
                            d.getValueDelegate = ILUtil.CreateGetValueDelegate(type, item.Name);
                        }
                        if (item.SetMethod != null)
                        {
                            d.setValue = ILUtil.CreateSetValue(type, item.Name);
                            d.setValueDelegate = ILUtil.CreateSetValueDelegate(type, item.Name);
                        }
                    }
                }
            }
            loopType = type;
            while (loopType.BaseType != typeof(object))
            {
                foreach (var item in loopType.BaseType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (item.Name != "Item")
                    {
                        EmitWrapperTypeFieldAndProperty d = all[item.Name] = new EmitWrapperTypeFieldAndProperty();
                        if (item.GetMethod != null)
                        {
                            d.getValue = ILUtil.CreateGetValue(type, item.Name);
                            d.getValueDelegate = ILUtil.CreateGetValueDelegate(type, item.Name);
                        }
                        if (item.SetMethod != null)
                        {
                            d.setValue = ILUtil.CreateSetValue(type, item.Name);
                            d.setValueDelegate = ILUtil.CreateSetValueDelegate(type, item.Name);
                        }
                    }
                }
                loopType = loopType.BaseType;
            }


        }

    }

    /// <summary> IL.Emit工具 </summary>
    public static class ILUtil
    {

        private static Type[] NoTypes = new Type[] { };

        /// <summary> IL动态添加创建新对象方法 </summary>
        public static Func<object> CreateCreateInstance(Type classType)
        {
            DynamicMethod method = new DynamicMethod(string.Empty, typeof(object), null, classType);
            if (classType.IsValueType)
            {
                // 实例化值类型
                ILGenerator il = method.GetILGenerator(32);
                var v0 = il.DeclareLocal(classType);
                il.Emit(OpCodes.Ldloca_S, v0);
                il.Emit(OpCodes.Initobj, classType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Box, classType);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                // 实例化引用类型
                ConstructorInfo ctor = classType.GetConstructor(NoTypes);
                ILGenerator il = method.GetILGenerator(16);
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
            }
            return method.CreateDelegate(typeof(Func<object>)) as Func<object>;
        }

        /// <summary> IL动态添加创建新数组方法 </summary>
        public static Func<int, Array> CreateCreateArray(Type classType, int length)
        {
            DynamicMethod method = new DynamicMethod(string.Empty, typeof(Array), new Type[] { typeof(int) }, typeof(Array));
            ILGenerator il = method.GetILGenerator(16);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newarr, classType.GetElementType());
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Func<int, Array>)) as Func<int, Array>;
        }

        private static Dictionary<Type, Func<object>> createInstanceCache = new Dictionary<Type, Func<object>>();
        private static Dictionary<Type, Func<int, Array>> createArrayCache = new Dictionary<Type, Func<int, Array>>();

        /// <summary> IL动态创建新的实例 </summary>
        /// <param name="classType">类型</param>
        public static object CreateInstance(Type classType)
        {
            Func<object> createMethod = null;
            if (!createInstanceCache.TryGetValue(classType, out createMethod))
            {
                createMethod = CreateCreateInstance(classType);
                createInstanceCache.Add(classType, createMethod);
            }
            return createMethod();
        }


        /// <summary> IL动态创建新数组 </summary>
        public static Array CreateArray(Type classType, int length)
        {
            Func<int, Array> createMethod = null;
            if (!createArrayCache.TryGetValue(classType, out createMethod))
            {
                createMethod = CreateCreateArray(classType, length);
                createArrayCache.Add(classType, createMethod);
            }
            return createMethod(length);
        }

        /// <summary> IL.Emit动态创建获取字段值的方法 </summary>
        private static Func<object, object> CreateGetField(Type classType, string fieldName)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, typeof(object), new Type[] { typeof(object) }, classType);
            ILGenerator il = method.GetILGenerator(16); // 默认大小64字节
            il.Emit(OpCodes.Ldarg_0);
            if (classType.IsValueType) il.Emit(OpCodes.Unbox_Any, classType);
            il.Emit(OpCodes.Ldfld, field);
            if (field.FieldType.IsValueType) il.Emit(OpCodes.Box, field.FieldType);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Func<object, object>)) as Func<object, object>;
        }

        /// <summary> IL.Emit动态创建获取字段值的方法 Delegate</summary>
        private static Delegate CreateGetFieldDelegate(Type classType, string fieldName)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, field.FieldType, new Type[] { classType }, classType);
            ILGenerator il = method.GetILGenerator(16); // 默认大小64字节
            il.Emit(OpCodes.Ldarg_0);
            //if (classType.IsValueType) il.Emit(OpCodes.Unbox_Any, classType);
            il.Emit(OpCodes.Ldfld, field);
            //if (field.FieldType.IsValueType) il.Emit(OpCodes.Box, field.FieldType);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Func<,>).MakeGenericType(classType, field.FieldType));
        }


        /// <summary> IL.Emit动态创建设置字段方法 </summary>
        private static Action<object, object> CreateSetField(Type classType, string fieldName)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, null, new Type[] { typeof(object), typeof(object) }, classType);
            ILGenerator il = method.GetILGenerator(32); // 默认大小64字节
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
        }

        /// <summary> IL.Emit动态创建设置字段方法 </summary>
        private static Delegate CreateSetFieldDelegate(Type classType, string fieldName)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, null, new Type[] { classType, field.FieldType }, classType);
            ILGenerator il = method.GetILGenerator(32); // 默认大小64字节
            il.Emit(OpCodes.Ldarg_0);
            //il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Ldarg_1);
            //il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Action<, >).MakeGenericType(classType, field.FieldType));
        }


        /// <summary> IL.Emit动态创建获取属性值的方法 </summary>
        private static Func<object, object> CreateGetProperty(Type classType, string propertyName)
        {
            PropertyInfo property = classType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, typeof(object), new Type[] { typeof(object) }, classType);
            ILGenerator il = method.GetILGenerator(32);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Call, property.GetGetMethod());
            if (property.PropertyType.IsValueType) il.Emit(OpCodes.Box, property.PropertyType);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Func<object, object>)) as Func<object, object>;
        }


        /// <summary> IL.Emit动态创建获取属性值的方法 </summary>
        private static Delegate CreateGetPropertyDelegate(Type classType, string propertyName)
        {
            PropertyInfo property = classType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            DynamicMethod method = new DynamicMethod(string.Empty, property.PropertyType, new Type[] { classType }, classType);
            ILGenerator il = method.GetILGenerator(32);
            il.Emit(OpCodes.Ldarg_0);
            //il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Call, property.GetGetMethod());
            //if (property.PropertyType.IsValueType) il.Emit(OpCodes.Box, property.PropertyType);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Func<, >).MakeGenericType(classType, property.PropertyType));
        }

        /// <summary> IL.Emit动态创建设置属性值的方法 </summary>
        private static Action<object, object> CreateSetProperty(Type classType, string propertyName)
        {
            PropertyInfo property = classType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo methodInfo = property.GetSetMethod();
            ParameterInfo parameter = methodInfo.GetParameters()[0];
            DynamicMethod method = new DynamicMethod(string.Empty, null, new Type[] { typeof(object), typeof(object) }, classType);
            ILGenerator il = method.GetILGenerator(32);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(parameter.ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameter.ParameterType);
            il.Emit(OpCodes.Call, methodInfo);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
        }

        /// <summary> IL.Emit动态创建设置属性值的方法 </summary>
        private static Delegate CreateSetPropertyDelegate(Type classType, string propertyName)
        {
            PropertyInfo property = classType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo methodInfo = property.GetSetMethod();
            ParameterInfo parameter = methodInfo.GetParameters()[0];
            DynamicMethod method = new DynamicMethod(string.Empty, null, new Type[] { classType, property.PropertyType }, classType);
            ILGenerator il = method.GetILGenerator(32);
            il.Emit(OpCodes.Ldarg_0);
            //il.Emit(classType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, classType);
            il.Emit(OpCodes.Ldarg_1);
            //il.Emit(parameter.ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameter.ParameterType);
            il.Emit(OpCodes.Call, methodInfo);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(typeof(Action<, >).MakeGenericType(classType, property.PropertyType));
        }



        /// <summary> IL.Emit动态创建获取字段或属性值的方法 </summary>
        /// <param name="classType">对象类型</param>
        /// <param name="fieldName">字段(或属性)名称</param>
        public static Func<object, object> CreateGetValue(Type classType, string fieldName)
        {
            MemberInfo[] members = classType.GetMember(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (members.Length == 0)
            {
                string error = "Type [{0}] don't contains member [{1}]";
                throw new Exception(string.Format(error, classType, fieldName));
            }
            Func<object, object> getValue = null;
            switch (members[0].MemberType)
            {
                case MemberTypes.Field:
                    getValue = CreateGetField(classType, fieldName);
                    break;
                case MemberTypes.Property:
                    getValue = CreateGetProperty(classType, fieldName);
                    break;
                default:
                    break;
            }
            return getValue;
        }

        public static Delegate CreateGetValueDelegate(Type classType, string fieldName)
        {
            MemberInfo[] members = classType.GetMember(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (members.Length == 0)
            {
                string error = "Type [{0}] don't contains member [{1}]";
                throw new Exception(string.Format(error, classType, fieldName));
            }
            Delegate getValue = null;
            switch (members[0].MemberType)
            {
                case MemberTypes.Field:
                    getValue = CreateGetFieldDelegate(classType, fieldName);
                    break;
                case MemberTypes.Property:
                    getValue = CreateGetPropertyDelegate(classType, fieldName);
                    break;
                default:
                    break;
            }
            return getValue;
        }


        /// <summary> IL.Emit动态创建设置字段值(或属性)值的方法 </summary>
        /// <param name="classType">对象类型</param>
        /// <param name="fieldName">字段(或属性)名称</param>
        public static Delegate CreateSetValueDelegate(Type classType, string fieldName)
        {
            MemberInfo[] members = classType.GetMember(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (members.Length == 0)
            {
                string error = "Type [{0}] does not contain field [{1}]";
                throw new Exception(string.Format(error, classType, fieldName));
            }
            Delegate setValue = null;
            switch (members[0].MemberType)
            {
                case MemberTypes.Field:
                    setValue = CreateSetFieldDelegate(classType, fieldName);
                    break;
                case MemberTypes.Property:
                    setValue = CreateSetPropertyDelegate(classType, fieldName);
                    break;
                default:
                    break;
            }
            return setValue;
        }

        /// <summary> IL.Emit动态创建设置字段值(或属性)值的方法 </summary>
        /// <param name="classType">对象类型</param>
        /// <param name="fieldName">字段(或属性)名称</param>
        public static Action<object, object> CreateSetValue(Type classType, string fieldName)
        {
            MemberInfo[] members = classType.GetMember(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (members.Length == 0)
            {
                string error = "Type [{0}] does not contain field [{1}]";
                throw new Exception(string.Format(error, classType, fieldName));
            }
            Action<object, object> setValue = null;
            switch (members[0].MemberType)
            {
                case MemberTypes.Field:
                    setValue = CreateSetField(classType, fieldName);
                    break;
                case MemberTypes.Property:
                    setValue = CreateSetProperty(classType, fieldName);
                    break;
                default:
                    break;
            }
            return setValue;
        }

        // Emit Getter 方法缓存字典
        private static Dictionary<Type, Dictionary<string, Func<object, object>>> getValueCache = new Dictionary<Type, Dictionary<string, Func<object, object>>>();
        // Emit Setter 方法缓存字典
        private static Dictionary<Type, Dictionary<string, Action<object, object>>> setValueCache = new Dictionary<Type, Dictionary<string, Action<object, object>>>();
       


        /// <summary> IL 获取对象成员的值(字段或属性) </summary>
        public static object GetValue(object obj, string fieldName)
        {
            // 查找一级缓存
            Type classType = obj.GetType();
            Dictionary<string, Func<object, object>> cache = null;
            if (!getValueCache.TryGetValue(classType, out cache))
            {
                cache = new Dictionary<string, Func<object, object>>();
                getValueCache.Add(classType, cache);
            }

            // 查找二级缓存
            Func<object, object> getValue = null;
            if (!cache.TryGetValue(fieldName, out getValue))
            {
                getValue = CreateGetValue(classType, fieldName);
                cache.Add(fieldName, getValue);
            }
            return getValue(obj);
        }

        /// <summary> IL 设置对象成员的值(字段或属性) </summary>
        public static void SetValue(object obj, string fieldName, object value)
        {
            // 查找一级缓存
            Type classType = obj.GetType();
            Dictionary<string, Action<object, object>> cache = null;
            if (!setValueCache.TryGetValue(classType, out cache))
            {
                cache = new Dictionary<string, Action<object, object>>();
                setValueCache.Add(classType, cache);
            }

            // 查找二级缓存
            Action<object, object> setValue = null;
            if (!cache.TryGetValue(fieldName, out setValue))
            {
                setValue = CreateSetValue(classType, fieldName);
                cache.Add(fieldName, setValue);
            }
            setValue(obj, value);
        }

        /// <summary> 清理已缓存IL方法 </summary>
        public static void ClearCache()
        {
            createInstanceCache.Clear();
            createArrayCache.Clear();
            getValueCache.Clear();
            setValueCache.Clear();
        }

    }
}