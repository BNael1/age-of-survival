using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace AgeOfSurvival.Editor
{
    public static class MultiplayerBuild
    {
        public static void BuildMacClient()
        {
            Build(
                BuildTarget.StandaloneOSX,
                StandaloneBuildSubtarget.Player,
                Argument("output") ?? "Builds/macOS/AgeOfSurvival.app",
                "arm64");
        }

        public static void BuildMacServer()
        {
            Build(
                BuildTarget.StandaloneOSX,
                StandaloneBuildSubtarget.Server,
                Argument("output") ?? "Builds/macOS-Server/AgeOfSurvivalServer.app",
                "arm64");
        }

        public static void BuildWindowsClient()
        {
            Build(
                BuildTarget.StandaloneWindows64,
                StandaloneBuildSubtarget.Player,
                Argument("output") ?? "Builds/Windows/AgeOfSurvival.exe",
                "x86_64");
        }

        public static void BuildLinuxServer()
        {
            Build(
                BuildTarget.StandaloneLinux64,
                StandaloneBuildSubtarget.Server,
                Argument("output") ?? "Builds/Linux-Server/AgeOfSurvivalServer",
                "x86_64");
        }

        private static void Build(
            BuildTarget target,
            StandaloneBuildSubtarget subtarget,
            string output,
            string architecture)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0) throw new InvalidOperationException("At least one enabled build scene is required.");
            string fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput));

            string platformName = BuildPipeline.GetBuildTargetName(target);
            string previousArchitecture = EditorUserBuildSettings.GetPlatformSettings(
                platformName,
                "Architecture");
            try
            {
                EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", architecture);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullOutput,
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    subtarget = (int)subtarget,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Build failed for {target}/{subtarget}: {report.summary.result}.");
                }

                Console.WriteLine(
                    $"AOS_BUILD_OK target={target} subtarget={subtarget} "
                    + $"bytes={report.summary.totalSize} output={fullOutput}");
            }
            finally
            {
                EditorUserBuildSettings.SetPlatformSettings(
                    platformName,
                    "Architecture",
                    previousArchitecture);
            }
        }

        private static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }

            return scenes.ToArray();
        }

        private static string Argument(string name)
        {
            string prefix = $"--{name}=";
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return arguments[index].Substring(prefix.Length);
                }
            }

            return null;
        }
    }
}
