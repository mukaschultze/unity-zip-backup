using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZipBackup {

    public enum ZipModes {
        _7Zip = 1,
        FastZip = 2
    }

    [InitializeOnLoad]
    public static partial class Backup {

        static Backup() {
            EditorApplication.update += () => {
                if(DateTime.Now.Subtract(LastBackup).Ticks > BackupTimeSpan.Ticks && CanBackup() && AutoBackup)
                    try {
                        StartBackup();
                    }
                    catch(Exception e) {
                        Debug.LogWarning("Disabling auto backup, if the error persists contact the developer");
                        Debug.LogException(e);
                        AutoBackup = false;
                    }
            };
        }

        private static bool backuping;

        private static string ProductNameForFile {
            get {
                var name = Application.productName;
                var chars = Path.GetInvalidFileNameChars();
                for(var i = 0; i < chars.Length; i++)
                    name = name.Replace(chars[i], '_');
                return name;
            }
        }

        private static string[] Folders {
            get { return EditorPrefs.GetString("ZipFolders", "/Assets;/ProjectSettings").Split(';'); }
            set { EditorPrefs.SetString("ZipFolders", string.Join(";", value)); }
        }

        private static TimeSpan BackupTimeSpan {
            get { return TimeSpan.FromSeconds(EditorPrefs.GetInt("BackupTimeSpan", (int)TimeSpan.FromHours(8).TotalSeconds)); }
            set { EditorPrefs.SetInt("BackupTimeSpan", (int)value.TotalSeconds); }
        }

        private static DateTime LastBackup {
            get { return DateTime.Parse(PlayerPrefs.GetString("BackupLastBackup", DateTime.MinValue.ToString())); }
            set { PlayerPrefs.SetString("BackupLastBackup", value.ToString()); }
        }


        [MenuItem("Assets/Backup Now")]
        public static void StartBackup() {
            if(backuping || (!FastZip.IsSupported && !SevenZip.IsSupported) && !EditorApplication.isPlaying)
                return;

            var path = string.Format("{0}/{1}_backup_{2}.zip", SaveLocation, ProductNameForFile, DateTime.Now.ToString(@"yyyy-dd-MM-HH-mm"));
            var startTime = EditorApplication.timeSinceStartup;
            var sources = (from f in Folders
                           where Directory.Exists((Application.dataPath.ToLower() + "/@@@@@").Replace("/assets/@@@@@", f))
                           select (Application.dataPath.ToLower() + "/@@@@@").Replace("/assets/@@@@@", f)).ToArray();
            ZipProcess zip;

            if((Mode == ZipModes.FastZip && FastZip.IsSupported) || !SevenZip.IsSupported) {
                zip = new FastZip(path, sources);
                (zip as FastZip).PackLevel = PackLevel;
                (zip as FastZip).Threads = Threads;
                (zip as FastZip).EarlyOutPercent = EarlyOut;
                (zip as FastZip).Options |= FastZipOpt.Verbose;
            }
            else
                zip = new SevenZip(path, sources);

            zip.onExit += (o, a) => {
                backuping = false;
                LastBackup = DateTime.Now;

                if(zip.Process.ExitCode == 0)
                    using(var stream = new FileStream(path, FileMode.Open)) {
                        var zipSize = stream.Length;
                        var time = (EditorApplication.timeSinceStartup - startTime).ToString("0.00");

                        if(LogToConsole)
                            Debug.LogFormat("Backed up project into {0} in {1} seconds", EditorUtility.FormatBytes(zipSize), time);
                    }
                else if(LogToConsole)
                    Debug.LogWarning("Something went wrong while zipping, exit code: " + zip.Process.ExitCode);
            };
            zip.errorDataReceivedThreaded += (o, a) => { Debug.LogError(a.Data); };

            backuping = zip.Start();

            if(LogToConsole)
                Debug.Log(backuping ? "Backing up..." : "Error starting zip process");
            if(!backuping)
                LastBackup = DateTime.Now;
        }

        [MenuItem("Assets/Backup Now", true)]
        private static bool CanBackup() {
            return !backuping && (FastZip.IsSupported || SevenZip.IsSupported) && !EditorApplication.isPlaying;
        }

    }

}
