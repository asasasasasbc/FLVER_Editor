using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using SoulsFormats;

namespace MySFformat
{
    public sealed class RenderCliOptions
    {
        public string Model, Screenshot, Camera, Member, Pose;
        public string HkxSkeleton, HkxAnimation;
        public int HkxFrame;
        public int Width = 1200, Height = 1200, Frames = 2;
        public static readonly string Help =
            "FLVER Editor synchronous render CLI (Windows graphics session required)\n" +
            "  MySFformat.Cli.exe --render MODEL --screenshot OUTPUT.png [options]\n" +
            "  --camera VIEW.json   FLVER coordinates: X right, Y up, forward -Z\n" +
            "  --width N --height N 64..4096, default 1200x1200\n" +
            "  --member NAME        Select FLVER member in a multi-model BND4/DCX\n" +
            "  --pose POSE.json     Native editor pose-node JSON\n" +
            "  --hkx-skeleton S.hkx --hkx-animation A.hkx --hkx-frame N (60 Hz)\n" +
            "  --frames N           Render 1..30 frames then save and exit\n" +
            "No interactive editor, no model writes, no mouse/keyboard inputs.\n";

        public static RenderCliOptions Parse(string[] args)
        {
            var o = new RenderCliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (i + 1 >= args.Length) throw new ArgumentException("Missing value for " + key);
                string value = args[++i];
                switch (key)
                {
                    case "--render": o.Model = value; break;
                    case "--screenshot": o.Screenshot = value; break;
                    case "--camera": o.Camera = value; break;
                    case "--member": o.Member = value; break;
                    case "--pose": o.Pose = value; break;
                    case "--hkx-skeleton": o.HkxSkeleton = value; break;
                    case "--hkx-animation": o.HkxAnimation = value; break;
                    case "--hkx-frame": o.HkxFrame = Int32.Parse(value); break;
                    case "--width": o.Width = Int32.Parse(value); break;
                    case "--height": o.Height = Int32.Parse(value); break;
                    case "--frames": o.Frames = Int32.Parse(value); break;
                    default: throw new ArgumentException("Unknown argument " + key);
                }
            }
            if (String.IsNullOrWhiteSpace(o.Model) || String.IsNullOrWhiteSpace(o.Screenshot))
                throw new ArgumentException("--render and --screenshot are required.");
            if (!o.Screenshot.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Screenshot output must be .png.");
            if (o.Width < 64 || o.Width > 4096 || o.Height < 64 || o.Height > 4096 || o.Frames < 1 || o.Frames > 30)
                throw new ArgumentException("Invalid render dimensions or frame count.");
            if ((o.HkxSkeleton == null) != (o.HkxAnimation == null) || o.HkxFrame < 0 || (o.HkxAnimation != null && o.Pose != null) || (o.HkxFrame != 0 && o.HkxAnimation == null))
                throw new ArgumentException("HKX requires skeleton + animation, a nonnegative frame, and no --pose.");
            return o;
        }
    }

    static partial class Program
    {
        internal static RenderCliOptions RenderOptions;
        internal static ViewerCameraSettings RenderCamera;

        private static int RunRenderCli(string[] args)
        {
            try
            {
                RenderOptions = RenderCliOptions.Parse(args);
                flverName = orgFileName = Path.GetFullPath(RenderOptions.Model);
                if (!File.Exists(flverName)) throw new FileNotFoundException("Model not found", flverName);
                loadTexture = false; show3D = true; legacyDisplay = false;
                if (BND4.Is(flverName))
                {
                    var binder = BND4.Read(flverName);
                    var models = binder.Files.Where(x => x.Name.EndsWith(".flver", StringComparison.OrdinalIgnoreCase)).ToArray();
                    var selected = String.IsNullOrEmpty(RenderOptions.Member)
                        ? (models.Length == 1 ? models[0] : null)
                        : models.SingleOrDefault(x => x.Name == RenderOptions.Member || Path.GetFileName(x.Name) == RenderOptions.Member);
                    if (selected == null) throw new ArgumentException("Select one FLVER with --member. Available: " + String.Join(", ", models.Select(x => x.Name)));
                    targetFlver = FLVER2.Read(selected.Bytes);
                }
                else targetFlver = FLVER2.Read(flverName);
                string cameraPath = RenderOptions.Camera ?? ViewerCameraSettings.FindPath(flverName);
                RenderCamera = cameraPath != null
                    ? ViewerCameraSettings.Parse(File.ReadAllText(cameraPath))
                    : ViewerCameraSettings.Parse("{\"camera\":[0,1,-4],\"target\":[0,1,0]}");
                boneDisplay = RenderCamera.showBones; dummyDisplay = RenderCamera.showDummies;
                resetPoses();
                if (RenderOptions.Pose != null)
                {
                    var nodes = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<System.Collections.Generic.List<FLVER.Node>>(File.ReadAllText(RenderOptions.Pose));
                    if (nodes == null || nodes.Count != targetFlver.Nodes.Count) throw new ArgumentException("Pose node count does not match the model.");
                    poseNodes = nodes; poseDisplay = true;
                }
                if (RenderOptions.HkxAnimation != null)
                {
                    var clip = HkxAnimationClip.Load(RenderOptions.HkxSkeleton, RenderOptions.HkxAnimation, HkxToolsPath);
                    var binding = BindHkx(clip);
                    SetHkxPreview(targetFlver, binding.Sample(RenderOptions.HkxFrame / (float)HkxAnimationClip.SampleRate));
                    Console.WriteLine("HKX matched " + binding.MatchedCount + "/" + targetFlver.Nodes.Count + " bones; duration " + clip.Duration);
                }
                using (mono = new Mono3D())
                {
                    updateVertices();
                    mono.Run();
                    if (!mono.RenderCompleted) throw new IOException("Renderer did not complete capture.");
                }
                string output = Path.GetFullPath(RenderOptions.Screenshot);
                if (!File.Exists(output)) throw new IOException("Renderer exited without screenshot.");
                File.WriteAllText(output + ".json", new JavaScriptSerializer().Serialize(new {
                    model = flverName, output, RenderOptions.Width, RenderOptions.Height,
                    RenderOptions.Frames, camera = RenderCamera.camera, target = RenderCamera.target,
                    RenderCamera.renderMode, pose = RenderOptions.Pose,
                    hkxSkeleton = RenderOptions.HkxSkeleton, hkxAnimation = RenderOptions.HkxAnimation, hkxFrame = RenderOptions.HkxFrame,
                    projectionAspect = mono.RenderProjectionAspect,
                    viewportAspect = mono.RenderViewportAspect,
                    preferredAspect = mono.RenderPreferredAspect,
                    projectionM11 = mono.RenderProjectionM11,
                    projectionM22 = mono.RenderProjectionM22,
                    meshes = targetFlver.Meshes.Count, nodes = targetFlver.Nodes.Count,
                    vertices = targetFlver.Meshes.Sum(x => x.Vertices.Count),
                    renderer = "FLVER Editor Mono3D GPU render target", backend = "Windows desktop graphics session; hidden native window"
                }));
                Console.WriteLine("Rendered " + output);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("Render failed: " + ex); return 1; }
        }
    }
}
