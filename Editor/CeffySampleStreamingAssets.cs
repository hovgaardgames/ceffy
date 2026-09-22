#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ceffy.Editor
{
    /// <summary>
    /// Copies StreamingAssets from imported Ceffy samples to the project-level
    /// Assets/StreamingAssets/ so that the streaming-assets:// protocol works
    /// at runtime.
    /// </summary>
    public class CeffySampleStreamingAssets : AssetPostprocessor
    {
        private static readonly string[] PackageSampleRoots =
        {
            "Ceffy",
            "com.hovgaard.ceffy"
        };

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var sampleRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in importedAssets)
            {
                if (!TryGetImportedSampleRoot(path, out var sampleRoot))
                    continue;

                sampleRoots.Add(sampleRoot);
            }

            bool changedAny = false;
            foreach (var sampleRoot in sampleRoots)
                changedAny |= CopyStreamingAssetsForSample(sampleRoot);

            if (changedAny)
                AssetDatabase.Refresh();
        }

        private static bool TryGetImportedSampleRoot(string assetPath, out string sampleRoot)
        {
            sampleRoot = null;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var normalizedPath = assetPath.Replace('\\', '/');
            var segments = normalizedPath.Split('/');

            if (segments.Length < 5)
                return false;

            if (!string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[1], "Samples", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsPackageSampleRoot(segments[2]))
                return false;

            sampleRoot = string.Join("/", segments, 0, 5);
            return true;
        }

        private static bool IsPackageSampleRoot(string value)
        {
            foreach (var packageSampleRoot in PackageSampleRoots)
            {
                if (string.Equals(packageSampleRoot, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool CopyStreamingAssetsForSample(string sampleRoot)
        {
            var sampleRootFullPath = AssetPathToFullPath(sampleRoot);
            var sourceStreamingAssetsPath = Path.Combine(sampleRootFullPath, "StreamingAssets");
            if (!Directory.Exists(sourceStreamingAssetsPath))
                return false;

            bool copiedAny = false;
            foreach (var sourcePath in Directory.GetFiles(sourceStreamingAssetsPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceStreamingAssetsPath, sourcePath);
                var destinationPath = Path.Combine(Application.streamingAssetsPath, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Copy(sourcePath, destinationPath, overwrite: true);
                copiedAny = true;
                Debug.Log($"[Ceffy] Copied sample StreamingAsset: {relativePath}");
            }

            if (!copiedAny)
                return false;

            var sourceStreamingAssetsAssetPath = $"{sampleRoot}/StreamingAssets";
            if (AssetDatabase.IsValidFolder(sourceStreamingAssetsAssetPath))
            {
                if (AssetDatabase.DeleteAsset(sourceStreamingAssetsAssetPath))
                {
                    Debug.Log($"[Ceffy] Removed imported sample StreamingAssets folder: {sourceStreamingAssetsAssetPath}");
                    return true;
                }

                Debug.LogWarning($"[Ceffy] Failed to remove imported sample StreamingAssets folder: {sourceStreamingAssetsAssetPath}");
            }

            return true;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var normalizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
        }
    }
}
#endif
