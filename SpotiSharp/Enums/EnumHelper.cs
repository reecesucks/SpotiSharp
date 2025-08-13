
using System;
using System.ComponentModel;
using System.Reflection;

namespace SpotiSharp.Enums
{
    static class EnumHelper
    {
        public static string GetDescription(Enum enumval)
        {
            try
            {
                FieldInfo fi = enumval.GetType().GetField(enumval.ToString());
                var attribute = fi.GetCustomAttribute<DescriptionAttribute>();
                return attribute?.Description ?? enumval.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static List<String> GetDescriptionIDList<TEnum>() where TEnum : Enum
        {
            var list = new List<String>();

            foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
            {
                //FieldInfo fieldInfo = typeof(TEnum).GetField(enumValue.ToString());
                //DescriptionAttribute[] attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);


                //string description = (attributes != null && attributes.Length > 0) ? attributes[0].Description : enumValue.ToString();

                var description = GetDescription(enumValue);
                int id = Convert.ToInt32(enumValue);

                list.Add($"{description}\n{id})");
            }
            return list;
        }

        public static Dictionary<int, string> GetEnumListAsDictionary<T>() where T : Enum
        {
            var dict = Enum.GetValues(typeof(T))
               .Cast<T>()
               .ToDictionary(t => Convert.ToInt32(t), t => GetDescription(t));
            return dict;
        }
    }
}
