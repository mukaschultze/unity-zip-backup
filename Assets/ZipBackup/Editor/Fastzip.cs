using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZipBackup {

    [Flags]
    public enum FastZipOpt {
        None = 0,
        ZipMode = 1 << 0,
        JunkPaths = 1 << 1,
        Verbose = 1 << 2,
    }

    public sealed class FastZip : ZipProcess {

        public int packLevel { get; set; }
        public int threads { get; set; }
        public int earlyOutPercent { get; set; }
        public FastZipOpt options { get; set; }

        public static bool isSupported {
            get {
                return !string.IsNullOrEmpty(path) &&
                    SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows &&
                    SystemInfo.operatingSystem.Contains("64bit");
            }
        }

        public static string path {
            get {
                var result = AssetDatabase.FindAssets("Fastzip")
                    .Select((guid) => AssetDatabase.GUIDToAssetPath(guid))
                    .Select((assetPath) => Path.GetFullPath(assetPath))
                    .FirstOrDefault((fullPath) => Path.GetExtension(fullPath) == ".exe");

                return result != null ?
                    result :
                    string.Empty;
            }
        }

        public FastZip(string output, params string[] sources) : this(output, -1, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, params string[] sources) : this(output, packLevel, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, int threads, int earlyOutPercent, FastZipOpt options, params string[] sources) {
            if (!isSupported)
                throw new FileLoadException("Fastzip is only supported on 64 bit windows machines");
            if (string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if (sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            this.output = output;
            this.packLevel = packLevel;
            this.threads = threads;
            this.earlyOutPercent = earlyOutPercent;
            this.options = options;
            this.sources = sources;
        }

        protected override ProcessStartInfo GetProcessStartInfo() {

            if (packLevel < 0 || packLevel > 9)
                packLevel = -1;
            if (threads < 1)
                threads = -1;
            if (earlyOutPercent < 0 || earlyOutPercent > 100)
                earlyOutPercent = -1;

            var startInfo = new ProcessStartInfo {
                FileName = path,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = string.Empty
            };

            if (packLevel != -1)
                startInfo.Arguments += string.Format("-{0} ", packLevel);
            if (threads != -1)
                startInfo.Arguments += string.Format("-t{0} ", threads);
            if (earlyOutPercent != -1)
                startInfo.Arguments += string.Format("-e{0} ", earlyOutPercent);
            if (options == (options | FastZipOpt.ZipMode))
                startInfo.Arguments += "-z ";
            if (options == (options | FastZipOpt.JunkPaths))
                startInfo.Arguments += "-j ";
            if (options == (options | FastZipOpt.Verbose))
                startInfo.Arguments += "-v ";

            startInfo.Arguments += string.Format("\"{0}\" ", output);

            startInfo.Arguments += string.Join(" ", sources
                .Where((s) => Directory.Exists(s) || File.Exists(s))
                .Select((s) => string.Format("\"{0}\"", s))
            );

            if (File.Exists(output))
                File.Delete(output);

            if (!Directory.Exists(Path.GetDirectoryName(output)))
                Directory.CreateDirectory(Path.GetDirectoryName(output));

            return startInfo;

        }

    }
}
