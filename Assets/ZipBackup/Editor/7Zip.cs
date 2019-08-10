using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZipBackup {
    public class SevenZip : ZipProcess {

        new public static bool IsSupported {
            get {
                if(string.IsNullOrEmpty(Path))
                    return false;
#if UNITY_5_5_OR_NEWER
                return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
#else
                return SystemInfo.operatingSystem.ToLower().Contains("windows");
#endif
            }
        }
        new public static string Path {
            get {
                var path = EditorApplication.applicationContentsPath + "/Tools/7z.exe";
                if(File.Exists(path))
                    return path;
                return string.Empty;
            }
        }

        public SevenZip(string output, params string[] sources) {
            if(!IsSupported)
                throw new FileLoadException("Fastzip is only supported on windows");
            if(string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if(sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            var sevenZip = this;
            sevenZip.Output = output;
            sevenZip.Sources = sources;
        }

        public override bool Start() {
            StartInfo = new ProcessStartInfo();
            StartInfo.FileName = Path;
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.Arguments += string.Format("a -tzip -bd \"{0}\" ", Output);

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