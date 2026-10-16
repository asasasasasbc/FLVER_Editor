// GPL-3.0. Scene adaptation informed by DSAnimStudio NativeClothAsset and
// SoulsAssetPipeline HKX2 (d11caf989c917c7a43e2c7559915b1c5af218153).
// Compiled separately with private augmented scene schemas, never editor schemas.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using SoulsFormats;

namespace MySFformat.Cloth.Preview
{
    internal static class PreviewAssetBuilder
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static Dictionary<string, XElement> objects, schema;
        static XElement section;
        static int next;
        static CLM2 seams;
        static PreviewInitialization[] InitializationCopies(XElement cloth)
        {
            var ops=Refs(cloth,"operators");var state=Refs(cloth,"clothStateDatas")[0];var sequence=T(state,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s,C)).ToArray();
            var buffers=Refs(cloth,"bufferDefinitions");var sims=Refs(cloth,"simClothDatas");var result=new List<PreviewInitialization>();
            foreach(int id in sequence.Where(i=>(string)ops[i].Attribute("class")=="hclSimulateOperator"))
            {
                int sim=I(ops[id],"simClothIndex"),count=int.Parse((string)F(sims[sim],"particleDatas").Attribute("numelements"),C);
                var preceding=sequence.TakeWhile(i=>i!=id).Select(i=>ops[i]).ToArray();var move=preceding.LastOrDefault(o=>(string)o.Attribute("class")=="hclMoveParticlesOperator"&&I(o,"simClothIndex")==sim);
                if(move==null)throw new InvalidDataException("No initialization particle mapping");int input=I(move,"refBufferIdx");var pairs=F(move,"vertexParticlePairs").Elements("hkobject").ToArray();
                if(input<0||input>=buffers.Length||pairs.Length==0||pairs.Any(p=>I(p,"particleIndex")<0||I(p,"particleIndex")>=count||I(p,"vertexIndex")<0||I(p,"vertexIndex")>=I(buffers[input],"numVertices")))throw new InvalidDataException("Invalid initialization particle mapping");
                bool referencePose=pairs.Any(p=>I(p,"vertexIndex")!=I(p,"particleIndex"))||I(buffers[input],"numVertices")!=count;
                var output=Enumerable.Range(0,buffers.Length).Where(i=>I(buffers[i],"type")==1&&I(buffers[i],"subType")==sim&&I(buffers[i],"numVertices")==count).ToArray();
                bool skinned=preceding.Any(o=>((string)o.Attribute("class")).Contains("SpaceSkin")&&I(o,"outputBufferIndex")==input);
                bool deformed=preceding.Any(o=>((string)o.Attribute("class")).Contains("MeshMeshDeform")&&I(o,"outputBufferIdx")==input);
                if(output.Length!=1||(!skinned&&!deformed))throw new InvalidDataException("No unique supported initialization buffer");
                referencePose|=!skinned;
                // 猎人套腰带等使用重排/精简 reference mesh，不能误当成连续粒子数组。
                // 此时只在初始化阶段使用文件自己的 simClothPose，随后仍由原始 move/solver 求解。
                result.Add(new PreviewInitialization{Operator=id,Simulation=sim,Input=input,Output=output[0],Count=count,UseReferencePose=referencePose});
            }
            if(result.Count!=sims.Length||result.Select(i=>i.Simulation).Distinct().Count()!=sims.Length)throw new InvalidDataException("Initialization does not cover all simulations");return result.ToArray();
        }
        static HashSet<int> Written(XElement cloth,int buffer,int count)
        {
            var ops=Refs(cloth,"operators");var active=T(Refs(cloth,"clothStateDatas")[0],"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Select(s=>ops[int.Parse(s,C)]);var result=new HashSet<int>();
            foreach(var op in active)
            {
                var output=op.Elements("hkparam").FirstOrDefault(p=>(string)p.Attribute("name")=="outputBufferIdx"||(string)p.Attribute("name")=="outputBufferIndex");
                if(output==null||int.Parse(output.Value,C)!=buffer)continue;string type=(string)op.Attribute("class");
                if(type=="hclCopyVerticesOperator")foreach(int i in Enumerable.Range(I(op,"startVertexOut"),I(op,"numberOfVertices")))result.Add(i);
                else if(type.Contains("MeshMeshDeform")||type.Contains("SpaceSkin"))foreach(var p in op.Descendants("hkparam").Where(p=>Regex.IsMatch((string)p.Attribute("name"),@"^vertexIndices\d*$")))foreach(var word in p.Value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))result.Add(int.Parse(word,C));
            }
            result.RemoveWhere(i=>i<0||i>=count);return result;
        }
        static XElement F(XElement o, string n) { return o.Elements("hkparam").Single(p => (string)p.Attribute("name") == n); }
        static string T(XElement o, string n) { return F(o,n).Value.Trim(); }
        static int I(XElement o, string n) { return int.Parse(T(o,n),C); }
        static XElement[] Refs(XElement o,string n) { return T(o,n).Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Select(id=>objects[id]).ToArray(); }
        static float[] Floats(string text) { return Regex.Split(text.Replace("("," ").Replace(")"," ").Trim(),@"\s+").Select(s=>float.Parse(s,C)).ToArray(); }
        static Matrix4x4 Matrix(float[] f) { return new Matrix4x4(f[0],f[1],f[2],f[3],f[4],f[5],f[6],f[7],f[8],f[9],f[10],f[11],f[12],f[13],f[14],f[15]); }
        static float[] Flat(Matrix4x4 m) { return new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44}; }
        static string Number(float f) { return f.ToString("R",C); }
        static string MatText(Matrix4x4 m) { var f=Flat(m);return string.Concat(Enumerable.Range(0,4).Select(i=>"("+string.Join(" ",f.Skip(i*4).Take(4).Select(Number))+")")); }
        static IEnumerable<XElement> Members(string cls)
        {
            var c=schema[cls];var p=(string)c.Attribute("parent");
            if(!string.IsNullOrEmpty(p))foreach(var m in Members(p))yield return m;
            foreach(var m in c.Element("members").Elements("member"))yield return m;
        }
        static XElement Template(string cls,string id)
        {
            var o=new XElement("hkobject",new XAttribute("class",cls),new XAttribute("name",id),new XAttribute("signature",(string)schema[cls].Attribute("signature")));
            foreach(var m in Members(cls))
            {
                if(((string)m.Attribute("flags")??"").Contains("SERIALIZE_IGNORED"))continue;
                var p=new XElement("hkparam",new XAttribute("name",(string)m.Attribute("name")));string type=(string)m.Attribute("vtype");
                if(type=="TYPE_ARRAY")p.SetAttributeValue("numelements",0);
                else if(type=="TYPE_POINTER")p.Value="null";
                else if(type=="TYPE_STRUCT")p.Add(Template((string)m.Attribute("ctype"),(string)m.Attribute("name")));
                else if(type=="TYPE_BOOL")p.Value="false";
                else if(type=="TYPE_MATRIX4")p.Value=MatText(Matrix4x4.Identity);
                else if(type=="TYPE_MATRIX3")p.Value="(1 0 0 0)(0 1 0 0)(0 0 1 0)";
                else if(type=="TYPE_VECTOR4")p.Value="(0 0 0 0)";
                else if(type!="TYPE_STRINGPTR"&&type!="TYPE_CSTRING")p.Value="0";
                o.Add(p);
            }
            return o;
        }
        static XElement Add(string cls) { var o=Template(cls,"#"+(++next));section.Add(o);return o; }
        static string Id(XElement o) { return (string)o.Attribute("name"); }
        static void Set(XElement o,string n,object v) { F(o,n).Value=Convert.ToString(v,C); }
        static void Array(XElement o,string n,IEnumerable<string> rows) { var a=rows.ToArray();Set(o,n,string.Join(" ",a));F(o,n).SetAttributeValue("numelements",a.Length); }
        static void Reference(XElement o,string n,XElement target) { Set(o,n,Id(target)); }
        static PreviewMesh DescribeMesh(XElement cloth,XElement b,int bi,FLVER2 flver)
        {
            string name=T(b,"name");int count=I(b,"numVertices"),tri=I(b,"numTriangles"),sub=I(b,"subType");
            var named=Enumerable.Range(0,flver.Meshes.Count).Where(i=>flver.Meshes[i].NodeIndex>=0&&flver.Nodes[flver.Meshes[i].NodeIndex].Name==name).ToArray();
            var matches=named.Where(i=>flver.Meshes[i].Vertices.Count==count&&(tri==0||flver.Meshes[i].FaceSets[0].Triangulate(true).Count/3==tri)).ToArray();
            if(matches.Length>1&&sub>=0&&sub<named.Length&&matches.Contains(named[sub]))matches=new[]{named[sub]};
            if(matches.Length!=1)throw new InvalidDataException("No unique mesh topology: "+name+" section "+sub+" count "+count);
            var operators=Refs(cloth,"operators");var state=Refs(cloth,"clothStateDatas")[0];var active=T(state,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Select(s=>operators[int.Parse(s,C)]);
            var converters=active.Where(o=>(string)o.Attribute("class")=="hclOutputConvertOperator"&&I(o,"userBufferIndex")==bi).ToArray();
            if(converters.Length>1)throw new InvalidDataException("Multiple output converters");
            var layout=F(b,"bufferLayout").Element("hkobject");
            var elements=Enumerable.Range(1,4).Select(i=>F(layout,"elementsLayout"+i).Element("hkobject")).ToArray();
            var slots=Enumerable.Range(1,4).Select(i=>F(layout,"slots"+i).Element("hkobject")).ToArray();
            var mesh=flver.Meshes[matches[0]];
            int read=converters.Length==0?bi:I(converters[0],"shadowBufferIndex");var written=Written(cloth,bi,count);written.UnionWith(Written(cloth,read,count));
            var aliases=seams==null?null:seams.Meshes[matches[0]].Where(e=>written.Contains(e.Unk00)&&!written.Contains(e.Unk02)&&e.Unk02>=0&&e.Unk02<count&&Vector3.Distance(mesh.Vertices[e.Unk00].Position,mesh.Vertices[e.Unk02].Position)<1e-6f).ToArray();
            return new PreviewMesh {BufferIndex=bi,MeshIndex=matches[0],VertexCount=count,SceneSection=sub,ReadBufferIndex=converters.Length==0?bi:I(converters[0],"shadowBufferIndex"),
                AliasSources=aliases==null?new int[0]:aliases.Select(e=>(int)e.Unk00).ToArray(),AliasTargets=aliases==null?new int[0]:aliases.Select(e=>(int)e.Unk02).ToArray(),
                Conversions=elements.Select(e=>I(e,"vectorConversion")).ToArray(),Slots=elements.Select(e=>I(e,"slotId")).ToArray(),Starts=elements.Select(e=>I(e,"slotStart")).ToArray(),Strides=slots.Select(s=>I(s,"stride")).ToArray(),
                Channels=Enumerable.Range(0,4).Select(channel=>mesh.Vertices.SelectMany(v=>{var t=v.Tangents.FirstOrDefault();var vec=channel==0?new Vector4(v.Position,1):channel==1?new Vector4(v.Normal,0):channel==2?t:new Vector4(Vector3.Cross(v.Normal,new Vector3(t.X,t.Y,t.Z))*t.W,0);return new[]{vec.X,vec.Y,vec.Z,vec.W};}).ToArray()).ToArray()};
        }
        static void Geometry(XElement node,IEnumerable<int> indices,FLVER2 flver)
        {
            var mesh=Add("hkxMesh");Reference(node,"object",mesh);Set(node,"bone","false");var sections=new List<string>();
            foreach(int index in indices)
            {
                var fm=flver.Meshes[index];var sec=Add("hkxMeshSection");sections.Add(Id(sec));var vb=Add("hkxVertexBuffer");Reference(sec,"vertexBuffer",vb);
                var data=F(vb,"data").Element("hkobject");var vectors=new List<string>();var uv=new List<string>();
                Func<float,string> Bits=f=>BitConverter.ToInt32(BitConverter.GetBytes(f),0).ToString(C);
                foreach(var v in fm.Vertices)
                {
                    var t=v.Tangents.FirstOrDefault();var bin=Vector3.Cross(v.Normal,new Vector3(t.X,t.Y,t.Z))*t.W;
                    vectors.AddRange(new[]{v.Position.X,v.Position.Y,v.Position.Z,1,v.Normal.X,v.Normal.Y,v.Normal.Z,0,t.X,t.Y,t.Z,0,bin.X,bin.Y,bin.Z,0}.Select(Bits));
                    var tex=v.UVs.FirstOrDefault();uv.Add(Bits(tex.X));uv.Add(Bits(tex.Y));
                }
                Array(data,"vectorData",vectors);Array(data,"floatData",uv);Set(data,"numVerts",fm.Vertices.Count);Set(data,"vectorStride",64);Set(data,"floatStride",8);
                var desc=F(vb,"desc").Element("hkobject");var decl=F(desc,"decls");decl.SetAttributeValue("numelements",5);
                for(int i=0;i<5;i++){var el=Template("hkxVertexDescriptionElementDecl","unused");el.Attributes().Remove();Set(el,"byteOffset",i==4?0:i*16);Set(el,"type",4);Set(el,"usage",new[]{1,4,8,16,32}[i]);Set(el,"byteStride",i==4?8:64);Set(el,"numElements",i==4?2:3);decl.Add(el);}
                var ib=Add("hkxIndexBuffer");Set(ib,"indexType",1);var triangles=fm.FaceSets[0].Triangulate(true);Array(ib,"indices32",triangles.Select(i=>i.ToString(C)));Set(ib,"length",triangles.Count);Array(sec,"indexBuffers",new[]{Id(ib)});
                // A null material is valid for a geometry-only input scene.
            }
            Array(mesh,"sections",sections);
        }
        static int Main(string[] args)
        {
            try
            {
                if(args.Length<4||args.Length>6)throw new ArgumentException("Usage: builder <cloth.hkx> <model.flver> <merged-schema.xml> <new-output-directory> [clm2] [preview-environment.json]");
                string output=Path.GetFullPath(args[3]);if(Directory.Exists(output))throw new IOException("Refusing existing asset directory");
                var xml=Path.GetExtension(args[0]).Equals(".xml",StringComparison.OrdinalIgnoreCase)?HkxPackfileCodec.LoadXml(args[0]):HkxPackfileCodec.Read(args[0]);
                var clothBytes=Path.GetExtension(args[0]).Equals(".xml",StringComparison.OrdinalIgnoreCase)?HkxPackfileCodec.Pack(xml):File.ReadAllBytes(args[0]);objects=xml.Root.Element("hksection").Elements("hkobject").ToDictionary(o=>Id(o));
                var flver=FLVER2.Read(args[1]);schema=XDocument.Load(args[2]).Root.Elements("class").ToDictionary(c=>(string)c.Attribute("name"));
                if(args.Length>=5&&!string.IsNullOrWhiteSpace(args[4])){seams=CLM2.Read(args[4]);if(seams.Meshes.Count!=flver.Meshes.Count)throw new InvalidDataException("CLM2 mesh count mismatch");}
                var environment=args.Length==6?new JavaScriptSerializer().Deserialize<PreviewEnvironment>(File.ReadAllText(args[5])):new PreviewEnvironment();environment.Validate();
                var skeletons=objects.Values.Where(o=>(string)o.Attribute("class")=="hkaSkeleton").Select(o=>{
                    var names=F(o,"bones").Elements("hkobject").Select(b=>T(b,"name")).ToArray();var parents=T(o,"parentIndices").Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s,C)).ToArray();
                    var poses=Regex.Matches(T(o,"referencePose"),@"(?:\([^()]*\)\s*){3}").Cast<Match>().Select(m=>Floats(m.Value)).Select(f=>Matrix4x4.CreateScale(f[8],f[9],f[10])*Matrix4x4.CreateFromQuaternion(new Quaternion(f[4],f[5],f[6],f[7]))*Matrix4x4.CreateTranslation(f[0],f[1],f[2])).ToArray();
                    if(names.Length!=poses.Length||parents.Length!=names.Length)throw new InvalidDataException("Skeleton arrays");for(int i=0;i<poses.Length;i++)if(parents[i]!=65535&&parents[i]!=-1){if(parents[i]<0||parents[i]>=i)throw new InvalidDataException("Parent order");poses[i]*=poses[parents[i]];}
                    return new PreviewSkeleton{Name=T(o,"name"),Bones=names,Matrices=poses.SelectMany(Flat).ToArray()};}).ToArray();
                var cloths=objects.Values.Where(o=>(string)o.Attribute("class")=="hclClothData").Select(o=>{
                    var states=Refs(o,"clothStateDatas");string name=T(states[0],"name");Func<string,int> State=prefix=>{var matches=Enumerable.Range(0,states.Length).Where(i=>T(states[i],"name")==prefix+name).ToArray();if(matches.Length>1)throw new InvalidDataException("Ambiguous initialization state");return matches.Length==0?-1:matches[0];};
                    int previous=State("_preAtoS_"),current=State("_curAtoS_");if((previous<0)!=(current<0))throw new InvalidDataException("Partial initialization states");
                    var buffers=Refs(o,"bufferDefinitions");return new PreviewCloth{Name=T(o,"name"),PreviousState=previous,CurrentState=current,Initializations=previous<0?InitializationCopies(o):new PreviewInitialization[0],
                        TransformSkeletons=Refs(o,"transformSetDefinitions").Select(t=>{var matches=Enumerable.Range(0,skeletons.Length).Where(i=>skeletons[i].Name==T(t,"name")&&skeletons[i].Bones.Length==I(t,"numTransforms")).ToArray();if(matches.Length!=1)throw new InvalidDataException("Transform skeleton mapping");return matches[0];}).ToArray(),
                        Meshes=buffers.Select((b,i)=>new{b,i}).Where(x=>I(x.b,"type")==4).Select(x=>DescribeMesh(o,x.b,x.i,flver)).ToArray(),ParticleCounts=Refs(o,"simClothDatas").Select(s=>int.Parse((string)F(s,"particleDatas").Attribute("numelements"),C)).ToArray(),SimulationNames=Refs(o,"simClothDatas").Select(s=>T(s,"name")).ToArray(),ReferenceParticles=Refs(o,"simClothDatas").Select(s=>Floats(T(Refs(s,"simClothPoses")[0],"positions"))).ToArray()};}).ToArray();
                if(skeletons.Length==0||cloths.Length==0)throw new InvalidDataException("No cloth/skeleton");
                section=new XElement("hksection",new XAttribute("name","__data__"));next=0;var container=Add("hkRootLevelContainer");var scene=Add("hkxScene");var root=Add("hkxNode");Set(root,"name","FLVER Preview");Reference(scene,"rootNode",root);Set(scene,"modeller","FLVER isolated preview");Set(scene,"asset","Geometry adapter");Set(scene,"numFrames",1);Set(scene,"sceneLength",1f/60);
                var variants=F(container,"namedVariants");variants.SetAttributeValue("numelements",1);variants.Add(new XElement("hkobject",new XElement("hkparam",new XAttribute("name","name"),"Scene Data"),new XElement("hkparam",new XAttribute("name","className"),"hkxScene"),new XElement("hkparam",new XAttribute("name","variant"),Id(scene))));
                var nodes=flver.Nodes.Select(n=>{var node=Add("hkxNode");Set(node,"name",n.Name);Set(node,"bone","true");Array(node,"keyFrames",new[]{MatText(Matrix4x4.CreateScale(n.Scale)*Matrix4x4.CreateRotationX(n.Rotation.X)*Matrix4x4.CreateRotationZ(n.Rotation.Z)*Matrix4x4.CreateRotationY(n.Rotation.Y)*Matrix4x4.CreateTranslation(n.Translation))});return node;}).ToArray();
                Array(root,"keyFrames",new[]{MatText(Matrix4x4.Identity)});Array(root,"children",Enumerable.Range(0,nodes.Length).Where(i=>flver.Nodes[i].ParentIndex<0).Select(i=>Id(nodes[i])));
                for(int i=0;i<nodes.Length;i++)Array(nodes[i],"children",Enumerable.Range(0,nodes.Length).Where(j=>flver.Nodes[j].ParentIndex==i).Select(j=>Id(nodes[j])));
                foreach(var group in cloths.SelectMany(c=>c.Meshes).GroupBy(m=>flver.Meshes[m.MeshIndex].NodeIndex))
                {
                    var nodeMeshes=Enumerable.Range(0,flver.Meshes.Count).Where(i=>flver.Meshes[i].NodeIndex==group.Key).ToArray();var sections=new List<int>();
                    for(int s=0;s<=group.Max(m=>m.SceneSection);s++){var mapped=group.Where(m=>m.SceneSection==s).Select(m=>m.MeshIndex).Distinct().ToArray();if(mapped.Length>1)throw new InvalidDataException("Conflicting native sections");int mesh=mapped.Length==1?mapped[0]:s<nodeMeshes.Length?nodeMeshes[s]:-1;if(mesh<0)throw new InvalidDataException("Missing intermediate section");sections.Add(mesh);}
                    Geometry(nodes[group.Key],sections,flver);
                }
                var sceneXml=new XDocument(new XElement("hkpackfile",new XAttribute("classversion",11),new XAttribute("contentsversion","hk_2014.1.0-r1"),new XAttribute("nativeLayout","8,1,0,1"),section));
                var sceneBytes=HkxPackfileCodec.Pack(sceneXml);string originalLayout=string.Join(",",clothBytes.Skip(16).Take(4));bool repacked=originalLayout!="8,1,0,1"||environment.OverrideGravity;
                var runtimeBytes=clothBytes;
                if(repacked){var normalized=new XDocument(xml);normalized.Root.SetAttributeValue("nativeLayout","8,1,0,1");if(environment.OverrideGravity)foreach(var sim in normalized.Root.Element("hksection").Elements("hkobject").Where(o=>(string)o.Attribute("class")=="hclSimClothData"))Set(F(sim,"simulationInfo").Element("hkobject"),"gravity","("+Number(environment.GravityX)+" "+Number(environment.GravityY)+" "+Number(environment.GravityZ)+" 0)");runtimeBytes=HkxPackfileCodec.Pack(normalized);}
                Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"scene.hkx"),sceneBytes);File.WriteAllBytes(Path.Combine(output,"cloth-original.hkx"),clothBytes);File.WriteAllBytes(Path.Combine(output,"cloth.hkx"),runtimeBytes);sceneXml.Save(Path.Combine(output,"scene.xml"));
                File.WriteAllText(Path.Combine(output,"description.json"),new JavaScriptSerializer{MaxJsonLength=128*1024*1024}.Serialize(new PreviewDescription{Skeletons=skeletons,Cloths=cloths,Clm2Loaded=seams!=null,Clm2Meshes=seams==null?0:seams.Meshes.Count,Clm2Entries=seams==null?0:seams.Meshes.Sum(m=>m.Count),RuntimeRepacked=repacked,OriginalLayout=originalLayout,RuntimeLayout="8,1,0,1",Environment=environment}));
                Console.WriteLine("PASS scene adapter: "+cloths.Length+" cloth instances / "+cloths.Sum(c=>c.Meshes.Length)+" display buffers");return 0;
            }
            catch(Exception e){Console.Error.WriteLine(e);return 1;}
        }
    }
}
