using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenPhysicsHKX;

namespace MySFformat.Cloth.Preview
{
    // Editor adapter only. No vendor runtime, temporary HKX, CLI process or JSON bridge.
    public sealed class ManagedClothPreviewSession : IClothPreviewSession
    {
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static readonly string[] Supported={"hclStandardLinkConstraintSet","hclStretchLinkConstraintSet","hclCompressibleLinkConstraintSet","hclLocalRangeConstraintSet","hclBendStiffnessConstraintSet","hclBendLinkConstraintSet","hclTransitionConstraintSet","hclVolumeConstraintMx"};
        readonly object gate=new object();
        readonly string[] names;
        ClothSession[] solvers;
        ManagedClothGraph[] graphs;
        readonly ManagedAnimationBinding animation;
        readonly ClothCollider[][] colliderMetadata;
        ManagedAnimationBinding.Frame lastAnimationFrame;
        int stepping,frame;
        bool disposed,faulted;
        public string Notice {get;private set;}
        public string DisplayName {get{return "OpenPhysicsHKX";}}
        public bool SupportsAnimation {get{return animation.Supported;}}
        public int ProcessId {get{return 0;}}
        public string[] DomainNames {get{return (string[])names.Clone();}}
        public string[] SkeletonNames {get{return animation.Names;}}
        public string[][] SkeletonBones {get{return animation.Bones;}}

