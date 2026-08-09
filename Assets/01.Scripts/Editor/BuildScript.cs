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
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/PollenGarden.app");
        }

        [MenuItem("Pollen Garden/Build/Windows Player")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/PollenGarden.exe");
        }

        private static void Build(BuildTarget target, string location)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(location));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = location,
                target = target,
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
