using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using SoulsFormats;
using static MySFformat.ClothSetupXml;

namespace MySFformat
{
    public sealed class ClothSetupOptions
    {
        public int MeshIndex,SupportBoneIndex;
        public bool AssignSingleBone;
        public int[] FixedParticles;
        public float TotalMass=.8f,GravityY=-9.81f,Damping=.6f,Radius=.003f,Range=.4f,StretchStrength=1f,BendStrength=.006f;
        public string DisplayName="Setup_Cloth";
        public string MaterialMtd=@"N:\SPRJ\data\Material\mtd\parts\P[ARSN]_al_Cloth.mtd";
    }
    public sealed class ClothSetupMesh
    {
        public Vector3[] Positions,Normals;
        public int[] DisplayMap,Triangles;
        public int DisplayVertices;
        public int[] TopRows(int count)
        {
            var heights=Positions.Select(v=>Math.Round(v.Y,5)).Distinct().OrderByDescending(v=>v).Take(count).ToArray();
            return Enumerable.Range(0,Positions.Length).Where(i=>heights.Contains(Math.Round(Positions[i].Y,5))).ToArray();
        }
    }
    public sealed class ClothSetupResult
    {
        public byte[] FlverBytes,HkxBytes,Clm2Bytes;
        public XDocument Xml;
        public string Report;
        public void SaveNewFolder(string directory,string basename)
        {
            if(string.IsNullOrWhiteSpace(basename)||basename.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new IOException("Invalid output model name.");
            string output=Path.GetFullPath(directory);if(Directory.Exists(output)||File.Exists(output))throw new IOException("Choose a new output folder. Existing folders are preserved.");
            string parent=Path.GetDirectoryName(output);if(!Directory.Exists(parent))throw new IOException("Output parent folder does not exist.");
            string temp=Path.Combine(parent,".cloth-setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllBytes(Path.Combine(temp,basename+".flver"),FlverBytes);File.WriteAllBytes(Path.Combine(temp,basename+"_c.hkx"),HkxBytes);File.WriteAllBytes(Path.Combine(temp,basename+"_c.clm2"),Clm2Bytes);
                Xml.Save(Path.Combine(temp,basename+"_c.xml"));File.WriteAllText(Path.Combine(temp,"setup-report.txt"),Report);
                Directory.Move(temp,output);
            }
            finally{if(Directory.Exists(temp)){foreach(var file in Directory.GetFiles(temp))File.Delete(file);Directory.Delete(temp);}}
        }
    }
    public static class ClothSetupBuilder
    {
        static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
        static bool Finite(Vector3 v){return Finite(v.X)&&Finite(v.Y)&&Finite(v.Z);}
        static string Vec(Vector3 v,float w){return "("+string.Join(" ",new[]{v.X,v.Y,v.Z,w}.Select(f=>f.ToString("R",CultureInfo.InvariantCulture)))+")";}
        static string Matrix(Matrix4x4 m){return Vec(new Vector3(m.M11,m.M12,m.M13),m.M14)+Vec(new Vector3(m.M21,m.M22,m.M23),m.M24)+Vec(new Vector3(m.M31,m.M32,m.M33),m.M34)+Vec(new Vector3(m.M41,m.M42,m.M43),m.M44);}
        public static ClothSetupMesh Analyze(FLVER2 model,int index)
        {
            if(model==null||index<0||index>=model.Meshes.Count)throw new InvalidDataException("Select an existing mesh.");
            var mesh=model.Meshes[index];if(mesh.Vertices.Count<3||mesh.Vertices.Count>30000)throw new InvalidDataException("This prototype supports 3..30000 display vertices.");
            if(mesh.Vertices.Any(v=>!Finite(v.Position)||!Finite(v.Normal)))throw new InvalidDataException("Non-finite mesh coordinates/normals.");
            var faces=mesh.FaceSets.Where(f=>f.Flags==0).ToArray();if(faces.Length!=1)throw new InvalidDataException("Select a mesh with exactly one base FaceSet; LOD / motion blur copies are excluded.");
            var raw=faces[0].Triangulate(true);if(raw.Count==0||raw.Count%3!=0||raw.Any(i=>i<0||i>=mesh.Vertices.Count))throw new InvalidDataException("Invalid base triangle indices.");
            var positions=new List<Vector3>();var normals=new List<Vector3>();var map=new int[mesh.Vertices.Count];var lookup=new Dictionary<string,int>();
            for(int i=0;i<map.Length;i++)
            {
                var v=mesh.Vertices[i];string key=string.Join(",",new[]{v.Position.X,v.Position.Y,v.Position.Z}.Select(f=>Math.Round(f,6).ToString("F6",CultureInfo.InvariantCulture)));
                int particle;if(!lookup.TryGetValue(key,out particle)){particle=positions.Count;lookup.Add(key,particle);positions.Add(v.Position);normals.Add(v.Normal);}else if(Vector3.Distance(positions[particle],v.Position)>2e-6f)throw new InvalidDataException("Weld tolerance mismatch.");
                map[i]=particle;
            }
            if(positions.Count>65535)throw new InvalidDataException("Particle index limit exceeded.");
            int[] triangles=raw.Select(i=>map[i]).ToArray();var adjacency=Enumerable.Range(0,positions.Count).Select(i=>new HashSet<int>()).ToArray();var edges=new Dictionary<string,int>();var unique=new HashSet<string>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(a==b||b==c||a==c||Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).Length()<1e-10f)throw new InvalidDataException("Degenerate welded triangle.");
                if(!unique.Add(string.Join(",",new[]{a,b,c}.OrderBy(v=>v))))throw new InvalidDataException("Duplicate welded triangles.");
                foreach(var edge in new[]{Tuple.Create(a,b),Tuple.Create(b,c),Tuple.Create(c,a)}){adjacency[edge.Item1].Add(edge.Item2);adjacency[edge.Item2].Add(edge.Item1);string key=Math.Min(edge.Item1,edge.Item2)+","+Math.Max(edge.Item1,edge.Item2);if(!edges.ContainsKey(key))edges[key]=0;if(++edges[key]>2)throw new InvalidDataException("Non-manifold welded edge.");}
            }
            var visited=new HashSet<int>();var pending=new Stack<int>();pending.Push(0);while(pending.Count>0){int p=pending.Pop();if(!visited.Add(p))continue;foreach(int q in adjacency[p])if(!visited.Contains(q))pending.Push(q);}
            if(visited.Count!=positions.Count)throw new InvalidDataException("Disconnected or unused particles are not supported in the first wizard version.");
            var sums=new Vector3[positions.Count];for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var n=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);sums[a]+=n;sums[b]+=n;sums[c]+=n;}
            if(sums.Any(v=>v.Length()<1e-8f))throw new InvalidDataException("Cannot construct particle normals.");
            return new ClothSetupMesh{Positions=positions.ToArray(),Normals=sums.Select(Vector3.Normalize).ToArray(),DisplayMap=map,Triangles=triangles,DisplayVertices=map.Length};
        }
        public static ClothSetupResult Build(FLVER2 source,ClothSetupOptions options)
        {
            var geometry=Analyze(source,options.MeshIndex);int count=geometry.Positions.Length;
            if(source.Meshes.Count>10000)throw new InvalidDataException("CLM2 mesh-count limit exceeded.");
            if(source.Meshes.Where((m,i)=>i!=options.MeshIndex).Any(m=>m.MaterialIndex>=0&&m.MaterialIndex<source.Materials.Count&&source.Materials[m.MaterialIndex].MTD.EndsWith("_al_Cloth.mtd",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Other Cloth-material meshes are present. This first wizard creates only one domain and cannot preserve existing multi-domain Cloth bindings.");
            if(options.SupportBoneIndex<0||options.SupportBoneIndex>=source.Nodes.Count)throw new InvalidDataException("Select a valid support bone.");
            if(options.SupportBoneIndex>255)throw new InvalidDataException("This first-version UByte4 skin layout supports global support-bone indices 0..255 only.");
            if(!Finite(options.TotalMass)||options.TotalMass<=0||!Finite(options.GravityY)||!Finite(options.Damping)||options.Damping<0||options.Damping>1||!Finite(options.Radius)||options.Radius<=0||!Finite(options.Range)||options.Range<=0||!Finite(options.StretchStrength)||options.StretchStrength<=0||options.StretchStrength>1||!Finite(options.BendStrength)||options.BendStrength<0||options.BendStrength>1)throw new InvalidDataException("Invalid physical parameters.");
            if(string.IsNullOrWhiteSpace(options.DisplayName)||source.Nodes.Any(n=>n.Name==options.DisplayName))throw new InvalidDataException("Choose a unique, nonempty display node name.");
            if(string.IsNullOrWhiteSpace(options.MaterialMtd)||!options.MaterialMtd.EndsWith("_al_Cloth.mtd",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("This BB prototype requires an explicit _al_Cloth.mtd material. Texture interfaces remain your responsibility.");
            var fixedSet=new HashSet<int>(options.FixedParticles??new int[0]);if(fixedSet.Count==0||fixedSet.Count==count||fixedSet.Any(i=>i<0||i>=count))throw new InvalidDataException("Select at least one fixed and one dynamic particle.");
            var original=source.Meshes[options.MeshIndex];if(!options.AssignSingleBone)
            {
                foreach(var v in original.Vertices)
                {
                    var active=Enumerable.Range(0,4).Where(k=>v.BoneWeights[k]>1e-6f).ToArray();
                    if(Enumerable.Range(0,4).Any(k=>!Finite(v.BoneWeights[k])||v.BoneWeights[k]<0)||active.Length!=1||v.BoneIndices[active[0]]!=options.SupportBoneIndex||Math.Abs(v.BoneWeights[active[0]]-1)>1e-5)throw new InvalidDataException("First version requires every display vertex to have the same single support-bone weight 1. Enable explicit single-bone assignment if needed.");
                }
            }
            var model=FLVER2.Read(source.Write());var mesh=model.Meshes[options.MeshIndex];
            if(mesh.MaterialIndex<0||mesh.MaterialIndex>=model.Materials.Count)throw new InvalidDataException("Mesh material index is invalid.");
            var mat=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<FLVER2.Material>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(model.Materials[mesh.MaterialIndex]));mat.Name=options.DisplayName+" Cloth";mat.MTD=options.MaterialMtd;mesh.MaterialIndex=model.Materials.Count;model.Materials.Add(mat);
            var node=new FLVER.Node{Name=options.DisplayName,ParentIndex=-1,FirstChildIndex=-1,NextSiblingIndex=-1,PreviousSiblingIndex=-1,Scale=Vector3.One};
            mesh.NodeIndex=model.Nodes.Count;model.Nodes.Add(node);mesh.UseBoneWeights=true;
            var layout0=new FLVER2.BufferLayout{new FLVER.LayoutMember(FLVER.LayoutType.Float3,FLVER.LayoutSemantic.Position,0),new FLVER.LayoutMember(FLVER.LayoutType.UByte4,FLVER.LayoutSemantic.Normal,0),new FLVER.LayoutMember(FLVER.LayoutType.UByte4,FLVER.LayoutSemantic.Tangent,0)};
            var layout1=new FLVER2.BufferLayout{new FLVER.LayoutMember(FLVER.LayoutType.UByte4,FLVER.LayoutSemantic.Bitangent,0),new FLVER.LayoutMember(FLVER.LayoutType.UByte4,FLVER.LayoutSemantic.Tangent,1),new FLVER.LayoutMember(FLVER.LayoutType.UByte4,FLVER.LayoutSemantic.BoneIndices,0),new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm,FLVER.LayoutSemantic.BoneWeights,0),new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm,FLVER.LayoutSemantic.VertexColor,1),new FLVER.LayoutMember(FLVER.LayoutType.Short4,FLVER.LayoutSemantic.UV,0)};
            int li=model.BufferLayouts.Count;model.BufferLayouts.Add(layout0);model.BufferLayouts.Add(layout1);mesh.VertexBuffers.Clear();mesh.VertexBuffers.Add(new FLVER2.VertexBuffer(li));mesh.VertexBuffers.Add(new FLVER2.VertexBuffer(li+1));mesh.BoneIndices.Clear();
            foreach(var v in mesh.Vertices){v.NormalW=0;v.Bitangent=new Vector4(1,-1,-1,-1);if(v.Tangents.Count==0)v.Tangents.Add(new Vector4(1,0,0,1));while(v.Tangents.Count<2)v.Tangents.Add(v.Tangents[0]);while(v.UVs.Count<2)v.UVs.Add(v.UVs.Count==0?Vector3.Zero:v.UVs[0]);if(v.Colors.Count==0)v.Colors.Add(new FLVER.VertexColor(255,255,255,255));if(options.AssignSingleBone)for(int k=0;k<4;k++){v.BoneIndices[k]=k==0?options.SupportBoneIndex:0;v.BoneWeights[k]=k==0?1:0;}}
            Program.UpdateImportedMeshBounds(model,mesh,false);
            var xml=CreateGraph(source,geometry,options,fixedSet);
            var cloth=ClothDocument.FromXml(xml,"Cloth Setup Wizard",false);if(cloth.Domains.Count!=1||cloth.Domains[0].Fixed.Count(v=>v)!=fixedSet.Count)throw new InvalidDataException("Generated domain/fixed-set validation failed.");
            byte[] hkx=HkxPackfileCodec.Pack(xml);byte[] flver=model.Write();var readback=FLVER2.Read(flver);
            if(readback.Meshes.Count!=source.Meshes.Count||readback.Meshes[options.MeshIndex].Vertices.Count!=geometry.DisplayVertices||readback.Nodes[readback.Meshes[options.MeshIndex].NodeIndex].Name!=options.DisplayName)throw new InvalidDataException("FLVER generated output readback mismatch.");
            byte[] clm=EmptyClm2(readback.Meshes.Count);
            string report="Bloodborne Cloth Setup Wizard — experimental single-bone / single-domain prototype\r\n"+"Mesh: "+options.MeshIndex+"; display: "+options.DisplayName+"\r\nDisplay vertices: "+geometry.DisplayVertices+"; particles: "+count+"; triangles: "+geometry.Triangles.Length/3+"\r\nFixed: "+fixedSet.Count+"; dynamic: "+(count-fixedSet.Count)+"; support bone: "+source.Nodes[options.SupportBoneIndex].Name+"\r\nBody collision: NONE\r\nCLM2: "+readback.Meshes.Count+" mesh records, empty entry lists (runtime/game certification required).\r\nHKX native pack/readback and FLVER/CLM2 library readbacks: PASS\r\nSelected mesh receives a new display node, Cloth material copy and a two-buffer output layout. Other meshes are retained.\r\nNo game launched. Original/in-memory FLVER was not modified.\r\n";
            return new ClothSetupResult{FlverBytes=flver,HkxBytes=hkx,Clm2Bytes=clm,Xml=xml,Report=report};
        }
        static byte[] EmptyClm2(int count)
        {
            byte[] scaffold;using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){w.Write(new byte[]{67,76,77,50});w.Write(0);w.Write((short)1);w.Write((short)1);w.Write(0);w.Write(0);w.Write(count);w.Write(40);w.Write(0);w.Write(0);w.Write(40);for(int i=0;i<count;i++)for(int j=0;j<4;j++)w.Write(0);scaffold=stream.ToArray();}
            var bytes=CLM2.Read(scaffold).Write();var reread=CLM2.Read(bytes);if(reread.Meshes.Count!=count||!bytes.SequenceEqual(reread.Write()))throw new InvalidDataException("Generated CLM2 readback failed.");return bytes;
        }
        static XDocument CreateGraph(FLVER2 source,ClothSetupMesh g,ClothSetupOptions o,HashSet<int> pins)
        {
            var x=new ClothSetupXml();int n=g.Positions.Length,padded=(n+15)/16*16;var chain=new List<int>();var visited=new HashSet<int>();int node=o.SupportBoneIndex;
            while(node>=0){if(node>=source.Nodes.Count||!visited.Add(node))throw new InvalidDataException("Invalid support-bone ancestry.");chain.Add(node);node=source.Nodes[node].ParentIndex;}chain.Reverse();int bone=chain.Count-1,boneCount=chain.Count;
            if(chain.Select(i=>source.Nodes[i].Name).Distinct().Count()!=boneCount)throw new InvalidDataException("Duplicate skeleton bone names.");
            var root=x.New("hkRootLevelContainer");var anim=x.New("hkaAnimationContainer");var skel=x.New("hkaSkeleton");var container=x.New("hclClothContainer");var owner=x.New("hclClothData");var transform=x.New("hclTransformSetDefinition");var sim=x.New("hclSimClothData");var pose=x.New("hclSimClothPose");
            var scratch=x.New("hclScratchBufferDefinition");var simulation=x.New("hclBufferDefinition");var display=x.New("hclBufferDefinition");var shadow=x.New("hclShadowBufferDefinition");var input=x.New("hclInputConvertOperator");var skin=x.New("hclObjectSpaceSkinPNOperator");var move=x.New("hclMoveParticlesOperator");var solve=x.New("hclSimulateOperator");var gather=x.New("hclGatherAllVerticesOperator");var output=x.New("hclOutputConvertOperator");var state=x.New("hclClothState");
            var standard=x.New("hclStandardLinkConstraintSet");var range=x.New("hclLocalRangeConstraintSet");var stretch=x.New("hclStretchLinkConstraintSet");var bend=x.New("hclStandardLinkConstraintSet");var transition=x.New("hclTransitionConstraintSet");
            Records(root,"namedVariants",new[]{Row("name","Animation Container","className","hkaAnimationContainer","variant",Ref(anim)),Row("name","Cloth Container","className","hclClothContainer","variant",Ref(container))});Array(anim,"skeletons",new object[]{Ref(skel)});Array(container,"clothDatas",new object[]{Ref(owner)});
            Set(skel,"name",source.Nodes[chain[0]].Name);Array(skel,"parentIndices",Enumerable.Range(0,boneCount).Select(i=>(object)(i==0?65535:i-1)));Records(skel,"bones",chain.Select(i=>Row("name",source.Nodes[i].Name,"lockTranslation","false")));
            var poses=new List<object>();Matrix4x4 world=Matrix4x4.Identity;
            foreach(int i in chain){var b=source.Nodes[i];var rotation=Matrix4x4.CreateRotationX(b.Rotation.X)*Matrix4x4.CreateRotationZ(b.Rotation.Z)*Matrix4x4.CreateRotationY(b.Rotation.Y);var q=Quaternion.CreateFromRotationMatrix(rotation);poses.Add(Vec(b.Translation,1)+"("+string.Join(" ",new[]{q.X,q.Y,q.Z,q.W}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)))+")"+Vec(b.Scale,0));world=Matrix4x4.CreateScale(b.Scale)*rotation*Matrix4x4.CreateTranslation(b.Translation)*world;}
            Matrix4x4 inverse;if(!Matrix4x4.Invert(world,out inverse))throw new InvalidDataException("Support bone rest transform is singular.");Array(skel,"referencePose",poses);
            Set(transform,"name",source.Nodes[chain[0]].Name);Set(transform,"type",1);Set(transform,"numTransforms",boneCount);
            Set(owner,"name",o.DisplayName);Array(owner,"simClothDatas",new object[]{Ref(sim)});Array(owner,"bufferDefinitions",new object[]{Ref(scratch),Ref(simulation),Ref(display),Ref(shadow)});Array(owner,"transformSetDefinitions",new object[]{Ref(transform)});Array(owner,"operators",new object[]{Ref(input),Ref(skin),Ref(move),Ref(solve),Ref(gather),Ref(output)});Array(owner,"clothStateDatas",new object[]{Ref(state)});Set(owner,"targetPlatform","HCL_PLATFORM_PS4");
            Set(sim,"name",o.DisplayName+" Simulation");var info=F(sim,"simulationInfo").Elements().Single();Set(info,"gravity",Vec(new Vector3(0,o.GravityY,0),0));Set(info,"globalDampingPerSecond",o.Damping);Set(info,"collisionTolerance",.003);Set(info,"subSteps",2);Set(sim,"totalMass",o.TotalMass);Set(sim,"maxParticleRadius",o.Radius);Set(sim,"doNormals","true");
            float mass=o.TotalMass/(n-pins.Count);if(!Finite(mass)||mass<=0||!Finite(1/mass))throw new InvalidDataException("Particle mass/inverse mass is outside finite float limits.");var inv=Enumerable.Range(0,n).Select(i=>pins.Contains(i)?0:1/mass).ToArray();Records(sim,"particleDatas",Enumerable.Range(0,n).Select(i=>Row("mass",pins.Contains(i)?0:mass,"invMass",inv[i],"radius",o.Radius,"friction",.35)));Array(sim,"fixedParticles",pins.OrderBy(i=>i).Cast<object>());Array(sim,"triangleIndices",g.Triangles.Cast<object>());Array(sim,"triangleFlips",Enumerable.Repeat((object)0,(g.Triangles.Length/3+7)/8));Array(sim,"staticCollisionMasks",Enumerable.Repeat((object)0,n));Array(sim,"perParticlePinchDetectionEnabledFlags",Enumerable.Repeat((object)"false",n));Array(sim,"staticConstraintSets",new object[]{Ref(standard),Ref(range),Ref(stretch),Ref(bend),Ref(transition)});Array(sim,"simClothPoses",new object[]{Ref(pose)});Set(pose,"name","Setup Rest Pose");Array(pose,"positions",g.Positions.Select(v=>(object)Vec(v,1)));
            var edgeOpposite=new Dictionary<Tuple<int,int>,List<int>>();for(int i=0;i<g.Triangles.Length;i+=3){int a=g.Triangles[i],b=g.Triangles[i+1],c=g.Triangles[i+2];foreach(var t in new[]{Tuple.Create(a,b,c),Tuple.Create(b,c,a),Tuple.Create(c,a,b)}){var key=Tuple.Create(Math.Min(t.Item1,t.Item2),Math.Max(t.Item1,t.Item2));if(!edgeOpposite.ContainsKey(key))edgeOpposite[key]=new List<int>();edgeOpposite[key].Add(t.Item3);}}
            Func<int,int,float,XElement> link=(a,b,strength)=>Row("particleA",a,"particleB",b,"restLength",Vector3.Distance(g.Positions[a],g.Positions[b]),"stiffness",strength/(inv[a]+inv[b]));Set(standard,"name","Setup Edge Links");Records(standard,"links",edgeOpposite.Keys.Where(e=>inv[e.Item1]+inv[e.Item2]>0).Select(e=>link(e.Item1,e.Item2,o.StretchStrength)));Set(bend,"name","Setup Cross-edge Bend Approximation");Records(bend,"links",edgeOpposite.Values.Where(v=>v.Count==2).Select(v=>Tuple.Create(Math.Min(v[0],v[1]),Math.Max(v[0],v[1]))).Distinct().Where(e=>inv[e.Item1]+inv[e.Item2]>0).Select(e=>link(e.Item1,e.Item2,o.BendStrength)));
            int[] dynamic=Enumerable.Range(0,n).Where(i=>!pins.Contains(i)).ToArray();Set(stretch,"name","Setup Anchor Stretch");Records(stretch,"links",dynamic.Select(i=>{int anchor=pins.OrderBy(a=>Vector3.DistanceSquared(g.Positions[a],g.Positions[i])).First();return Row("particleA",anchor,"particleB",i,"restLength",Vector3.Distance(g.Positions[anchor],g.Positions[i])*1.1f,"stiffness",1);}));
            Set(range,"name","Setup Local Range");Set(range,"stiffness",1);Set(range,"shapeType","SHAPE_SPHERE");Set(range,"applyNormalComponent","true");Records(range,"localConstraints",dynamic.Select(i=>Row("particleIndex",i,"referenceVertex",i,"maximumDistance",o.Range,"maxNormalDistance",o.Range,"minNormalDistance",-o.Range)));
            Set(transition,"name","Setup Transition");foreach(var f in new[]{"toAnimPeriod","toAnimPlusDelayPeriod","toSimPeriod","toSimPlusDelayPeriod"})Set(transition,f,1);Records(transition,"perParticleData",Enumerable.Range(0,n).Select(i=>Row("particleIndex",i,"referenceVertex",i,"toAnimDelay",0,"toSimDelay",0,"toSimMaxDistance",1)));
            Set(move,"name","Setup Fixed Move");Records(move,"vertexParticlePairs",pins.OrderBy(i=>i).Select(i=>Row("vertexIndex",i,"particleIndex",i)));Set(solve,"name","Setup Solve");Set(solve,"subSteps",2);Set(solve,"numberOfSolveIterations",4);Set(solve,"adaptConstraintStiffness","true");Array(solve,"constraintExecution",new object[]{0,3,2,1,-1,4});Set(gather,"name","Setup Exact Gather");Set(gather,"inputBufferIdx",1);Set(gather,"outputBufferIdx",2);Set(gather,"gatherNormals","true");Array(gather,"vertexInputFromVertexOutput",g.DisplayMap.Cast<object>());
            Set(skin,"name","Setup Single Bone Skin");Array(skin,"transformSubset",new object[]{bone});Array(skin,"boneFromSkinMeshTransforms",new object[]{Matrix(inverse)});var deformer=F(skin,"objectSpaceDeformer").Elements().Single();Set(deformer,"endVertexIndex",padded-1);Array(deformer,"controlBytes",Enumerable.Repeat((object)3,padded/16));
            var blocks=new List<XElement>();var localPNs=new List<XElement>();for(int start=0;start<padded;start+=16){var block=new XElement("hkobject");var local=new XElement("hkobject");for(int lane=0;lane<16;lane++){int i=Math.Min(start+lane,n-1);block.Add(new XElement("hkparam",new XAttribute("name","vertexIndices"+(lane+1)),start+lane));block.Add(new XElement("hkparam",new XAttribute("name","boneIndices"+(lane+1)),0));local.Add(new XElement("hkparam",new XAttribute("name","localPosition"+(lane+1)),Vec(g.Positions[i],1)));local.Add(new XElement("hkparam",new XAttribute("name","localNormal"+(lane+1)),Vec(g.Normals[i],0)));}blocks.Add(block);localPNs.Add(local);}Records(deformer,"oneBlendEntries",blocks);Records(skin,"localUnpackedPNs",localPNs);
            Buffer(x,scratch,"Setup Reference",6,padded,0,0);Set(scratch,"storeNormals","true");Buffer(x,simulation,"Setup Simulation",1,n,g.Triangles.Length/3,0);Buffer(x,display,o.DisplayName,4,g.DisplayVertices,g.Triangles.Length/3,1);Buffer(x,shadow,"Setup Shadow",7,g.DisplayVertices,0,2);Set(shadow,"subType",-1);Set(shadow,"shadowNormals","true");
            Convert(input,"Setup IN",false);Convert(output,"Setup OUT",true);
            Set(state,"name","Setup Default");Array(state,"operators",new object[]{0,1,2,3,4,5});Array(state,"usedSimCloths",new object[]{0});var usages=new List<XElement>();for(int i=0;i<4;i++){var row=Row("bufferIndex",i,"shadowBufferIndex",i>=2?3:i);var usage=x.Inline("hclBufferUsage");Set(usage,"perComponentFlags1",i==0?7:i==1?9:2);Set(usage,"perComponentFlags2",i==0?7:i==1?9:2);Set(usage,"trianglesRead",i==1?"true":"false");row.Add(new XElement("hkparam",new XAttribute("name","bufferUsage"),usage));usages.Add(row);}Records(state,"usedBuffers",usages);
            var trow=Row("transformSetIndex",0);var tu=x.Inline("hclTransformSetUsage");Set(tu,"perComponentFlags1",9);var trackers=new List<XElement>();for(int i=0;i<2;i++){var tracker=new XElement("hkobject");foreach(string name in new[]{"read","readBeforeWrite","written"}){var bits=x.Inline("hkBitField");var storage=F(bits,"storage").Elements().Single();var words=new object[(boneCount+31)/32];for(int j=0;j<words.Length;j++)words[j]=0;if(name!="written")words[bone/32]=unchecked((int)(1u<<(bone%32)));Array(storage,"words",words);Set(storage,"numBits",boneCount);tracker.Add(new XElement("hkparam",new XAttribute("name",name),bits));}trackers.Add(tracker);}Records(tu,"perComponentTransformTrackers",trackers);trow.Add(new XElement("hkparam",new XAttribute("name","transformSetUsage"),tu));Records(state,"usedTransformSets",new[]{trow});return x.Document;
        }
        static void Buffer(ClothSetupXml x,XElement o,string name,int type,int vertices,int triangles,int mode)
        {
            Set(o,"name",name);Set(o,"type",type);Set(o,"numVertices",vertices);Set(o,"numTriangles",triangles);var layout=F(o,"bufferLayout").Elements().Single();
            for(int i=1;i<=4;i++){var e=F(layout,"elementsLayout"+i).Elements().Single();Set(e,"vectorConversion",mode==0?(i<=2?0:250):mode==1?(i==1?1:i<=3?2:250):(i==2?0:250));Set(e,"vectorSize",mode==0?(i<=2?16:0):mode==1?(i==1?12:i<=3?4:0):(i==2?16:0));Set(e,"slotId",mode==0&&i==2?1:0);Set(e,"slotStart",mode==1&&i==2?12:mode==1&&i==3?16:0);var slot=F(layout,"slots"+i).Elements().Single();Set(slot,"flags",mode==0&&i<=2||mode==2&&i==1?1:0);Set(slot,"stride",mode==0&&i<=2||mode==2&&i==1?16:mode==1&&i==1?20:0);}Set(layout,"numSlots",mode==0?2:mode==1?0:1);Set(layout,"triangleFormat","TF_OTHER");
        }
        static void Convert(XElement o,string name,bool output)
        {
            Set(o,"name",name);Set(o,"userBufferIndex",2);Set(o,"shadowBufferIndex",3);var info=F(o,"conversionInfo").Elements().Single();var slot=F(info,"slotConversions1").Elements().Single();Set(slot,"numElements",1);Set(slot,"partialWrite",output?"true":"false");for(int i=1;i<=4;i++){var e=F(info,"elementConversions"+i).Elements().Single();Set(e,"index",i==1?1:0);Set(e,"offset",i==1?12:0);Set(e,"conversion",i==1?2:250);}Set(info,"numSlotsConverted",1);Set(info,"numElementsConverted",1);
        }
    }
}
