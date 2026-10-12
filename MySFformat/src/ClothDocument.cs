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
        public string NativeLayout {get{return (string)Xml.Root.Attribute("nativeLayout")??"8,1,1,1";}}
        public void SetNativeLayout(string layout)
        {
            if(layout!="8,1,1,1"&&layout!="8,1,0,1")throw new ArgumentException("Unsupported native header layout.");
            Xml.Root.SetAttributeValue("nativeLayout",layout);Edited=true;
        }
        public string Source { get; private set; }
        public Dictionary<string, XElement> Objects { get; private set; }
        public List<ClothDomain> Domains { get; private set; }
        public static XElement Field(XElement o, string name) { return o.Elements("hkparam").First(p => (string)p.Attribute("name") == name); }
        public static string Value(XElement o, string name) { return Field(o, name).Value.Trim(); }
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
                if (files.Length != 1) throw new InvalidDataException("Binder must contain exactly one _c.hkx. For multiple cloth files, unpack and select one explicitly.");
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
                    if(Numbers(Value(op,"constraintExecution")).Any(i=>i< -1||i>=constraints))throw new InvalidDataException("Constraint execution index outside constraint list.");
                }
                foreach (var c in d.Constraints.Where(c=>(string)c.Attribute("class")=="hclLocalRangeConstraintSet"))
                    foreach (var row in Field(c,"localConstraints").Elements())
                    {
                        int i=int.Parse(Value(row,"particleIndex"),Invariant); if(i<0||i>=particles.Length)throw new InvalidDataException("LocalRange particle index is invalid.");
                        d.Ranges[i]=Math.Max(d.Ranges[i],(float)double.Parse(Value(row,"maximumDistance"),Invariant));
                    }
                var map=Field(sim,"collidableTransformMap").Elements().Single();
                var refs=Refs(sim,"perInstanceCollidables");var indices=Numbers(Value(map,"transformIndices"));var offsets=Numbers(Value(map,"offsets"));
                if(indices.Length!=refs.Length || offsets.Length!=refs.Length*16)throw new InvalidDataException("Collider binding dimensions do not match.");
                for(int i=0;i<refs.Length;i++)
                {
                    var collider=Objects[refs[i]];var shape=Objects[Value(collider,"shape")];string cls=(string)shape.Attribute("class");
                    int bone=(int)indices[i];if(bone<0||bone>=bones.Length)throw new InvalidDataException("Collider bone index is invalid.");
                    var transform=Matrix(offsets,i*16)*world[bone];
                    var visual=new ClothCollider {Bone=bones[bone],Shape=cls};
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
