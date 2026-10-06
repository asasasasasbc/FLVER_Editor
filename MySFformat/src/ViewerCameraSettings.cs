using System;
using System.IO;
using System.Web.Script.Serialization;

namespace MySFformat
{
    // JSON coordinates use FLVER space: X right, Y up, Z depth.
    // Reading camera files never writes FLVER data or changes a model transform.
    public sealed class ViewerCameraSettings
    {
        public float[] camera;
        public float[] target;
        public string renderMode = "BothNoTex";
        public bool showBones;
        public bool showDummies;

        public static ViewerCameraSettings Parse(string json)
        {
            var settings = new JavaScriptSerializer().Deserialize<ViewerCameraSettings>(json);
            if (settings == null) throw new FormatException("Empty camera settings.");
            ValidateVector(settings.camera, "camera");
            ValidateVector(settings.target, "target");
            double dx = settings.camera[0] - settings.target[0];
            double dy = settings.camera[1] - settings.target[1];
            double dz = settings.camera[2] - settings.target[2];
            // Y is the up axis; purely vertical views require a separate up vector.
            if (dx * dx + dz * dz < 0.000001)
                throw new FormatException("Camera must not coincide with target or look parallel to Y-up.");
            if (dx * dx + dy * dy + dz * dz > 1000000)
                throw new FormatException("Camera distance is too large.");
            if (!Enum.IsDefined(typeof(RenderMode), settings.renderMode))
                throw new FormatException("Unknown renderMode.");
            return settings;
        }

        private static void ValidateVector(float[] vector, string name)
        {
            if (vector == null || vector.Length != 3)
                throw new FormatException(name + " must have exactly three numbers.");
            foreach (float value in vector)
                if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000)
                    throw new FormatException(name + " must contain finite, bounded numbers.");
        }

        public static string FindPath(string flverPath)
        {
            if (String.IsNullOrEmpty(flverPath)) return null;
            string modelFile = Path.GetFullPath(flverPath) + ".view.json";
            if (File.Exists(modelFile)) return modelFile;
            string folderFile = Path.Combine(Path.GetDirectoryName(modelFile), "viewer-camera.json");
            return File.Exists(folderFile) ? folderFile : null;
        }
    }
}
