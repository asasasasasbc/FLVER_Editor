using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using OpenPhysicsHKX;

namespace MySFformat.Cloth.Preview
{
    // Serial execution of the physics-relevant slice of one owner's authored
    // default schedule. Buffers are owner-scoped and persist between frames.
    internal sealed class ManagedClothGraph
    {
        sealed class Buffer
        {
            internal double[][] Positions,Normals;
            internal int[] Triangles;
        }
        readonly ClothDocument document;
        readonly XElement owner;
        readonly XElement[] sequence,definitions;
        readonly ClothDomain[] domains;
        readonly int[] ids;
        readonly Buffer[] buffers;
        readonly HashSet<int> needed=new HashSet<int>();
        readonly Dictionary<int,ManagedReferenceBinding> staticSkin=new Dictionary<int,ManagedReferenceBinding>();
        readonly Dictionary<int,ManagedReferenceBinding.SkinBinding> skinBindings=new Dictionary<int,ManagedReferenceBinding.SkinBinding>();
        readonly Dictionary<int,double[]>[] pins;
        internal readonly Dictionary<int,ManagedReferenceBinding> Initial=new Dictionary<int,ManagedReferenceBinding>();
        internal bool HasDeformation {get;private set;}
        internal bool HasSkin {get{return staticSkin.Count>0;}}
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static XElement F(XElement o,string k){return o.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")==k);}
        static string V(XElement o,string k){return ClothDocument.Value(o,k);}
        static int I(XElement o,string k){return int.Parse(V(o,k),C);}
        static int Index(double value){if(value!=Math.Truncate(value)||value<int.MinValue||value>int.MaxValue)throw new InvalidDataException("Nonintegral/out-of-range graph index.");return (int)value;}
        static double[] N(XElement o,string k){return ClothDocument.Numbers(V(o,k));}
        static string[] R(XElement o,string k){return V(o,k).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);}
        static string Class(XElement o){return (string)o.Attribute("class");}
        static Vector3 Vector(double[] p){return new Vector3((float)p[0],(float)p[1],(float)p[2]);}
        static double[] Array(Vector3 p){return new[]{(double)p.X,p.Y,p.Z};}
        static Matrix4x4 Matrix(double[] a,int p){return new Matrix4x4((float)a[p],(float)a[p+1],(float)a[p+2],(float)a[p+3],(float)a[p+4],(float)a[p+5],(float)a[p+6],(float)a[p+7],(float)a[p+8],(float)a[p+9],(float)a[p+10],(float)a[p+11],(float)a[p+12],(float)a[p+13],(float)a[p+14],(float)a[p+15]);}
        static int Output(XElement op){var p=F(op,"outputBufferIdx")??F(op,"outputBufferIndex");return p==null?-1:int.Parse(p.Value,C);}
        static bool IsMesh(string cls){return cls=="hclMeshMeshDeformOperator"||cls.StartsWith("hclObjectSpaceMeshMeshDeform",StringComparison.Ordinal)||cls.StartsWith("hclBoneSpaceMeshMeshDeform",StringComparison.Ordinal);}
        Buffer B(int i){if(i<0||i>=buffers.Length)throw new InvalidDataException("Managed graph buffer index outside owner.");return buffers[i];}
        int S(XElement op){int s=I(op,"simClothIndex");if(s<0||s>=domains.Length)throw new InvalidDataException("Managed graph simulation index outside owner.");return s;}

