using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MySFformat.Cloth.Preview
{
    public sealed class ClothPreviewFrame
    {
        public int Frame;
        public double SolveMilliseconds;
        public Dictionary<string,Vector3[]> Domains;
    }
    // Editor-side client only. HCT/native code is never loaded into the editor.
    public sealed class ClothPreviewSession : IDisposable
    {
        readonly NamedPipeServerStream pipe;
        readonly string[] names;
        readonly int[] counts;
        readonly string[] skeletonNames;
        readonly string[][] skeletonBones;
        readonly Process process;
        readonly System.Threading.Timer watchdog;
        readonly object diagnosticsGate=new object();
        readonly string log;
        string diagnostic="";
        BinaryReader reader;BinaryWriter writer;
        int disposed,sequence,stepping;
        readonly PreviewEnvironment environment;
        public string WorkDirectory {get;private set;}
        public int ProcessId {get{return process.Id;}}
        public string[] DomainNames {get{return (string[])names.Clone();}}
        public string[] SkeletonNames {get{return (string[])skeletonNames.Clone();}}
        public string[][] SkeletonBones {get{return skeletonBones.Select(b=>(string[])b.Clone()).ToArray();}}
        public static string HelpersDirectory {get{return Path.Combine(Path.GetDirectoryName(typeof(ClothPreviewSession).Assembly.Location),"tools","ClothPreview");}}
        public static string RuntimeSettingFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FLVER_Editor","ClothPreview","runtime-path.txt");}}
        public static string DefaultRuntime
        {
            get
            {
                string env=Environment.GetEnvironmentVariable("FLVER_CLOTH_RUNTIME");if(!string.IsNullOrWhiteSpace(env))return env;
                return File.Exists(RuntimeSettingFile)?File.ReadAllText(RuntimeSettingFile).Trim():"";
            }
        }
        public static void SaveRuntimeSetting(string folder)
        {
            ValidateRuntime(folder);Directory.CreateDirectory(Path.GetDirectoryName(RuntimeSettingFile));File.WriteAllText(RuntimeSettingFile,Path.GetFullPath(folder));
        }
        public static void ValidateRuntime(string folder)
        {
            if(string.IsNullOrWhiteSpace(folder))throw new InvalidOperationException("Choose HCT Runtime first (the folder containing tools/hctPreviewPlugin.dll).");
            foreach(string file in new[]{"tools/hctPreviewPlugin.dll","utils/hkCompatFormats.dll","tools/gac/Havok.Tool.UI.dll","tools/gac/Havok.Tool.Interfaces.dll","tools/gac/Havok.Graphics.dll","tools/gac/Havok.Graphics.Bridge.dll"})
                if(!File.Exists(Path.Combine(folder,file)))throw new FileNotFoundException("Missing local HCT runtime file: "+file);
        }
        static string Quote(string value){return "\""+value.TrimEnd('\\').Replace("\"","\\\"")+"\"";}
        static ProcessStartInfo StartInfo(string exe,string arguments,string working)
        {
            return new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=working};
        }
        static void KillOwned(Process process){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
        public static Task<ClothPreviewSession> CreateAsync(string xml,byte[] flver,byte[] clm2,string runtime,CancellationToken cancel,IProgress<string> progress,PreviewEnvironment environment=null)
        {
            var settings=(environment??new PreviewEnvironment()).Clone();settings.Validate();return Task.Run(()=>Create(xml,flver,clm2,runtime,cancel,progress,settings));
        }
        static ClothPreviewSession Create(string xml,byte[] flver,byte[] clm2,string runtime,CancellationToken cancel,IProgress<string> progress,PreviewEnvironment environment)
        {
            ValidateRuntime(runtime);cancel.ThrowIfCancellationRequested();
            string helper=Path.Combine(HelpersDirectory,"HctPreviewProbe.exe"),builder=Path.Combine(HelpersDirectory,"PreviewAssetBuilder.exe"),schema=Path.Combine(HelpersDirectory,"schema-bb.xml");
            foreach(string path in new[]{helper,builder,schema})if(!File.Exists(path))throw new FileNotFoundException("Preview helper is missing; rebuild/deploy the editor: "+path);
            string baseWork=Environment.GetEnvironmentVariable("FLVER_CLOTH_WORK");if(string.IsNullOrWhiteSpace(baseWork))baseWork=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FLVER_Editor","ClothPreview","work");
            string work=Path.Combine(baseWork,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
            File.WriteAllText(Path.Combine(work,"input.xml"),xml);File.WriteAllBytes(Path.Combine(work,"input.flver"),flver);if(clm2!=null)File.WriteAllBytes(Path.Combine(work,"input.clm2"),clm2);
            File.WriteAllText(Path.Combine(work,"preview-environment.json"),new JavaScriptSerializer().Serialize(environment));
            if(progress!=null)progress.Report("Preparing current in-memory HKX / FLVER / CLM2 snapshots…");
            string assets=Path.Combine(work,"assets"),arguments=Quote(Path.Combine(work,"input.xml"))+" "+Quote(Path.Combine(work,"input.flver"))+" "+Quote(schema)+" "+Quote(assets)+" "+Quote(clm2==null?"":Path.Combine(work,"input.clm2"))+" "+Quote(Path.Combine(work,"preview-environment.json"));
            using(var process=new Process{StartInfo=StartInfo(builder,arguments,work)})
            {
                var text=new System.Text.StringBuilder();object gate=new object();DataReceivedEventHandler output=(s,e)=>{if(e.Data!=null)lock(gate){if(text.Length<100000)text.AppendLine(e.Data);}};
                process.OutputDataReceived+=output;process.ErrorDataReceived+=output;process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
                using(cancel.Register(()=>KillOwned(process)))
                {
                    var clock=Stopwatch.StartNew();while(!process.WaitForExit(100)){cancel.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>45){KillOwned(process);throw new TimeoutException("Cloth scene preparation timed out.");}}
                    process.WaitForExit();cancel.ThrowIfCancellationRequested();File.WriteAllText(Path.Combine(work,"assets.log"),text.ToString());if(process.ExitCode!=0)throw new InvalidDataException("Cloth scene preparation failed: "+text);
                }
            }
            cancel.ThrowIfCancellationRequested();if(progress!=null)progress.Report("Starting isolated native Cloth (offline network guard)…");
            return new ClothPreviewSession(helper,Path.GetFullPath(runtime),assets,work,cancel,environment);
        }
        ClothPreviewSession(string helper,string runtime,string assets,string work,CancellationToken cancel,PreviewEnvironment settings)
        {
            environment=settings.Clone();
            WorkDirectory=work;log=Path.Combine(work,"host.log");string pipeName="FLVER.ClothPreview."+Guid.NewGuid().ToString("N");
            var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
            pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,65536,65536,security);
            process=new Process{StartInfo=StartInfo(helper,Quote(runtime)+" "+Quote(Path.Combine(work,"host-report.json"))+" "+Quote(assets)+" "+Quote("pipe:"+pipeName),work)};
            process.StartInfo.EnvironmentVariables["DEVPATH"]=Path.Combine(runtime,"tools","gac");process.StartInfo.EnvironmentVariables["PATH"]=Environment.GetFolderPath(Environment.SpecialFolder.System);
            DataReceivedEventHandler output=(s,e)=>{if(e.Data!=null)lock(diagnosticsGate){diagnostic+=e.Data+"\n";if(diagnostic.Length>12000)diagnostic=diagnostic.Substring(diagnostic.Length-12000);try{File.AppendAllText(log,e.Data+Environment.NewLine);}catch(IOException){}}};
            process.OutputDataReceived+=output;process.ErrorDataReceived+=output;
            watchdog=new System.Threading.Timer(_=>Abort("Native preview timed out."),null,Timeout.Infinite,Timeout.Infinite);
            try
            {
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();watchdog.Change(45000,Timeout.Infinite);
                using(cancel.Register(()=>Abort("Preview startup cancelled.")))
                {
                    var connected=pipe.WaitForConnectionAsync(cancel);
                    while(!connected.Wait(100)){cancel.ThrowIfCancellationRequested();if(process.HasExited)throw new InvalidOperationException("Native preview exited during initialization: "+Diagnostic());}
                    // 独立读写缓冲，避免逐个矩阵 float 触发管道系统调用。
                    connected.GetAwaiter().GetResult();cancel.ThrowIfCancellationRequested();reader=new BinaryReader(new BufferedStream(pipe,65536));writer=new BinaryWriter(new BufferedStream(pipe,65536));
                    if(reader.ReadInt32()!=0x34565043)throw new InvalidDataException("Cloth preview protocol mismatch");int domains=reader.ReadInt32();if(domains<1||domains>1024)throw new InvalidDataException("Unsafe preview domain count");
                    names=new string[domains];counts=new int[domains];int total=0;for(int i=0;i<domains;i++){names[i]=reader.ReadString();counts[i]=reader.ReadInt32();if(names[i].Length>4096||counts[i]<1||counts[i]>1000000)throw new InvalidDataException("Unsafe preview topology");total=checked(total+counts[i]);}
                    if(total>1000000||names.Distinct().Count()!=domains)throw new InvalidDataException("Ambiguous/oversized preview topology");
                    int skeletonCount=reader.ReadInt32();if(skeletonCount<1||skeletonCount>64)throw new InvalidDataException("Unsafe preview skeleton count");
                    skeletonNames=new string[skeletonCount];skeletonBones=new string[skeletonCount][];int boneTotal=0;
                    for(int s=0;s<skeletonCount;s++){skeletonNames[s]=reader.ReadString();int bones=reader.ReadInt32();if(skeletonNames[s].Length>4096||bones<1||bones>4096)throw new InvalidDataException("Unsafe preview skeleton");boneTotal+=bones;skeletonBones[s]=new string[bones];for(int b=0;b<bones;b++){skeletonBones[s][b]=reader.ReadString();if(skeletonBones[s][b].Length>4096)throw new InvalidDataException("Unsafe bone name");}}
                    if(boneTotal>16384)throw new InvalidDataException("Oversized preview skeletons");
                    watchdog.Change(Timeout.Infinite,Timeout.Infinite);
                }
            }
            catch{Dispose();throw;}
        }
        string Diagnostic(){lock(diagnosticsGate)return diagnostic;}
        void Abort(string reason){lock(diagnosticsGate)diagnostic=reason+"\n"+diagnostic;try{pipe.Dispose();}catch{}KillOwned(process);}
        public Task<ClothPreviewFrame> StepAsync(int steps,float[][][] poses=null)
        {
            if(steps<1||steps>4)throw new ArgumentOutOfRangeException("steps");
            if(poses!=null){if(poses.Length!=steps)throw new ArgumentException("Pose substep count mismatch");poses=poses.Select(p=>{if(p==null||p.Length!=skeletonBones.Length)throw new ArgumentException("Pose skeleton count mismatch");return p.Select((values,s)=>{if(values==null||values.Length!=skeletonBones[s].Length*16||values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new ArgumentException("Invalid pose matrices");return (float[])values.Clone();}).ToArray();}).ToArray();}
            if(Interlocked.CompareExchange(ref stepping,1,0)!=0)throw new InvalidOperationException("Only one native frame request may be in flight.");
            return Task.Run(()=>
            {
                try
                {
                    if(Volatile.Read(ref disposed)!=0)throw new ObjectDisposedException("ClothPreviewSession");watchdog.Change(15000,Timeout.Infinite);
                    int request=++sequence;writer.Write(1);writer.Write(request);writer.Write(steps);writer.Flush();
                    writer.Write(environment.WindEnabled);writer.Write(environment.WindX);writer.Write(environment.WindY);writer.Write(environment.WindZ);writer.Write(environment.WindSpeed);writer.Write(environment.WindMaxDrag);writer.Flush();
                    writer.Write(poses!=null);if(poses!=null)foreach(var pose in poses)foreach(var skeleton in pose)foreach(float value in skeleton)writer.Write(value);writer.Flush();
                    if(reader.ReadInt32()!=3||reader.ReadInt32()!=request)throw new InvalidDataException("Cloth preview frame sequence mismatch");
                    int frame=reader.ReadInt32();double milliseconds=reader.ReadDouble();int values=reader.ReadInt32();if(frame<1||values!=counts.Sum()*3||double.IsNaN(milliseconds)||double.IsInfinity(milliseconds))throw new InvalidDataException("Cloth preview frame bounds");
                    var domains=new Dictionary<string,Vector3[]>();for(int d=0;d<counts.Length;d++){var points=new Vector3[counts[d]];for(int i=0;i<points.Length;i++){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))throw new InvalidDataException("Nonfinite native frame");points[i]=new Vector3(x,y,z);}domains.Add(names[d],points);}
                    return new ClothPreviewFrame{Frame=frame,SolveMilliseconds=milliseconds,Domains=domains};
                }
                catch(Exception e){throw new InvalidOperationException("Native Cloth frame failed: "+e.Message+"\n"+Diagnostic(),e);}
                finally{Interlocked.Exchange(ref stepping,0);try{watchdog.Change(Timeout.Infinite,Timeout.Infinite);}catch(ObjectDisposedException){}}
            });
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;watchdog.Dispose();try{pipe.Dispose();}catch{}
            try{if(!process.HasExited&&!process.WaitForExit(1000)){KillOwned(process);process.WaitForExit(1000);}}catch(InvalidOperationException){}
            process.Dispose();
        }
    }
}