        public static Task<IClothPreviewSession> CreateAsync(XDocument snapshot,PreviewEnvironment environment,CancellationToken cancel,IProgress<string> progress)
        {
            var settings=environment.Clone();settings.Validate();
            if(settings.WindEnabled)throw new NotSupportedException("OpenPhysicsHKX does not implement native HCT wind.");
            return Task.Run<IClothPreviewSession>(()=>
            {
                cancel.ThrowIfCancellationRequested();
                if(progress!=null)progress.Report("Preparing bundled OpenPhysicsHKX…");
                string sourceNotice="";var input=snapshot;
                if(HkxTagfileReader.IsTagfileDocument(snapshot))
                {
                    HkxTagfileReader.CompatibilityBackend(snapshot); // reject unknown source SDKs
                    bool sekiro=(string)snapshot.Root.Attribute("sourceFormat")=="TAG0-20160200";
                    input=HkxTagfilePreviewAdapter.Create(snapshot);
                    sourceNotice=(sekiro?"Sekiro 2016":"ER 2018")+" TAG0 input compatibility approximation · NOT game runtime";
                }
                var document=ClothDocument.FromXml(input,"OpenPhysicsHKX memory snapshot",false);
                var unknown=document.Domains.SelectMany(d=>d.Constraints).Select(c=>(string)c.Attribute("class")).Distinct().Where(c=>!Supported.Contains(c)).ToArray();
                if(unknown.Length>0&&!settings.AllowUnmodeledConstraints)throw new NotSupportedException("OpenPhysicsHKX cannot preview this complete asset: "+string.Join(", ",unknown)+". Use Native HCT; no constraints were silently skipped. Approximate omission requires explicit opt-in in Preview Environment.");
                var graphs=document.Domains.Select(d=>d.Owner).Distinct().Select(o=>new ManagedClothGraph(document,o)).ToArray();
                var bindings=graphs.SelectMany(g=>g.Initial).ToDictionary(p=>p.Key,p=>p.Value);
                var definitions=document.Domains.Select((d,i)=>Describe(document,d,i,settings,cancel,bindings)).ToArray();
                cancel.ThrowIfCancellationRequested();
                return new ManagedClothPreviewSession(document,definitions,unknown,sourceNotice,bindings,graphs);
            },cancel);
        }
        ManagedClothPreviewSession(ClothDocument document,ClothDefinition[] definitions,string[] omitted,string sourceNotice,Dictionary<int,ManagedReferenceBinding> bindings,ManagedClothGraph[] graphs)
        {
            names=document.Domains.Select(d=>d.Name).ToArray();animation=new ManagedAnimationBinding(document);
            colliderMetadata=document.Domains.Select(d=>d.Colliders.ToArray()).ToArray();
            if(names.Distinct().Count()!=names.Length)throw new InvalidDataException("OpenPhysicsHKX domain names must be unique.");
            this.graphs=graphs;
            solvers=definitions.Select(d=>new ClothSession(new[]{d},new SolverOptions {LinkForm="raw",RespectFixedStretchAnchors=true,AllowApproximateConstraints=true})).ToArray();
            foreach(var pair in bindings)solvers[pair.Key].SetFrameInputs(new[]{new ClothFrameInput{ClothId=pair.Key,FixedTargets=pair.Value.FixedTargets}});
            var notices=new List<string>{"rest-pose physics / animation-ready"};
            if(graphs.Any(g=>g.HasSkin))notices.Add("reference meshes follow skeleton poses");
            notices.Add("file-defined operation order / separate reference buffers");
            if(graphs.Any(g=>g.HasDeformation))notices.Add("dynamic triangle-frame MeshMeshDeform approximation");
            if(!string.IsNullOrEmpty(sourceNotice))notices.Add(sourceNotice);
            var approximate=solvers.SelectMany(s=>s.ApproximateConstraintClasses).Distinct().ToArray();if(approximate.Length>0)notices.Add("approximate: "+string.Join(", ",approximate));
            if(omitted.Length>0)notices.Add("OMITTED: "+string.Join(", ",omitted));
            Notice=string.Join(" · ",notices);
        }
        static string[] Refs(XElement node,string field){return ClothDocument.Value(node,field).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);}
        static double Number(XElement node,string field){return double.Parse(ClothDocument.Value(node,field),NumberStyles.Float,C);}
        static XElement Optional(XElement node,string field){return node.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")==field);}
        static ClothDefinition Describe(ClothDocument document,ClothDomain d,int id,PreviewEnvironment environment,CancellationToken cancel,Dictionary<int,ManagedReferenceBinding> bindings)
        {
            cancel.ThrowIfCancellationRequested();
            var poses=Refs(d.Sim,"simClothPoses");var coordinates=ClothDocument.Numbers(ClothDocument.Value(document.Objects[poses[0]],"positions"));
            var rest=Enumerable.Range(0,d.Positions.Length).Select(i=>new[]{coordinates[i*4],coordinates[i*4+1],coordinates[i*4+2]}).ToArray();
            var particles=ClothDocument.Field(d.Sim,"particleDatas").Elements().ToArray();
            var simulation=ClothDocument.Field(d.Sim,"simulationInfo").Elements().Single();
            int simIndex=Array.IndexOf(Refs(d.Owner,"simClothDatas"),(string)d.Sim.Attribute("name"));
            var operators=Refs(d.Owner,"operators").Select(r=>document.Objects[r]).ToArray();
            var states=Refs(d.Owner,"clothStateDatas");
            if(states.Length==0)throw new NotSupportedException("No default Cloth execution state.");
            var schedule=ClothDocument.Numbers(ClothDocument.Value(document.Objects[states[0]],"operators"));
            if(schedule.Any(v=>v!=Math.Truncate(v)||v<0||v>=operators.Length))throw new InvalidDataException("Invalid default operator schedule.");
            var active=schedule.Select(v=>operators[(int)v]).ToArray();
            var simulators=active.Where(o=>(string)o.Attribute("class")=="hclSimulateOperator"&&(int)Number(o,"simClothIndex")==simIndex).ToArray();
            if(simulators.Length!=1)throw new NotSupportedException("OpenPhysicsHKX requires one active simulation operator per domain.");
            var op=simulators[0];
            var binding=bindings[id];
            // The graph has already validated the required producer slice and vertex coverage.
            var def=new ClothDefinition {Id=id,Name=d.Name,RestPositions=rest,
                ReferenceBuffers=binding.Buffers,
                Masses=particles.Select(p=>Number(p,"mass")).ToArray(),InverseMasses=particles.Select(p=>Number(p,"invMass")).ToArray(),
                Damping=Number(simulation,"globalDampingPerSecond"),Gravity=environment.OverrideGravity?new[]{(double)environment.GravityX,environment.GravityY,environment.GravityZ}:ClothDocument.Numbers(ClothDocument.Value(simulation,"gravity")).Take(3).ToArray(),
                Substeps=Math.Max(1,(int)Number(op,"subSteps")),Iterations=(int)Number(op,"numberOfSolveIterations"),
                AdaptConstraintStiffness=ClothDocument.Value(op,"adaptConstraintStiffness")=="true",
                ExecutionOrder=ClothDocument.Numbers(ClothDocument.Value(op,"constraintExecution")).Where(v=>v>=0).Select(v=>(int)v).ToArray(),
                AllowEmptyExecution=!ClothDocument.Numbers(ClothDocument.Value(op,"constraintExecution")).Any(v=>v>=0),
                CollisionTolerance=0};
            // Core's offset tolerance is a world-distance radius reduction, not an
            // established mapping of HKX simulationInfo.collisionTolerance. Passing
            // the file's common 0.2 value erased small capsules. Project against the
            // saved shape surface (+ particle radius) until HCT tolerance semantics
            // are implemented. Do not change the file value or the Core/CLI default.
            // Particle collision masks and friction response remain unsupported.
            var radii=particles.Where(p=>Number(p,"invMass")>0).Select(p=>Number(p,"radius")).Distinct().ToArray();
            if(d.Colliders.Count>0&&radii.Length>1)throw new NotSupportedException("OpenPhysicsHKX cannot yet map variable dynamic particle radii: "+d.Name+". Use Native HCT; maximum-radius substitution is not supported.");
            def.ParticleRadius=radii.Length==0?0:radii[0];
            if(d.Colliders.Count!=Refs(d.Sim,"perInstanceCollidables").Length)throw new NotSupportedException("OpenPhysicsHKX cannot map all collider shapes in this domain.");
            def.Capsules=d.Colliders.Select(c=>new[]{(double)c.Start.X,c.Start.Y,c.Start.Z,c.End.X,c.End.Y,c.End.Z,c.RadiusA,c.RadiusB}).ToArray();
            var kept=d.Constraints.Select((c,i)=>new{c,i}).Where(x=>Supported.Contains((string)x.c.Attribute("class"))).ToArray();
            var slotMap=kept.Select((x,i)=>new{oldSlot=x.i,newSlot=i}).ToDictionary(x=>x.oldSlot,x=>x.newSlot);
            def.ExecutionOrder=def.ExecutionOrder.Where(i=>slotMap.ContainsKey(i)).Select(i=>slotMap[i]).ToArray();
            def.Constraints=kept.Select(item=>
            {
                var c=item.c;
                string kind=(string)c.Attribute("class");
                if(kind=="hclVolumeConstraintMx")return DescribeVolume(c);
                string member=kind=="hclLocalRangeConstraintSet"?"localConstraints":kind=="hclTransitionConstraintSet"?"perParticleData":"links";
                var rows=ClothDocument.Field(c,member).Elements().ToArray();
                // Empty sets still retain their slot in the explicit execution order.
                var fields=rows.Length>0?rows[0].Elements().Select(p=>(string)p.Attribute("name")).ToArray():EmptyFields(kind);
                var shape=Optional(c,"shapeType");var normal=Optional(c,"applyNormalComponent");
                if(shape!=null&&shape.Value.Trim()!="SHAPE_SPHERE")throw new NotSupportedException("OpenPhysicsHKX does not implement LocalRange "+shape.Value.Trim()+"; it is not replaced with a sphere.");
                var records=rows.Select(row=>fields.Select(f=>Number(row,f)).ToArray()).ToArray();
                int referenceColumn=Array.IndexOf(fields,"referenceVertex");
                int referenceBuffer=0;ReferenceBufferData referenceData=null;
                if(referenceColumn>=0)
                {
                    if(Optional(c,"referenceMeshBufferIdx")==null)throw new InvalidDataException("Constraint is missing its reference buffer declaration.");
                    referenceBuffer=(int)Number(c,"referenceMeshBufferIdx");
                    if(!binding.Buffers.TryGetValue(referenceBuffer,out referenceData))throw new InvalidDataException("Constraint reference buffer missing from graph snapshot.");
                    foreach(var record in records)
                        if(record[referenceColumn]<0||record[referenceColumn]>=referenceData.Positions.Length)
                            throw new NotSupportedException("OpenPhysicsHKX has no evaluated reference for "+d.Name+", "+kind+" referenceVertex="+record[referenceColumn].ToString(C)+". Reference indices are not replaced with particle indices.");
                }
                int[] normalMap=null;
                if(kind=="hclLocalRangeConstraintSet"&&normal!=null&&normal.Value.Trim()=="true")
                {
                    normalMap=Enumerable.Repeat(-1,rest.Length).ToArray();int pi=Array.IndexOf(fields,"particleIndex");
                    foreach(var row in records){int p=(int)row[pi],rv=(int)row[referenceColumn];if(normalMap[p]>=0&&normalMap[p]!=rv)throw new NotSupportedException("One LocalRange set requires different reference normals for the same particle.");normalMap[p]=rv;}
                }
                var toSim=Optional(c,"toSimPeriod");var toAnim=Optional(c,"toAnimPeriod");
                return new ConstraintDefinition {ClassName=kind,Fields=fields,Records=records,
                    ReferenceBufferId=referenceBuffer,NormalReferenceVertices=normalMap,
                    Shape=shape==null?"SHAPE_SPHERE":shape.Value.Trim(),ApplyNormal=normal!=null&&normal.Value.Trim()=="true",
                    ToSimulationPeriod=toSim==null?1:double.Parse(toSim.Value,C),ToAnimationPeriod=toAnim==null?1:double.Parse(toAnim.Value,C)};
            }).ToArray();
            def.ParticleNormals=RestNormals(rest,d.Triangles);
            return def;
        }
        static ConstraintDefinition DescribeVolume(XElement constraint)
        {
            var records=new List<double[]>();
            foreach(int role in new[]{0,1})
            {
                string prefix=role==0?"frame":"apply",factor=role==0?"weight":"stiffness";
                foreach(bool batch in new[]{true,false})
                    foreach(var row in ClothDocument.Field(constraint,prefix+(batch?"BatchDatas":"SingleDatas")).Elements())
                        for(int lane=0;lane<(batch?16:1);lane++)
                        {
                            string suffix=batch?(lane+1).ToString(C):"";var vector=ClothDocument.Numbers(ClothDocument.Value(row,"frameVector"+suffix));
                            if(vector.Length!=4||Math.Abs(vector[3])>1e-6)throw new NotSupportedException("Unsupported Volume frame-vector homogeneous component."); // tolerate authored float roundoff in the unused W lane
                            double value=Number(row,factor+suffix);
                            var record=new[]{Number(row,"particleIndex"+suffix),vector[0],vector[1],vector[2],role==0?value:0,role==1?value:0,(double)role};
                            // SIMD padding repeats the last lane; only exactly identical repeats
                            // are padding. Conflicting repeated author records remain invalid.
                            if(!records.Any(r=>r.SequenceEqual(record)))records.Add(record);
                        }
            }
            return new ConstraintDefinition{ClassName="hclVolumeConstraintMx",Fields=new[]{"particleIndex","frameX","frameY","frameZ","weight","stiffness","role"},Records=records.ToArray()};
        }
        static double[][] RestNormals(double[][] points,int[] triangles)
        {
            var normals=points.Select(p=>new double[3]).ToArray();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                double ux=points[b][0]-points[a][0],uy=points[b][1]-points[a][1],uz=points[b][2]-points[a][2];
                double vx=points[c][0]-points[a][0],vy=points[c][1]-points[a][1],vz=points[c][2]-points[a][2];
                double x=uy*vz-uz*vy,y=uz*vx-ux*vz,z=ux*vy-uy*vx;
                foreach(int v in new[]{a,b,c}){normals[v][0]+=x;normals[v][1]+=y;normals[v][2]+=z;}
            }
            foreach(var n in normals){double length=Math.Sqrt(n[0]*n[0]+n[1]*n[1]+n[2]*n[2]);if(length>1e-12){n[0]/=length;n[1]/=length;n[2]/=length;}}
            return normals;
        }
        static string[] EmptyFields(string kind)
        {
            if(kind=="hclLocalRangeConstraintSet")return new[]{"particleIndex","referenceVertex","maximumDistance","maxNormalDistance","minNormalDistance"};
            if(kind=="hclBendStiffnessConstraintSet")return new[]{"particleA","particleB","particleC","particleD","weightA","weightB","weightC","weightD","bendStiffness"};
            if(kind=="hclBendLinkConstraintSet")return new[]{"particleA","particleB","bendMinLength","stretchMaxLength","bendStiffness","stretchStiffness"};
            if(kind=="hclTransitionConstraintSet")return new[]{"particleIndex","referenceVertex","toAnimDelay","toSimDelay","toSimMaxDistance"};
            return new[]{"particleA","particleB","restLength","stiffness"};
        }
        public Task<ClothPreviewFrame> StepAsync(int steps,float[][][] poses=null)
        {
            if(steps<1||steps>4)throw new ArgumentOutOfRangeException("steps");
            if(Interlocked.CompareExchange(ref stepping,1,0)!=0)throw new InvalidOperationException("Only one OpenPhysicsHKX frame request may be in flight.");
            ManagedAnimationBinding.Frame[] inputs;
            try
            {
                lock(gate)
                {
                    if(disposed)throw new ObjectDisposedException("OpenPhysicsHKX preview");
                    if(faulted)throw new InvalidOperationException("Solver graph faulted; Stop/Reset before continuing.");
                    inputs=animation.Snapshot(poses,steps);
                }
            }
            catch {Interlocked.Exchange(ref stepping,0);throw;}
            return Task.Run(()=>
            {
                try
                {
                    lock(gate)
                    {
                        if(disposed)throw new ObjectDisposedException("OpenPhysicsHKX preview");
                        if(faulted)throw new InvalidOperationException("Solver graph faulted; Stop/Reset before continuing.");
                        var clock=Stopwatch.StartNew();var positions=new Dictionary<int,double[][]>();
                        try
                        {
                            for(int n=0;n<steps;n++)
                            {
                                var input=inputs==null?lastAnimationFrame:inputs[n];
                                foreach(var graph in graphs)graph.ExecuteAnimated((id,binding)=>
                                {
                                    solvers[id].SetFrameInputs(new[]{new ClothFrameInput{ClothId=id,ReferenceBuffers=binding.Buffers,FixedTargets=binding.FixedTargets,Capsules=input==null?null:input.Capsules[id]}});
                                    solvers[id].Step();var p=solvers[id].GetPositions()[id];positions[id]=p;return p;
                                },input==null?null:input.Poses);
                                lastAnimationFrame=input;
                            }
                            if(inputs!=null)Notice=Notice.Replace("rest-pose physics / animation-ready","animation-driven physics (60 Hz pose inputs)");
                            var domains=new Dictionary<string,Vector3[]>();
                            foreach(var pair in positions)
                            {
                                var vectors=pair.Value.Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();
                                if(vectors.Any(v=>float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z)))throw new InvalidDataException("Solver positions exceed display range.");
                                domains.Add(names[pair.Key],vectors);
                            }
                            Dictionary<string,List<ClothCollider>> colliders=null;
                            if(lastAnimationFrame!=null)
                            {
                                colliders=new Dictionary<string,List<ClothCollider>>();
                                for(int id=0;id<names.Length;id++)colliders.Add(names[id],lastAnimationFrame.Capsules[id].Select((c,i)=>new ClothCollider {
                                    Bone=colliderMetadata[id][i].Bone,Shape=colliderMetadata[id][i].Shape,
                                    Start=new Vector3((float)c[0],(float)c[1],(float)c[2]),End=new Vector3((float)c[3],(float)c[4],(float)c[5]),RadiusA=(float)c[6],RadiusB=(float)c[7]}).ToList());
                            }
                            frame=checked(frame+steps);return new ClothPreviewFrame {Frame=frame,SolveMilliseconds=clock.Elapsed.TotalMilliseconds,Domains=domains,Colliders=colliders};
                        }
                        catch{faulted=true;throw;}
                    }
                }
                finally{Interlocked.Exchange(ref stepping,0);}
            });
        }
        public void Dispose(){lock(gate){disposed=true;solvers=null;graphs=null;}}
    }
}
