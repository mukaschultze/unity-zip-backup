using System;
using System.Diagnostics;
using UnityEditor;

namespace ZipBackup {
    public abstract class ZipProcess {

        public delegate void DataReceivedCallback(object sender, DataReceivedEventArgs args);
        public delegate void ExitCallback(object sender, EventArgs args);

        public DataReceivedCallback outputDataReceived = (o, a) => { };
        public DataReceivedCallback outputDataReceivedThreaded = (o, a) => { };
        public DataReceivedCallback errorDataReceived = (o, a) => { };
        public DataReceivedCallback errorDataReceivedThreaded = (o, a) => { };
        public ExitCallback onExit = (o, a) => { };
        public ExitCallback onExitThreaded = (o, a) => { };

        public string output { get; protected set; }
        public string[] sources { get; protected set; }
        public Process process { get; protected set; }
        public ProcessStartInfo startInfo { get; protected set; }

        public static bool isSupported { get; private set; }
        public static bool path { get; private set; }

        public abstract bool Start();

        public bool Start(bool lockUnityThread) {
            var started = Start();

            if(lockUnityThread && started)
                process.WaitForExit();

            return started;
        }

        protected void OutputDataReceived(object sender, DataReceivedEventArgs args) {
            if(string.IsNullOrEmpty(args.Data))
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
            if(string.IsNullOrEmpty(args.Data))
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