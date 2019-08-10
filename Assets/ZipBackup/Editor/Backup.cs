using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ZipBackup {
    public enum ZipModes {
        _7Zip = 1,
        FastZip = 2
    }

    [InitializeOnLoad]
    public static class Backup {

        static Backup() {
            EditorApplication.update += () => {
                if (DateTime.Now.Subtract(lastBackupTime).Ticks > backupTimeSpan.Ticks && CanBackup() && autoBackup)
                    try {
                        StartBackup();
                    }
                catch (Exception ex) {
                    Debug.LogWarning("Disabling auto backup, if the error persists contact the developer");
                    Debug.LogException(ex);
                    autoBackup = false;
                }
            };
        }

        private static bool backingup;
        private static Vector2 scroll;

        private static readonly GUIContent zipModeContent = new GUIContent("Zip mode", "The application that will be used to zip files");
        private static readonly GUIContent packLevelContent = new GUIContent("Pack level", "Zip-mode compression level, a higher value may decrease performance, while a lower value may increase the file size\n\n0=Store only, without compression");
        private static readonly GUIContent earlyOutContent = new GUIContent("Early out (%)", "The worst detected compression for switching to store");
        private static readonly GUIContent threadsContent = new GUIContent("Threads", "Worker threads count");
        private static readonly GUIContent useCustomSaveLocationContent = new GUIContent("Custom backups folder", "Specify the folder to store zip files\nIf enabled, backups from all projects will be store at this location, if disabled each backup will be store on its own project folder");
        private static readonly GUIContent customSaveLocationContent = new GUIContent("Backups folder location", "The folder to store zip files");
        private static readonly GUIContent logToConsoleContent = new GUIContent("Log to console", "Log Fastzip events to the console");
        private static readonly GUIContent autoBackupContent = new GUIContent("Auto backup", "Automatically backup in the specified time");

        private static ZipModes mode {
            get { return (ZipModes)EditorPrefs.GetInt("BackupMode", FastZip.isSupported ? 2 : 1); }
            set { EditorPrefs.SetInt("BackupMode", (int)value); }
        }
        private static int packLevel {
            get { return EditorPrefs.GetInt("BackupPackLevel", 1); }
            set { EditorPrefs.SetInt("BackupPackLevel", value); }
        }
        private static int earlyOut {
            get { return EditorPrefs.GetInt("BackupEarlyOut", 98); }
            set { EditorPrefs.SetInt("BackupEarlyOut", value); }
        }
        private static int threads {
            get { return EditorPrefs.GetInt("BackupThreads", SystemInfo.processorCount); }
            set { EditorPrefs.SetInt("BackupThreads", value); }
        }
        private static bool autoBackup {
            get { return EditorPrefs.GetBool("BackupEnabled", false); }
            set { EditorPrefs.SetBool("BackupEnabled", value); }
        }
        private static bool logToConsole {
            get { return EditorPrefs.GetBool("BackupLogToConsole", true); }
            set { EditorPrefs.SetBool("BackupLogToConsole", value); }
        }
        private static bool useCustomSaveLocation {
            get { return EditorPrefs.GetBool("BackupUseCustomSave", false); }
            set { EditorPrefs.SetBool("BackupUseCustomSave", value); }
        }
        private static string customSaveLocation {
            get { return EditorPrefs.GetString("BackupCustomSave", string.Empty); }
            set { EditorPrefs.SetString("BackupCustomSave", value); }
        }

        private static List<string> _backedupFolders;
        private static List<string> backedupFolders {
            get {
                if (!EditorPrefs.HasKey("BackupFoldersCount"))
                    _backedupFolders = new List<string>() { "Assets", "ProjectSettings" };

                if (_backedupFolders == null) {
                    var count = EditorPrefs.GetInt("BackupFoldersCount");
                    _backedupFolders = new List<string>(count);

                    for (var i = 0; i < count; i++) {
                        var key = string.Format("BackupFolder{0}", i);
                        var str = EditorPrefs.GetString(key);

                        _backedupFolders.Add(str);
                    }
                }

                return _backedupFolders;
            }
            set {
                EditorPrefs.SetInt("BackupFoldersCount", value.Count);

                for (var i = 0; i < value.Count; i++) {
                    var key = string.Format("BackupFolder{0}", i);
                    EditorPrefs.SetString(key, value[i]);
                }

                _backedupFolders = value;
                // Debug.Log("Saving folders to backup");
            }
        }

        private static string saveLocation {
            get {
                return !useCustomSaveLocation || string.IsNullOrEmpty(customSaveLocation) ?
                    Path.Combine(Application.dataPath, "../Backups/") :
                    customSaveLocation;
            }
        }
        private static string productNameForFile {
            get {
                var name = Application.productName;
                var chars = Path.GetInvalidFileNameChars();

                return chars.Aggregate(name, (acc, c) => acc.Replace(c, '-'));
            }
        }
        private static TimeSpan backupTimeSpan {
            get { return TimeSpan.FromSeconds(EditorPrefs.GetInt("BackupTimeSpan", (int)TimeSpan.FromHours(8).TotalSeconds)); }
            set { EditorPrefs.SetInt("BackupTimeSpan", (int)value.TotalSeconds); }
        }
        private static DateTime lastBackupTime {
            get { return DateTime.Parse(PlayerPrefs.GetString("BackupLastBackup", DateTime.MinValue.ToString())); }
            set { PlayerPrefs.SetString("BackupLastBackup", value.ToString()); }
        }

        private static ReorderableList reorderableList;

        [PreferenceItem("Zip Backup")]
        private static void PreferencesGUI() {

            if (reorderableList == null) {
                reorderableList = new ReorderableList(backedupFolders, typeof(string));

                reorderableList.drawHeaderCallback += (rect) => EditorGUI.LabelField(rect, "Backup folder list");
                reorderableList.onAddCallback += (list) => {
                    var path = EditorUtility.OpenFolderPanel("Select folder to backup", "", "");

                    if (string.IsNullOrEmpty(path))
                        return;

                    var relativePath = FileUtil.GetProjectRelativePath(path);

                    list.list.Add(string.IsNullOrEmpty(relativePath) ?
                        path :
                        relativePath);

                    backedupFolders = list.list as List<string>;
                };

                reorderableList.onRemoveCallback += (ReorderableList list) => {
                    list.list.RemoveAt(list.index);
                    backedupFolders = list.list as List<string>;
                };

            }

            EditorGUILayout.Space();

            if (!SevenZip.isSupported && !FastZip.isSupported) {
                EditorGUILayout.HelpBox("7Zip and FastZip aren't supported, Zip Backup won't work", MessageType.Error);
                return;
            } else if (!FastZip.isSupported)
                EditorGUILayout.HelpBox("FastZip isn't supported, either Fastzip.exe was not found or Unity is not running on Windows 64bit", MessageType.Warning);
            else if (!SevenZip.isSupported)
                EditorGUILayout.HelpBox("7z.exe was not found, 7Zip won't work", MessageType.Warning);

            scroll = EditorGUILayout.BeginScrollView(scroll, false, false);
            GUI.enabled = FastZip.isSupported && SevenZip.isSupported;
            mode = (ZipModes)EditorGUILayout.EnumPopup(zipModeContent, mode);

            if (!FastZip.isSupported)
                mode = ZipModes._7Zip;
            else if (!SevenZip.isSupported)
                mode = ZipModes.FastZip;

            GUI.enabled = true;
            EditorGUILayout.Space();

            if (mode == ZipModes.FastZip) {
                packLevel = EditorGUILayout.IntSlider(packLevelContent, packLevel, 0, 9);
                GUI.enabled = packLevel > 0;
                earlyOut = EditorGUILayout.IntSlider(earlyOutContent, earlyOut, 0, 100);
                GUI.enabled = true;
                threads = EditorGUILayout.IntSlider(threadsContent, threads, 1, 8);
            }

            EditorGUILayout.Space();
            logToConsole = EditorGUILayout.Toggle(logToConsoleContent, logToConsole);
            EditorGUILayout.Space();
            reorderableList.DoLayoutList();
            EditorGUILayout.Space();

            if (useCustomSaveLocation = EditorGUILayout.Toggle(useCustomSaveLocationContent, useCustomSaveLocation)) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(customSaveLocationContent, EditorStyles.popup);
                if (GUILayout.Button(string.IsNullOrEmpty(customSaveLocation) ? "Browse..." : customSaveLocation, EditorStyles.popup, GUILayout.Width(150f))) {
                    var path = EditorUtility.OpenFolderPanel("Select backups destination", customSaveLocation, "Backups");
                    if (path.Length > 0)
                        customSaveLocation = path;
                }
                EditorGUILayout.EndHorizontal();
            } else
                customSaveLocation = string.Empty;

            EditorGUILayout.Space();

            autoBackup = EditorGUILayout.ToggleLeft(autoBackupContent, autoBackup);
            GUI.enabled = autoBackup;
            EditorGUI.indentLevel++;

            var days = EditorGUILayout.IntSlider("Days", backupTimeSpan.Days, 0, 7);
            var hours = EditorGUILayout.IntSlider("Hours", backupTimeSpan.Hours, 0, 23);
            var minutes = EditorGUILayout.IntSlider("Minutes", backupTimeSpan.Minutes, 0, 59);

            if (days == 0 && hours == 0 && minutes < 5)
                minutes = 5;

            backupTimeSpan = new TimeSpan(days, hours, minutes, 0);

            EditorGUI.indentLevel--;
            GUI.enabled = true;

            EditorGUILayout.Space();

            if (lastBackupTime != DateTime.MinValue)
                EditorGUILayout.LabelField("Last backup: " + lastBackupTime);
            else
                EditorGUILayout.LabelField("Last backup: Never backed up");
            if (backingup)
                EditorGUILayout.LabelField("Next backup: Backing up now...");
            else if (!autoBackup)
                EditorGUILayout.LabelField("Next backup: Disabled");
            else
                EditorGUILayout.LabelField("Next backup: " + lastBackupTime.Add(backupTimeSpan));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Defaults", GUILayout.Width(120f))) {
                EditorPrefs.DeleteKey("BackupMode");
                EditorPrefs.DeleteKey("BackupPackLevel");
                EditorPrefs.DeleteKey("BackupEarlyOut");
                EditorPrefs.DeleteKey("BackupThreads");
                EditorPrefs.DeleteKey("BackupEnabled");
                EditorPrefs.DeleteKey("BackupLogToConsole");
                EditorPrefs.DeleteKey("BackupUseCustomSave");
                EditorPrefs.DeleteKey("BackupCustomSave");
                EditorPrefs.DeleteKey("BackupTimeSpan");
                EditorPrefs.DeleteKey("BackupFoldersCount");
                _backedupFolders = null;
                reorderableList.list = backedupFolders;
            }
            GUI.enabled = !backingup;
            if (GUILayout.Button("Backup now", GUILayout.Width(120f)))
                StartBackup();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Assets/Backup Now")]
        public static void StartBackup() {

            if (!CanBackup())
                return;

            var filename = string.Format("{0}-backup-{1}.zip", productNameForFile, DateTime.Now.ToString("yyyy-MM-ddTHH-mm"));

            var outputPath = Path.GetFullPath(Path.Combine(saveLocation, filename));
            var sources = backedupFolders
                .Select((p) => Path.Combine("..", p))
                .Select((p) => Path.Combine(Application.dataPath, p))
                .Select((p) => Path.GetFullPath(p))
                .ToArray();

            var startTime = EditorApplication.timeSinceStartup;
            ZipProcess zip;

            if ((mode == ZipModes.FastZip && FastZip.isSupported) || !SevenZip.isSupported) {
                var fastZip = new FastZip(outputPath, sources);
                fastZip.packLevel = packLevel;
                fastZip.threads = threads;
                fastZip.earlyOutPercent = earlyOut;
                zip = fastZip;
            } else
                zip = new SevenZip(outputPath, sources);

            zip.errorDataReceived += (sender, args) => {
                Debug.LogErrorFormat("Zip Error: {0}", args.Data);
            };

            zip.onExit += (sender, args) => {
                backingup = false;
                lastBackupTime = DateTime.Now;

                if (zip.process.ExitCode == 0)
                    using(var stream = new FileStream(outputPath, FileMode.Open)) {
                        var zipSize = stream.Length;
                        var time = (EditorApplication.timeSinceStartup - startTime).ToString("0.00");

                        if (logToConsole)
                            Debug.LogFormat("Backed up project into {0} in {1} seconds", EditorUtility.FormatBytes(zipSize), time);
                    }
                else // Always show warning messages
                    Debug.LogWarningFormat("Something went wrong while zipping, process exited with code {0}", zip.process.ExitCode);

                InternalEditorUtility.RepaintAllViews();
            };

            backingup = zip.Start();

            if (!backingup)
                Debug.LogWarning("Failed to spawn zip process");
            else if (logToConsole)
                Debug.Log("Backing up...");

            if (!backingup)
                lastBackupTime = DateTime.Now;

            InternalEditorUtility.RepaintAllViews();

        }

        [MenuItem("Assets/Backup Now", true)]
        private static bool CanBackup() {
            return !EditorApplication.isPlaying && !backingup && (FastZip.isSupported || SevenZip.isSupported);
        }

    }
}
