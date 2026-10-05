using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace Ceffy.Bridge.Editor
{
    /// <summary>
    /// Scans all assemblies for interfaces tagged with [UnityMethods] or [UiMethods],
    /// transitively walks all referenced types, and emits a .d.ts file.
    /// Runs automatically after each C# compilation.
    /// </summary>
    internal static class TypeScriptGenerator
    {
        private static IContractResolver ContractResolver => BridgeSerializer.Settings.ContractResolver;

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            var outputPath = CeffyBridgePreferences.TypeScriptOutputPath;
            if (!string.IsNullOrEmpty(outputPath))
                Generate();
        }

        public static void Generate()
        {
            var outputPath = CeffyBridgePreferences.TypeScriptOutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.Log("[Ceffy Bridge] TypeScript output path not set. Configure it in the Ceffy Bridge window.");
                return;
            }

            var (unityMethodInterfaces, uiMethodInterfaces, discoveredModels) = DiscoverAll();

            if (unityMethodInterfaces.Count == 0 && uiMethodInterfaces.Count == 0)
            {
                Debug.Log("[Ceffy Bridge] No [UnityMethods] or [UiMethods] interfaces found. Skipping generation.");
                return;
            }

            foreach (var iface in unityMethodInterfaces)
                WalkInterface(iface, discoveredModels);
            foreach (var iface in uiMethodInterfaces)
                WalkInterface(iface, discoveredModels);

            var ts = BuildTypeScript(unityMethodInterfaces, uiMethodInterfaces, discoveredModels);

            if (File.Exists(outputPath) && File.ReadAllText(outputPath) == ts)
                return;

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, ts);
            Debug.Log($"[Ceffy Bridge] Generated TypeScript declarations at {outputPath} " +
                      $"({unityMethodInterfaces.Count + uiMethodInterfaces.Count} interfaces, " +
                      $"{discoveredModels.Count} models)");
        }

        #region Type walking

        internal static void WalkInterface(Type iface, HashSet<Type> models)
        {
            foreach (var method in iface.GetMethods())
            {
                WalkType(method.ReturnType, models, ContractResolver);
                foreach (var p in method.GetParameters())
                    WalkType(p.ParameterType, models, ContractResolver);
            }
        }

        private static void WalkType(Type type, HashSet<Type> models, IContractResolver resolver)
        {
            if (type == null) return;
            if (type == typeof(void) || type == typeof(Task)) return;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                WalkType(type.GetGenericArguments()[0], models, resolver);
                return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                WalkType(Nullable.GetUnderlyingType(type), models, resolver);
                return;
            }
            if (IsPrimitive(type)) return;
            if (type.IsEnum)
            {
                models.Add(type);
                return;
            }

            if (TryGetDictionaryTypes(type, out var keyType, out var valueType))
            {
                WalkType(keyType, models, resolver);
                WalkType(valueType, models, resolver);
                return;
            }
            if (TryGetCollectionElementType(type, out var elementType))
            {
                WalkType(elementType, models, resolver);
                return;
            }

            var contract = resolver.ResolveContract(type);
            if (!(contract is JsonObjectContract objectContract) || !models.Add(type)) return;

            foreach (var property in GetSerializableProperties(objectContract))
                WalkType(property.PropertyType, models, resolver);

            foreach (var derivedType in TypeCache.GetTypesDerivedFrom(type))
                if (!derivedType.ContainsGenericParameters)
                    WalkType(derivedType, models, resolver);
        }

        private static IEnumerable<JsonProperty> GetSerializableProperties(JsonObjectContract contract)
        {
            foreach (var property in contract.Properties)
            {
                if (property.Ignored || property.PropertyType == null
                    || !(property.Readable || property.Writable)
                    || IsIndexer(property))
                    continue;

                yield return property;
            }
        }

        private static bool IsIndexer(JsonProperty property)
        {
            if (property.DeclaringType == null || string.IsNullOrEmpty(property.UnderlyingName))
                return false;

            var member = property.DeclaringType.GetMember(
                property.UnderlyingName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var candidate in member)
            {
                if (candidate is PropertyInfo propertyInfo && propertyInfo.GetIndexParameters().Length > 0)
                    return true;
            }

            return false;
        }

        private static bool IsPrimitive(Type type)
        {
            return MapPrimitiveType(type) != null || type == typeof(void);
        }

        private static string MapPrimitiveType(Type type)
        {
            if (type.IsEnum) return null;
            if (type == typeof(string) || type == typeof(char) || type == typeof(byte[])
                || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
                return "string";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(object)) return "any";
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
                || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";

            return null;
        }

        #endregion

        #region TypeScript emission

        internal static string BuildTypeScript(
            List<Type> unityMethods,
            List<Type> uiMethods,
            HashSet<Type> models)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated by Ceffy Bridge -- do not edit");
            sb.AppendLine();

            var orderedModels = new List<Type>(models);
            orderedModels.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCulture));
            foreach (var model in orderedModels)
            {
                if (model.IsEnum)
                    EmitEnum(sb, model);
                else
                    EmitInterface(sb, model, ContractResolver);
            }

            sb.AppendLine("declare global {");
            sb.AppendLine("    interface Window {");
            sb.AppendLine("        ceffy: {");

            sb.AppendLine("            unity: {");
            foreach (var iface in unityMethods)
                EmitMethodSignatures(sb, iface, isUnityMethods: true);
            sb.AppendLine("            };");

            sb.AppendLine("            ui: {");
            foreach (var iface in uiMethods)
                EmitMethodSignatures(sb, iface, isUnityMethods: false);
            sb.AppendLine("            };");

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("export {}");

            return sb.ToString();
        }

        private static void EmitEnum(StringBuilder sb, Type enumType)
        {
            sb.AppendLine($"export enum {enumType.Name} {{");
            foreach (var name in Enum.GetNames(enumType))
            {
                var val = Convert.ToInt64(Enum.Parse(enumType, name));
                sb.AppendLine($"    {name} = {val},");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitInterface(StringBuilder sb, Type type, IContractResolver resolver)
        {
            sb.AppendLine($"export interface {type.Name} {{");
            if (resolver.ResolveContract(type) is JsonObjectContract contract)
            {
                foreach (var property in GetSerializableProperties(contract))
                {
                    var member = GetMember(property.DeclaringType ?? type, property.UnderlyingName);
                    var memberName = property.PropertyName ?? property.UnderlyingName;
                    var isOptional = CanBeOmitted(property);
                    var memberType = MapType(property.PropertyType, member);
                    if (property.Required == Required.AllowNull && !memberType.EndsWith(" | null"))
                        memberType += " | null";
                    sb.AppendLine(
                        $"    {FormatPropertyName(memberName)}{(isOptional ? "?" : string.Empty)}: {memberType};");
                }
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitMethodSignatures(StringBuilder sb, Type iface, bool isUnityMethods)
        {
            foreach (var method in iface.GetMethods())
            {
                var parameterStrings = new List<string>();
                foreach (var parameter in method.GetParameters())
                    parameterStrings.Add(
                        $"{FormatPropertyName(BridgeNaming.ToCamelCase(parameter.Name))}" +
                        $"{(parameter.IsOptional ? "?" : string.Empty)}: " +
                        $"{MapType(parameter.ParameterType, parameter)}");
                var paramList = string.Join(", ", parameterStrings);
                var jsMethodName = BridgeNaming.ToCamelCase(method.Name);

                string returnTs;
                if (isUnityMethods)
                {
                    var inner = MapReturnType(method);
                    returnTs = $"Promise<{inner}>";
                }
                else
                {
                    var uiResultType = MapReturnType(method);
                    if (method.ReturnType == typeof(Task)
                        || method.ReturnType.IsGenericType
                        && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                        uiResultType = $"{uiResultType} | Promise<{uiResultType}>";

                    returnTs = $"(({paramList}) => {uiResultType}) | null";
                    sb.AppendLine($"                {jsMethodName}: {returnTs};");
                    continue;
                }

                sb.AppendLine($"                {jsMethodName}({paramList}): {returnTs};");
            }
        }

        private static string MapReturnType(MethodInfo method)
        {
            var type = method.ReturnType;
            if (type == typeof(void) || type == typeof(Task)) return "void";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return MapType(type.GetGenericArguments()[0], method.ReturnParameter, 1);
            return MapType(type, method.ReturnParameter);
        }

        private static string MapType(
            Type type,
            ICustomAttributeProvider nullableProvider = null,
            int nullablePosition = 0)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return MapType(Nullable.GetUnderlyingType(type)) + " | null";

            var isNullableReference = IsNullableReference(nullableProvider, nullablePosition);
            var primitiveType = MapPrimitiveType(type);
            if (primitiveType != null)
                return isNullableReference && !type.IsValueType ? $"{primitiveType} | null" : primitiveType;

            if (type.IsArray)
            {
                var arrayType = MapArrayType(type.GetElementType(), nullableProvider, nullablePosition + 1);
                return isNullableReference ? $"{arrayType} | null" : arrayType;
            }

            if (TryGetDictionaryTypes(type, out var keyType, out var valueType))
            {
                var valuePosition = nullablePosition + 1 + CountNullableFlags(keyType);
                var dictionaryType = $"Record<{MapType(keyType, nullableProvider, nullablePosition + 1)}, " +
                                     $"{MapType(valueType, nullableProvider, valuePosition)}>";
                return isNullableReference ? $"{dictionaryType} | null" : dictionaryType;
            }
            if (TryGetCollectionElementType(type, out var elementType))
            {
                var collectionType = MapArrayType(elementType, nullableProvider, nullablePosition + 1);
                return isNullableReference ? $"{collectionType} | null" : collectionType;
            }

            return isNullableReference ? $"{type.Name} | null" : type.Name;
        }

        private static int CountNullableFlags(Type type)
        {
            if (type.IsArray) return 1 + CountNullableFlags(type.GetElementType());
            if (!type.IsGenericType) return type.IsValueType ? 0 : 1;
            if (Nullable.GetUnderlyingType(type) is Type underlyingType)
                return CountNullableFlags(underlyingType);

            var count = 1;
            foreach (var argument in type.GetGenericArguments())
                count += CountNullableFlags(argument);

            return count;
        }

        private static string MapArrayType(
            Type elementType,
            ICustomAttributeProvider nullableProvider,
            int nullablePosition)
        {
            var mappedElementType = MapType(elementType, nullableProvider, nullablePosition);
            return mappedElementType.Contains(" | ") ? $"({mappedElementType})[]" : $"{mappedElementType}[]";
        }

        private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            var contract = ContractResolver.ResolveContract(type);
            if (contract is JsonDictionaryContract dictionaryContract)
            {
                keyType = dictionaryContract.DictionaryKeyType;
                valueType = dictionaryContract.DictionaryValueType;
                return keyType != null && valueType != null;
            }

            foreach (var candidate in GetTypeAndInterfaces(type))
            {
                if (!candidate.IsGenericType) continue;
                var definition = candidate.GetGenericTypeDefinition();
                if (definition != typeof(IDictionary<,>) && definition != typeof(IReadOnlyDictionary<,>))
                    continue;

                var arguments = candidate.GetGenericArguments();
                keyType = arguments[0];
                valueType = arguments[1];
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }

        private static bool TryGetCollectionElementType(Type type, out Type elementType)
        {
            var contract = ContractResolver.ResolveContract(type);
            if (contract is JsonArrayContract arrayContract && arrayContract.CollectionItemType != null)
            {
                elementType = arrayContract.CollectionItemType;
                return true;
            }

            foreach (var candidate in GetTypeAndInterfaces(type))
            {
                if (!candidate.IsGenericType) continue;
                var definition = candidate.GetGenericTypeDefinition();
                if (definition != typeof(IEnumerable<>) && definition != typeof(ICollection<>)
                    && definition != typeof(IList<>) && definition != typeof(IReadOnlyCollection<>)
                    && definition != typeof(IReadOnlyList<>))
                    continue;

                elementType = candidate.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private static IEnumerable<Type> GetTypeAndInterfaces(Type type)
        {
            yield return type;
            foreach (var interfaceType in type.GetInterfaces())
                yield return interfaceType;
        }

        private static bool CanBeOmitted(JsonProperty property)
        {
            if (property.Required == Required.Always || property.Required == Required.AllowNull)
                return false;

            var nullValueHandling = property.NullValueHandling ?? BridgeSerializer.Settings.NullValueHandling;
            var canBeNull = !property.PropertyType.IsValueType
                || Nullable.GetUnderlyingType(property.PropertyType) != null;
            return canBeNull && nullValueHandling == NullValueHandling.Ignore;
        }

        private static string FormatPropertyName(string propertyName)
        {
            if (!string.IsNullOrEmpty(propertyName) &&
                (char.IsLetter(propertyName[0]) || propertyName[0] == '_' || propertyName[0] == '$'))
            {
                var isIdentifier = true;
                for (var i = 1; i < propertyName.Length; i++)
                {
                    if (char.IsLetterOrDigit(propertyName[i]) || propertyName[i] == '_' || propertyName[i] == '$')
                        continue;
                    isIdentifier = false;
                    break;
                }

                if (isIdentifier) return propertyName;
            }

            return JsonConvert.ToString(propertyName);
        }

        private static MemberInfo GetMember(Type type, string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return null;
            var members = type.GetMember(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return members.Length > 0 ? members[0] : null;
        }

        private static bool IsNullableReference(ICustomAttributeProvider provider, int position = 0)
        {
            var nullableFlag = GetNullableFlag(provider, position);
            if (nullableFlag.HasValue) return nullableFlag.Value == 2;

            var context = GetNullableContext(provider);
            return context == 2;
        }

        private static byte? GetNullableFlag(ICustomAttributeProvider provider, int position)
        {
            IList<CustomAttributeData> attributes;
            if (provider is MemberInfo member)
                attributes = member.GetCustomAttributesData();
            else if (provider is ParameterInfo parameter)
                attributes = parameter.GetCustomAttributesData();
            else
                return null;

            foreach (var attribute in attributes)
            {
                if (attribute.AttributeType.Name != "NullableAttribute" || attribute.ConstructorArguments.Count == 0)
                    continue;

                var value = attribute.ConstructorArguments[0].Value;
                if (value is byte flag) return flag;
                if (value is IList<CustomAttributeTypedArgument> flags && position < flags.Count
                    && flags[position].Value is byte selectedFlag)
                    return selectedFlag;
            }

            return null;
        }

        private static byte GetNullableContext(ICustomAttributeProvider provider)
        {
            var member = provider as MemberInfo;
            if (provider is ParameterInfo parameter)
                member = parameter.Member;

            while (member != null)
            {
                var context = GetNullableContext(member.GetCustomAttributesData());
                if (context.HasValue) return context.Value;
                member = member.DeclaringType;
            }

            return 0;
        }

        private static byte? GetNullableContext(IList<CustomAttributeData> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeType.Name == "NullableContextAttribute"
                    && attribute.ConstructorArguments.Count > 0
                    && attribute.ConstructorArguments[0].Value is byte flag)
                    return flag;
            }

            return null;
        }

        #endregion

        #region Introspection (used by the Editor Window)

        public static (List<Type> unityMethods, List<Type> uiMethods, HashSet<Type> models) DiscoverAll()
        {
            var unityMethodInterfaces = new List<Type>();
            var uiMethodInterfaces = new List<Type>();
            var discoveredModels = new HashSet<Type>();

            foreach (var type in TypeCache.GetTypesWithAttribute<UnityMethodsAttribute>())
            {
                if (type.IsInterface)
                {
                    unityMethodInterfaces.Add(type);
                    WalkInterface(type, discoveredModels);
                }
            }

            foreach (var type in TypeCache.GetTypesWithAttribute<UiMethodsAttribute>())
            {
                if (type.IsInterface && type.GetCustomAttribute<UnityMethodsAttribute>() == null)
                {
                    uiMethodInterfaces.Add(type);
                    WalkInterface(type, discoveredModels);
                }
            }

            return (unityMethodInterfaces, uiMethodInterfaces, discoveredModels);
        }

        #endregion
    }
}
