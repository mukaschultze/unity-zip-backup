using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZipBackup {

    [Serializable]
    public sealed class PrefItem<T> {

        [Serializable]
        private struct Wrapper {
            [SerializeField]
            public T value;
        }

        private const string KEY_PREFIX = "ZipBackup.";

        private readonly string key;
        private Wrapper wrapper;

        public GUIContent Label { get; private set; }

        public T DefaultValue { get; private set; }

        public T Value {
            get { return wrapper.value; }
            set { SetValue(value, false); }
        }

        private bool UsingDefaultValue { get { return !EditorPrefs.HasKey(key); } }

        public PrefItem(string key, T defaultValue, string text = "", string tooltip = "") {
            this.key = KEY_PREFIX + key;

            DefaultValue = defaultValue;
            Label = new GUIContent(text, tooltip);
            Backup.onResetPreferences += ResetValue;

            if(UsingDefaultValue)
                wrapper.value = defaultValue;
            else
                LoadValue();
        }

        private void LoadValue() {
            try {
                if(!EditorPrefs.HasKey(key))
                    return;

                var json = EditorPrefs.GetString(key);
                wrapper = JsonUtility.FromJson<Wrapper>(json);
            }
            catch(Exception e) {
                Debug.LogWarningFormat("Failed to load preference item \"{0}\", using default value: {1}", key, DefaultValue);
                Debug.LogException(e);
                ResetValue();
            }
        }

        private void SetValue(T newValue, bool forceSave) {
            try {
                if(Value != null && Value.Equals(newValue) && !forceSave)
                    return;

                wrapper.value = newValue;

                var json = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(key, json);
            }
            catch(Exception e) {
                Debug.LogWarningFormat("Failed to save {0}: {1}", key, e);
                Debug.LogException(e);
            }
            finally {
                wrapper.value = newValue;
            }
        }

        private void ResetValue() {
            if(UsingDefaultValue)
                return;

            wrapper.value = DefaultValue;
            EditorPrefs.DeleteKey(key);
        }

        public void ForceSave() { SetValue(wrapper.value, true); }

        public static implicit operator T(PrefItem<T> pb) { return pb.Value; }

        public static implicit operator GUIContent(PrefItem<T> pb) { return pb.Label; }

    }

    public static partial class Backup {

        public delegate void OnResetPreferences();

        public static OnResetPreferences onResetPreferences = () => { };

        public static PrefItem<ZipModes> Mode { get; private set; }
        public static PrefItem<int> PackLevel { get; private set; }
        public static PrefItem<int> EarlyOut { get; private set; }
        public static PrefItem<int> Threads { get; private set; }
        public static PrefItem<bool> UseCustomSaveLocation { get; private set; }
        public static PrefItem<string> CustomSaveLocation { get; private set; }
        public static PrefItem<bool> LogToConsole { get; private set; }
        public static PrefItem<bool> AutoBackup { get; private set; }

        static Backup() {
            Mode = new PrefItem<ZipModes>("BackupMode", FastZip.IsSupported ? ZipModes.FastZip : ZipModes._7Zip, "Zip mode", "The application that will be used to zip files");
            PackLevel = new PrefItem<int>("PackLevel", 1, "Pack level", "Zip-mode compression level, a higher value may decrease performance, while a lower value may increase the file size\n\n0=Store only, without compression");
            EarlyOut = new PrefItem<int>("EarlyOut", 98, "Early out (%)", "The worst detected compression for switching to store");
            Threads = new PrefItem<int>("Threads", SystemInfo.processorCount, "Threads", "Worker threads count");
            UseCustomSaveLocation = new PrefItem<bool>("UseCustomSaveLocation", false, "Custom backups folder", "Specify the folder to store zip files\nIf enabled, backups from all projects will be store at this location, if disabled each backup will be store on its own project folder");
            CustomSaveLocation = new PrefItem<string>("CustomSaveLocation", string.Empty, "Backups folder location", "The folder to store zip files");
            LogToConsole = new PrefItem<bool>("LogToConsole", true, "Log to console", "Log zipping events to the console");
            AutoBackup = new PrefItem<bool>("AutoBackup", false, "Auto backup", "Automatically backup in the specified time");
        }

        private static string SaveLocation {
            get {
                if(!UseCustomSaveLocation || string.IsNullOrEmpty(CustomSaveLocation))
                    return (Application.dataPath.ToLower() + "/@@@@@").Replace("/assets/@@@@@", "/Backups/");
                else
                    return CustomSaveLocation;
            }
        }

        private static Vector2 scroll;
        private static ReorderableList rList;

        private static bool ComparePath(string a, string b) {
            if(a == null && b == null)
                return true;

            if(a == null || b == null)
                return false;

            a = a.Replace("\\", "/");
            b = b.Replace("\\", "/");

            if(a.StartsWith("/")) a = a.Substring(1);
            if(b.StartsWith("/")) b = b.Substring(1);

            if(a.EndsWith("/")) a = a.Remove(a.Length - 1);
            if(b.EndsWith("/")) b = b.Remove(b.Length - 1);

            return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
        }

        [PreferenceItem("Zip Backup")]
        private static void PreferencesGUI() {

            if(rList == null) {
                var notRemovable = new[] { "/Assets", "/ProjectSettings" };

                rList = new ReorderableList(Folders.ToList(), typeof(string), false, true, true, true);
                rList.drawHeaderCallback += rect => EditorGUI.LabelField(rect, "Folders to pack");
                rList.onCanRemoveCallback += list => !notRemovable.Any(a => ComparePath(a, rList.list[rList.index] as string));

                rList.onAddCallback += list => {
                    var path = EditorUtility.OpenFolderPanel("Select folder to include", "", "");

                    if(string.IsNullOrEmpty(path))
                        return;

                    list.list.Add(path);
                };

                rList.drawElementCallback += (rect, index, active, focused) => {
                    var path = (string)rList.list[index];

                    GUI.enabled = !notRemovable.Any(a => ComparePath(a, rList.list[rList.index] as string));
                    GUI.changed = false;

                    rect.height -= 2f;
                    rect.xMin += 15f;
                    path = EditorGUI.TextField(rect, path);

                    if(!path.StartsWith("/"))
                        path = path.Insert(0, "/");

                    if(GUI.changed && (path == "/Assets" || path == "/ProjectSettings"))
                        path += "_";

                    rList.list[index] = path;
                    GUI.enabled = true;
                };
            }

            EditorGUILayout.Space();

            #region Helpboxes
            if(!SevenZip.IsSupported && !FastZip.IsSupported) {
                EditorGUILayout.HelpBox("7Zip and FastZip aren't supported, Zip Backup won't work", MessageType.Error);
                return;
            }
            else if(!FastZip.IsSupported)
                EditorGUILayout.HelpBox("FastZip isn't supported, either Fastzip.exe was not found or Unity is not running on Windows 64bit", MessageType.Warning);
            else if(!SevenZip.IsSupported)
                EditorGUILayout.HelpBox("7z.exe was not found, 7Zip won't work", MessageType.Warning);
            #endregion

            scroll = EditorGUILayout.BeginScrollView(scroll, false, false);

            #region Basic Settings
            GUI.enabled = FastZip.IsSupported && SevenZip.IsSupported;
            Mode = (ZipModes)EditorGUILayout.EnumPopup(zipModeContent, Mode);

            if(!FastZip.IsSupported)
                Mode = ZipModes._7Zip;
            else if(!SevenZip.IsSupported)
                Mode = ZipModes.FastZip;

            GUI.enabled = true;
            EditorGUILayout.Space();

            if(Mode == ZipModes.FastZip) {
                PackLevel = EditorGUILayout.IntSlider(packLevelContent, PackLevel, 0, 9);
                GUI.enabled = PackLevel > 0;
                EarlyOut = EditorGUILayout.IntSlider(earlyOutContent, EarlyOut, 0, 100);
                GUI.enabled = true;
                Threads = EditorGUILayout.IntSlider(threadsContent, Threads, 1, 30);
            }

            LogToConsole = EditorGUILayout.Toggle(logToConsoleContent, LogToConsole);
            #endregion

            EditorGUILayout.Space();

            #region Custom save location
            if(UseCustomSaveLocation = EditorGUILayout.Toggle(useCustomSaveLocationContent, UseCustomSaveLocation)) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(customSaveLocationContent, EditorStyles.popup);
                if(GUILayout.Button(string.IsNullOrEmpty(CustomSaveLocation) ? "Browse..." : CustomSaveLocation, EditorStyles.popup, GUILayout.Width(150f))) {
                    var path = EditorUtility.OpenFolderPanel("Browse for backups folder", CustomSaveLocation, "Backups");
                    if(path.Length > 0)
                        CustomSaveLocation = path;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
                CustomSaveLocation = string.Empty;
            #endregion

            #region Custom name
            #endregion

            EditorGUILayout.Space();
            rList.DoLayoutList();
            Folders = rList.list.Cast<string>().ToArray();
            EditorGUILayout.Space();

            #region Autobackup
            AutoBackup = EditorGUILayout.ToggleLeft(autoBackupContent, AutoBackup);
            GUI.enabled = AutoBackup;
            EditorGUI.indentLevel++;

            var days = EditorGUILayout.IntSlider("Days", BackupTimeSpan.Days, 0, 7);
            var hours = EditorGUILayout.IntSlider("Hours", BackupTimeSpan.Hours, 0, 23);
            var minutes = EditorGUILayout.IntSlider("Minutes", BackupTimeSpan.Minutes, 0, 59);

            if(days == 0 && hours == 0 && minutes < 5)
                minutes = 5;

            BackupTimeSpan = new TimeSpan(days, hours, minutes, 0);

            EditorGUI.indentLevel--;
            GUI.enabled = true;
            #endregion

            EditorGUILayout.Space();

            #region Next and last backups
            if(LastBackup != DateTime.MinValue)
                EditorGUILayout.LabelField("Last backup: " + LastBackup);
            else
                EditorGUILayout.LabelField("Last backup: Never backuped");
            if(backuping)
                EditorGUILayout.LabelField("Next backup: Backuping now...");
            else if(!AutoBackup)
                EditorGUILayout.LabelField("Next backup: Disabled");
            else
                EditorGUILayout.LabelField("Next backup: " + LastBackup.Add(BackupTimeSpan));
            #endregion

            EditorGUILayout.EndScrollView();

            using(new EditorGUILayout.HorizontalScope()) {
                if(GUILayout.Button("Use Defaults", GUILayout.Width(120f)))
                    onResetPreferences();

                using(new EditorGUI.DisabledGroupScope(!backuping))
                    if(GUILayout.Button("Backup now", GUILayout.Width(120f)))
                        StartBackup();
            }
        }

        private static void DoGUI(this PrefItem<int> prefItem) {
            prefItem.Value = EditorGUILayout.IntField(prefItem, prefItem);
        }

        private static void DoGUI(this PrefItem<float> prefItem) {
            prefItem.Value = EditorGUILayout.FloatField(prefItem, prefItem);
        }

        private static void DoGUISlider(this PrefItem<int> prefItem, int min, int max) {
            prefItem.Value = EditorGUILayout.IntSlider(prefItem, prefItem, min, max);
        }

        private static void DoGUISlider(this PrefItem<float> prefItem, float min, float max) {
            prefItem.Value = EditorGUILayout.Slider(prefItem, prefItem, min, max);
        }

        private static void DoGUI(this PrefItem<bool> prefItem) {
            prefItem.Value = EditorGUILayout.Toggle(prefItem, prefItem);
        }

        private static void DoGUI(this PrefItem<string> prefItem) {
            prefItem.Value = EditorGUILayout.TextField(prefItem.Label, prefItem);
        }

        private static void DoGUI(this PrefItem<Color> prefItem) {
            prefItem.Value = EditorGUILayout.ColorField(prefItem, prefItem);
        }

        private static void DoGUI<T>(this PrefItem<T> prefItem) where T : struct, IConvertible {
            prefItem.Value = (T)(object)EditorGUILayout.EnumPopup(prefItem, (Enum)(object)prefItem.Value);
        }

    }
}