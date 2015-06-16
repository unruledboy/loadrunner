using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Org.LoadRunner.Core.Infrastructure
{
    public static class Extensions
    {
        public const char ParameterItemSplitter = '&';

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source != null && action != null)
            {
                foreach (var item in source)
                {
                    action(item);
                }
            }
        }

        public static string ToHTTPData(this Dictionary<string, string> data)
        {
            var result = new StringBuilder();
            data.ForEach(d =>
            {
                result.AppendFormat("{0}={1}{2}", d.Key, Uri.EscapeDataString(d.Value), ParameterItemSplitter);
            });
            return result.ToString().TrimEnd(ParameterItemSplitter);
        }

        public static T Parse<T>(this string value) where T : struct
        {
            try
            {
                T res = (T)Enum.Parse(typeof(T), value);
                if (!Enum.IsDefined(typeof(T), res))
                    return default(T);
                return res;
            }
            catch
            {
                return default(T);
            }
        }

        public static long ToKB(this long value)
        {
            return value / 1024;
        }

        public static string Serialize<T>(this T value)
        {
            if (value == null)
                return string.Empty;

            try
            {
                var xmlserializer = new XmlSerializer(typeof(T));
                var stringWriter = new StringWriter();
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    xmlserializer.Serialize(writer, value);
                    return stringWriter.ToString();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static T Deserialize<T>(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return default(T);

            try
            {
                var xmlserializer = new XmlSerializer(typeof(T));
                using (var stream = new StringReader(value))
                {
                    return (T)xmlserializer.Deserialize(stream);
                }
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}
