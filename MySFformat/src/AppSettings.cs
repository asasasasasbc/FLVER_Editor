using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MySFformat
{
    internal sealed class EditorPreferences
    {
        public bool PrettyFormatJson { get; set; } = true;
        public bool LoadTexture { get; set; } = true;
        public bool Show3D { get; set; } = false;
        public bool LegacyDisplay { get; set; } = false;
    }

    internal sealed class ClothPreviewPreferences
    {
        public string RuntimePath { get; set; } = "";
    }

    internal sealed class AnimationPreferences
    {
        public string SkeletonPath { get; set; } = "";
        public string AnimationPath { get; set; } = "";
        public decimal Speed { get; set; } = 1;
        public bool Loop { get; set; } = true;
    }

    internal static class AppSettings
    {
        internal static readonly SettingsStore Store = new SettingsStore(
            Path.GetDirectoryName(typeof(AppSettings).Assembly.Location),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLVER_Editor"));
        internal static string FilePath { get { return Store.FilePath; } }
        internal static T Load<T>(string section) where T : new() { return Store.Load<T>(section); }
        internal static void Save(string section, object values) { Store.Save(section, values); }
    }

    // Read-modify-write under a per-file process/thread mutex. Each section save merges
    // into the latest document so independent tool windows cannot erase one another.
    internal sealed class SettingsStore
    {
        readonly string directory, legacyDirectory, mutexName;
        internal string FilePath { get; private set; }
        internal SettingsStore(string directory, string legacyDirectory)
        {
            this.directory = directory; this.legacyDirectory = legacyDirectory;
            FilePath = Path.Combine(directory, "flver_editor_settings.json");
            using (var hash = SHA256.Create())
                mutexName = "Local\\FLVER_Editor_Settings_" + BitConverter.ToString(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(Path.GetFullPath(FilePath).ToUpperInvariant()))).Replace("-", "");
        }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }; }
        T Locked<T>(Func<T> action)
        {
            using (var mutex = new Mutex(false, mutexName))
            {
                bool held = false;
                try
                {
                    try { held = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("Settings are busy in another editor instance.");
                    return action();
                }
                finally { if (held) mutex.ReleaseMutex(); }
            }
        }
        internal T Load<T>(string section) where T : new()
        {
            try
            {
                return Locked(() =>
                {
                    var root = ReadRoot(); object value;
                    if (!root.TryGetValue(section, out value) || value == null) return new T();
                    return Serializer().Deserialize<T>(Serializer().Serialize(value));
                });
            }
            catch (Exception ex) { Trace.WriteLine("Settings load (" + section + "): " + ex.Message); return new T(); }
        }
        internal void Save(string section, object values)
        {
            Locked(() =>
            {
                var root = ReadRoot(); object existing;
                var merged = root.TryGetValue(section, out existing) ? existing as Dictionary<string, object> : null;
                if (merged == null) merged = new Dictionary<string, object>();
                var fields = Serializer().Deserialize<Dictionary<string, object>>(Serializer().Serialize(values));
                foreach (var field in fields) merged[field.Key] = field.Value;
                root[section] = merged; WriteRoot(root); return true;
            });
        }
        Dictionary<string, object> ReadRoot()
        {
            if (File.Exists(FilePath))
            {
                var root = Serializer().DeserializeObject(File.ReadAllText(FilePath)) as Dictionary<string, object>;
                if (root == null) throw new InvalidDataException("Settings JSON must contain an object; the original file was not changed.");
                return root;
            }
            var migrated = Migrate(); WriteRoot(migrated); return migrated;
        }
        void WriteRoot(Dictionary<string, object> root)
        {
            Directory.CreateDirectory(directory);
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(Serializer().Serialize(root));
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static void TryLegacy(Action read)
        { try { read(); } catch (Exception ex) { Trace.WriteLine("Legacy settings migration: " + ex.Message); } }
        Dictionary<string, object> Migrate()
        {
            var root = new Dictionary<string, object> { { "SchemaVersion", 1 } };
            var editor = new EditorPreferences();
            TryLegacy(() =>
            {
                string path = Path.Combine(directory, "MySFformat.ini"); if (!File.Exists(path)) return;
                var ini = new IniParser(path);
                editor.LoadTexture = ini.GetSetting("FLVER", "loadTexture") != "0";
                string show = ini.GetSetting("FLVER", "show3D"); if (show != null) editor.Show3D = show.Trim() != "0";
                string legacy = ini.GetSetting("FLVER", "legacyDisplay"); if (legacy != null) editor.LegacyDisplay = legacy.Trim() != "0";
            });
            TryLegacy(() =>
            {
                string path = Path.Combine(legacyDirectory, "editor-settings.ini");
                if (File.Exists(path)) editor.PrettyFormatJson = new IniParser(path).GetSetting("Editor", "PrettyFormatJson") != "0";
            });
            root["Editor"] = Serializer().DeserializeObject(Serializer().Serialize(editor));
            foreach (var pair in new[] { new[] { "FbxImport", "fbx_import_settings.json" }, new[] { "ClothAnimation", "cloth_animation_settings.json" } })
                TryLegacy(() =>
                {
                    string path = Path.Combine(directory, pair[1]);
                    if (File.Exists(path))
                    {
                        var value = Serializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
                        if (value != null) root[pair[0]] = value;
                    }
                });
            TryLegacy(() =>
            {
                string path = Path.Combine(legacyDirectory, "ClothPreview", "runtime-path.txt");
                if (File.Exists(path)) root["ClothPreview"] = new Dictionary<string, object> { { "RuntimePath", File.ReadAllText(path).Trim() } };
            });
            return root;
        }
    }
}
