using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SoulsFormats;

namespace MySFformat
{
    // 明确区分“需要手动选来源”与 HKX 损坏/解码失败；底层不创建任何 UI。
    public sealed class ClothBinderSelectionException : IOException
    {
        public int ClothFileCount { get; private set; }

        public ClothBinderSelectionException(string source, string[] files)
            : base("This Binder contains " + files.Length + " _c.hkx file(s).\r\n" +
                "Cloth requires exactly one unambiguous source. No HKX or CLM2 was automatically loaded.\r\n" +
                "Unpack the Binder, then use Load Cloth and Load CLM2 to select matching files manually.\r\n\r\n" +
                "此 Binder 包含 " + files.Length + " 个 _c.hkx，无法唯一确定布料来源。\r\n" +
                "未自动加载任何 HKX 或 CLM2。请解包后用 Load Cloth 和 Load CLM2 手动选择配套文件。\r\n\r\n" +
                source + (files.Length == 0 ? "" : "\r\n" + string.Join("\r\n", files.Take(8)) +
                    (files.Length > 8 ? "\r\n…" : "")))
        {
            ClothFileCount = files.Length;
        }
    }

    // Serialized rest data only. This is deliberately not a replacement Havok solver.
    public sealed class ClothDomain
    {
        public string Name;
        public XElement Sim, Owner;
        public Vector3[] Positions;
        public bool[] Fixed;
        public int[] Triangles;
        public float[] Ranges;
        public List<XElement> Constraints;
        public List<ClothCollider> Colliders = new List<ClothCollider>();
        public override string ToString() { return Name; }
    }
    public sealed class ClothCollider
    {
        public string Bone, Shape;
        public Vector3 Start, End;
        public float RadiusA, RadiusB;
    }
    public sealed class ClothDocument
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public XDocument Xml { get; private set; }
        public bool Edited { get; private set; }
        public bool IsTagfile {get{return HkxTagfileReader.IsTagfileDocument(Xml);}}
        public bool IsSekiroTagfile {get{return (string)Xml.Root.Attribute("sourceFormat")=="TAG0-20160200";}}
        public string TagfileBackend {get{return HkxTagfileReader.CompatibilityBackend(Xml);}}
        public string NativeLayout {get{return IsTagfile?(string)Xml.Root.Attribute("sourceFormat"):(string)Xml.Root.Attribute("nativeLayout")??"8,1,1,1";}}
        public void SetNativeLayout(string layout)
        {
            if(IsTagfile)throw new NotSupportedException("Sekiro/ER TAG0 is read-only; changing a packfile header does not convert it.");
            if(layout!="8,1,1,1"&&layout!="8,1,0,1")throw new ArgumentException("Unsupported native header layout.");
            Xml.Root.SetAttributeValue("nativeLayout",layout);Edited=true;
        }
        public string Source { get; private set; }
        public Dictionary<string, XElement> Objects { get; private set; }
        public List<ClothDomain> Domains { get; private set; }
        public static XElement Field(XElement o, string name) { return o.Elements("hkparam").First(p => (string)p.Attribute("name") == name); }
        public static string Value(XElement o, string name) { return Field(o, name).Value.Trim(); }
        public static XElement SolverConfig(XElement op)
        {
            var configs=op.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")=="simulateOpConfigs");
            if(configs==null)return op;
            var first=configs.Elements("hkobject").FirstOrDefault();if(first==null)throw new InvalidDataException("Simulation operator has no configuration.");return first;
        }
        public static double[] Numbers(string s) { return Regex.Matches(s, @"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?").Cast<Match>().Select(m => double.Parse(m.Value, Invariant)).ToArray(); }
        static string[] Refs(XElement o, string name) { return Value(o, name).Split((char[])null, StringSplitOptions.RemoveEmptyEntries); }
        static Vector3 Vector(double[] values, int i) { return new Vector3((float)values[i], (float)values[i + 1], (float)values[i + 2]); }
        static Matrix4x4 Matrix(double[] v, int i)
        { return new Matrix4x4((float)v[i], (float)v[i+1], (float)v[i+2], (float)v[i+3], (float)v[i+4], (float)v[i+5], (float)v[i+6], (float)v[i+7], (float)v[i+8], (float)v[i+9], (float)v[i+10], (float)v[i+11], (float)v[i+12], (float)v[i+13], (float)v[i+14], (float)v[i+15]); }

