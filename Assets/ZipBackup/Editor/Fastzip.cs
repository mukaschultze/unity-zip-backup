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

        public int PackLevel { get; set; }
        public int Threads { get; set; }
        public int EarlyOutPercent { get; set; }
        public FastZipOpt Options { get; set; }

        new public static bool IsSupported {
            get {
                if(string.IsNullOrEmpty(Path))
                    return false;
#if UNITY_5_5_OR_NEWER
                return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows && SystemInfo.operatingSystem.Contains("64bit");
#else
                return SystemInfo.operatingSystem.ToLower().Contains("windows") && SystemInfo.operatingSystem.Contains("64bit");
#endif
            }
        }
        new public static string Path {
            get {
                foreach(var guid in AssetDatabase.FindAssets("Fastzip")) {
                    var path = System.IO.Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
                    if(System.IO.Path.GetExtension(path) == ".exe")
                        return path;
                }
                return string.Empty;
            }
        }

        public FastZip(string output, params string[] sources) : this(output, -1, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, params string[] sources) : this(output, packLevel, -1, -1, FastZipOpt.JunkPaths | FastZipOpt.ZipMode, sources) { }

        public FastZip(string output, int packLevel, int threads, int earlyOutPercent, FastZipOpt options, params string[] sources) {
            if(!IsSupported)
                throw new FileLoadException("Fastzip is only supported on 64 bit windows machines");
            if(string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if(sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            Output = output;
            PackLevel = packLevel;
            Threads = threads;
            EarlyOutPercent = earlyOutPercent;
            Options = options;
            Sources = sources;
        }

        public override bool Start() {
            if(PackLevel < 0 || PackLevel > 9)
                PackLevel = -1;
            if(Threads < 1)
                Threads = -1;
            if(EarlyOutPercent < 0 || EarlyOutPercent > 100)
                EarlyOutPercent = -1;

            StartInfo = new ProcessStartInfo();
            StartInfo.FileName = Path;
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.Arguments = string.Empty;

            if(PackLevel != -1)
                StartInfo.Arguments += string.Format("-{0} ", PackLevel);
            if(Threads != -1)
                StartInfo.Arguments += string.Format("-t{0} ", Threads);
            if(EarlyOutPercent != -1)
                StartInfo.Arguments += string.Format("-e{0} ", EarlyOutPercent);
            if(Options == (Options | FastZipOpt.ZipMode))
                StartInfo.Arguments += "-z ";
            if(Options == (Options | FastZipOpt.JunkPaths))
                StartInfo.Arguments += "-j ";
            if(Options == (Options | FastZipOpt.Verbose))
                StartInfo.Arguments += "-v ";

            StartInfo.Arguments += string.Format("\"{0}\" ", Output);

            for(int i = 0; i < Sources.Length; i++)
                if(Directory.Exists(Sources[i]) || File.Exists(Sources[i]))
                    StartInfo.Arguments += string.Format("\"{0}\" ", Sources[i]);

            if(File.Exists(Output))
                File.Delete(Output);
            if(!Directory.Exists(System.IO.Path.GetDirectoryName(Output)))
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Output));

            Process = new Process();
            Process.StartInfo = StartInfo;
            Process.EnableRaisingEvents = true;
            Process.OutputDataReceived += OutputDataReceived;
            Process.ErrorDataReceived += ErrorDataReceived;
            Process.Exited += Exited;

            var started = Process.Start();
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();

            return started;
        }

    }
}