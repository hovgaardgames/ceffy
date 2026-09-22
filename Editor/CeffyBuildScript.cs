#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Ceffy.Editor {
    public static class CeffyBuildScript {

        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject) {
            var rid = GetRid(buildTarget);
            if (string.IsNullOrEmpty(rid) || rid == "unknown") {
                Debug.LogWarning($"[Ceffy] Post-build: unknown RID for buildTarget={buildTarget}. Skipping runtime copy.");
                return;
            }

            var gameRoot = GetGameRoot(pathToBuiltProject);

            var pkgInfo = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(CeffyBuildScript).Assembly);
            if (pkgInfo == null) {
                Debug.LogError("[Ceffy] Post-build: could not locate Ceffy package. Skipping runtime copy.");
                return;
            }

            var source = Path.Combine(pkgInfo.resolvedPath, "NativeRuntime", rid);
            var dest = Path.Combine(gameRoot, "Ceffy.Runtime", rid);

            if (!Directory.Exists(source)) {
                Debug.LogWarning($"[Ceffy] Post-build: source runtime folder not found: {source}. Skipping copy.");
                return;
            }

            Debug.Log($"[Ceffy] Post-build: copying runtime {rid} from {source} to {dest}");
            CopyAndReplaceDirectory(source, dest);
        }

        private static string GetRid(BuildTarget buildTarget) {
            switch (buildTarget) {
                case BuildTarget.StandaloneWindows64:
                    return "win-x64";
                case BuildTarget.StandaloneWindows:
                    return "win-x86";
                case BuildTarget.StandaloneOSX:
                    return "osx-arm64";
                default:
                    return "unknown";
            }
        }

        private static string GetGameRoot(string pathToBuiltProject) {
            if (File.Exists(pathToBuiltProject)) {
                return Path.GetDirectoryName(pathToBuiltProject);
            }
            if (Directory.Exists(pathToBuiltProject)) {
                return pathToBuiltProject;
            }
            return Path.GetDirectoryName(pathToBuiltProject);
        }

        private static void CopyAndReplaceDirectory(string sourceDir, string destDir) {
            if (Directory.Exists(destDir)) {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);

            foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));
            }
            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)) {
                var destPath = filePath.Replace(sourceDir, destDir);
                File.Copy(filePath, destPath, true);
            }
        }
    }
}
#endif
