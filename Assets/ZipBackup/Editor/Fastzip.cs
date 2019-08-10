using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZipBackup {
    [Flags]
    public enum FastZipOpt {
        None = 0,
        ZipMode = 1,
        JunkPaths = 2,
        Verbose = 4,
    }

    public sealed class FastZip : ZipProcess {

        public int packLevel { get; set; }
        public int threads { get; set; }
        public int earlyOutPercent { get; set; }
        public FastZipOpt options { get; set; }

        new public static bool isSupported {
            get {
                if(string.IsNullOrEmpty(path))
                    return false;
#if UNITY_5_5_OR_NEWER
                return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows && SystemInfo.operatingSystem.Contains("64bit");
#else
                return SystemInfo.operatingSystem.ToLower().Contains("windows") && SystemInfo.operatingSystem.Contains("64bit");
#endif
            }
        }
        new public static string path {
            get {
                foreach(var guid in AssetDatabase.FindAssets("Fastzip")) {
                    var path = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
                    if(Path.GetExtension(path) == ".exe")
                        return path;
                }
                return string.Empty;
            }
        }

        public FastZip(string output, params string[] sources) : this(output, -1, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, params string[] sources) : this(output, packLevel, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, int threads, int earlyOutPercent, FastZipOpt options, params string[] sources) {
            if(!isSupported)
                throw new FileLoadException("Fastzip is only supported on 64 bit windows machines");
            if(string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if(sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            this.output = output;
            this.packLevel = packLevel;
            this.threads = threads;
            this.earlyOutPercent = earlyOutPercent;
            this.options = options;
            this.sources = sources;
        }

        public override bool Start() {
            if(packLevel < 0 || packLevel > 9)
                packLevel = -1;
            if(threads < 1)
                threads = -1;
            if(earlyOutPercent < 0 || earlyOutPercent > 100)
                earlyOutPercent = -1;

            startInfo = new ProcessStartInfo();
            startInfo.FileName = path;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.Arguments = string.Empty;

            if(packLevel != -1)
                startInfo.Arguments += string.Format("-{0} ", packLevel);
            if(threads != -1)
                startInfo.Arguments += string.Format("-t{0} ", threads);
            if(earlyOutPercent != -1)
                startInfo.Arguments += string.Format("-e{0} ", earlyOutPercent);
            if(options == (options | FastZipOpt.ZipMode))
                startInfo.Arguments += "-z ";
            if(options == (options | FastZipOpt.JunkPaths))
                startInfo.Arguments += "-j ";
            if(options == (options | FastZipOpt.Verbose))
                startInfo.Arguments += "-v ";

            startInfo.Arguments += string.Format("\"{0}\" ", output);

            for(int i = 0; i < sources.Length; i++)
                if(Directory.Exists(sources[i]) || File.Exists(sources[i]))
                    startInfo.Arguments += string.Format("\"{0}\" ", sources[i]);

            if(File.Exists(output))
                File.Delete(output);
            if(!Directory.Exists(Path.GetDirectoryName(output)))
                Directory.CreateDirectory(Path.GetDirectoryName(output));

            process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += OutputDataReceived;
            process.ErrorDataReceived += ErrorDataReceived;
            process.Exited += Exited;

            var started = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return started;
        }

    }
}