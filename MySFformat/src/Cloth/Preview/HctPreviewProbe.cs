// GPL-3.0-or-later. Experimental local-only HCT adapter.
// Initialization sequence informed by KamiyamaShiki0704/DSAnimStudio,
// feat/root-motion-bullet-physics-fx-preview, NativePhysicsHost (GPL-3.0).
// No vendor code or binaries are included. See README.md in this directory.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MySFformat.Cloth.Preview
{
    // Separate x64 .NET Framework process: never loads mixed-mode HCT into the editor.
    internal static partial class HctPreviewProbe
    {
        const string SupportedHash = "83E33363FBD320BB89613521633E5EDD4F5169D7501012426A850F982F025F75";
        static string root;
        static readonly List<object> stages = new List<object>();
        static readonly List<object> snapshots = new List<object>();
        [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);

        static object Call(Type type, object instance, string method, params object[] args)
        {
            var info = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (info == null) throw new MissingMethodException(type.FullName, method);
            return info.Invoke(instance, args);
        }
        static Type TypeIn(string file, string name)
        {
            return Assembly.LoadFrom(Path.Combine(root, file)).GetType(name, true);
        }
        static void Stage(string name)
        {
            AssertVendorPaths();
            Console.WriteLine(name); Console.Out.Flush();
            stages.Add(new { name = name, elapsedUtc = DateTime.UtcNow.ToString("o") });
            Capture(name);
        }
        static void AssertVendorPaths()
        {
            string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
            Action<string> check=path=>{string file=Path.GetFileName(path);if(file.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)&&(file.StartsWith("Havok.",StringComparison.OrdinalIgnoreCase)||file.StartsWith("hct",StringComparison.OrdinalIgnoreCase)||file.StartsWith("hkCompat",StringComparison.OrdinalIgnoreCase))&&!Path.GetFullPath(path).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Vendor module escaped the selected local runtime: "+path);};
            using(var process=Process.GetCurrentProcess())foreach(ProcessModule module in process.Modules)check(module.FileName);
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())if(!assembly.IsDynamic)check(assembly.Location);
        }
        static void Capture(string name)
        {
            var modules = new List<string>();
            using (var process = Process.GetCurrentProcess())
                foreach (ProcessModule module in process.Modules) modules.Add(module.FileName);
            var assemblies = new List<string>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (!assembly.IsDynamic) assemblies.Add(assembly.Location);
            snapshots.Add(new { stage = name, modules = modules, assemblies = assemblies });
        }
        static string Hash(string file)
        {
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length != 2 && args.Length != 4)
            {
                Console.Error.WriteLine("Usage: HctPreviewProbe.exe <local-runtime-root> <new-report.json> [assets-directory frame-count]");
                return 2;
            }
            root = Path.GetFullPath(args[0]);
            string report = Path.GetFullPath(args[1]);
            if (File.Exists(report)) { Console.Error.WriteLine("Refusing existing report."); return 2; }
            SetErrorMode(0x8003);
            bool ready = false, cleanupOk = true;
            string failure = null;
            string actualPluginHash = null;
            object simulation = null;
            bool simulationPassed = false;
            HostNetworkGuard network = null;
            int deniedSocketCalls = 0, guardedImports = 0;
            object graphicsControl = null, control = null;
            Type graphicsControlType = null, previewType = null, baseType = null;
            bool baseReady = false, previewBaseReady = false, previewReady = false;
            IntPtr compatibility = IntPtr.Zero;
            ToolStrip toolbar = null; MenuStrip menu = null; Form parent = null;
            try
            {
                if (IntPtr.Size != 8) throw new PlatformNotSupportedException("The HCT probe must run x64.");
                string plugin = Path.Combine(root, "tools", "hctPreviewPlugin.dll");
                actualPluginHash = Hash(plugin);
                if (actualPluginHash != SupportedHash) throw new InvalidDataException("Unsupported HCT preview DLL; do not guess native offsets.");
                network = new HostNetworkGuard();
                // Do not inherit an installation directory via PATH or working directory.
                Environment.CurrentDirectory = root;
                Environment.SetEnvironmentVariable("PATH", root + ";" + Path.Combine(root, "bin") + ";" +
                    Path.Combine(root, "tools") + ";" + Path.Combine(root, "tools", "gac") + ";" +
                    Environment.GetFolderPath(Environment.SpecialFolder.System));
                if (!SetDllDirectory(Path.Combine(root, "tools"))) throw new System.ComponentModel.Win32Exception();
                AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
                {
                    string name = new AssemblyName(e.Name).Name + ".dll";
                    foreach (string dir in new[] { "tools/gac", "tools", "bin", "" })
                    {
                        string path = Path.Combine(root, dir, name);
                        if (File.Exists(path)) return Assembly.LoadFrom(path);
                    }
                    return null;
                };
                Stage("VERSION_CHECKED");
                // HCT otherwise finds the compatibility module through its
                // installed tools path. Preload the exact local copy by name.
                compatibility = LoadLibrary(Path.Combine(root, "utils", "hkCompatFormats.dll"));
                if (compatibility == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                network.GuardLoadedModules();
                Stage("LOCAL_COMPATIBILITY_MODULE_LOADED");
                graphicsControlType = TypeIn("tools/gac/Havok.Tool.UI.dll", "Havok.Tool.GraphicsControl");
                graphicsControl = Activator.CreateInstance(graphicsControlType, new object[] { "Direct3D11", IntPtr.Zero, null, null });
                Stage("HIDDEN_GRAPHICS_CONTEXT_CREATED");
                baseType = TypeIn("tools/gac/Havok.Tool.Interfaces.dll", "Havok.Tool.ToolBaseSystem");
                object initResult = Call(baseType, null, "init"); baseReady = true;
                Console.WriteLine("BASE_INIT_RESULT " + initResult);
                var graphics = TypeIn("tools/gac/Havok.Graphics.dll", "Havok.Graphics.hkgBaseSystemCLR");
                var system = TypeIn("tools/gac/Havok.Graphics.dll", "Havok.Graphics.hkgSystemCLR");
                var sync = Call(graphics, null, "getBaseSyncInfoAsStaticPtr");
                var graphicsInfo = Call(system, null, "getGraphicsInfoAsStaticPtr");
                previewType = TypeIn("tools/hctPreviewPlugin.dll", "PreviewPlugin.hctPreviewControl");
                control = Activator.CreateInstance(previewType);
                Call(previewType, control, "initBaseSystem", sync, graphicsInfo); previewBaseReady = true;
                toolbar = new ToolStrip();
                toolbar.Items.Add(new ToolStripButton { Name = "toolStripFlyModeButton" });
                toolbar.Items.Add(new ToolStripButton { Name = "toolStripFirstPersonButton" });
                menu = new MenuStrip(); parent = new Form { ShowInTaskbar = false };
                Call(previewType, control, "setNavigationToolBar", toolbar);
                Call(previewType, control, "setParentMenu", menu);
                Call(previewType, control, "setParentControl", parent, null);
                Call(previewType, control, "init"); previewReady = true;
                var window = graphicsControlType.GetField("m_windowHKG").GetValue(graphicsControl);
                var displayWorld = graphicsControlType.GetField("m_displayWorldHKG").GetValue(graphicsControl);
                var context = Call(window.GetType(), window, "getContext");
                Call(previewType, control, "setContext", displayWorld, context, window);
                Stage("NATIVE_PREVIEW_READY"); ready = true;
                if (args.Length == 4)
                {
                    string pipeName=args[3].StartsWith("pipe:",StringComparison.Ordinal)?args[3].Substring(5):null;
                    int frames=pipeName==null?int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture):0;
                    if (pipeName==null&&(frames < 2 || frames > 3600)) throw new ArgumentOutOfRangeException("frame-count");
                    if(pipeName!=null&&(pipeName.Length<16||pipeName.Length>120||pipeName.IndexOfAny(new[]{'\\','/'})>=0))throw new ArgumentException("Invalid local pipe name");
                    simulation = RunSimulation(previewType, control, Path.GetFullPath(args[2]), frames,pipeName);
                    simulationPassed = true; Stage("NATIVE_CLOTH_STEPPED");
                }
                // Without an asset argument this remains a bootstrap-only probe.
            }
            catch (Exception e)
            {
                failure = e.ToString(); Console.Error.WriteLine(failure);
                try { Capture("FAILED"); } catch { }
            }
            finally
            {
                Action<Action> cleanup = delegate(Action action)
                {
                    try { action(); } catch (Exception e) { cleanupOk = false; Console.Error.WriteLine("CLEANUP " + e); }
                };
                if (previewReady) cleanup(delegate { Call(previewType, control, "quit"); });
                if (previewBaseReady) cleanup(delegate { Call(previewType, control, "quitBaseSystem"); });
                if (control is IDisposable) cleanup(delegate { ((IDisposable)control).Dispose(); });
                if (baseReady) cleanup(delegate { Call(baseType, null, "quitBaseSystem"); });
                if (graphicsControl != null) cleanup(delegate { Call(graphicsControlType, graphicsControl, "Cleanup"); });
                if (graphicsControl is IDisposable) cleanup(delegate { ((IDisposable)graphicsControl).Dispose(); });
                if (network != null) cleanup(delegate { deniedSocketCalls = network.DeniedSocketCalls; guardedImports = network.GuardedImports; network.Dispose(); });
                if (compatibility != IntPtr.Zero) cleanup(delegate { FreeLibrary(compatibility); });
                if (parent != null) parent.Dispose(); if (menu != null) menu.Dispose(); if (toolbar != null) toolbar.Dispose();
                var result = new
                {
                    status = ready && cleanupOk && failure == null ? (simulationPassed ? "PASS_NATIVE_CLOTH" : "PASS_BOOTSTRAP_ONLY") : "FAIL",
                    runtimeRoot = root, pluginSha256 = actualPluginHash, expectedPluginSha256 = SupportedHash, nativeReady = ready,
                    cleanupOk = cleanupOk, clothSimulationTested = simulationPassed, simulation = simulation, graphicsRequired = true,
                    vendorNetworkDisabled = guardedImports > 0, deniedSocketCalls = deniedSocketCalls, guardedSocketImports = guardedImports,
                    executionLevel = "asInvoker",
                    error = failure, stages = stages, snapshots = snapshots
                };
                var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
                using (var output = new StreamWriter(new FileStream(report, FileMode.CreateNew, FileAccess.Write)))
                    output.Write(serializer.Serialize(result));
            }
            return ready && cleanupOk && failure == null ? 0 : 1;
        }
    }
}