        internal ManagedClothGraph(ClothDocument document,XElement owner)
        {
            this.document=document;this.owner=owner;
            var simRefs=R(owner,"simClothDatas");domains=simRefs.Select(id=>document.Domains.Single(d=>ReferenceEquals(d.Owner,owner)&&(string)d.Sim.Attribute("name")==id)).ToArray();
            ids=domains.Select(d=>document.Domains.IndexOf(d)).ToArray();
            var ops=R(owner,"operators").Select(r=>document.Objects[r]).ToArray();var states=R(owner,"clothStateDatas");if(states.Length==0)throw new NotSupportedException("No authored default cloth state.");
            var order=N(document.Objects[states[0]],"operators");if(order.Any(v=>v!=Math.Truncate(v)||v<0||v>=ops.Length))throw new InvalidDataException("Invalid managed graph schedule.");
            sequence=order.Select(v=>ops[(int)v]).ToArray();
            definitions=R(owner,"bufferDefinitions").Select(r=>document.Objects[r]).ToArray();buffers=new Buffer[definitions.Length];pins=domains.Select(d=>new Dictionary<int,double[]>()).ToArray();
            for(int i=0;i<buffers.Length;i++)
            {
                int count=I(definitions[i],"numVertices");if(count<0||count>2000000)throw new InvalidDataException("Unsafe graph buffer size.");
                buffers[i]=new Buffer{Positions=new double[count][],Normals=new double[count][]};
                if(I(definitions[i],"type")==1)
                {
                    int sim=I(definitions[i],"subType");if(sim<0||sim>=domains.Length||domains[sim].Positions.Length!=count)throw new InvalidDataException("Simulation buffer layout mismatch.");
                    var pose=N(document.Objects[R(domains[sim].Sim,"simClothPoses")[0]],"positions");buffers[i].Positions=Enumerable.Range(0,count).Select(j=>new[]{pose[j*4],pose[j*4+1],pose[j*4+2]}).ToArray();buffers[i].Triangles=domains[sim].Triangles;
                }
            }
            for(int s=0;s<domains.Length;s++)
            {
                var simulators=Enumerable.Range(0,sequence.Length).Where(i=>Class(sequence[i])=="hclSimulateOperator"&&S(sequence[i])==s).ToArray();
                if(simulators.Length!=1)throw new NotSupportedException("Managed graph requires one scheduled simulator per domain.");
                int at=simulators[0];
                foreach(var constraint in domains[s].Constraints.Where(c=>F(c,"referenceMeshBufferIdx")!=null))RequireBuffer(I(constraint,"referenceMeshBufferIdx"),at);
                for(int j=0;j<at;j++)if(Class(sequence[j])=="hclMoveParticlesOperator"&&S(sequence[j])==s){needed.Add(j);RequireBuffer(I(sequence[j],"refBufferIdx"),j);}
            }
            foreach(int index in needed.OrderBy(i=>i))
            {
                var op=sequence[index];string cls=Class(op);
                if(cls.Contains("SpaceSkin"))
                {
                    var skin=ManagedReferenceBinding.PrepareSkin(document,owner,op,B(Output(op)).Positions.Length,sequence.Take(index).ToArray());
                    skinBindings.Add(index,skin);staticSkin.Add(index,skin.Evaluate(null));
                }
                else if(IsMesh(cls))HasDeformation=true;
                else if(cls!="hclMoveParticlesOperator"&&cls!="hclCopyVerticesOperator"&&cls!="hclGatherAllVerticesOperator"&&cls!="hclGatherSomeVerticesOperator")throw new NotSupportedException("Unsupported required managed graph operator: "+cls);
            }
            Execute(null); // initialize explicit numeric inputs from the authored poses, without solving
        }
        void RequireBuffer(int buffer,int before)
        {
            B(buffer);bool producer=I(definitions[buffer],"type")==1;
            // Include earlier writes too: a partial deformer must not erase untouched Skin vertices.
            for(int i=0;i<before;i++)if(Output(sequence[i])==buffer)
            {
                producer=true;if(!needed.Add(i))continue;var op=sequence[i];string cls=Class(op);
                if(IsMesh(cls)||cls=="hclCopyVerticesOperator"||cls=="hclGatherAllVerticesOperator"||cls=="hclGatherSomeVerticesOperator")RequireBuffer(I(op,"inputBufferIdx"),i);
                else if(!cls.Contains("SpaceSkin"))throw new NotSupportedException("Unsupported managed reference producer: "+cls);
            }
            if(!producer)throw new NotSupportedException("Required graph buffer has no authored producer: "+buffer);
        }
        static double[] Read(Buffer b,int vertex,bool normal=false)
        {
            if(vertex<0||vertex>=b.Positions.Length)throw new InvalidDataException("Managed graph vertex outside buffer.");var p=normal?b.Normals[vertex]:b.Positions[vertex];
            if(p==null)throw new NotSupportedException("Required graph vertex/channel was not written; no rest fallback.");return p;
        }
        static void Merge(Buffer output,ManagedReferenceBinding value)
        {
            if(value.Positions.Length!=output.Positions.Length)throw new InvalidDataException("Graph output topology mismatch.");
            for(int i=0;i<output.Positions.Length;i++){if(value.Positions[i]!=null)output.Positions[i]=value.Positions[i];if(value.Normals[i]!=null)output.Normals[i]=value.Normals[i];}
        }
        internal ManagedReferenceBinding Snapshot(int sim)
        {
            var result=new ManagedReferenceBinding{Buffers=new Dictionary<int,ReferenceBufferData>()};
            foreach(var pair in pins[sim])result.FixedTargets.Add(pair.Key,(double[])pair.Value.Clone());
            foreach(var group in domains[sim].Constraints.Where(c=>F(c,"referenceMeshBufferIdx")!=null).GroupBy(c=>I(c,"referenceMeshBufferIdx")))
            {
                var buffer=B(group.Key);
                foreach(var constraint in group)
                {
                    bool normals=Class(constraint)=="hclLocalRangeConstraintSet"&&F(constraint,"applyNormalComponent")!=null&&V(constraint,"applyNormalComponent")=="true";
                    foreach(var f in constraint.Descendants("hkparam").Where(p=>(string)p.Attribute("name")=="referenceVertex")){int vertex=int.Parse(f.Value,C);Read(buffer,vertex);if(normals)Read(buffer,vertex,true);}
                }
                result.Buffers.Add(group.Key,new ReferenceBufferData{Positions=buffer.Positions.Select(p=>p??new double[3]).ToArray(),Normals=buffer.Normals.Select(p=>p??new double[3]).ToArray()});
            }
            // Fixed targets are independent from the constraint reference buffers.
            // A Move from buffer 2 never overwrites a constraint's buffer 0 target.
            return result;
        }
        internal void Execute(Func<int,ManagedReferenceBinding,double[][]> step)
        {ExecuteAnimated(step,null);}
        internal void ExecuteAnimated(Func<int,ManagedReferenceBinding,double[][]> step,Dictionary<XElement,Matrix4x4[]> poses)
        {
            foreach(var p in pins)p.Clear();
            for(int index=0;index<sequence.Length;index++)
            {
                var op=sequence[index];string cls=Class(op);
                if(cls=="hclSimulateOperator")
                {
                    int sim=S(op);var binding=Snapshot(sim);
                    if(step==null)Initial[ids[sim]]=binding;
                    else
                    {
                        var positions=step(ids[sim],binding);
                        for(int buffer=0;buffer<buffers.Length;buffer++)if(I(definitions[buffer],"type")==1&&I(definitions[buffer],"subType")==sim)buffers[buffer].Positions=positions;
                    }
                    continue;
                }
                if(!needed.Contains(index))continue;
                ManagedReferenceBinding skin;if(staticSkin.TryGetValue(index,out skin)){Merge(B(Output(op)),poses==null?skin:skinBindings[index].Evaluate(poses));continue;}
                if(cls=="hclMoveParticlesOperator")
                {
                    int sim=S(op);var source=B(I(op,"refBufferIdx"));
                    foreach(var pair in ClothDocument.Field(op,"vertexParticlePairs").Elements())
                    {
                        int particle=I(pair,"particleIndex");if(particle<0||particle>=domains[sim].Fixed.Length||!domains[sim].Fixed[particle])throw new NotSupportedException("Dynamic MoveParticles needs explicit state-transition handling.");
                        pins[sim][particle]=(double[])Read(source,I(pair,"vertexIndex")).Clone();
                    }
                }
                else if(IsMesh(cls))Merge(B(Output(op)),Deform(op));
                else Copy(op);
            }
        }
        void Copy(XElement op)
        {
            var source=B(I(op,"inputBufferIdx"));var target=B(Output(op));string cls=Class(op);bool normal=(F(op,"gatherNormals")!=null&&V(op,"gatherNormals")=="true")||(F(op,"copyNormals")!=null&&V(op,"copyNormals")=="true");
            if(ReferenceEquals(source,target))source=new Buffer{Positions=source.Positions.Select(p=>p==null?null:(double[])p.Clone()).ToArray(),Normals=source.Normals.Select(p=>p==null?null:(double[])p.Clone()).ToArray(),Triangles=source.Triangles};
            Action<int,int> copy=(from,to)=>{if(to<0||to>=target.Positions.Length)throw new InvalidDataException("Gather target outside buffer.");target.Positions[to]=(double[])Read(source,from).Clone();if(normal)target.Normals[to]=(double[])Read(source,from,true).Clone();};
            if(cls=="hclCopyVerticesOperator")for(int i=0;i<I(op,"numberOfVertices");i++)copy(I(op,"startVertexIn")+i,I(op,"startVertexOut")+i);
            else if(cls=="hclGatherAllVerticesOperator")
            {var map=N(op,"vertexInputFromVertexOutput");if(map.Length!=target.Positions.Length)throw new InvalidDataException("Gather map length mismatch.");for(int i=0;i<map.Length;i++){int from=Index(map[i]);if(from>=32768&&from<=65535)from-=65536;if(from== -1)continue;if(from<0)throw new InvalidDataException("Invalid negative gather index.");copy(from,i);}}
            else foreach(var pair in ClothDocument.Field(op,"vertexPairs").Elements())copy(I(pair,"indexInput"),I(pair,"indexOutput"));
        }
        internal static Matrix4x4 TriangleFrame(Vector3 a,Vector3 b,Vector3 c,string mode)
        {
            var center=(a+b+c)/3;var u=a-center;var v=b-center;var n=Vector3.Cross(u,v);float squared=n.LengthSquared();
            if(mode=="SCALE_NORMAL_IGNORE"||mode=="0"){if(squared<1e-30f)throw new NotSupportedException("Degenerate triangle frame.");n/= (float)Math.Sqrt(squared);}
            else if(mode=="SCALE_NORMAL_INVERT"||mode=="2"){if(squared<1e-30f)throw new NotSupportedException("Degenerate inverse-normal triangle frame.");n/=squared;}
            else if(mode!="SCALE_NORMAL_APPLY"&&mode!="1")throw new NotSupportedException("Unknown triangle normal scaling mode.");
            return new Matrix4x4(u.X,u.Y,u.Z,0,v.X,v.Y,v.Z,0,n.X,n.Y,n.Z,0,center.X,center.Y,center.Z,1);
        }
        Matrix4x4[] Frames(XElement op,Buffer input,int count)
        {
            if(input.Triangles==null)throw new NotSupportedException("Deformer input has no authored simulation triangle topology.");
            var subset=N(op,"inputTrianglesSubset");if(subset.Length!=0&&subset.Length!=count)throw new InvalidDataException("Triangle subset/frame count mismatch.");var frames=new Matrix4x4[count];
            for(int i=0;i<count;i++)
            {
                int tri=subset.Length==0?i:Index(subset[i]);if(tri<0||tri>=input.Triangles.Length/3)throw new InvalidDataException("Deformer triangle outside input topology.");
                frames[i]=TriangleFrame(Vector(Read(input,input.Triangles[tri*3])),Vector(Read(input,input.Triangles[tri*3+1])),Vector(Read(input,input.Triangles[tri*3+2])),V(op,"scaleNormalBehaviour"));
            }
            return frames;
        }
        ManagedReferenceBinding Deform(XElement op)
        {
            var input=B(I(op,"inputBufferIdx"));int count=B(Output(op)).Positions.Length;string cls=Class(op);
            var subset=N(op,"inputTrianglesSubset");int frameCount=subset.Length!=0?subset.Length:input.Triangles==null?0:input.Triangles.Length/3;
            if(cls!="hclMeshMeshDeformOperator")
            {
                bool bone=cls.Contains("BoneSpace");double[] bind=bone?null:N(op,"triangleFromMeshTransforms");if(!bone){if(bind.Length%16!=0)throw new InvalidDataException("Invalid triangle inverse matrix count.");frameCount=bind.Length/16;}
                var frames=Frames(op,input,frameCount);
                if(!bone)for(int i=0;i<frames.Length;i++)frames[i]=Matrix(bind,i*16)*frames[i];
                // Mesh local normals are frame-direction data, not skeleton covectors.
                return ManagedReferenceBinding.EvaluateDeformer(op,count,frames,(Matrix4x4[])frames.Clone());
            }
            var legacyFrames=Frames(op,input,frameCount);var starts=N(op,"triangleVertexStartForVertex");var pairs=ClothDocument.Field(op,"triangleVertexPairs").Elements().ToArray();
            int first=I(op,"startVertex"),last=I(op,"endVertex");if(first<0||last>=count||last<first||starts.Length!=last-first+2)throw new InvalidDataException("Legacy deformer output range mismatch.");
            var result=new ManagedReferenceBinding{Positions=new double[count][],Normals=new double[count][]};bool normal=V(op,"deformNormals")=="true";
            for(int vertex=first;vertex<=last;vertex++)
            {
                int start=Index(starts[vertex-first]),end=Index(starts[vertex-first+1]);if(start<0||end<start||end>pairs.Length)throw new InvalidDataException("Legacy deformer pair range invalid.");if(start==end)continue;
                var position=Vector4.Zero;var direction=Vector3.Zero;double sum=0;
                for(int j=start;j<end;j++)
                {
                    var pair=pairs[j];int tri=I(pair,"triangleIndex");if(tri<0||tri>=legacyFrames.Length)throw new InvalidDataException("Legacy pair triangle outside subset.");double weight=double.Parse(V(pair,"weight"),C);if(weight<0||weight>1)throw new InvalidDataException("Invalid triangle blend weight.");sum+=weight;
                    var p=N(pair,"localPosition");if(p.Length!=4)throw new InvalidDataException("Legacy local position is not XYZW.");position+=Vector4.Transform(new Vector4((float)p[0],(float)p[1],(float)p[2],(float)p[3]),legacyFrames[tri])*(float)weight;
                    if(normal)direction+=Vector3.TransformNormal(Vector(N(pair,"localNormal")),legacyFrames[tri])*(float)weight;
                }
                if(Math.Abs(sum-1)>1e-4)throw new NotSupportedException("Triangle blend weights do not sum to one; not renormalized.");
                result.Positions[vertex]=Array(new Vector3(position.X,position.Y,position.Z));if(normal){if(direction.LengthSquared()>1e-20f)direction=Vector3.Normalize(direction);result.Normals[vertex]=Array(direction);}
            }
            return result;
        }
    }
}
