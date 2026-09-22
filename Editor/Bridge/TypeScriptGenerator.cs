using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
                      $"({unityMethodInterfaces.Count + uiMethodInterfaces.Count} interfaces, {discoveredModels.Count} models)");
        }

        #region Type walking

        private static void WalkInterface(Type iface, HashSet<Type> models)
        {
            foreach (var method in iface.GetMethods())
            {
                WalkType(method.ReturnType, models);
                foreach (var p in method.GetParameters())
                    WalkType(p.ParameterType, models);
            }
        }

        private static void WalkType(Type type, HashSet<Type> models)
        {
            type = UnwrapType(type);
            if (type == null || IsPrimitive(type)) return;
            if (!models.Add(type)) return;

            if (type.IsEnum) return;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                WalkType(field.FieldType, models);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                WalkType(prop.PropertyType, models);
        }

        private static Type UnwrapType(Type type)
        {
            if (type == null) return null;
            if (type == typeof(void) || type == typeof(Task)) return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return UnwrapType(Nullable.GetUnderlyingType(type));
            if (type.IsArray)
                return UnwrapType(type.GetElementType());
            if (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                UnwrapType(args[0]);
                return UnwrapType(args[1]);
            }
            return type;
        }

        private static bool IsPrimitive(Type type)
        {
            return type == typeof(string) || type == typeof(int) || type == typeof(long)
                || type == typeof(float) || type == typeof(double) || type == typeof(bool)
                || type == typeof(decimal) || type == typeof(byte) || type == typeof(short)
                || type == typeof(DateTime) || type == typeof(Guid)
                || type == typeof(object) || type == typeof(void);
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
                    EmitInterface(sb, model);
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

        private static void EmitInterface(StringBuilder sb, Type type)
        {
            sb.AppendLine($"export interface {type.Name} {{");
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"    {BridgeNaming.ToCamelCase(field.Name)}: {MapType(field.FieldType)};");
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine($"    {BridgeNaming.ToCamelCase(prop.Name)}: {MapType(prop.PropertyType)};");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitMethodSignatures(StringBuilder sb, Type iface, bool isUnityMethods)
        {
            foreach (var method in iface.GetMethods())
            {
                var parameterStrings = new List<string>();
                foreach (var parameter in method.GetParameters())
                    parameterStrings.Add($"{BridgeNaming.ToCamelCase(parameter.Name)}: {MapType(parameter.ParameterType)}");
                var paramList = string.Join(", ", parameterStrings);
                var jsMethodName = BridgeNaming.ToCamelCase(method.Name);

                string returnTs;
                if (isUnityMethods)
                {
                    var inner = MapReturnType(method.ReturnType);
                    returnTs = $"Promise<{inner}>";
                }
                else
                {
                    returnTs = $"(({paramList}) => {MapReturnType(method.ReturnType)}) | null";
                    sb.AppendLine($"                {jsMethodName}: {returnTs};");
                    continue;
                }

                sb.AppendLine($"                {jsMethodName}({paramList}): {returnTs};");
            }
        }

        private static string MapReturnType(Type type)
        {
            if (type == typeof(void) || type == typeof(Task)) return "void";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return MapType(type.GetGenericArguments()[0]);
            return MapType(type);
        }

        private static string MapType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)
                || type == typeof(byte) || type == typeof(float) || type == typeof(double)
                || type == typeof(decimal)) return "number";
            if (type == typeof(DateTime)) return "Date";
            if (type == typeof(Guid)) return "string";
            if (type == typeof(object)) return "any";

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return MapType(Nullable.GetUnderlyingType(type)) + " | null";

            if (type.IsArray)
                return MapType(type.GetElementType()) + "[]";

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return MapType(type.GetGenericArguments()[0]) + "[]";

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"Record<{MapType(args[0])}, {MapType(args[1])}>";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return MapType(type.GetGenericArguments()[0]);

            if (type == typeof(void)) return "void";

            return type.Name;
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
