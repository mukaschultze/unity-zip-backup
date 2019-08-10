using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ZipBackup {
    public class SevenZip : ZipProcess {

        new public static bool isSupported {
            get {
                if(string.IsNullOrEmpty(path))
                    return false;
#if UNITY_5_5_OR_NEWER
                return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
#else
                return SystemInfo.operatingSystem.ToLower().Contains("windows");
#endif
            }
        }
        new public static string path {
            get {
                var path = EditorApplication.applicationContentsPath + "/Tools/7z.exe";
                if(File.Exists(path))
                    return path;
                return string.Empty;
            }
        }

        public SevenZip(string output, params string[] sources) {
            if(!isSupported)
                throw new FileLoadException("Fastzip is only supported on windows");
            if(string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if(sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            this.output = output;
            this.sources = sources;
        }

        public override bool Start() {
            startInfo = new ProcessStartInfo();
            startInfo.FileName = path;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.Arguments += string.Format("a -tzip -bd \"{0}\" ", output);

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