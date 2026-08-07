using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Editor
{
    /// <summary>
    /// Player builds, invokable headless:
    /// <c>Unity -quit -batchmode -executeMethod CONFUSEDGAMEDEV.PollenGarden.Editor.BuildScript.BuildMac</c>
    /// </summary>
    public static class BuildScript
    {
        private static readonly string[] Scenes = { "Assets/Scenes/test.unity" };

        [MenuItem("Pollen Garden/Build/macOS Player")]
        public static void BuildMac()
        {
            const string location = "Builds/macOS/PollenGarden.app";
            Directory.CreateDirectory(Path.GetDirectoryName(location));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = location,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildScript] {summary.result} → {summary.outputPath} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s, " +
                      $"errors: {summary.totalErrors})");
        }
    }
}
