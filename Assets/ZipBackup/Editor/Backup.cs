using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZipBackup {
    public enum ZipModes {
        _7Zip = 1,
        FastZip = 2
    }

    [InitializeOnLoad]
    public static class Backup {

        static Backup() {
            EditorApplication.update += () => {
                if(DateTime.Now.Subtract(lastBackup).Ticks > backupTimeSpan.Ticks && CanBackup() && autoBackup)
                    try {
                        StartBackup();
                    }
                    catch(Exception e) {
                        Debug.LogWarning("Disabling auto backup, if the error persists contact the developer");
                        Debug.LogException(e);
                        autoBackup = false;
                    }
            };
        }

        private static bool backuping;
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
        private static string saveLocation {
            get {
                if(!useCustomSaveLocation || string.IsNullOrEmpty(customSaveLocation))
                    return (Application.dataPath.ToLower() + "/@@@@@").Replace("/assets/@@@@@", "/Backups/");
                else
                    return customSaveLocation;
            }
        }
        private static string productNameForFile {
            get {
                var name = Application.productName;
                var chars = Path.GetInvalidFileNameChars();
                for(int i = 0; i < chars.Length; i++)
                    name = name.Replace(chars[i], '_');
                return name;
            }
        }
        private static TimeSpan backupTimeSpan {
            get { return TimeSpan.FromSeconds(EditorPrefs.GetInt("BackupTimeSpan", (int)TimeSpan.FromHours(8).TotalSeconds)); }
            set { EditorPrefs.SetInt("BackupTimeSpan", (int)value.TotalSeconds); }
        }
        private static DateTime lastBackup {
            get { return DateTime.Parse(PlayerPrefs.GetString("BackupLastBackup", DateTime.MinValue.ToString())); }
            set { PlayerPrefs.SetString("BackupLastBackup", value.ToString()); }
        }

        [PreferenceItem("Zip Backup")]
        private static void PreferencesGUI() {
            EditorGUILayout.Space();

            if(!SevenZip.isSupported && !FastZip.isSupported) {
                EditorGUILayout.HelpBox("7Zip and FastZip aren't supported, Zip Backup won't work", MessageType.Error);
                return;
            }
            else if(!FastZip.isSupported)
                EditorGUILayout.HelpBox("FastZip isn't supported, either Fastzip.exe was not found or Unity is not running on Windows 64bit", MessageType.Warning);
            else if(!SevenZip.isSupported)
                EditorGUILayout.HelpBox("7z.exe was not found, 7Zip won't work", MessageType.Warning);

            scroll = EditorGUILayout.BeginScrollView(scroll, false, false);
            GUI.enabled = FastZip.isSupported && SevenZip.isSupported;
            mode = (ZipModes)EditorGUILayout.EnumPopup(zipModeContent, mode);

            if(!FastZip.isSupported)
                mode = ZipModes._7Zip;
            else if(!SevenZip.isSupported)
                mode = ZipModes.FastZip;

            GUI.enabled = true;
            EditorGUILayout.Space();

            if(mode == ZipModes.FastZip) {
                packLevel = EditorGUILayout.IntSlider(packLevelContent, packLevel, 0, 9);
                GUI.enabled = packLevel > 0;
                earlyOut = EditorGUILayout.IntSlider(earlyOutContent, earlyOut, 0, 100);
                GUI.enabled = true;
                threads = EditorGUILayout.IntSlider(threadsContent, threads, 1, 30);
            }

            logToConsole = EditorGUILayout.Toggle(logToConsoleContent, logToConsole);

            EditorGUILayout.Space();

            if(useCustomSaveLocation = EditorGUILayout.Toggle(useCustomSaveLocationContent, useCustomSaveLocation)) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(customSaveLocationContent, EditorStyles.popup);
                if(GUILayout.Button(string.IsNullOrEmpty(customSaveLocation) ? "Browse..." : customSaveLocation, EditorStyles.popup, GUILayout.Width(150f))) {
                    var path = EditorUtility.OpenFolderPanel("Browse for backups folder", customSaveLocation, "Backups");
                    if(path.Length > 0)
                        customSaveLocation = path;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
                customSaveLocation = string.Empty;

            EditorGUILayout.Space();

            autoBackup = EditorGUILayout.ToggleLeft(autoBackupContent, autoBackup);
            GUI.enabled = autoBackup;
            EditorGUI.indentLevel++;

            var days = EditorGUILayout.IntSlider("Days", backupTimeSpan.Days, 0, 7);
            var hours = EditorGUILayout.IntSlider("Hours", backupTimeSpan.Hours, 0, 23);
            var minutes = EditorGUILayout.IntSlider("Minutes", backupTimeSpan.Minutes, 0, 59);

            if(days == 0 && hours == 0 && minutes < 5)
                minutes = 5;

            backupTimeSpan = new TimeSpan(days, hours, minutes, 0);

            EditorGUI.indentLevel--;
            GUI.enabled = true;

            EditorGUILayout.Space();

            if(lastBackup != DateTime.MinValue)
                EditorGUILayout.LabelField("Last backup: " + lastBackup);
            else
                EditorGUILayout.LabelField("Last backup: Never backuped");
            if(backuping)
                EditorGUILayout.LabelField("Next backup: Backuping now...");
            else if(!autoBackup)
                EditorGUILayout.LabelField("Next backup: Disabled");
            else
                EditorGUILayout.LabelField("Next backup: " + lastBackup.Add(backupTimeSpan));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Use Defaults", GUILayout.Width(120f))) {
                EditorPrefs.DeleteKey("BackupMode");
                EditorPrefs.DeleteKey("BackupPackLevel");
                EditorPrefs.DeleteKey("BackupEarlyOut");
                EditorPrefs.DeleteKey("BackupThreads");
                EditorPrefs.DeleteKey("BackupEnabled");
                EditorPrefs.DeleteKey("BackupLogToConsole");
                EditorPrefs.DeleteKey("BackupUseCustomSave");
                EditorPrefs.DeleteKey("BackupCustomSave");
                EditorPrefs.DeleteKey("BackupTimeSpan");
            }
            GUI.enabled = !backuping;
            if(GUILayout.Button("Backup now", GUILayout.Width(120f)))
                StartBackup();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Assets/Backup Now")]
        public static void StartBackup() {
            if(backuping || (!FastZip.isSupported && !SevenZip.isSupported) && !EditorApplication.isPlaying)
                return;

            var path = string.Format("{0}/{1}_backup_{2}.zip", saveLocation, productNameForFile, DateTime.Now.ToString(@"yyyy-dd-MM-HH-mm"));
            var assetsPath = Application.dataPath;
            var projectSettingsPath = Application.dataPath.Replace("/Assets", "/ProjectSettings");
            var startTime = EditorApplication.timeSinceStartup;
            ZipProcess zip;

            if((mode == ZipModes.FastZip && FastZip.isSupported) || !SevenZip.isSupported) {
                zip = new FastZip(path, assetsPath, projectSettingsPath);
                (zip as FastZip).packLevel = packLevel;
                (zip as FastZip).threads = threads;
                (zip as FastZip).earlyOutPercent = earlyOut;
            }
            else
                zip = new SevenZip(path, assetsPath, projectSettingsPath);

            zip.onExit += (o, a) => {
                backuping = false;
                lastBackup = DateTime.Now;

                if(zip.process.ExitCode == 0) {
                    var zipSize = File.ReadAllBytes(path).Length;
                    var time = (EditorApplication.timeSinceStartup - startTime).ToString("0.00");

                    if(logToConsole)
                        Debug.LogFormat("Backuped project into {0} in {1} seconds", EditorUtility.FormatBytes(zipSize), time);

                }
                else if(logToConsole)
                    Debug.LogWarning("Something went wrong while zipping");
            };

            backuping = zip.Start();

            if(logToConsole)
                Debug.Log(backuping ? "Backuping..." : "Error starting zip process");
            if(!backuping)
                lastBackup = DateTime.Now;
        }

        [MenuItem("Assets/Backup Now", true)]
        private static bool CanBackup() {
            return !backuping && (FastZip.isSupported || SevenZip.isSupported) && !EditorApplication.isPlaying;
        }
    }
}