        public static ClothDocument Load(string path)
        {
            string source = Path.GetFullPath(path);
            if (Path.GetExtension(source).Equals(".xml", StringComparison.OrdinalIgnoreCase)) return Parse(source, source);
            byte[] bytes;
            if (BND4.Is(source))
            {
                var files = BND4.Read(source).Files.Where(f => f.Name.EndsWith("_c.hkx", StringComparison.OrdinalIgnoreCase)).ToArray();
                // 这是需要用户明确选择的情况，不是可自动猜测的第一份 Cloth。
                // 在解码或读取 CLM2 前退出，让界面显示说明并保持原有数据。
                if (files.Length != 1) throw new ClothBinderSelectionException(source, files.Select(f => f.Name).ToArray());
                bytes=files[0].Bytes;
            }
            else bytes=File.ReadAllBytes(source);
            return FromXml(HkxPackfileCodec.Read(bytes),source,false);
        }
        public static ClothDocument FromXml(XDocument xml,string source,bool edited=true)
        {
            var result=new ClothDocument {Xml=new XDocument(xml),Source=source,Edited=edited};
            result.ReadDomains();return result;
        }
        static ClothDocument Parse(string xml, string source)
        {
            using (var reader = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 100000000 }))
            {
                return FromXml(XDocument.Load(reader),source,false);
            }
        }
        void ReadDomains()
        {
            Objects = Xml.Descendants("hksection").Elements("hkobject").ToDictionary(o => (string)o.Attribute("name"));
            Domains = new List<ClothDomain>();
            var skeleton = Objects.Values.First(o => (string)o.Attribute("class") == "hkaSkeleton");
            var bones = Field(skeleton, "bones").Elements().Select(o => Value(o, "name")).ToArray();
            var parent = Numbers(Value(skeleton, "parentIndices")).Select(i => i >= 32768 ? (int)i - 65536 : (int)i).ToArray();
            var pose = Numbers(Value(skeleton, "referencePose"));
            if (pose.Length != bones.Length * 12 || parent.Length != bones.Length) throw new InvalidDataException("Skeleton array dimensions do not match.");
            var local = new Matrix4x4[bones.Length]; var world = new Matrix4x4[bones.Length]; var marks = new byte[bones.Length];
            for (int i=0;i<bones.Length;i++)
            {
                int j=i*12;
                var q = new Quaternion((float)pose[j+4], (float)pose[j+5], (float)pose[j+6], (float)pose[j+7]);
                local[i] = Matrix4x4.CreateScale(Vector(pose,j+8)) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(Vector(pose,j));
            }
            Func<int, Matrix4x4> resolve = null;
            resolve = i => {
                if (i < 0 || i >= bones.Length || marks[i] == 1) throw new InvalidDataException("Skeleton parent index is invalid or cyclic.");
                if (marks[i] == 2) return world[i]; marks[i]=1;
                world[i]=parent[i]<0 ? local[i] : local[i]*resolve(parent[i]); marks[i]=2; return world[i];
            };
            for (int i=0;i<bones.Length;i++) resolve(i);
            foreach (var owner in Objects.Values.Where(o => (string)o.Attribute("class") == "hclClothData"))
            foreach (var simRef in Refs(owner,"simClothDatas"))
            {
                var sim=Objects[simRef]; var poses=Refs(sim,"simClothPoses");
                if (poses.Length==0) throw new InvalidDataException("Simulation domain has no authored rest pose.");
                var coordinates=Numbers(Value(Objects[poses[0]],"positions"));
                var particles=Field(sim,"particleDatas").Elements().ToArray();
                if (coordinates.Length!=particles.Length*4) throw new InvalidDataException("Particle/rest pose dimensions do not match.");
                var d=new ClothDomain {Name=Value(owner,"name")+" / "+Value(sim,"name"),Sim=sim,Owner=owner,
                    Positions=Enumerable.Range(0,particles.Length).Select(i=>Vector(coordinates,i*4)).ToArray(),
                    Fixed=particles.Select(p=>double.Parse(Value(p,"invMass"),Invariant)==0).ToArray(),
                    Triangles=Numbers(Value(sim,"triangleIndices")).Select(i=>(int)i).ToArray(),
                    Ranges=new float[particles.Length],Constraints=Refs(sim,"staticConstraintSets").Select(r=>Objects[r]).ToList()};
                if (d.Triangles.Length%3!=0 || d.Triangles.Any(i=>i<0 || i>=particles.Length)) throw new InvalidDataException("Triangle particle indices are invalid.");
                if(Numbers(Value(sim,"staticCollisionMasks")).Length!=particles.Length)throw new InvalidDataException("Particle collision-mask count mismatch.");
                foreach(var c in d.Constraints)
                    foreach(var p in c.Descendants("hkparam").Where(p=>new[]{"particleA","particleB","particleC","particleD","particleIndex"}.Contains((string)p.Attribute("name"))))
                    {int i=int.Parse(p.Value,Invariant);if(i<0||i>=particles.Length)throw new InvalidDataException("Constraint particle index outside simulation domain.");}
                var ownerSimRefs=Refs(owner,"simClothDatas");
                foreach(var op in Refs(owner,"operators").Select(r=>Objects[r]).Where(o=>(string)o.Attribute("class")=="hclSimulateOperator"))
                {
                    int index=int.Parse(Value(op,"simClothIndex"),Invariant);if(index<0||index>=ownerSimRefs.Length)throw new InvalidDataException("SimulateOperator domain index invalid.");
                    var target=Objects[ownerSimRefs[index]];int constraints=Refs(target,"staticConstraintSets").Length;
                    var configs=op.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")=="simulateOpConfigs");
                    foreach(var config in configs==null?new[]{op}:configs.Elements("hkobject"))
                        if(Numbers(Value(config,"constraintExecution")).Any(i=>i< -1||i>=constraints))throw new InvalidDataException("Constraint execution index outside constraint list.");
                }
                foreach (var c in d.Constraints.Where(c=>(string)c.Attribute("class")=="hclLocalRangeConstraintSet"))
                    foreach (var row in Field(c,"localConstraints").Elements())
                    {
                        int i=int.Parse(Value(row,"particleIndex"),Invariant); if(i<0||i>=particles.Length)throw new InvalidDataException("LocalRange particle index is invalid.");
                        d.Ranges[i]=Math.Max(d.Ranges[i],(float)double.Parse(Value(row,"maximumDistance"),Invariant));
                    }
                var map=Field(sim,"collidableTransformMap").Elements().Single();
                var refs=Refs(sim,"perInstanceCollidables");var indices=Numbers(Value(map,"transformIndices"));var offsets=Numbers(Value(map,"offsets"));
                // -1 with empty map arrays means no transform-set binding. Such a
                // collider retains its authored hclCollidable.transform (DS3 6650).
                // Do not invent a bone or silently accept a partial bound map.
                int transformSet=int.Parse(Value(map,"transformSetIndex"),Invariant);
                bool unbound=transformSet==-1&&indices.Length==0&&offsets.Length==0;
                if(!unbound&&(transformSet<0||indices.Length!=refs.Length||offsets.Length!=refs.Length*16))throw new InvalidDataException("Collider binding dimensions do not match.");
                for(int i=0;i<refs.Length;i++)
                {
                    var collider=Objects[refs[i]];var shape=Objects[Value(collider,"shape")];string cls=(string)shape.Attribute("class");
                    Matrix4x4 transform;string boneName;
                    if(unbound)
                    {
                        var authored=Numbers(Value(collider,"transform"));if(authored.Length!=16)throw new InvalidDataException("Authored collider transform dimensions do not match.");
                        transform=Matrix(authored,0);boneName="(unbound)";
                    }
                    else
                    {
                        int bone=(int)indices[i];if(bone<0||bone>=bones.Length)throw new InvalidDataException("Collider bone index is invalid.");
                        transform=Matrix(offsets,i*16)*world[bone];boneName=bones[bone];
                    }
                    var visual=new ClothCollider {Bone=boneName,Shape=cls};
                    if(cls=="hclCapsuleShape" || cls=="hclTaperedCapsuleShape")
                    {
                        visual.Start=Vector3.Transform(Vector(Numbers(Value(shape,cls=="hclCapsuleShape"?"start":"small")),0),transform);
                        visual.End=Vector3.Transform(Vector(Numbers(Value(shape,cls=="hclCapsuleShape"?"end":"big")),0),transform);
                        visual.RadiusA=(float)double.Parse(Value(shape,cls=="hclCapsuleShape"?"radius":"smallRadius"),Invariant);
                        visual.RadiusB=cls=="hclCapsuleShape"?visual.RadiusA:(float)double.Parse(Value(shape,"bigRadius"),Invariant);
                    }
                    else if(cls=="hclSphereShape")
                    {
                        var sphere=Numbers(Value(shape,"sphere"));visual.Start=visual.End=Vector3.Transform(Vector(sphere,0),transform);
                        visual.RadiusA=visual.RadiusB=(float)sphere[3];
                    }
                    else continue; // Unsupported shapes stay visible in the raw XML, not guessed.
                    d.Colliders.Add(visual);
                }
                Domains.Add(d);
            }
            if(Domains.Count==0)throw new InvalidDataException("No native Cloth domains found. Animation HKX is not cloth HKX.");
        }
        public void SaveRangeCopy(int domain, double factor, string path)
        {
            if (factor < 0.1 || factor > 4 || double.IsNaN(factor)) throw new ArgumentOutOfRangeException("factor");
            if (String.Equals(Path.GetFullPath(path),Source,StringComparison.OrdinalIgnoreCase)) throw new IOException("Cannot overwrite the loaded source. Save to a new XML path.");
            if (File.Exists(path)) throw new IOException("Output already exists. Choose a new filename.");
            if (!Path.GetExtension(path).Equals(".xml",StringComparison.OrdinalIgnoreCase)) throw new IOException("Only author XML export is supported. No direct game HKX/Binder writes.");
            var clone=RangeClone(domain,factor);
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))clone.Save(stream);
        }
        public void SaveHkxCopy(string path,int domain,double factor)
        {
            if(string.Equals(Path.GetFullPath(path),Source,StringComparison.OrdinalIgnoreCase))throw new IOException("Cannot overwrite the loaded source.");
            var clone=RangeClone(domain,factor);FromXml(clone,Source); // validate semantic indices/dimensions before creating output
            HkxPackfileCodec.SaveNew(clone,path);
        }
        public XDocument RangeClone(int domain,double factor)
        {
            if(factor<.1||factor>4||double.IsNaN(factor))throw new ArgumentOutOfRangeException("factor");
            if(domain<0||domain>=Domains.Count)throw new ArgumentOutOfRangeException("domain");
            var clone=new XDocument(Xml);var sim=clone.Descendants("hksection").Elements("hkobject").Single(o=>(string)o.Attribute("name")== (string)Domains[domain].Sim.Attribute("name"));
            var objects=clone.Descendants("hksection").Elements("hkobject").ToDictionary(o=>(string)o.Attribute("name"));
            foreach(var reference in Refs(sim,"staticConstraintSets"))
            {
                var c=objects[reference];if((string)c.Attribute("class")!="hclLocalRangeConstraintSet")continue;
                foreach(var row in Field(c,"localConstraints").Elements())
                foreach(var key in new[]{"maximumDistance","maxNormalDistance","minNormalDistance"})
                {
                    var p=Field(row,key);double value=double.Parse(p.Value,Invariant);
                    if(Math.Abs(value)<1e20) p.Value=(value*factor).ToString("R",Invariant); // preserve float-max sentinels
                }
            }
            return clone;
        }
    }
}
