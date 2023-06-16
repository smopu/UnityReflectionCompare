using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace ExpressionReflection
{
    public class ExpressionWrapperType
    {
        Dictionary<string, ExpressionWrapper> all = new Dictionary<string, ExpressionWrapper>();
        public Type type { get; private set; }
        public ExpressionWrapper GetWarp(string name)
        {
            ExpressionWrapper cache;
            if (all.TryGetValue(name, out cache))
            {
                return cache;
            }
            return null;
        }

        public ExpressionWrapperType(Type type)
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
                    ExpressionWrapper d = all[item.Name] = new ExpressionWrapper();
                    d.getValue = ExpressionUtil.CreateGetField(type, item.Name);
                    d.setValue = ExpressionUtil.CreateSetField(type, item.Name);
                    d.getValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.FieldType).GetMethod("CreateGetField", BindingFlags.Static | BindingFlags.Public).
                        Invoke(null, new object[] { item });
                    d.setValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.FieldType).GetMethod("CreateSetField", BindingFlags.Static | BindingFlags.Public).
                        Invoke(null, new object[] { item });


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
                            ExpressionWrapper d = all[item.Name] = new ExpressionWrapper();
                            d.getValue = ExpressionUtil.CreateGetField(type, item.Name);
                            d.setValue = ExpressionUtil.CreateSetField(type, item.Name);
                            d.getValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.FieldType).GetMethod("CreateGetField", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item });
                            d.setValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.FieldType).GetMethod("CreateSetField", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item });
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
                        ExpressionWrapper d = all[item.Name] = new ExpressionWrapper();
                        if (item.GetMethod != null)
                        {
                            d.getValue = ExpressionUtil.CreateGetProperty(type, item.Name);
                            d.getValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.PropertyType).
                                GetMethod("CreateGetProperty", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item });
                        }
                        if (item.SetMethod != null)
                        {
                            d.setValue = ExpressionUtil.CreateSetProperty(type, item.Name);
                            d.setValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.PropertyType).
                                GetMethod("CreateSetProperty", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item }); 
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
                        ExpressionWrapper d = all[item.Name] = new ExpressionWrapper();
                        if (item.GetMethod != null)
                        {
                            d.getValue = ExpressionUtil.CreateGetProperty(type, item.Name);
                            d.getValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.PropertyType).
                                GetMethod("CreateGetProperty", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item });
                        }
                        if (item.SetMethod != null)
                        {
                            d.setValue = ExpressionUtil.CreateSetProperty(type, item.Name);
                            d.setValueDelegate = (Delegate)typeof(ExpressionUtil<,>).MakeGenericType(type, item.PropertyType).
                                GetMethod("CreateSetProperty", BindingFlags.Static | BindingFlags.Public).
                                Invoke(null, new object[] { item });
                        }
                    }
                }
                loopType = loopType.BaseType;
            }


        }

    }

    public class ExpressionWrapper
    {
        public Func<object, object> getValue;
        public Action<object, object> setValue;
        public Delegate getValueDelegate;
        public Delegate setValueDelegate;
    }

    public class ExpressionUtil<T, V>
    {
        public static Action<T, V> CreateSetField(FieldInfo field)
        {
            //var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            ParameterExpression targetExp = Expression.Parameter(typeof(T), "target");
            ParameterExpression valueExp = Expression.Parameter(field.FieldType, "value");
            MemberExpression fieldExp = Expression.Field(targetExp, field);
            BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);
            return Expression.Lambda<Action<T, V>>
                (assignExp, targetExp, valueExp).Compile();
        }

        public static Func<T, V> CreateGetField(FieldInfo field)
        {
            //var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var paramExpr = Expression.Parameter(typeof(T), "x");
            var expr = Expression.Field(Expression.Convert(paramExpr, typeof(T)), field);
            Func<T, V> getValue = Expression.Lambda<Func<T, V>>(expr, paramExpr).Compile();
            return getValue;
        }

        public static Action<T, V> CreateSetProperty(PropertyInfo property)
        {
            //var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            ParameterExpression targetExp = Expression.Parameter(typeof(T), "target");
            ParameterExpression valueExp = Expression.Parameter(property.PropertyType, "value");
            MemberExpression propertyExp = Expression.Property(targetExp, property);
            BinaryExpression assignExp = Expression.Assign(propertyExp, valueExp);
            return Expression.Lambda<Action<T, V>>
                (assignExp, targetExp, valueExp).Compile();
        }

        public static Func<T, V> CreateGetProperty(PropertyInfo property)
        {
            var paramExpr = Expression.Parameter(typeof(T), "x");
            var expr = Expression.Property(Expression.Convert(paramExpr, typeof(T)), property);
            Func<T, V> getValue = Expression.Lambda<Func<T, V>>(expr, paramExpr).Compile();
            return getValue;
        }


    }

    /// <summary> 
    /// <para>Expression.Lamda 反射加速工具</para>
    /// <para>ZhangYu 2022-06-11</para>
    /// </summary>
    public class ExpressionUtil
    {
        public static Func<object, object> CreateGetField(Type type, string fieldName)
        {
            var objectObj = Expression.Parameter(typeof(object), "obj");
            var classObj = Expression.Convert(objectObj, type);
            var classFunc = Expression.Field(classObj, fieldName);
            var objectFunc = Expression.Convert(classFunc, typeof(object));
            Func<object, object> getValue = Expression.Lambda<Func<object, object>>(objectFunc, objectObj).Compile();
            return getValue;
        }

        public static Func<object, object> CreateGetProperty(Type type, string propertyName)
        {
            var objectObj = Expression.Parameter(typeof(object), "obj");
            var classObj = Expression.Convert(objectObj, type);
            var classFunc = Expression.Property(classObj, propertyName);
            var objectFunc = Expression.Convert(classFunc, typeof(object));
            Func<object, object> getValue = Expression.Lambda<Func<object, object>>(objectFunc, objectObj).Compile();
            return getValue;
        }

        public static Action<object, object> CreateSetField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var objectObj = Expression.Parameter(typeof(object), "target");
            var objectValue = Expression.Parameter(typeof(object), "value");
            var targetExp = Expression.Convert(objectObj, type);
            var valueExp = Expression.Convert(objectValue, field.FieldType);

            MemberExpression fieldExp = Expression.Field(targetExp, field);
            BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);
            return Expression.Lambda<Action<object, object>>
                (assignExp, objectObj, objectValue).Compile();
        }


        public static Action<object, object> CreateSetProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var objectObj = Expression.Parameter(typeof(object), "target");
            var objectValue = Expression.Parameter(typeof(object), "value");
            var targetExp = Expression.Convert(objectObj, type);
            var valueExp = Expression.Convert(objectValue, property.PropertyType);

            MemberExpression fieldExp = Expression.Property(targetExp, property);
            BinaryExpression assignExp = Expression.Assign(fieldExp, valueExp);
            return Expression.Lambda<Action<object, object>>
                (assignExp, objectObj, objectValue).Compile();

            //var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            //var objectObj = Expression.Parameter(typeof(object), "obj");
            //var objectValue = Expression.Parameter(typeof(object), "value");

            //var classObj = Expression.Convert(objectObj, type);
            //var classValue = Expression.Convert(objectValue, property.PropertyType);
            //var classFunc = Expression.Call(classObj, property.GetSetMethod(), classValue);
            //var setProperty = Expression.Lambda<Action<object, object>>(classFunc, objectObj, objectValue).Compile();
            //return setProperty;
        }

        private static Dictionary<Type, Dictionary<string, ExpressionWrapper>> cache = new Dictionary<Type, Dictionary<string, ExpressionWrapper>>();

        public static ExpressionWrapper GetPropertyWrapper(Type type, string propertyName)
        {
            // 查找一级缓存
            Dictionary<string, ExpressionWrapper> wrapperDic = null;
            if (!cache.TryGetValue(type, out wrapperDic))
            {
                wrapperDic = new Dictionary<string, ExpressionWrapper>();
                cache.Add(type, wrapperDic);
            }

            // 查找二级缓存
            ExpressionWrapper wrapper = null;
            if (!wrapperDic.TryGetValue(propertyName, out wrapper))
            {
                wrapper = new ExpressionWrapper();
                wrapper.getValue = CreateGetProperty(type, propertyName);
                wrapper.setValue = CreateSetProperty(type, propertyName);
                wrapperDic.Add(propertyName, wrapper);
            }
            return wrapper;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

    }
}