using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ceffy.Bridge.Editor
{
    /// <summary>
    /// Editor window that shows all shared interfaces, methods, and models
    /// discovered via [UnityMethods] and [UiMethods] attributes.
    /// </summary>
    internal class CeffySettingsWindow : EditorWindow
    {
        private const float SidePadding = 10f;
        private List<Type> unityMethodInterfaces = new();
        private List<Type> uiMethodInterfaces = new();
        private HashSet<Type> models = new();
        private readonly Dictionary<Type, bool> modelFoldouts = new();
        private Vector2 scrollPos;
        private bool foldUnity = true;
        private bool foldUi = true;
        private bool foldModels = true;

        private static Color UnityAccent => EditorGUIUtility.isProSkin
            ? new Color(0.33f, 0.62f, 0.96f)
            : new Color(0.12f, 0.39f, 0.78f);

        private static Color UiAccent => EditorGUIUtility.isProSkin
            ? new Color(0.29f, 0.78f, 0.52f)
            : new Color(0.11f, 0.50f, 0.27f);

        private static Color ModelsAccent => EditorGUIUtility.isProSkin
            ? new Color(0.95f, 0.66f, 0.28f)
            : new Color(0.74f, 0.42f, 0.08f);

        private static Color MutedText => EditorGUIUtility.isProSkin
            ? new Color(0.78f, 0.78f, 0.78f)
            : new Color(0.30f, 0.30f, 0.30f);

        private static GUIStyle RichFoldoutHeader => new(EditorStyles.foldoutHeader)
        {
            richText = true
        };

        private static GUIStyle RichBoldLabel => new(EditorStyles.boldLabel)
        {
            richText = true
        };

        private static GUIStyle RichMiniLabel => new(EditorStyles.miniLabel)
        {
            richText = true
        };

        private static GUIStyle RichFoldout => new(EditorStyles.foldout)
        {
            richText = true,
            fontStyle = FontStyle.Bold
        };

        private static GUIStyle PaddedHelpBox => new(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(0, 0, 3, 3)
        };

        [MenuItem("Window/Ceffy Settings")]
        public static void Open()
        {
            GetWindow<CeffySettingsWindow>("Ceffy Settings");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            var result = TypeScriptGenerator.DiscoverAll();
            unityMethodInterfaces = result.unityMethods;
            uiMethodInterfaces = result.uiMethods;
            models = result.models;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Refresh();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SidePadding);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginVertical(PaddedHelpBox);
            EditorGUILayout.LabelField(
                $"<color={ToHex(UnityAccent)}>TypeScript Declarations</color>",
                RichBoldLabel);
            EditorGUILayout.Space(4);

            var outputPath = CeffyBridgePreferences.TypeScriptOutputPath;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Output Path", outputPath);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                var initialDirectory = CeffyBridgePreferences.GetOutputDirectory();
                if (string.IsNullOrEmpty(initialDirectory))
                    initialDirectory = Application.dataPath;

                var selectedPath = EditorUtility.SaveFilePanel(
                    "TypeScript Output Path",
                    initialDirectory,
                    CeffyBridgePreferences.TypeScriptFileName,
                    "d.ts");

                if (!string.IsNullOrEmpty(selectedPath))
                    CeffyBridgePreferences.SetOutputDirectory(Path.GetDirectoryName(selectedPath) ?? string.Empty);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                CeffyBridgePreferences.SetOutputDirectory(string.Empty);

            EditorGUILayout.EndHorizontal();

            outputPath = CeffyBridgePreferences.TypeScriptOutputPath;
            var hasPath = !string.IsNullOrWhiteSpace(outputPath);
            if (!hasPath)
            {
                EditorGUILayout.HelpBox(
                    $"Choose an output location. The generated file name is always {CeffyBridgePreferences.TypeScriptFileName}.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"The generated declaration file will be written to {outputPath}.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!hasPath))
            {
                if (GUILayout.Button("Generate TypeScript Declarations", GUILayout.Height(26)))
                    TypeScriptGenerator.Generate();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSection("Unity Methods", unityMethodInterfaces, ref foldUnity, true, UnityAccent);

            DrawSection("UI Methods", uiMethodInterfaces, ref foldUi, false, UiAccent);

            DrawModelsSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            GUILayout.Space(SidePadding);
            EditorGUILayout.EndHorizontal();

            // Status bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var totalMethods = 0;
            foreach (var interfaceType in unityMethodInterfaces)
                totalMethods += interfaceType.GetMethods().Length;
            foreach (var interfaceType in uiMethodInterfaces)
                totalMethods += interfaceType.GetMethods().Length;
            EditorGUILayout.LabelField(
                $"{unityMethodInterfaces.Count + uiMethodInterfaces.Count} interfaces  |  " +
                $"{totalMethods} methods  |  " +
                $"{models.Count} models",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSection(string title, List<Type> interfaces,
            ref bool foldout, bool isUnityMethods, Color accent)
        {
            EditorGUILayout.Space(4);
            var titleMarkup = $"<color={ToHex(accent)}>{title}</color>  <color={ToHex(MutedText)}>({interfaces.Count})</color>";
            foldout = EditorGUILayout.Foldout(foldout, titleMarkup, true, RichFoldoutHeader);
            if (!foldout) return;

            if (interfaces.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"No [{(isUnityMethods ? "UnityMethods" : "UiMethods")}] interfaces found.",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var iface in interfaces)
            {
                EditorGUILayout.BeginVertical(PaddedHelpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<color={ToHex(accent)}>{iface.Name}</color>", RichBoldLabel);
                if (GUILayout.Button("Go to source", EditorStyles.miniButton, GUILayout.Width(85)))
                    OpenSourceFile(iface);
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                foreach (var method in iface.GetMethods())
                {
                    var parameterStrings = new List<string>();
                    foreach (var parameter in method.GetParameters())
                        parameterStrings.Add($"{FormatTypeName(parameter.ParameterType)} {parameter.Name}");
                    var paramStr = string.Join(", ", parameterStrings);
                    var returnStr = FormatTypeName(method.ReturnType);
                    EditorGUILayout.LabelField(
                        $"<color={ToHex(MutedText)}>{method.Name}({paramStr}) -> {returnStr}</color>",
                        RichMiniLabel);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModelsSection()
        {
            EditorGUILayout.Space(4);
            var titleMarkup = $"<color={ToHex(ModelsAccent)}>Shared Models</color>  <color={ToHex(MutedText)}>({models.Count})</color>";
            foldModels = EditorGUILayout.Foldout(foldModels, titleMarkup, true, RichFoldoutHeader);
            if (!foldModels) return;

            EditorGUI.indentLevel++;
            if (models.Count == 0)
            {
                EditorGUILayout.LabelField("No shared models discovered.", EditorStyles.miniLabel);
            }
            else
            {
                var orderedModels = new List<Type>(models);
                orderedModels.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCulture));
                foreach (var model in orderedModels)
                {
                    string kind;
                    string detail;
                    if (model.IsEnum)
                    {
                        kind = "enum";
                        detail = $"{Enum.GetNames(model).Length} values";
                    }
                    else
                    {
                        kind = model.IsValueType ? "struct" : "class";
                        var fieldCount = model.GetFields(BindingFlags.Public | BindingFlags.Instance).Length
                                       + model.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length;
                        detail = $"{fieldCount} fields";
                    }

                    var isExpanded = modelFoldouts.TryGetValue(model, out var expanded) && expanded;

                    EditorGUILayout.BeginVertical(PaddedHelpBox);
                    EditorGUILayout.BeginHorizontal();
                    isExpanded = EditorGUILayout.Foldout(
                        isExpanded,
                        $"<color={ToHex(GetModelAccent(model))}>{model.Name}</color>  <color={ToHex(MutedText)}>({kind}, {detail})</color>",
                        true,
                        RichFoldout);
                    modelFoldouts[model] = isExpanded;
                    if (GUILayout.Button("Go", EditorStyles.miniButton, GUILayout.Width(30)))
                        OpenSourceFile(model);
                    EditorGUILayout.EndHorizontal();

                    if (isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        if (model.IsEnum)
                        {
                            foreach (var value in Enum.GetNames(model))
                                EditorGUILayout.LabelField($"<color={ToHex(MutedText)}>{value}</color>", RichMiniLabel);
                        }
                        else
                        {
                            foreach (var field in model.GetFields(BindingFlags.Public | BindingFlags.Instance))
                                EditorGUILayout.LabelField(
                                    $"<color={ToHex(MutedText)}>{field.Name}: {FormatTypeName(field.FieldType)}</color>",
                                    RichMiniLabel);

                            foreach (var property in model.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                EditorGUILayout.LabelField(
                                    $"<color={ToHex(MutedText)}>{property.Name}: {FormatTypeName(property.PropertyType)}</color>",
                                    RichMiniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUI.indentLevel--;
        }

        private static Color GetModelAccent(Type model)
        {
            if (model.IsEnum)
                return ModelsAccent;

            return model.IsValueType
                ? new Color(ModelsAccent.r * 0.90f, ModelsAccent.g * 0.90f, ModelsAccent.b * 0.90f)
                : ModelsAccent;
        }

        private static string ToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

        private static string FormatTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(Task)) return "Task";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return $"Task<{FormatTypeName(type.GetGenericArguments()[0])}>";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return $"{FormatTypeName(Nullable.GetUnderlyingType(type))}?";
            if (type.IsArray)
                return $"{FormatTypeName(type.GetElementType())}[]";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return $"List<{FormatTypeName(type.GetGenericArguments()[0])}>";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"Dictionary<{FormatTypeName(args[0])}, {FormatTypeName(args[1])}>";
            }
            return type.Name;
        }

        private static void OpenSourceFile(Type type)
        {
            var guids = AssetDatabase.FindAssets($"{type.Name} t:Script");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{type.Name}.cs"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (asset != null)
                    {
                        AssetDatabase.OpenAsset(asset);
                        return;
                    }
                }
            }
            Debug.LogWarning($"[Ceffy Bridge] Could not find source file for {type.FullName}");
        }
    }
}
