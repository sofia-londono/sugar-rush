using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Web version (WebGL) build, ready for Vercel: Brotli-compressed files served with the right
    /// headers (vercel.json), hashed file names (safe to cache forever), the candy loading page
    /// (WebGLTemplates/SugarRush, noindex), robots.txt, and the access code.
    /// The code is read from web-access-code.txt in the project folder (never committed: the repo
    /// is public); only its salted hash goes into the build.
    /// Output: ./Web (also never committed; it is deployed from there).
    /// </summary>
    public static class WebBuild
    {
        const string CodeFile = "web-access-code.txt";
        const string HashAsset = "Assets/_SugarRush/Resources/Web/AccessHash.txt";
        const string OutputDir = "Web";

        [MenuItem("Sugar Rush/Web Build")]
        public static string Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                return "Web build: the Web Build Support module is not installed (Unity Hub > Installs > Add modules).";
            if (!File.Exists(CodeFile))
                return $"Web build: create {CodeFile} in the project folder with the access code on the first line.";
            string code = File.ReadAllLines(CodeFile)[0].Trim();
            if (code.Length < 4) return "Web build: the access code must have at least 4 characters.";

            Directory.CreateDirectory(Path.GetDirectoryName(HashAsset));
            File.WriteAllText(HashAsset, AccessGate.Hash(code));
            AssetDatabase.ImportAsset(HashAsset);

            ConfigurePlayer();
            ConfigureAudio();

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                return $"Web build FAILED: {report.summary.result}, errors={report.summary.totalErrors}";

            File.Copy("Tools/web/vercel.json", Path.Combine(OutputDir, "vercel.json"), true);
            File.Copy("Tools/web/robots.txt", Path.Combine(OutputDir, "robots.txt"), true);

            long bytes = 0;
            foreach (var f in Directory.GetFiles(OutputDir, "*", SearchOption.AllDirectories)) bytes += new FileInfo(f).Length;
            return $"Web build OK: {bytes / 1048576f:0.0} MB download in ./{OutputDir}, time {report.summary.totalTime:mm\\:ss}";
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.WebGL.template = "PROJECT:SugarRush";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false; // Vercel sends Content-Encoding: br (vercel.json)
            PlayerSettings.WebGL.nameFilesAsHashes = true;      // new names on every build, so caching never serves stale files
            PlayerSettings.WebGL.dataCaching = true;            // second visit loads from the browser cache
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            // Keep reflection-heavy online packages working: strip as little as possible.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.runInBackground = true; // online races keep running in a background tab
        }

        /// <summary>
        /// Browsers decode music to raw samples in memory, so the web copy of the music uses half
        /// the sample rate (a few MB instead of tens of MB of RAM per song).
        /// </summary>
        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_SugarRush/Audio/Music" }))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var settings = importer.defaultSampleSettings;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 22050;
                settings.quality = 0.35f;
                if (importer.SetOverrideSampleSettings("WebGL", settings)) importer.SaveAndReimport();
            }
        }
    }
}
