using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SoulsFormats;

namespace MySFformat
{
    public sealed class Clm2Entry
    {
        public short Unk00 { get; set; }
        public short Unk02 { get; set; }
    }
    public sealed class Clm2Document
    {
        public string Source { get; private set; }
        public List<List<Clm2Entry>> Meshes { get; private set; }
        public bool Edited { get; set; }
        public static Clm2Document Load(string path)
        {return FromBytes(File.ReadAllBytes(path),Path.GetFullPath(path));}
        public static Clm2Document LoadCompanion(string clothSource)
        {
            if(string.IsNullOrWhiteSpace(clothSource)||!File.Exists(clothSource))return null;
            string extension=Path.GetExtension(clothSource);
            if(extension.Equals(".hkx",StringComparison.OrdinalIgnoreCase)||extension.Equals(".xml",StringComparison.OrdinalIgnoreCase))
            {string companion=Path.ChangeExtension(clothSource,".clm2");return File.Exists(companion)?Load(companion):null;}
            if(!BND4.Is(clothSource))return null;
            var binder=BND4.Read(clothSource);var hkx=binder.Files.Where(f=>f.Name.EndsWith("_c.hkx",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(hkx.Length!=1)return null;string basename=Path.GetFileNameWithoutExtension(hkx[0].Name)+".clm2";
            var files=binder.Files.Where(f=>Path.GetFileName(f.Name).Equals(basename,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(files.Length>1)throw new InvalidDataException("Binder contains multiple matching CLM2 companions.");
            return files.Length==0?null:FromBytes(files[0].Bytes,Path.GetFullPath(clothSource)+"!"+files[0].Name);
        }
        static Clm2Document FromBytes(byte[] bytes,string source)
        {
            // Bounds preflight before the library allocates counts or follows offsets.
            if(bytes.Length<40||Encoding.ASCII.GetString(bytes,0,4)!="CLM2")throw new InvalidDataException("Invalid CLM2 header.");
            int count=BitConverter.ToInt32(bytes,20);
            if(count<0||count>10000||40L+count*16L>bytes.Length)throw new InvalidDataException("CLM2 mesh table exceeds file bounds.");
            for(int i=0;i<count;i++)
            {
                int start=40+i*16;int entries=BitConverter.ToInt32(bytes,start+4);uint offset=BitConverter.ToUInt32(bytes,start+8);
                if(entries<0||entries>1000000||(entries>0&&(offset<40L+count*16L||(long)offset+entries*4L>bytes.Length)))throw new InvalidDataException("CLM2 entry table exceeds file bounds.");
            }
            var file=CLM2.Read(bytes);
            return new Clm2Document { Source=source,Meshes=file.Meshes.Select(m=>m.Select(e=>new Clm2Entry{Unk00=e.Unk00,Unk02=e.Unk02}).ToList()).ToList() };
        }
        public Clm2Document Clone()
        {return new Clm2Document{Source=Source,Edited=Edited,Meshes=Meshes.Select(m=>m.Select(e=>new Clm2Entry{Unk00=e.Unk00,Unk02=e.Unk02}).ToList()).ToList()};}
        public byte[] Serialize()
        {
            if(Meshes.Count>10000||Meshes.Any(m=>m.Count>1000000))throw new InvalidDataException("CLM2 counts exceed safe limits.");
            byte[] bytes;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("CLM2"));writer.Write(0);writer.Write((short)1);writer.Write((short)1);
                writer.Write(0);writer.Write(0);writer.Write(Meshes.Count);writer.Write(0x28);writer.Write(0);writer.Write(0);writer.Write(0x28);
                // Mesh has no public constructor in this library. Read an empty table
                // scaffold, then let SoulsFormats write all real entries/offsets/padding.
                for(int i=0;i<Meshes.Count;i++){writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);}
                bytes=stream.ToArray();
            }
            var file=CLM2.Read(bytes);
            for(int i=0;i<Meshes.Count;i++)foreach(var entry in Meshes[i])file.Meshes[i].Add(new CLM2.Mesh.Entry(entry.Unk00,entry.Unk02));
            var canonical=file.Write();
            if(!canonical.SequenceEqual(CLM2.Read(canonical).Write()))throw new InvalidDataException("CLM2 SoulsFormats read/write roundtrip failed.");
            return canonical;
        }
        public void SaveCopy(string path)
        {
            if(!Path.GetExtension(path).Equals(".clm2",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a .clm2 output path.");
            if(string.Equals(Path.GetFullPath(path),Source,StringComparison.OrdinalIgnoreCase)||File.Exists(path))throw new IOException("Choose a new output path. Source and existing files are not overwritten.");
            var bytes=Serialize();using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))stream.Write(bytes,0,bytes.Length);
        }
        public static string Verify(Clm2Document clm,ClothDocument cloth,FLVER2 flver,string flverSource)
        {
            var lines=new List<string>();int failed=0,warnings=0,skipped=0;
            Action<string,string> add=(level,text)=>{lines.Add("["+level+"] "+text);if(level=="FAIL")failed++;if(level=="WARN")warnings++;if(level=="SKIP")skipped++;};
            lines.Add("CLM2: "+(clm==null?"Not loaded":clm.Source+(clm.Edited?" (in-memory edits)":"")));
            lines.Add("HKX/XML: "+(cloth==null?"Not loaded":cloth.Source));lines.Add("Current FLVER (in-memory): "+(flver==null?"Not loaded":flverSource));
            if(clm==null)add("SKIP","CLM2 is not loaded.");
            else
            {
                try{clm.Serialize();add("PASS","CLM2 offsets/counts/layout roundtrip through SoulsFormats.");}catch(Exception e){add("FAIL",e.Message);}
                lines.Add("CLM2 meshes: "+clm.Meshes.Count+"; entries: "+clm.Meshes.Sum(m=>m.Count));
            }
            if(flver==null)add("SKIP","No current FLVER to compare. Counts have not been verified.");
            else if(clm!=null)
            {
                add(clm.Meshes.Count==flver.Meshes.Count?"PASS":"FAIL","CLM2 mesh count "+clm.Meshes.Count+" vs FLVER mesh count "+flver.Meshes.Count+".");
                int candidates=0,outside=0;
                for(int i=0;i<Math.Min(clm.Meshes.Count,flver.Meshes.Count);i++)foreach(var entry in clm.Meshes[i])
                {candidates++;if(entry.Unk00<0||entry.Unk02<0||entry.Unk00>=flver.Meshes[i].Vertices.Count||entry.Unk02>=flver.Meshes[i].Vertices.Count)outside++;}
                if(candidates>0)add(outside>0?"WARN":"PASS",outside+" / "+candidates+" entries outside corresponding FLVER vertex bounds (vertex-pair hypothesis only; Unk00/Unk02 semantics are unknown).");
                else add("WARN","All CLM2 entry lists are empty. Layout/counts may be valid; runtime behavior is not certified.");
            }
            if(cloth==null)add("SKIP","HKX/XML is not loaded.");
            else
            {
                lines.Add("HKX simulation domains: "+cloth.Domains.Count+". This is NOT the CLM2 mesh-count field.");
                lines.Add(cloth.IsTagfile?cloth.NativeLayout+": read-only author data; compatibility physics is not the game runtime.":"HKX native header layout: "+cloth.NativeLayout+". Layout rules are not a definitive game identifier.");
                foreach(var domain in cloth.Domains)
                {
                    int n=domain.Positions.Length;var data=ClothDocument.Field(domain.Sim,"particleDatas");
                    add(n==data.Elements().Count()?"PASS":"FAIL",domain.Name+": rest/particle dimensions ("+n+").");
                    var masks=ClothDocument.Numbers(ClothDocument.Value(domain.Sim,"staticCollisionMasks"));
                    add(masks.Length==n?"PASS":"FAIL",domain.Name+": collision-mask count "+masks.Length+" vs particles "+n+".");
                    foreach(var constraint in domain.Constraints)
                    {
                        int invalid=constraint.Descendants("hkparam").Count(p=>new[]{"particleA","particleB","particleC","particleD","particleIndex"}.Contains((string)p.Attribute("name"))&&(int.Parse(p.Value)<0||int.Parse(p.Value)>=n));
                        if(invalid>0)add("FAIL",domain.Name+": "+invalid+" out-of-range constraint particle indices.");
                    }
                }
                if(flver!=null)
                {
                    var owners=cloth.Domains.Select(d=>d.Owner).Distinct().ToArray();int displayCount=0;
                    foreach(var owner in owners)
                    foreach(var reference in ClothDocument.Value(owner,"bufferDefinitions").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
                    {
                        var buffer=cloth.Objects[reference];if(ClothDocument.Value(buffer,"type")!="4")continue;displayCount++;
                        string name=ClothDocument.Value(buffer,"name");int count=int.Parse(ClothDocument.Value(buffer,"numVertices"));
                        var matching=flver.Meshes.Where(m=>m.NodeIndex>=0&&m.NodeIndex<flver.Nodes.Count&&flver.Nodes[m.NodeIndex].Name==name).ToArray();
                        if(matching.Length==0)add("WARN","HKX display buffer '"+name+"' has no FLVER mesh with the same node name. Binding conventions may differ.");
                        else if(matching.Length>1)add("WARN","HKX display buffer '"+name+"' matches multiple FLVER meshes; ambiguous binding, not certified.");
                        else add(matching[0].Vertices.Count==count?"PASS":"FAIL","Display '"+name+"': HKX vertices "+count+" vs FLVER "+matching[0].Vertices.Count+".");
                    }
                    if(displayCount==0)add("WARN","No recognized type-4 HKX display buffers; FLVER/HKX binding not verified.");
                }
            }
            lines.Add("\r\nResult: "+failed+" failure(s), "+warnings+" warning(s), "+skipped+" skipped check(s).");
            lines.Add("Count/layout inspection only. No Havok simulation, global-wind test, or runtime certification. Unk00/Unk02 remain unknown signed shorts.");
            return string.Join(Environment.NewLine,lines);
        }
    }
}
