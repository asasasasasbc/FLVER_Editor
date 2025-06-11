// FbxImportSettings.cs
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace MySFformat
{
    /// <summary>
    /// Holds all settings for the FBX import process.
    /// Can be serialized to/from JSON to save user preferences.
    /// </summary>
    public class FbxImportSettings
    {
        private static string SettingsFilePath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "fbx_import_settings.json");

        public enum Axis { X, Y, Z, NegX, NegY, NegZ }

        // File Paths
        public string ImportFilePath { get; set; } = "";
        public bool UseBoneConversion { get; set; } = true;
        public string BoneConversionFilePath { get; set; } = "";

        // Axis Settings
        public Axis PrimaryAxis { get; set; } = Axis.X;
        public Axis SecondaryAxis { get; set; } = Axis.Y;
        public bool MirrorTertiaryAxis { get; set; } = true;

        public bool blenderTan { get; set; } = false;

        // Other Options
        public bool SetTexture { get; set; } = true;
        public bool SetLOD { get; set; } = false;
        public bool ImportAndOverrideBones { get; set; } = false;

        public FbxImportSettings()
        {
            // Set default bone conversion file path relative to the executable
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            BoneConversionFilePath = Path.Combine(assemblyPath, "boneConvertion.ini");
        }

        /// <summary>
        /// Saves the current settings to a JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle the error appropriately, for now we just ignore it
                Console.WriteLine($"Failed to save FBX import settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from a JSON file. If the file doesn't exist, returns default settings.
        /// </summary>
        public static FbxImportSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<FbxImportSettings>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load FBX import settings, using defaults: {ex.Message}");
            }
            return new FbxImportSettings();
        }
    }
}