using UnityEditor;
using System.IO;

namespace Ceffy.Bridge.Editor
{
    internal static class CeffyBridgePreferences
    {
        private const string PrefKey = "Ceffy_TypeScriptOutputPath";
        public const string TypeScriptFileName = "ceffy-bridge.d.ts";

        public static string TypeScriptOutputPath
        {
            get => EditorPrefs.GetString(PrefKey, "");
            set => EditorPrefs.SetString(PrefKey, value);
        }

        public static string GetOutputDirectory()
        {
            var outputPath = TypeScriptOutputPath;
            return string.IsNullOrEmpty(outputPath) ? string.Empty : Path.GetDirectoryName(outputPath) ?? string.Empty;
        }

        public static void SetOutputDirectory(string directory)
        {
            TypeScriptOutputPath = string.IsNullOrWhiteSpace(directory)
                ? string.Empty
                : Path.Combine(directory, TypeScriptFileName);
        }
    }
}
