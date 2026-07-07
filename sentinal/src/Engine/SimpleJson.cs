using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SentinelX.Engine
{
    public static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";

            if (obj is string)
            {
                return "\"" + Escape((string)obj) + "\"";
            }
            if (obj is bool)
            {
                return (bool)obj ? "true" : "false";
            }
            if (obj is int || obj is long || obj is double || obj is float || obj is decimal)
            {
                return obj.ToString();
            }
            if (obj is DateTime)
            {
                return "\"" + ((DateTime)obj).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "\"";
            }
            if (obj is IDictionary)
            {
                var dict = (IDictionary)obj;
                var entries = new List<string>();
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key == null) continue;
                    entries.Add(string.Format("\"{0}\":{1}", Escape(entry.Key.ToString()), Serialize(entry.Value)));
                }
                return "{" + string.Join(",", entries.ToArray()) + "}";
            }
            if (obj is IEnumerable)
            {
                var enumerable = (IEnumerable)obj;
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(Serialize(item));
                }
                return "[" + string.Join(",", items.ToArray()) + "]";
            }

            // Fallback for custom classes using reflection
            var type = obj.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var propEntries = new List<string>();
            foreach (var prop in props)
            {
                // Skip properties with ignore or indexing
                if (prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var val = prop.GetValue(obj, null);
                    propEntries.Add(string.Format("\"{0}\":{1}", Escape(prop.Name), Serialize(val)));
                }
                catch
                {
                    // Ignore property if evaluation fails
                }
            }
            return "{" + string.Join(",", propEntries.ToArray()) + "}";
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
