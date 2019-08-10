using System;
using System.Diagnostics;
using UnityEditor;

namespace ZipBackup {
    public abstract class ZipProcess {

        public DataReceivedEventHandler outputDataReceived = (sender, args) => { };
        public DataReceivedEventHandler outputDataReceivedThreaded = (sender, args) => { };
        public DataReceivedEventHandler errorDataReceived = (sender, args) => { };
        public DataReceivedEventHandler errorDataReceivedThreaded = (sender, args) => { };
        public EventHandler onExit = (sender, args) => { };
        public EventHandler onExitThreaded = (sender, args) => { };

        public string output { get; protected set; }
        public string[] sources { get; protected set; }
        public Process process { get; protected set; }

        public virtual bool Start() {
            process = new Process();
            process.StartInfo = GetProcessStartInfo();
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += OutputDataReceived;
            process.ErrorDataReceived += ErrorDataReceived;
            process.Exited += Exited;

            var started = process.Start();

            // UnityEngine.Debug.LogWarningFormat("Spawning process: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return started;
        }

        public virtual bool Start(bool waitForExit) {
            var started = Start();

            if (waitForExit && started)
                process.WaitForExit();

            return started;
        }

        protected abstract ProcessStartInfo GetProcessStartInfo();

        protected void OutputDataReceived(object sender, DataReceivedEventArgs args) {
            if (string.IsNullOrEmpty(args.Data))
                return;

            outputDataReceivedThreaded(sender, args);

            var update = new EditorApplication.CallbackFunction(() => { });

            update = () => {
                EditorApplication.update -= update;
                outputDataReceived(sender, args);
            };

            EditorApplication.update += update;
        }

        protected void ErrorDataReceived(object sender, DataReceivedEventArgs args) {
            if (string.IsNullOrEmpty(args.Data))
                return;

            errorDataReceivedThreaded(sender, args);

            var update = new EditorApplication.CallbackFunction(() => { });

            update = () => {
                EditorApplication.update -= update;
                errorDataReceived(sender, args);
            };

            EditorApplication.update += update;
        }

        protected void Exited(object sender, EventArgs args) {
            onExitThreaded(sender, args);

            var update = new EditorApplication.CallbackFunction(() => { });

            update = () => {
                EditorApplication.update -= update;
                onExit(sender, args);
            };

            EditorApplication.update += update;
        }

    }
}
