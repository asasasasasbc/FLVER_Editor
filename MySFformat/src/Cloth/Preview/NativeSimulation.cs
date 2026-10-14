// GPL-3.0. Native layout/call sequence informed by DSAnimStudio
// NativePhysicsHost on feat/root-motion-bullet-physics-fx-preview.
// Only the exact hash-gated local HCT version is supported. No vendor code.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MySFformat.Cloth.Preview
{
    internal static partial class HctPreviewProbe
    {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AssetCall(IntPtr manager,IntPtr asset);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void WorldCall(IntPtr world);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StepCall(IntPtr world,IntPtr jobs,int threads,float dt,IntPtr scratch);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ScratchCall(IntPtr world,int threads);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StateCall(IntPtr instance,int state);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr AllocateCall(ulong bytes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ConstructorCall(IntPtr instance);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr StreamCall(IntPtr instance,IntPtr bytes,int length,int memoryType,long offset);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AddCall(IntPtr manager,IntPtr asset,[MarshalAs(UnmanagedType.I1)] bool enabled);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MapCall(IntPtr buffer,IntPtr result,IntPtr usage);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr UnmapCall(IntPtr buffer,IntPtr result);
        static readonly Dictionary<IntPtr, UserBuffer> buffers = new Dictionary<IntPtr,UserBuffer>();
        static readonly MapCall mapCallback = Map;
        static readonly UnmapCall unmapCallback = Unmap;
        static T Function<T>(int rva) { var module=GetModuleHandle("hctPreviewPlugin.dll");if(module==IntPtr.Zero)throw new InvalidOperationException("Native module is not loaded");return (T)(object)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,rva),typeof(T)); }
        static unsafe IntPtr Manager(Type t,object control) { return Marshal.ReadIntPtr((IntPtr)Pointer.Unbox(t.GetField("m_si",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(control)),0x20); }
        static void Range(int value,int maximum,string name) { if(value<0||value>maximum)throw new InvalidDataException("Native range: "+name); }
        static bool Finite(float f) { return !float.IsNaN(f)&&!float.IsInfinity(f); }
        static IntPtr Map(IntPtr buffer,IntPtr result,IntPtr usage) { try{buffers[buffer].Map();Marshal.WriteInt32(result,0);}catch{Marshal.WriteInt32(result,-1);}return result; }
        static IntPtr Unmap(IntPtr buffer,IntPtr result) { Marshal.WriteInt32(result,0);return result; }
        sealed class UserBuffer : IDisposable
        {
            readonly IntPtr buffer,table;readonly PreviewMesh description;
            readonly byte[] original=new byte[136];readonly IntPtr[] allocations=new IntPtr[4],slots=new IntPtr[4];
            public UserBuffer(IntPtr b,PreviewMesh d)
            {
                buffer=b;description=d;Marshal.Copy(b,original,0,136);table=Marshal.AllocHGlobal(256);
                byte[] vtable=new byte[256];Marshal.Copy(Marshal.ReadIntPtr(b),vtable,0,256);Marshal.Copy(vtable,0,table,256);
                try
                {
                    for(int c=0;c<4;c++)
                    {
                        int conversion=d.Conversions[c],slot=d.Slots[c],start=d.Starts[c];if(conversion==250)continue;
                        int size=conversion==0?16:conversion==1?12:conversion==2?4:conversion==3?6:0;
                        if(size==0||slot<0||slot>=4||start<0||d.Strides[slot]<start+size||d.Strides[slot]>255||d.Channels[c].Length!=d.VertexCount*4)throw new InvalidDataException("Unverified user-buffer layout");
                        if(slots[slot]==IntPtr.Zero){allocations[slot]=Marshal.AllocHGlobal(checked(d.Strides[slot]*d.VertexCount+16));slots[slot]=new IntPtr((allocations[slot].ToInt64()+15)&~15L);}
                        for(int i=0;i<d.VertexCount;i++)
                        {
                            var p=IntPtr.Add(slots[slot],i*d.Strides[slot]+start);
                            if(conversion<=1)Marshal.Copy(d.Channels[c],i*4,p,conversion==0?4:3);
                            else for(int k=0;k<(conversion==2?4:3);k++){float v=Math.Max(-1,Math.Min(1,d.Channels[c][i*4+k]));if(conversion==2)Marshal.WriteByte(p,k,(byte)Math.Round(v*127+127));else Marshal.WriteInt16(p,k*2,(short)Math.Round(v*32767));}
                        }
                    }
                    Marshal.WriteIntPtr(table,40,Marshal.GetFunctionPointerForDelegate(mapCallback));Marshal.WriteIntPtr(table,48,Marshal.GetFunctionPointerForDelegate(unmapCallback));
                    buffers.Add(b,this);Marshal.WriteIntPtr(b,table);Map();
                }
                catch{Dispose();throw;}
            }
            public void Map()
            {
                int[] offsets={0x18,0x40,0x58,0x70};var d=description;
                for(int c=0;c<4;c++){int offset=offsets[c],slot=d.Slots[c];bool present=d.Conversions[c]!=250;
                    Marshal.WriteIntPtr(buffer,offset,present?IntPtr.Add(slots[slot],d.Starts[c]):IntPtr.Zero);Marshal.WriteInt32(buffer,offset+8,present?d.VertexCount:0);
                    Marshal.WriteByte(buffer,offset+12,present?(byte)d.Strides[slot]:(byte)0);Marshal.WriteInt32(buffer,offset+16,present&&d.Conversions[c]==0&&d.Starts[c]%16==0&&d.Strides[slot]%16==0?1:0);}
            }
            public float[] Positions()
            {
                var d=description;if(d.Conversions[0]>1)throw new InvalidDataException("Position conversion is not a certified float channel");
                var values=new float[d.VertexCount*3];for(int i=0;i<d.VertexCount;i++)Marshal.Copy(IntPtr.Add(slots[d.Slots[0]],i*d.Strides[d.Slots[0]]+d.Starts[0]),values,i*3,3);
                if(values.Any(v=>!Finite(v)))throw new InvalidDataException("Nonfinite native display positions");return values;
            }
            public float[] PreviewPositions()
            {
                var values=Positions();for(int i=0;i<description.AliasSources.Length;i++)Array.Copy(values,description.AliasSources[i]*3,values,description.AliasTargets[i]*3,3);return values;
            }
            public void Dispose(){buffers.Remove(buffer);Marshal.Copy(original,0,buffer,original.Length);foreach(var a in allocations)if(a!=IntPtr.Zero)Marshal.FreeHGlobal(a);Marshal.FreeHGlobal(table);}
        }
        static void LoadDisabled(Type type,object control,byte[] bytes)
        {
            var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);IntPtr stream=Marshal.AllocHGlobal(64),asset=IntPtr.Zero;bool constructed=false;
            try
            {
                Function<StreamCall>(0x187B50)(stream,pinned.AddrOfPinnedObject(),bytes.Length,2,0);constructed=true;
                asset=Function<AllocateCall>(0x6DDD0)(288);if(asset==IntPtr.Zero)throw new OutOfMemoryException();Function<ConstructorCall>(0x5BC80)(asset);Function<AssetCall>(0x5C270)(asset,stream);
                if(Marshal.ReadIntPtr(asset,24)==IntPtr.Zero)throw new InvalidDataException("Cloth loader returned no root");Function<AddCall>(0x5D490)(Manager(type,control),asset,false);
            }
            finally{if(asset!=IntPtr.Zero)Function<WorldCall>(0x118E80)(asset);if(constructed)Function<WorldCall>(0x187C00)(stream);Marshal.FreeHGlobal(stream);pinned.Free();}
        }
        static Action BindScene(Type type,object control)
        {
            var manager=Manager(type,control);int count=Marshal.ReadInt32(manager,0x20);Range(count,32,"asset count");var array=Marshal.ReadIntPtr(manager,0x18);
            IntPtr cloth=IntPtr.Zero,clothRoot=IntPtr.Zero,scene=IntPtr.Zero;
            for(int i=0;i<count;i++)
            {
                var asset=Marshal.ReadIntPtr(array,i*8);var rootObject=Marshal.ReadIntPtr(asset,24);int variants=Marshal.ReadInt32(rootObject,8);Range(variants,64,"variant count");
                for(int j=0;j<variants;j++){var v=IntPtr.Add(Marshal.ReadIntPtr(rootObject),j*24);string name=Marshal.PtrToStringAnsi(new IntPtr(Marshal.ReadIntPtr(v,8).ToInt64()&~1L));if(name=="hkxScene")scene=v;if(name=="hclClothContainer"){cloth=asset;clothRoot=rootObject;}}
            }
            if(cloth==IntPtr.Zero||scene==IntPtr.Zero)throw new InvalidDataException("Original cloth and scene variants are required");
            int oldCount=Marshal.ReadInt32(clothRoot,8),oldCapacity=Marshal.ReadInt32(clothRoot,12);var old=Marshal.ReadIntPtr(clothRoot);var memory=Marshal.AllocHGlobal((oldCount+1)*24);
            var data=new byte[oldCount*24];Marshal.Copy(old,data,0,data.Length);Marshal.Copy(data,0,memory,data.Length);data=new byte[24];Marshal.Copy(scene,data,0,24);Marshal.Copy(data,0,IntPtr.Add(memory,oldCount*24),24);
            Marshal.WriteIntPtr(clothRoot,memory);Marshal.WriteInt32(clothRoot,8,oldCount+1);Marshal.WriteInt32(clothRoot,12,unchecked((int)0x80000000)|(oldCount+1));
            Action restore=delegate{Function<AssetCall>(0x5D890)(manager,cloth);Marshal.WriteIntPtr(clothRoot,old);Marshal.WriteInt32(clothRoot,8,oldCount);Marshal.WriteInt32(clothRoot,12,oldCapacity);Marshal.FreeHGlobal(memory);};
            try{Function<AssetCall>(0x5D6F0)(manager,cloth);return restore;}catch{restore();throw;}
        }
        static IntPtr[] Instances(IntPtr world,PreviewDescription d)
        {
            int count=Marshal.ReadInt32(world,0x20);if(count!=d.Cloths.Length)throw new InvalidDataException("Native instance count mismatch");
            var byName=new Dictionary<string,IntPtr>();for(int i=0;i<count;i++){var instance=Marshal.ReadIntPtr(Marshal.ReadIntPtr(world,0x18),i*8);var data=Marshal.ReadIntPtr(instance,0x18);byName.Add(Marshal.PtrToStringAnsi(new IntPtr(Marshal.ReadIntPtr(data,0x18).ToInt64()&~1L)),instance);}
            var result=d.Cloths.Select(c=>byName[c.Name]).ToArray();
            for(int c=0;c<count;c++)
            {
                var instance=result[c];if(Marshal.ReadInt32(instance,0x114)!=0||Marshal.ReadInt32(instance,0x38)!=d.Cloths[c].TransformSkeletons.Length||Marshal.ReadInt32(instance,0x48)!=d.Cloths[c].ParticleCounts.Length)throw new InvalidDataException("Native topology/state mismatch");
                for(int t=0;t<d.Cloths[c].TransformSkeletons.Length;t++){var ts=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance,0x30),t*8);int bones=d.Skeletons[d.Cloths[c].TransformSkeletons[t]].Bones.Length;if(Marshal.ReadInt32(ts,0x20)!=bones||Marshal.ReadInt32(ts,0x30)!=bones)throw new InvalidDataException("Native transform topology mismatch");}
                foreach(var m in d.Cloths[c].Meshes){if(m.BufferIndex<0||m.BufferIndex>=Marshal.ReadInt32(instance,0x28))throw new InvalidDataException("Display buffer index");var b=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance,0x20),m.BufferIndex*8);if(Marshal.ReadIntPtr(Marshal.ReadIntPtr(b),40).ToInt64()-GetModuleHandle("hctPreviewPlugin.dll").ToInt64()!=0x8763f0)throw new InvalidDataException("Native display vtable mismatch");if(!buffers.ContainsKey(b))new UserBuffer(b,m);}
            }
            return result;
        }
        static float[] Particles(IntPtr[] instances,PreviewDescription d)
        {
            var all=new List<float>();for(int c=0;c<instances.Length;c++)for(int s=0;s<d.Cloths[c].ParticleCounts.Length;s++)
            {
                var sim=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instances[c],0x40),s*8);int count=Marshal.ReadInt32(sim,0x28);if(count!=d.Cloths[c].ParticleCounts[s])throw new InvalidDataException("Native particle topology");
                var data=new float[count*4];Marshal.Copy(Marshal.ReadIntPtr(sim,0x20),data,0,data.Length);for(int i=0;i<count;i++)for(int lane=0;lane<3;lane++){float v=data[i*4+lane];if(!Finite(v))throw new InvalidDataException("Nonfinite native particle position");all.Add(v);}
            }
            return all.ToArray();
        }
        static float[] Flat(Matrix4x4 m){return new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44};}
        static Action InitializationCopies(IntPtr[] instances,PreviewDescription d)
        {
            var locations=new List<IntPtr>();var originals=new List<IntPtr>();var allocations=new List<IntPtr>();
            Action restore=delegate{for(int i=0;i<locations.Count;i++)Marshal.WriteIntPtr(locations[i],originals[i]);foreach(var p in allocations)Marshal.FreeHGlobal(p);};
            try
            {
                for(int c=0;c<instances.Length;c++)foreach(var init in d.Cloths[c].Initializations)
                {
                    var data=Marshal.ReadIntPtr(instances[c],0x18);if(init.Operator<0||init.Operator>=Marshal.ReadInt32(data,0x58)||init.Simulation<0||init.Simulation>=Marshal.ReadInt32(instances[c],0x48)||init.Count!=d.Cloths[c].ParticleCounts[init.Simulation]||init.Input<0||init.Input>=Marshal.ReadInt32(instances[c],0x28)||init.Output<0||init.Output>=Marshal.ReadInt32(instances[c],0x28))throw new InvalidDataException("Initialization index bounds");
                    var location=IntPtr.Add(Marshal.ReadIntPtr(data,0x50),init.Operator*8);var original=Marshal.ReadIntPtr(location);var module=GetModuleHandle("hctPreviewPlugin.dll");
                    if(Marshal.ReadIntPtr(Marshal.ReadIntPtr(original),0x20).ToInt64()-module.ToInt64()!=0x7FF620||Marshal.ReadInt32(original,0x48)!=init.Simulation)throw new InvalidDataException("Unverified simulate operator layout");
                    if(init.UseReferencePose)
                    {
                        // 非 identity 的 reference mesh 不可直接 memcpy 到粒子顺序。
                        // 只初始化原生粒子 XYZ；保留原生第四分量，第一步后照常核验 rest 与历史。
                        var sim=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instances[c],0x40),init.Simulation*8);
                        if(Marshal.ReadInt32(sim,0x28)!=init.Count)throw new InvalidDataException("Reference initialization topology");
                        var reference=d.Cloths[c].ReferenceParticles[init.Simulation];
                        if(reference.Length!=init.Count*4||reference.Any(v=>!Finite(v)))throw new InvalidDataException("Invalid reference initialization pose");
                        var values=new float[init.Count*4];var positions=Marshal.ReadIntPtr(sim,0x20);Marshal.Copy(positions,values,0,values.Length);
                        for(int i=0;i<init.Count;i++)Array.Copy(reference,i*4,values,i*4,3);
                        Marshal.Copy(values,0,positions,values.Length);
                        Console.WriteLine("REFERENCE_POSE_INITIALIZATION "+d.Cloths[c].Name+" / "+d.Cloths[c].SimulationNames[init.Simulation]);
                    }
                    var copy=Marshal.AllocHGlobal(96);allocations.Add(copy);Marshal.Copy(new byte[96],0,copy,96);Marshal.WriteIntPtr(copy,IntPtr.Add(module,0xBBBEC0));Marshal.WriteInt32(copy,0x48,init.Input);Marshal.WriteInt32(copy,0x4c,init.Output);Marshal.WriteInt32(copy,0x50,init.UseReferencePose?0:init.Count);
                    locations.Add(location);originals.Add(original);Marshal.WriteIntPtr(location,copy);
                }
                return restore;
            }
            catch{restore();throw;}
        }
        static void InitializationHistory(IntPtr[] instances,PreviewDescription d)
        {
            for(int c=0;c<instances.Length;c++)foreach(var init in d.Cloths[c].Initializations)
            {
                var sim=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instances[c],0x40),init.Simulation*8);if(Marshal.ReadInt32(sim,0x28)!=init.Count||Marshal.ReadInt32(sim,0x38)!=init.Count)throw new InvalidDataException("History topology");
                var data=new float[init.Count*4];Marshal.Copy(Marshal.ReadIntPtr(sim,0x20),data,0,data.Length);
                var reference=d.Cloths[c].ReferenceParticles[init.Simulation];if(reference.Length!=data.Length)throw new InvalidDataException("Reference-particle count mismatch");
                for(int i=0;i<init.Count;i++)for(int lane=0;lane<3;lane++)if(!Finite(data[i*4+lane])||Math.Abs(data[i*4+lane]-reference[i*4+lane])>.0005f)throw new InvalidDataException("Skinned initialization does not reconstruct authored particle order/rest pose");
                Marshal.Copy(data,0,Marshal.ReadIntPtr(sim,0x30),data.Length);
            }
        }
        static void Poses(IntPtr[] instances,PreviewDescription d)
        {
            for(int c=0;c<instances.Length;c++)for(int t=0;t<d.Cloths[c].TransformSkeletons.Length;t++)
            {
                var data=d.Skeletons[d.Cloths[c].TransformSkeletons[t]].Matrices;var inverse=new float[data.Length];
                for(int i=0;i<data.Length;i+=16){var m=new Matrix4x4(data[i],data[i+1],data[i+2],data[i+3],data[i+4],data[i+5],data[i+6],data[i+7],data[i+8],data[i+9],data[i+10],data[i+11],data[i+12],data[i+13],data[i+14],data[i+15]);Matrix4x4 inv;if(!Matrix4x4.Invert(m,out inv))throw new InvalidDataException("Singular input pose");var transposed=Flat(Matrix4x4.Transpose(inv));if(transposed.Any(v=>!Finite(v)))throw new InvalidDataException("Nonfinite inverse input pose");Array.Copy(transposed,0,inverse,i,16);}
                var ts=Marshal.ReadIntPtr(Marshal.ReadIntPtr(instances[c],0x30),t*8);Marshal.Copy(data,0,Marshal.ReadIntPtr(ts,0x18),data.Length);Marshal.Copy(inverse,0,Marshal.ReadIntPtr(ts,0x28),inverse.Length);
            }
        }
        static object RunSimulation(Type type,object control,string assets,int frames,string pipeName=null)
        {
            var json=new JavaScriptSerializer{MaxJsonLength=128*1024*1024};var d=json.Deserialize<PreviewDescription>(File.ReadAllText(Path.Combine(assets,"description.json")));
            Action restore=null;HostEnvironment environment=null;
            System.Windows.Forms.CheckBox clothControl=null;bool originalClothEnabled=false;
            try
            {
                Stage("LOAD_ADAPTER_SCENE");Call(type,control,"updateContentsFromPackfile",(object)File.ReadAllBytes(Path.Combine(assets,"scene.hkx")));
                Stage("LOAD_ORIGINAL_CLOTH_DISABLED");LoadDisabled(type,control,File.ReadAllBytes(Path.Combine(assets,"cloth.hkx")));
                Stage("BIND_ORIGINAL_CLOTH_TO_SCENE");restore=BindScene(type,control);
                var world=Marshal.ReadIntPtr(Manager(type,control),0x290);var instances=Instances(world,d);Stage("NATIVE_BINDINGS_VALIDATED");
                if(d.Environment==null)d.Environment=new PreviewEnvironment();d.Environment.Validate();environment=new HostEnvironment(type,control);environment.Apply(d.Environment);Stage("PREVIEW_ENVIRONMENT_APPLIED");
                var post=Function<WorldCall>(0x733DD0);var state=Function<StateCall>(0x736F90);
                // Use HCT's complete preview step path: the direct world solver
                // bypasses the preview wind integration. Retain the audited world
                // post/readback sequence used by this process-isolated adapter.
                clothControl=type.GetField("clothCheckBox",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(control) as System.Windows.Forms.CheckBox;
                if(clothControl==null)throw new NotSupportedException("HCT cloth control not available");
                originalClothEnabled=clothControl.Checked;
                IntPtr scratchAllocation=IntPtr.Zero,scratch=IntPtr.Zero;int scratchCapacity=0;
                Action run=delegate{clothControl.Checked=true;Call(type,control,"step",1f/60);};
                Action animatedRun=delegate
                {
                    // HCT 暂停它自己的 Cloth 步进，只更新预览环境/scene；
                    // 核验粒子未被重复求解后，用编辑器姿态执行一次原生步进。
                    clothControl.Checked=false;
                    var before=Particles(instances,d);
                    Call(type,control,"step",1f/60);
                    if(Marshal.ReadInt32(world,0x20)!=instances.Length)throw new InvalidDataException("Cloth UI pause changed instance ownership");
                    var after=Particles(instances,d);
                    if(before.Where((value,i)=>Math.Abs(value-after[i])>1e-6f).Any())throw new InvalidDataException("HCT Cloth pause did not prevent duplicate stepping");
                    int bytes=Function<ScratchCall>(0x733210)(world,1);
                    Range(bytes,256*1024*1024,"animated scratch allocation");
                    if(scratch==IntPtr.Zero||bytes>scratchCapacity)
                    {
                        if(scratchAllocation!=IntPtr.Zero)Marshal.FreeHGlobal(scratchAllocation);
                        scratchAllocation=IntPtr.Zero;scratch=IntPtr.Zero;
                        scratchAllocation=Marshal.AllocHGlobal(bytes+16);
                        scratch=new IntPtr((scratchAllocation.ToInt64()+15)&~15L);
                        scratchCapacity=bytes;
                    }
                    Poses(instances,d);
                    Function<AssetCall>(0x733710)(world,IntPtr.Zero);
                    Function<StepCall>(0x733A20)(world,IntPtr.Zero,1,1f/60,scratch);
                };
                for(int phase=0;phase<2;phase++)
                {
                    for(int c=0;c<instances.Length;c++){int target=phase==0?d.Cloths[c].PreviousState:d.Cloths[c].CurrentState;state(instances[c],target<0?0:target);}Poses(instances,d);
                    Action restoreCopies=phase==0?InitializationCopies(instances,d):null;
                    try{run();if(phase==0)InitializationHistory(instances,d);post(world);}finally{if(restoreCopies!=null)restoreCopies();}
                }
                for(int c=0;c<instances.Length;c++)state(instances[c],0);
                Stage("AUTHORED_INITIALIZATION_EXECUTED");Poses(instances,d);run();var initial=Particles(instances,d);var firstDisplay=buffers.Values.SelectMany(b=>b.Positions()).ToArray();post(world);
                Stage("NATIVE_MANAGER_STEP_WITH_ENVIRONMENT");
                if(pipeName!=null)
                {
                    try{return ServePreview(pipeName,instances,d,run,animatedRun,delegate{post(world);},environment);}
                    finally{if(scratchAllocation!=IntPtr.Zero)Marshal.FreeHGlobal(scratchAllocation);}
                }
                var watch=Stopwatch.StartNew();float[] final=initial;float maxDelta=0;int changed=0;float[] display=firstDisplay;
                for(int frame=1;frame<frames;frame++)
                {
                    Poses(instances,d);run();final=Particles(instances,d);display=buffers.Values.SelectMany(b=>b.Positions()).ToArray();post(world);
                    for(int i=0;i<final.Length;i++)maxDelta=Math.Max(maxDelta,Math.Abs(final[i]-initial[i]));
                }
                watch.Stop();for(int i=0;i<final.Length;i+=3)if(Math.Abs(final[i]-initial[i])+Math.Abs(final[i+1]-initial[i+1])+Math.Abs(final[i+2]-initial[i+2])>1e-6)changed++;
                float displayDelta=0;for(int i=0;i<display.Length;i++)displayDelta=Math.Max(displayDelta,Math.Abs(display[i]-firstDisplay[i]));
                if(changed==0||maxDelta<=1e-6||displayDelta<=1e-6)throw new InvalidDataException("No measurable native cloth/display movement");
                // Captures are evidence, not author-file edits or model writeback.
                File.WriteAllText(Path.Combine(assets,"native-result-"+Guid.NewGuid().ToString("N")+".json"),json.Serialize(new{initialParticles=initial,finalParticles=final,initialDisplay=firstDisplay,finalDisplay=display,previewDisplay=buffers.Values.SelectMany(b=>b.PreviewPositions()).ToArray()}));
                return new{frames=frames,dt=1f/60,instances=instances.Length,particles=final.Length/3,changedParticles=changed,maxParticleComponentDelta=maxDelta,maxDisplayComponentDelta=displayDelta,milliseconds=watch.Elapsed.TotalMilliseconds,millisecondsPerFrame=watch.Elapsed.TotalMilliseconds/(frames-1),runtimeClothBytesUnmodified=!d.RuntimeRepacked,authoredClothKnownFieldsPreserved=!d.Environment.OverrideGravity,runtimeRepacked=d.RuntimeRepacked,originalLayout=d.OriginalLayout,runtimeLayout=d.RuntimeLayout,poseMode="constant authored reference",finitePositions=true,clm2Loaded=d.Clm2Loaded,clm2Meshes=d.Clm2Meshes,clm2Entries=d.Clm2Entries,clm2PreviewAliases=d.Cloths.SelectMany(c=>c.Meshes).Sum(m=>m.AliasSources.Length),temporaryInitializationCopies=d.Cloths.Sum(c=>c.Initializations.Length),previewEnvironment=d.Environment,stepMode="hctPreviewControl.step with environment stage"};
            }
            finally
            {
                if(clothControl!=null)clothControl.Checked=originalClothEnabled;
                if(environment!=null)environment.Dispose();foreach(var b in new List<UserBuffer>(buffers.Values))b.Dispose();if(restore!=null)restore();
            }
        }
        static object ServePreview(string name,IntPtr[] instances,PreviewDescription d,Action run,Action animatedRun,Action post,HostEnvironment environment)
        {
            int frames=0;using(var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut))
            {
                // v4 字节布局不变；整批 Flush，避免逐 float 做管道 I/O。
                pipe.Connect(15000);var reader=new BinaryReader(new BufferedStream(pipe,65536));var writer=new BinaryWriter(new BufferedStream(pipe,65536));
                writer.Write(0x34565043);writer.Write(d.Cloths.Sum(c=>c.ParticleCounts.Length));
                foreach(var cloth in d.Cloths)for(int s=0;s<cloth.ParticleCounts.Length;s++){writer.Write(cloth.Name+" / "+cloth.SimulationNames[s]);writer.Write(cloth.ParticleCounts[s]);}
                writer.Write(d.Skeletons.Length);foreach(var skeleton in d.Skeletons){writer.Write(skeleton.Name);writer.Write(skeleton.Bones.Length);foreach(string bone in skeleton.Bones)writer.Write(bone);}writer.Flush();
                var authored=d.Skeletons.Select(s=>(float[])s.Matrices.Clone()).ToArray();
                try
                {
                    while(true)
                    {
                        int command=reader.ReadInt32();if(command==2)break;if(command!=1)throw new InvalidDataException("Unknown preview command");
                        int sequence=reader.ReadInt32(),steps=reader.ReadInt32();if(sequence<1||steps<1||steps>4)throw new InvalidDataException("Preview step bounds");
                        var settings=d.Environment.Clone();settings.WindEnabled=reader.ReadBoolean();settings.WindX=reader.ReadSingle();settings.WindY=reader.ReadSingle();settings.WindZ=reader.ReadSingle();settings.WindSpeed=reader.ReadSingle();settings.WindMaxDrag=reader.ReadSingle();settings.Validate();environment.Apply(settings);
                        float[][][] poses=null;if(reader.ReadBoolean())
                        {
                            poses=new float[steps][][];for(int p=0;p<steps;p++){poses[p]=new float[d.Skeletons.Length][];for(int s=0;s<d.Skeletons.Length;s++){var values=new float[authored[s].Length];for(int v=0;v<values.Length;v++){values[v]=reader.ReadSingle();if(!Finite(values[v]))throw new InvalidDataException("Nonfinite animation pose");}poses[p][s]=values;}}
                        }
                        var watch=Stopwatch.StartNew();float[] positions=null;
                        for(int i=0;i<steps;i++){for(int s=0;s<d.Skeletons.Length;s++)d.Skeletons[s].Matrices=poses==null?authored[s]:poses[i][s];Poses(instances,d);if(poses==null)run();else animatedRun();try{positions=Particles(instances,d);}finally{post();}frames++;}
                        watch.Stop();writer.Write(3);writer.Write(sequence);writer.Write(frames);writer.Write(watch.Elapsed.TotalMilliseconds);writer.Write(positions.Length);foreach(float value in positions)writer.Write(value);writer.Flush();
                    }
                }
                catch(IOException){/* Owned client closed/cancelled; no cross-process or source changes. */}
            }
            return new{mode="local named pipe",frames=frames,poseMode="authored reference or streamed animation",finitePositions=true,clm2Loaded=d.Clm2Loaded,runtimeRepacked=d.RuntimeRepacked};
        }
    }
}
