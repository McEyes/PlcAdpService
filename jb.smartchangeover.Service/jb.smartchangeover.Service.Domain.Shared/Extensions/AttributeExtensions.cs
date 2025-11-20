using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public static class AttributeExtensions
    {
        public static string GetDescription<T>(this string field)
        {
            FieldInfo fieldInfo = typeof(T).GetField(field);
            if (fieldInfo == null)
            {
                var memberInfo = typeof(T).GetMember(field).FirstOrDefault();
                if (memberInfo != null)
                {
                    var attr = Attribute.GetCustomAttribute(memberInfo, typeof(DescriptionAttribute)) as DescriptionAttribute;
                    return attr == null ? field : attr.Description;
                }
                return field;
            }
            var attribute = Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute == null ? field : attribute.Description;
        }
        public static string GetFieldDescription<T>(this T obj, string field) where T : class
        {
            var fieldInfo = typeof(T).GetField(field);
            if (fieldInfo == null)
            {
                var memberInfo = typeof(T).GetMember(field).FirstOrDefault();
                if (memberInfo != null)
                {
                    var attr = Attribute.GetCustomAttribute(memberInfo, typeof(DescriptionAttribute)) as DescriptionAttribute;
                    return attr == null ? field : attr.Description;
                }
                return field;
            }
            var attribute = Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute == null ? field : attribute.Description;
        }
    }
}
