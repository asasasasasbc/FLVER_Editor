using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace MySFformat
{
    // HavokLib owns binary HKX/spline decoding. This adapter consumes its sampled GLB,
    // retaining quaternion tracks, binding indices, reference pose and independent scale.
    public sealed class HkxAnimationClip
    {
        public string[] Names { get; private set; }
        public int[] Parents { get; private set; }
        public int NodeCount { get { return Names.Length; } }
        public float Duration { get; private set; }
        public const int SampleRate = 60;
        public string ConversionLog { get; private set; }
        Vector3[] translations, scales;
        Quaternion[] rotations;
        readonly List<Track> tracks = new List<Track>();
        sealed class Track { public int Node; public string Path; public float[] Times, Values; public int Width; public bool Step; }
        static Dictionary<string, object> Obj(object o) { return (Dictionary<string, object>)o; }
        static object[] Arr(object o) { return ((IEnumerable)o).Cast<object>().ToArray(); }
        static int Int(Dictionary<string, object> d, string k, int def = 0) { return d.ContainsKey(k) ? Convert.ToInt32(d[k]) : def; }
        static float[] Vec(Dictionary<string, object> d, string key, params float[] fallback) { return d.ContainsKey(key) ? Arr(d[key]).Select(Convert.ToSingle).ToArray() : fallback; }
        static Vector3 V(float[] v) { return new Vector3(v[0], v[1], v[2]); }
        static Quaternion Q(float[] v) { return Quaternion.Normalize(new Quaternion(v[0], v[1], v[2], v[3])); }
        public static HkxAnimationClip Load(string skeleton, string animation, string toolDirectory)
        {
            skeleton = Path.GetFullPath(skeleton); animation = Path.GetFullPath(animation);
            if (!File.Exists(skeleton) || !File.Exists(animation)) throw new FileNotFoundException("Select an existing skeleton.hkx and animation HKX.");
            toolDirectory = Path.GetFullPath(toolDirectory);
            if (!File.Exists(Path.Combine(toolDirectory, "havok_toolset.exe"))) throw new FileNotFoundException("HavokToolset missing: " + toolDirectory);
            string work = Path.Combine(Path.GetTempPath(), "FLVER-HKX", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                // Private per-load copy: never write beside game assets or share mutable tool config.
                File.Copy(Path.Combine(toolDirectory, "havok_toolset.exe"), Path.Combine(work, "havok_toolset.exe"));
                Directory.CreateDirectory(Path.Combine(work, "modules"));
                foreach (string module in Directory.GetFiles(Path.Combine(toolDirectory, "modules"), "*.spk"))
                    File.Copy(module, Path.Combine(work, "modules", Path.GetFileName(module)));
                File.Copy(skeleton, Path.Combine(work, "skeleton.hkx"));
                File.Copy(animation, Path.Combine(work, "animation.hkx"));
                // Spike CLI options differ between releases; use explicit XML config instead.
                var config = new XElement("hk_to_gltf", new XAttribute("extension-patterns", ""),
                    new XElement("animation", new XAttribute("blend-override", "AUTO"), new XAttribute("sample-rate", SampleRate), new XAttribute("scale-type", "INDEPENDENT"), new XAttribute("filename-anims", true)),
                    new XElement("skeleton", new XAttribute("skeleton-path", Path.Combine(work, "skeleton.hkx")), new XAttribute("generation", "DEFAULT"), new XAttribute("visualize", false), new XAttribute("create-root-node", false)),
                    new XElement("scene", new XAttribute("units", "METER"), new XAttribute("custom-scale", 1), new XAttribute("up-axis", "Y+"), new XAttribute("forward-axis", "Z+"), new XAttribute("right-handed", true)));
                File.WriteAllText(Path.Combine(work, "havok_toolset.config"), config.ToString());
                var start = new ProcessStartInfo(Path.Combine(work, "havok_toolset.exe"), "hk_to_gltf animation.hkx") {
                    WorkingDirectory = work, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                string log;
                using (var process = Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(120000)) { process.Kill(); process.WaitForExit(); throw new TimeoutException("HKX conversion exceeded 120 seconds."); }
                    log = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                    if (process.ExitCode != 0 || !File.Exists(Path.Combine(work, "animation.glb")))
                        throw new InvalidDataException("HavokLib could not decode this HKX pair.\n" + log);
                }
                var clip = ReadGlb(Path.Combine(work, "animation.glb")); clip.ConversionLog = log; return clip;
            }
            finally { try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public static HkxAnimationClip ReadGlb(string path)
        {
            Dictionary<string, object> doc; byte[] binary;
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadUInt32() != 0x46546C67 || r.ReadUInt32() != 2) throw new InvalidDataException("Expected GLB v2.");
                uint length = r.ReadUInt32(); if (length != r.BaseStream.Length) throw new InvalidDataException("Truncated GLB.");
                int n = r.ReadInt32(); if (r.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException("Missing GLB JSON.");
                doc = Obj(new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(Encoding.UTF8.GetString(r.ReadBytes(n))));
                n = r.ReadInt32(); if (r.ReadUInt32() != 0x004E4942) throw new InvalidDataException("Missing GLB binary.");
                binary = r.ReadBytes(n); if (binary.Length != n) throw new InvalidDataException("Truncated GLB buffer.");
            }
            var nodes = Arr(doc["nodes"]); int count = nodes.Length;
            var c = new HkxAnimationClip { Names = new string[count], Parents = Enumerable.Repeat(-1, count).ToArray(), translations = new Vector3[count], rotations = new Quaternion[count], scales = new Vector3[count] };
            for (int i = 0; i < count; i++)
            {
                var node = Obj(nodes[i]); c.Names[i] = node.ContainsKey("name") ? (string)node["name"] : "";
                c.translations[i] = V(Vec(node, "translation", 0, 0, 0)); c.rotations[i] = Q(Vec(node, "rotation", 0, 0, 0, 1)); c.scales[i] = V(Vec(node, "scale", 1, 1, 1));
                if (node.ContainsKey("matrix")) throw new InvalidDataException("Matrix nodes are unsupported in HKX conversion output.");
                if (node.ContainsKey("children")) foreach (object child in Arr(node["children"]))
                { int ci = Convert.ToInt32(child); if (ci < 0 || ci >= count || c.Parents[ci] != -1) throw new InvalidDataException("Invalid skeleton hierarchy."); c.Parents[ci] = i; }
            }
            if (!doc.ContainsKey("animations") || Arr(doc["animations"]).Length != 1) throw new InvalidDataException("Select one animation HKX, not a skeleton or multi-animation container.");
            var anim = Obj(Arr(doc["animations"])[0]); var samplers = Arr(anim["samplers"]);
            foreach (object item in Arr(anim["channels"]))
            {
                var channel = Obj(item); var target = Obj(channel["target"]); var sampler = Obj(samplers[Int(channel, "sampler")]);
                string trackPath = (string)target["path"]; if (trackPath != "translation" && trackPath != "rotation" && trackPath != "scale") throw new InvalidDataException("Unsupported animation channel.");
                string interpolation = sampler.ContainsKey("interpolation") ? (string)sampler["interpolation"] : "LINEAR";
                if (interpolation != "LINEAR" && interpolation != "STEP") throw new InvalidDataException("Unsupported interpolation: " + interpolation);
                var t = new Track { Node = Int(target, "node"), Path = trackPath, Times = ReadAccessor(doc, binary, Int(sampler, "input")), Values = ReadAccessor(doc, binary, Int(sampler, "output")), Width = trackPath == "rotation" ? 4 : 3, Step = interpolation == "STEP" };
                if (t.Node < 0 || t.Node >= count || t.Times.Length == 0 || t.Values.Length != t.Times.Length * t.Width) throw new InvalidDataException("Invalid HKX track dimensions.");
                for (int j = 1; j < t.Times.Length; j++) if (t.Times[j] <= t.Times[j - 1]) throw new InvalidDataException("Non-increasing track times.");
                c.Duration = Math.Max(c.Duration, t.Times[t.Times.Length - 1]); c.tracks.Add(t);
            }
            c.SampleWorld(0); // Validate hierarchy before publishing the clip.
            return c;
        }
        static float[] ReadAccessor(Dictionary<string, object> doc, byte[] data, int index)
        {
            var a = Obj(Arr(doc["accessors"])[index]); var view = Obj(Arr(doc["bufferViews"])[Int(a, "bufferView")]);
            if (a.ContainsKey("sparse") || Int(view, "buffer") != 0) throw new InvalidDataException("Unsupported GLB accessor.");
            int type = Int(a, "componentType"), size = type == 5126 ? 4 : type == 5122 || type == 5123 ? 2 : 1;
            string shape = (string)a["type"]; int width = shape == "SCALAR" ? 1 : shape == "VEC3" ? 3 : shape == "VEC4" ? 4 : 0;
            if (width == 0) throw new InvalidDataException("Unsupported track shape.");
            int count = Int(a, "count"), stride = Int(view, "byteStride", size * width), start = Int(view, "byteOffset") + Int(a, "byteOffset");
            bool normalized = a.ContainsKey("normalized") && (bool)a["normalized"]; var values = new float[checked(count * width)];
            for (int i = 0; i < count; i++) for (int k = 0; k < width; k++)
            {
                int at = checked(start + i * stride + k * size); if (at < 0 || at + size > data.Length) throw new InvalidDataException("Accessor outside GLB buffer.");
                float v;
                switch (type) {
                    case 5126: v = BitConverter.ToSingle(data, at); break;
                    case 5122: v = BitConverter.ToInt16(data, at); if (normalized) v = Math.Max(-1f, v / 32767f); break;
                    case 5123: v = BitConverter.ToUInt16(data, at); if (normalized) v /= 65535f; break;
                    case 5121: v = data[at]; if (normalized) v /= 255f; break;
                    default: throw new InvalidDataException("Unsupported track component type.");
                }
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Non-finite animation data."); values[i * width + k] = v;
            }
            return values;
        }
        public Matrix4x4[] SampleWorld(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException("seconds");
            seconds = Math.Max(0, Math.Min(Duration, seconds));
            var p = (Vector3[])translations.Clone(); var q = (Quaternion[])rotations.Clone(); var s = (Vector3[])scales.Clone();
            foreach (var t in tracks)
            {
                int hi = Array.BinarySearch(t.Times, seconds); if (hi < 0) hi = ~hi; hi = Math.Min(hi, t.Times.Length - 1);
                int lo = Math.Max(0, hi - 1); float alpha = hi == lo ? 0 : Math.Max(0, Math.Min(1, (seconds - t.Times[lo]) / (t.Times[hi] - t.Times[lo])));
                if (t.Step && alpha < 1) alpha = 0;
                int a = lo * t.Width, b = hi * t.Width;
                if (t.Path == "rotation") q[t.Node] = Quaternion.Normalize(Quaternion.Slerp(Q(new[] { t.Values[a], t.Values[a + 1], t.Values[a + 2], t.Values[a + 3] }), Q(new[] { t.Values[b], t.Values[b + 1], t.Values[b + 2], t.Values[b + 3] }), alpha));
                else { var value = Vector3.Lerp(new Vector3(t.Values[a], t.Values[a + 1], t.Values[a + 2]), new Vector3(t.Values[b], t.Values[b + 1], t.Values[b + 2]), alpha); if (t.Path == "scale") s[t.Node] = value; else p[t.Node] = value; }
            }
            var world = new Matrix4x4[NodeCount]; var state = new byte[NodeCount];
            for (int i = 0; i < NodeCount; i++) World(i, p, q, s, world, state);
            return world;
        }
        void World(int i, Vector3[] p, Quaternion[] q, Vector3[] s, Matrix4x4[] world, byte[] state)
        {
            if (state[i] == 2) return; if (state[i] == 1) throw new InvalidDataException("Cyclic skeleton."); state[i] = 1;
            world[i] = Matrix4x4.CreateScale(s[i]) * Matrix4x4.CreateFromQuaternion(q[i]) * Matrix4x4.CreateTranslation(p[i]);
            if (Parents[i] >= 0) { World(Parents[i], p, q, s, world, state); world[i] *= world[Parents[i]]; }
            state[i] = 2;
        }
    }
}
