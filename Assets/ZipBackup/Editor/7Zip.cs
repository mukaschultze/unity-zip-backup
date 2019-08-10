using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZipBackup {
    public class SevenZip : ZipProcess {

        public static bool isSupported {
            get {
                return !string.IsNullOrEmpty(path) && SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
            }
        }

        public static string path {
            get {
                var path = Path.Combine(EditorApplication.applicationContentsPath, "Tools/7z.exe");

                return File.Exists(path) ?
                    path :
                    string.Empty;
            }
        }

        public SevenZip(string output, params string[] sources) {
            if (!isSupported)
                throw new FileLoadException("Fastzip is only supported on windows");
            if (string.IsNullOrEmpty(output))
                throw new ArgumentException("Invalid output file path");
            if (sources.Length < 1)
                throw new ArgumentException("Need at least one source file");

            this.output = output;
            this.sources = sources;
        }

        protected override ProcessStartInfo GetProcessStartInfo() {

            var startInfo = new ProcessStartInfo {
                FileName = path,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = string.Format("a -tzip -bd \"{0}\" ", output)
            };

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
