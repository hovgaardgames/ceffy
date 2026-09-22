using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Ceffy.Bridge
{
    internal static class BridgeSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                    OverrideSpecifiedNames = false
                }
            }
        };

        public static string SerializeRequest(string method, IEnumerable<object> parameters, int? id)
        {
            using var sw = new StringWriter();
            using var w = new JsonTextWriter(sw);
            w.WriteStartObject();
            w.WritePropertyName("method");
            w.WriteValue(method);
            if (parameters != null)
            {
                w.WritePropertyName("params");
                w.WriteRawValue(JsonConvert.SerializeObject(parameters, Settings));
            }
            if (id.HasValue)
            {
                w.WritePropertyName("id");
                w.WriteValue(id.Value);
            }
            w.WriteEndObject();
            return sw.ToString();
        }

        public static string SerializeResponse(int id, object result)
        {
            using var sw = new StringWriter();
            using var w = new JsonTextWriter(sw);
            w.WriteStartObject();
            w.WritePropertyName("id");
            w.WriteValue(id);
            w.WritePropertyName("result");
            WriteValue(w, result);
            w.WriteEndObject();
            return sw.ToString();
        }

        public static string SerializeError(int id, string error)
        {
            using var sw = new StringWriter();
            using var w = new JsonTextWriter(sw);
            w.WriteStartObject();
            w.WritePropertyName("id");
            w.WriteValue(id);
            w.WritePropertyName("error");
            w.WriteValue(error);
            w.WriteEndObject();
            return sw.ToString();
        }

        private static void WriteValue(JsonTextWriter w, object value)
        {
            if (value == null)
                w.WriteNull();
            else if (value is JToken jt)
                jt.WriteTo(w);
            else if (value is string or int or long or float or double or bool)
                w.WriteValue(value);
            else
                w.WriteRawValue(JsonConvert.SerializeObject(value, Settings));
        }

        public static string ExtractStringField(string json, string fieldName)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName
                        || !string.Equals((string)reader.Value, fieldName, StringComparison.Ordinal))
                        continue;
                    if (!reader.Read() || reader.TokenType != JsonToken.String)
                        return null;

                    return (string)reader.Value;
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static int? ExtractIntField(string json, string fieldName)
        {
            var pattern = "\"" + fieldName + "\":";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pattern.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            int start = idx;
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-')) idx++;
            return int.TryParse(json.Substring(start, idx - start), out int val) ? val : null;
        }

        public static List<object> ExtractParams(string json)
        {
            var pattern = "\"params\":";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pattern.Length;
            while (idx < json.Length && json[idx] != '[') idx++;
            using var reader = new JsonTextReader(new StringReader(json.Substring(idx)));
            reader.Read();
            var list = new List<object>(4);
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                list.Add(ReadJsonValue(reader));
            }
            return list.Count > 0 ? list : null;
        }

        public static object ExtractResult(string json)
        {
            var pattern = "\"result\":";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pattern.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            using var reader = new JsonTextReader(new StringReader(json.Substring(idx)));
            reader.Read();
            return ReadJsonValue(reader);
        }

        private static object ReadJsonValue(JsonTextReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject: return JObject.Load(reader);
                case JsonToken.StartArray: return JArray.Load(reader);
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                    return reader.Value;
                case JsonToken.Null:
                default:
                    return null;
            }
        }

        public static object[] ConvertParameters(MethodInfo method, List<object> parameters)
        {
            var paramTypes = method.GetParameters();

            if (parameters == null || parameters.Count == 0)
            {
                if (paramTypes.Length == 0)
                    return Array.Empty<object>();
                var defaults = new object[paramTypes.Length];
                for (int i = 0; i < paramTypes.Length; i++)
                    defaults[i] = paramTypes[i].IsOptional ? paramTypes[i].DefaultValue : null;
                return defaults;
            }

            var result = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var targetType = paramTypes[i].ParameterType;
                result[i] = ConvertValue(param, targetType);
            }
            return result;
        }

        public static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsPrimitive || targetType == typeof(string))
                return Convert.ChangeType(value, targetType);
            return JsonConvert.DeserializeObject(
                JsonConvert.SerializeObject(value, Settings), targetType, Settings);
        }
    }
}
