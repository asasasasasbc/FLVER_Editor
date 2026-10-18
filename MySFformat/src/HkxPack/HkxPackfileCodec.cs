// Native Cloth packfile codec. C# port informed by DexesTTP / JeNoVaViRuS
// hkxpack-souls (MIT), with this project's audited layout/fixup corrections.
// Scope: audited Havok 2014.1 v11, 64-bit little-endian BB / DS3 Cloth schemas.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MySFformat
{
    public static class HkxPackfileCodec
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static readonly Lazy<Schema> Metadata = new Lazy<Schema>(() => new Schema("MySFformat.NativeClothSchema.xml"));
        static readonly Lazy<Schema> Ds3Metadata = new Lazy<Schema>(() => new Schema("MySFformat.NativeDs3ClothSchema.xml"));
        static Schema ForLayout(string layout){if(layout=="8,1,0,1")return Ds3Metadata.Value;if(layout=="8,1,1,1")return Metadata.Value;throw new NotSupportedException("Unsupported native layout profile: "+layout);}
        const string Sdk = "hk_2014.1.0-r1";
        const int MaximumBytes = 128 * 1024 * 1024;
        public static int SchemaClassCount { get { return Metadata.Value.Classes.Count; } }
        public static XDocument Read(string path) { return Read(File.ReadAllBytes(path)); }
        public static XDocument Read(byte[] bytes) { if(HkxTagfileReader.IsTagfile(bytes))return new HkxTagfileReader(bytes).Read();return new Reader(bytes, bytes.Length>18&&bytes[18]==0?Ds3Metadata.Value:Metadata.Value).Read(); }
        public static byte[] Pack(XDocument xml)
        {
            if(HkxTagfileReader.IsTagfileDocument(xml))throw new NotSupportedException("TAG0 author export is not implemented. A 2014 preview conversion is not a game-compatible Sekiro/ER HKX.");
            byte[] bytes = new Writer(ForLayout((string)xml.Root.Attribute("nativeLayout")??"8,1,1,1")).Pack(xml);
            AssertEquivalent(xml, Read(bytes)); // fail before any output file is created
            return bytes;
        }
        public static void SaveNew(XDocument xml, string path)
        {
            if (!Path.GetExtension(path).Equals(".hkx", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a .hkx output path.");
            if (File.Exists(path)) throw new IOException("Output already exists. Choose a new HKX filename.");
            var bytes = Pack(xml);
            WriteNewBytes(bytes,path);
        }
        internal static void WriteNewBytes(byte[] bytes,string path)
        {
            string output=Path.GetFullPath(path);
            if(File.Exists(output))throw new IOException("Output already exists.");
            string temp=Path.Combine(Path.GetDirectoryName(output),".native-hkx-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                File.Move(temp,output); // same-directory atomic rename; never overwrite a racing existing output
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        public static XDocument LoadXml(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes }))
                return XDocument.Load(reader);
        }
        static int Int(string value) { return int.Parse(value, C); }
        static string[] Tokens(string value) { return value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries); }
        static uint Hex(string value) { return uint.Parse(value.StartsWith("0x") ? value.Substring(2) : value, NumberStyles.HexNumber, C); }
        static string Signature(uint value) { return "0x" + value.ToString("x", C); }
        static bool IsArray(string type) { return type == "TYPE_ARRAY" || type == "TYPE_SIMPLEARRAY" || type == "TYPE_RELARRAY"; }
        static bool IsString(string type) { return type == "TYPE_STRINGPTR" || type == "TYPE_CSTRING"; }
        static int PrimitiveSize(string type)
        {
            switch (type)
            {
                case "TYPE_BOOL": case "TYPE_CHAR": case "TYPE_INT8": case "TYPE_UINT8": return 1;
                case "TYPE_INT16": case "TYPE_UINT16": return 2;
                case "TYPE_INT32": case "TYPE_UINT32": case "TYPE_REAL": return 4;
                case "TYPE_INT64": case "TYPE_UINT64": case "TYPE_ULONG": case "TYPE_POINTER": case "TYPE_STRINGPTR": case "TYPE_CSTRING": return 8;
                case "TYPE_VECTOR4": case "TYPE_QUATERNION": return 16;
                case "TYPE_QSTRANSFORM": case "TYPE_MATRIX3": return 48;
                case "TYPE_MATRIX4": case "TYPE_TRANSFORM": return 64;
                case "TYPE_ARRAY": case "TYPE_SIMPLEARRAY": return 16;
                case "TYPE_RELARRAY": return 4;
                default: throw new NotSupportedException("Unsupported native HKX type: " + type);
            }
        }
        sealed class Member
        {
            public string Name, Type, Subtype, Target, Enum;
            public int Offset;
            public bool Ignored;
        }
        sealed class Class
        {
            public string Name;
            public int Size;
            public uint Signature;
            public List<Member> Members = new List<Member>();
        }
        sealed class Schema
        {
            public readonly Dictionary<string, Class> Classes = new Dictionary<string, Class>();
            readonly Dictionary<string, XElement> raw;
            readonly HashSet<string> resolving = new HashSet<string>();
            public readonly Dictionary<string, Dictionary<long, string>> Enums = new Dictionary<string, Dictionary<long, string>>();
            public Schema(string resource)
            {
                using (var input = typeof(HkxPackfileCodec).Assembly.GetManifestResourceStream(resource))
                {
                    if (input == null) throw new InvalidDataException("Embedded Cloth schema is missing: "+resource);
                    raw = XDocument.Load(input).Root.Elements("class").ToDictionary(e => (string)e.Attribute("name"));
                }
                foreach (var item in raw)
                foreach (var enumeration in item.Value.Elements("enums").Elements())
                {
                    var map = new Dictionary<long, string>();
                    foreach (var value in enumeration.Elements()) map[long.Parse((string)value.Attribute("value"), C)] = (string)value.Attribute("name");
                    Enums[item.Key + "." + (string)enumeration.Attribute("name")] = map;
                }
                foreach (var name in raw.Keys) Get(name);
            }
            public Class Get(string name)
            {
                Class result;
                if (Classes.TryGetValue(name, out result)) return result;
                XElement source;
                if (!raw.TryGetValue(name, out source)) throw new NotSupportedException("No audited schema in the selected native layout profile for class: " + name);
                if (!resolving.Add(name)) throw new InvalidDataException("Cyclic schema inheritance/inline layout: " + name);
                result = new Class { Name = name, Size = Int((string)source.Attribute("size")), Signature = Hex((string)source.Attribute("signature")) };
                var parent = (string)source.Attribute("parent");
                if (!string.IsNullOrEmpty(parent)) result.Members.AddRange(Get(parent).Members);
                foreach (var element in source.Elements("members").Elements("member"))
                {
                    string type = (string)element.Attribute("vtype"), subtype = (string)element.Attribute("vsubtype"), target = (string)element.Attribute("ctype");
                    int count = Int((string)element.Attribute("arrsize"));
                    bool ignored = ((string)element.Attribute("flags") ?? "").Contains("SERIALIZE_IGNORED");
                    int stride = count > 0 ? (type == "TYPE_STRUCT" ? Get(target).Size : PrimitiveSize(type)) : 0;
                    for (int i = 0; i < Math.Max(1, count); i++)
                    {
                        var member = new Member { Name = (string)element.Attribute("name") + (count > 0 ? (i + 1).ToString(C) : ""),
                            Offset = Int((string)element.Attribute("offset")) + i * stride, Type = type, Subtype = subtype,
                            Target = target, Enum = name + "." + ((string)element.Attribute("etype") ?? ""), Ignored = ignored };
                        result.Members.Add(member);
                    }
                }
                Classes.Add(name, result); resolving.Remove(name);
                return result;
            }
            public int Size(Member member, string type)
            { return type == "TYPE_STRUCT" ? Get(member.Target).Size : PrimitiveSize(type); }
            public string EnumName(Member member, long value)
            {
                Dictionary<long, string> map; string result;
                return Enums.TryGetValue(member.Enum, out map) && map.TryGetValue(value, out result) ? result : value.ToString(C);
            }
            public string EnumValue(Member member, string value)
            {
                Dictionary<long, string> map;
                if (Enums.TryGetValue(member.Enum, out map)) foreach (var pair in map) if (pair.Value == value) return pair.Key.ToString(C);
                long parsed;
                if (!long.TryParse(value, NumberStyles.Integer, C, out parsed)) throw new InvalidDataException("Unknown enum value for " + member.Name + ": " + value);
                return parsed.ToString(C);
            }
        }
        sealed class Section
        {
            public string Name;
            public int Base, Local, Global, Virtual, Exports, Imports, End;
        }
        sealed class Reader
        {
            readonly byte[] data;
            readonly Schema schema;
            readonly List<Section> sections = new List<Section>();
            readonly Dictionary<int, int> pointers = new Dictionary<int, int>();
            readonly List<Tuple<int, string>> objects = new List<Tuple<int, string>>();
            readonly Dictionary<int, string> names = new Dictionary<int, string>();
            public Reader(byte[] bytes, Schema metadata) { data = bytes; schema = metadata; }
            void Bounds(int address, int length)
            { if (address < 0 || length < 0 || (long)address + length > data.Length) throw new InvalidDataException("HKX data access outside file bounds."); }
            uint U32(int address) { Bounds(address, 4); return BitConverter.ToUInt32(data, address); }
            int I32(int address) { return checked((int)U32(address)); }
            string String(int address)
            {
                Bounds(address, 1); int end = address;var section=sections.FirstOrDefault(s=>address>=s.Base&&address<s.Base+s.Local);int limit=section==null?data.Length:section.Base+section.Local;
                while (end < limit && data[end] != 0) end++;
                if (end == limit || end - address > MaximumBytes) throw new InvalidDataException("Unterminated HKX string.");
                return new UTF8Encoding(false, true).GetString(data, address, end - address);
            }
            void AddPointer(int from, int to)
            {
                Bounds(from, 8); Bounds(to, 1);
                if (pointers.ContainsKey(from)) throw new InvalidDataException("Duplicate HKX fixup source.");
                pointers.Add(from, to);
            }
            public XDocument Read()
            {
                if (data.Length < 0x50 || data.Length > MaximumBytes || !data.Take(8).SequenceEqual(new byte[] { 0x57,0xe0,0xe0,0x57,0x10,0xc0,0xc0,0x10 })) throw new InvalidDataException("Invalid or oversized HKX packfile.");
                if (U32(12) != 11 || data[16] != 8 || data[17] != 1 || (data[18] != 0 && data[18] != 1) || data[19] != 1 || String(40) != Sdk)
                    throw new NotSupportedException("Only audited 64-bit little-endian Havok 2014.1 v11 cloth layouts are supported.");
                int count = I32(20); if (count < 1 || count > 16) throw new InvalidDataException("Invalid HKX section count.");
                Bounds(0x50, count * 0x40);
                for (int i = 0; i < count; i++)
                {
                    int p = 0x50 + i * 0x40;
                    var s = new Section { Name = String(p), Base = I32(p+20), Local = I32(p+24), Global = I32(p+28), Virtual = I32(p+32), Exports = I32(p+36), Imports = I32(p+40), End = I32(p+44) };
                    if (s.Base < 0x50 + count * 0x40 || s.Local > s.Global || s.Global > s.Virtual || s.Virtual > s.Exports || s.Exports > s.Imports || s.Imports > s.End) throw new InvalidDataException("Invalid HKX section layout.");
                    if (s.Exports != s.Imports || s.Imports != s.End) throw new NotSupportedException("HKX imports/exports are not supported by the BB Cloth codec.");
                    Bounds(s.Base, s.End); sections.Add(s);
                }
                var occupied=sections.Where(s=>s.End>0).OrderBy(s=>s.Base).ToArray();
                for(int i=1;i<occupied.Length;i++)if(occupied[i-1].Base+(long)occupied[i-1].End>occupied[i].Base)throw new InvalidDataException("Overlapping HKX sections.");
                foreach (var s in sections)
                {
                    for (int p = s.Base + s.Local; p + 8 <= s.Base + s.Global; p += 8)
                    {
                        if (U32(p) == uint.MaxValue) continue;
                        int from = I32(p), to = I32(p+4);
                        if (from + 8L > s.Local || to >= s.Local) throw new InvalidDataException("Local fixup outside section data.");
                        AddPointer(s.Base+from, s.Base+to);
                    }
                    for (int p = s.Base + s.Global; p + 12 <= s.Base + s.Virtual; p += 12)
                    {
                        if (U32(p) == uint.MaxValue) continue;
                        int from = I32(p), id = I32(p+4), to = I32(p+8);
                        if (from + 8L > s.Local || id >= sections.Count || to >= sections[id].Local) throw new InvalidDataException("Global fixup outside section data.");
                        AddPointer(s.Base+from, sections[id].Base+to);
                    }
                    for (int p = s.Base + s.Virtual; p + 12 <= s.Base + s.Exports; p += 12)
                    {
                        if (U32(p) == uint.MaxValue) continue;
                        int from = I32(p), id = I32(p+4), to = I32(p+8);
                        if (from >= s.Local || id >= sections.Count || to < 5 || to >= sections[id].Local) throw new InvalidDataException("Invalid HKX virtual fixup.");
                        string type = String(sections[id].Base+to); var cls = schema.Get(type);
                        if (U32(sections[id].Base+to-5) != cls.Signature) throw new NotSupportedException("Class signature does not match audited schema: " + type);
                        if(from+(long)cls.Size>s.Local)throw new InvalidDataException("Virtual object extends outside native section data.");
                        Bounds(s.Base+from, cls.Size); objects.Add(Tuple.Create(s.Base+from,type));
                    }
                }
                if (objects.Count < 1 || objects.Count > 10000) throw new InvalidDataException("Invalid HKX virtual object count.");
                // Virtual table order is preserved; address dictionaries resolve cycles and shared references.
                for (int i=0; i<objects.Count; i++) names.Add(objects[i].Item1,"#"+(i+1).ToString(C));
                int rootSection=I32(24), rootOffset=I32(28);
                if(rootSection>=sections.Count || !names.ContainsKey(sections[rootSection].Base+rootOffset))throw new InvalidDataException("Invalid HKX root object.");
                int rootAddress=sections[rootSection].Base+rootOffset;var rootObject=objects.Single(o=>o.Item1==rootAddress);
                int classSection=I32(32),classOffset=I32(36);
                if(rootObject.Item2!="hkRootLevelContainer"||classSection>=sections.Count||classOffset>=sections[classSection].Local||String(sections[classSection].Base+classOffset)!=rootObject.Item2)throw new InvalidDataException("HKX root class descriptor does not match the root object.");
                var xml=new XDocument(new XElement("hkpackfile",new XAttribute("classversion",11),new XAttribute("contentsversion",Sdk),new XElement("hksection",new XAttribute("name","__data__"))));
                if(data[18]==0)xml.Root.SetAttributeValue("nativeLayout","8,1,0,1"); // preserve DS3 layout rule; legacy BB XML defaults to 8,1,1,1
                var output=xml.Root.Element("hksection");
                foreach(var item in objects)
                {
                    var cls=schema.Get(item.Item2);var element=new XElement("hkobject",new XAttribute("class",cls.Name),new XAttribute("name",names[item.Item1]),new XAttribute("signature",Signature(cls.Signature)));
                    ReadObject(element,cls,item.Item1,0);output.Add(element);
                }
                return xml;
            }
            void ReadObject(XElement output,Class cls,int address,int depth)
            {
                if(depth>64)throw new InvalidDataException("HKX inline nesting limit exceeded.");Bounds(address,cls.Size);
                foreach(var m in cls.Members)
                {
                    if(m.Ignored){output.Add(new XComment(m.Name+" SERIALIZE_IGNORED"));continue;}
                    int size=(m.Type=="TYPE_ENUM"||m.Type=="TYPE_FLAGS")?PrimitiveSize(m.Subtype):schema.Size(m,m.Type);
                    if(m.Offset<0||m.Offset+(long)size>cls.Size)throw new InvalidDataException("Schema member outside declared size: "+cls.Name+"."+m.Name);
                    var param=new XElement("hkparam",new XAttribute("name",m.Name));
                    try {ReadValue(param,m,m.Type,address+m.Offset,depth+1);}
                    catch(InvalidDataException ex){throw new InvalidDataException(cls.Name+"."+m.Name+" at 0x"+(address+m.Offset).ToString("x")+": "+ex.Message,ex);}
                    output.Add(param);
                }
            }
            void ReadValue(XElement value,Member m,string type,int address,int depth)
            {
                if(IsArray(type))
                {
                    int count=type=="TYPE_RELARRAY"?BitConverter.ToUInt16(data,address):I32(address+8);
                    if(count<0||count>1000000)throw new InvalidDataException("Unsafe HKX array count.");value.SetAttributeValue("numelements",count);
                    int start;
                    if(count==0)return;
                    if(type=="TYPE_RELARRAY")start=checked(address+BitConverter.ToUInt16(data,address+2));
                    else if(!pointers.TryGetValue(address,out start))throw new InvalidDataException("Nonempty HKX array has no source-address fixup.");
                    int stride=schema.Size(m,m.Subtype);Bounds(start,checked(count*stride));
                    var text=new List<string>();
                    for(int i=0;i<count;i++)
                    {
                        if(m.Subtype=="TYPE_STRUCT") { var item=new XElement("hkobject");ReadObject(item,schema.Get(m.Target),start+i*stride,depth);value.Add(item); }
                        else if(IsString(m.Subtype)){var item=new XElement("hkcstring");ReadValue(item,m,m.Subtype,start+i*stride,depth);value.Add(item);}
                        else {var item=new XElement("v");ReadValue(item,m,m.Subtype,start+i*stride,depth);text.Add(item.Value);}
                    }
                    if(text.Count>0)value.Value=string.Join(" ",text);return;
                }
                if(type=="TYPE_STRUCT")
                {
                    var cls=schema.Get(m.Target);var item=new XElement("hkobject",new XAttribute("class",cls.Name),new XAttribute("name",m.Name),new XAttribute("signature",Signature(cls.Signature)));
                    ReadObject(item,cls,address,depth);value.Add(item);return;
                }
                if(type=="TYPE_POINTER")
                {
                    int target;string name;
                    if(!pointers.TryGetValue(address,out target)){Bounds(address,8);if(BitConverter.ToUInt64(data,address)!=0)throw new InvalidDataException("Nonzero pointer without a fixup.");value.Value="null";return;}
                    if(!names.TryGetValue(target,out name))throw new NotSupportedException("Pointer targets an untyped/interior object.");value.Value=name;return;
                }
                if(IsString(type))
                {
                    int target;
                    if(pointers.TryGetValue(address,out target))value.Value=String(target);
                    else{Bounds(address,8);if(BitConverter.ToUInt64(data,address)!=0)throw new NotSupportedException("String pointer has no fixup and is not null.");value.Value="";}
                    return;
                }
                if(type=="TYPE_ENUM"||type=="TYPE_FLAGS") { value.Value=schema.EnumName(m,long.Parse(Scalar(m.Subtype,address),C));return; }
                value.Value=Scalar(type,address);
            }
            string Scalar(string type,int address)
            {
                int size=PrimitiveSize(type);Bounds(address,size);
                if(size>=16)
                {
                    var values=new List<string>();for(int i=0;i<size;i+=16){var group=new List<string>();for(int j=0;j<16;j+=4)group.Add(Scalar("TYPE_REAL",address+i+j));values.Add("("+string.Join(" ",group)+")");}return string.Join("",values);
                }
                switch(type)
                {
                    case "TYPE_BOOL":if(data[address]>1)throw new InvalidDataException("Invalid HKX bool.");return data[address]==0?"false":"true";
                    // hkxpack-souls XML historically spells narrow signed values as
                    // unsigned bit patterns (e.g. parent -1 -> 65535). Keep XML compatibility.
                    case "TYPE_CHAR":case "TYPE_INT8":return data[address].ToString(C);
                    case "TYPE_UINT8":return data[address].ToString(C);
                    case "TYPE_INT16":return BitConverter.ToUInt16(data,address).ToString(C);
                    case "TYPE_UINT16":return BitConverter.ToUInt16(data,address).ToString(C);
                    case "TYPE_INT32":return BitConverter.ToInt32(data,address).ToString(C);
                    case "TYPE_UINT32":return BitConverter.ToInt32(data,address).ToString(C); // legacy XML spelling of 32-bit bitsets
                    case "TYPE_INT64":return BitConverter.ToInt64(data,address).ToString(C);
                    case "TYPE_UINT64":case "TYPE_ULONG":return BitConverter.ToUInt64(data,address).ToString(C);
                    case "TYPE_REAL":float f=BitConverter.ToSingle(data,address);if(float.IsNaN(f)||float.IsInfinity(f))throw new NotSupportedException("Non-finite serialized float is not certified for BB Cloth writeback.");return f.ToString("R",C);
                    default:throw new NotSupportedException(type);
                }
            }
        }
        sealed class Writer
        {
            readonly Schema schema;
            readonly MemoryStream data=new MemoryStream();
            readonly List<Tuple<int,int>> local=new List<Tuple<int,int>>();
            readonly List<Tuple<int,string>> global=new List<Tuple<int,string>>();
            readonly Dictionary<string,int> addresses=new Dictionary<string,int>();
            readonly List<Tuple<int,string>> virtuals=new List<Tuple<int,string>>();
            public Writer(Schema metadata){schema=metadata;}
            int Alloc(int size,int alignment=16)
            {
                if(size<0)throw new InvalidDataException("Negative HKX allocation.");long start=(data.Length+alignment-1)/alignment*alignment;long end=start+size;
                if(end>MaximumBytes)throw new InvalidDataException("HKX output exceeds size limit.");data.SetLength(end);return checked((int)start);
            }
            void Put(int address,byte[] bytes){if(address<0||(long)address+bytes.Length>data.Length)throw new InvalidDataException("HKX writer outside allocation.");data.Position=address;data.Write(bytes,0,bytes.Length);}
            void Put32(int address,uint value){Put(address,BitConverter.GetBytes(value));}
            void WriteObject(XElement input,Class cls,int address,int depth)
            {
                if(depth>64)throw new InvalidDataException("HKX inline nesting limit exceeded.");
                var fields=input.Elements("hkparam").ToDictionary(p=>(string)p.Attribute("name"));var expected=cls.Members.Where(m=>!m.Ignored).Select(m=>m.Name).ToArray();
                if(fields.Keys.Except(expected).Any()||expected.Except(fields.Keys).Any())throw new InvalidDataException("Missing/unknown serialized field(s) in "+cls.Name+": "+string.Join(",",fields.Keys.Except(expected).Concat(expected.Except(fields.Keys))));
                foreach(var m in cls.Members.Where(m=>!m.Ignored))
                {
                    int size=(m.Type=="TYPE_ENUM"||m.Type=="TYPE_FLAGS")?PrimitiveSize(m.Subtype):schema.Size(m,m.Type);
                    if(m.Offset<0||m.Offset+(long)size>cls.Size)throw new InvalidDataException("Schema member exceeds declared size.");WriteValue(fields[m.Name],m,m.Type,address+m.Offset,depth+1);
                }
            }
            void WriteValue(XElement input,Member m,string type,int address,int depth)
            {
                if(IsArray(type))
                {
                    int count=Int((string)input.Attribute("numelements"));if(count<0||count>1000000)throw new InvalidDataException("Unsafe array count.");
                    int stride=schema.Size(m,m.Subtype);int start=count==0?0:Alloc(checked(count*stride));
                    if(type=="TYPE_RELARRAY")
                    {
                        if(count>ushort.MaxValue||(count>0&&(start-address<0||start-address>ushort.MaxValue)))throw new NotSupportedException("RelArray displacement exceeds UInt16 range.");
                        Put(address,BitConverter.GetBytes((ushort)count));Put(address+2,BitConverter.GetBytes((ushort)(count==0?0:start-address)));
                    }
                    else {Put32(address+8,(uint)count);if(type=="TYPE_ARRAY")Put32(address+12,(uint)count|0x80000000);if(count>0)local.Add(Tuple.Create(address,start));}
                    if(m.Subtype=="TYPE_STRUCT")
                    {
                        var rows=input.Elements("hkobject").ToArray();if(rows.Length!=count)throw new InvalidDataException("Struct array count mismatch: "+m.Name);
                        for(int i=0;i<count;i++)WriteObject(rows[i],schema.Get(m.Target),start+i*stride,depth);
                    }
                    else if(IsString(m.Subtype))
                    {
                        var rows=input.Elements("hkcstring").ToArray();if(rows.Length!=count)throw new InvalidDataException("String array count mismatch.");for(int i=0;i<count;i++)WriteValue(rows[i],m,m.Subtype,start+i*stride,depth);
                    }
                    else
                    {
                        string[] values=stride>=16?Regex.Matches(input.Value,@"(?:\([^()]*\)\s*){"+(stride/16)+"}").Cast<Match>().Select(x=>x.Value.Trim()).ToArray():Tokens(input.Value);
                        if(values.Length!=count)throw new InvalidDataException("Primitive/pointer array count mismatch: "+m.Name);
                        for(int i=0;i<count;i++)WriteValue(new XElement("v",values[i]),m,m.Subtype,start+i*stride,depth);
                    }
                    return;
                }
                if(type=="TYPE_STRUCT")
                {
                    var child=input.Elements("hkobject").Single();var cls=schema.Get(m.Target);
                    if((string)child.Attribute("class")!=cls.Name||(string)child.Attribute("signature")!=Signature(cls.Signature)||(string)child.Attribute("name")!=m.Name)throw new InvalidDataException("Inline struct class/name/signature mismatch: "+m.Name);
                    WriteObject(child,cls,address,depth);return;
                }
                if(type=="TYPE_POINTER"){string target=input.Value.Trim();if(target!="null")global.Add(Tuple.Create(address,target));return;}
                if(IsString(type))
                {
                    if(input.Value.IndexOf('\0')>=0)throw new InvalidDataException("Embedded NUL in HKX string.");
                    if(input.Value.Length>0){byte[] bytes=new UTF8Encoding(false,true).GetBytes(input.Value+"\0");int target=Alloc(bytes.Length);Put(target,bytes);local.Add(Tuple.Create(address,target));}return;
                }
                if(type=="TYPE_ENUM"||type=="TYPE_FLAGS"){Put(address,Scalar(m.Subtype,schema.EnumValue(m,input.Value.Trim())));return;}
                Put(address,Scalar(type,input.Value.Trim()));
            }
            byte[] Scalar(string type,string value)
            {
                int size=PrimitiveSize(type);
                if(size>=16)
                {
                    string[] values=Tokens(value.Replace("("," ").Replace(")"," "));if(values.Length!=size/4)throw new InvalidDataException("Vector/matrix lane count mismatch.");
                    var bytes=new byte[size];for(int i=0;i<values.Length;i++)Array.Copy(Scalar("TYPE_REAL",values[i]),0,bytes,i*4,4);return bytes;
                }
                switch(type)
                {
                    case "TYPE_BOOL":if(value!="true"&&value!="false")throw new InvalidDataException("Invalid bool.");return new[]{(byte)(value=="true"?1:0)};
                    case "TYPE_CHAR":case "TYPE_INT8":int narrow8=int.Parse(value,C);if(narrow8<sbyte.MinValue||narrow8>byte.MaxValue)throw new InvalidDataException("Int8 value outside signed/legacy-bit-pattern range.");return new[]{unchecked((byte)narrow8)};
                    case "TYPE_UINT8":return new[]{byte.Parse(value,C)};
                    case "TYPE_INT16":int narrow16=int.Parse(value,C);if(narrow16<short.MinValue||narrow16>ushort.MaxValue)throw new InvalidDataException("Int16 value outside signed/legacy-bit-pattern range.");return BitConverter.GetBytes(unchecked((ushort)narrow16));
                    case "TYPE_UINT16":return BitConverter.GetBytes(ushort.Parse(value,C));
                    case "TYPE_INT32":return BitConverter.GetBytes(int.Parse(value,C));
                    case "TYPE_UINT32":long wide32=long.Parse(value,C);if(wide32<int.MinValue||wide32>uint.MaxValue)throw new InvalidDataException("UInt32 outside native/legacy-bit-pattern range.");return BitConverter.GetBytes(unchecked((uint)wide32));
                    case "TYPE_INT64":return BitConverter.GetBytes(long.Parse(value,C));
                    case "TYPE_UINT64":case "TYPE_ULONG":return BitConverter.GetBytes(ulong.Parse(value,C));
                    case "TYPE_REAL":float f=float.Parse(value,NumberStyles.Float,C);if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Non-finite serialized float.");return BitConverter.GetBytes(f);
                    default:throw new NotSupportedException(type);
                }
            }
            public byte[] Pack(XDocument xml)
            {
                if(xml.Root==null||xml.Root.Name!="hkpackfile"||(string)xml.Root.Attribute("classversion")!="11"||(string)xml.Root.Attribute("contentsversion")!=Sdk)throw new NotSupportedException("Only BB64 Havok 2014.1 packfile XML is supported.");
                string layout=(string)xml.Root.Attribute("nativeLayout")??"8,1,1,1";
                if(layout!="8,1,1,1"&&layout!="8,1,0,1")throw new NotSupportedException("Unsupported nativeLayout. Expected 8,1,1,1 or 8,1,0,1.");
                var sections=xml.Root.Elements("hksection").ToArray();if(sections.Length!=1||(string)sections[0].Attribute("name")!="__data__")throw new NotSupportedException("Expected one __data__ XML section.");
                var objects=sections[0].Elements("hkobject").ToArray();if(objects.Length<1||objects.Length>10000)throw new InvalidDataException("Invalid object count.");
                foreach(var obj in objects)
                {
                    var cls=schema.Get((string)obj.Attribute("class"));if((string)obj.Attribute("signature")!=Signature(cls.Signature))throw new InvalidDataException("Class signature mismatch: "+cls.Name);
                    string id=(string)obj.Attribute("name");if(!Regex.IsMatch(id??"",@"^#\d+$"))throw new InvalidDataException("Invalid XML object ID.");
                    int address=Alloc(cls.Size);addresses.Add(id,address);virtuals.Add(Tuple.Create(address,cls.Name));
                }
                for(int i=0;i<objects.Length;i++)WriteObject(objects[i],schema.Get(virtuals[i].Item2),virtuals[i].Item1,0);
                foreach(var pointer in global)if(!addresses.ContainsKey(pointer.Item2))throw new InvalidDataException("Unresolved object reference: "+pointer.Item2);
                var classBytes=new MemoryStream();var classOffsets=new Dictionary<string,int>();
                Action<string,uint> addClass=(name,signature)=>{if(classOffsets.ContainsKey(name))return;var bytes=BitConverter.GetBytes(signature);classBytes.Write(bytes,0,4);classBytes.WriteByte(9);classOffsets[name]=checked((int)classBytes.Position);bytes=Encoding.ASCII.GetBytes(name+"\0");classBytes.Write(bytes,0,bytes.Length);};
                addClass("hkClass",0x33d42383);addClass("hkClassMember",0xb0efa719);addClass("hkClassEnum",0x8a3609cf);addClass("hkClassEnumItem",0xce6f8a6c);
                foreach(var obj in virtuals)addClass(obj.Item2,schema.Get(obj.Item2).Signature);
                var payload=data.ToArray();
                using(var output=new MemoryStream())using(var writer=new BinaryWriter(output))
                {
                    output.SetLength(0x110);output.Position=0x110;writer.Write(classBytes.ToArray());Pad(writer,16,0xff);int classEnd=(int)output.Position-0x110;
                    int typesBase=(int)output.Position;int dataBase=typesBase;writer.Write(payload);Pad(writer,16,0);
                    int localStart=(int)output.Position-dataBase;
                    foreach(var p in local.OrderBy(p=>p.Item1)){writer.Write(p.Item1);writer.Write(p.Item2);}Pad(writer,16,0xff);
                    int globalStart=(int)output.Position-dataBase;
                    // Fixups are keyed by source, not ordered by address. Keep schema
                    // traversal order for interoperability with legacy cursor-based readers.
                    foreach(var p in global){writer.Write(p.Item1);writer.Write(2);writer.Write(addresses[p.Item2]);}Pad(writer,16,0xff);
                    int virtualStart=(int)output.Position-dataBase;
                    foreach(var obj in virtuals){writer.Write(obj.Item1);writer.Write(0);writer.Write(classOffsets[obj.Item2]);}Pad(writer,16,0xff);
                    int end=(int)output.Position-dataBase;
                    var root=virtuals.Single(v=>v.Item2=="hkRootLevelContainer");
                    output.Position=0;writer.Write(new byte[]{0x57,0xe0,0xe0,0x57,0x10,0xc0,0xc0,0x10});writer.Write(0);writer.Write(11);writer.Write(new byte[]{8,1,(byte)(layout=="8,1,0,1"?0:1),1});writer.Write(3);writer.Write(2);writer.Write(root.Item1);writer.Write(0);writer.Write(classOffsets[root.Item2]);
                    writer.Write(Encoding.ASCII.GetBytes(Sdk));writer.Write(new byte[]{0,0xff,0,0,0,0});writer.Write((ushort)21);writer.Write((ushort)16);writer.Write(20);writer.Write(0);writer.Write(0);writer.Write(0);
                    SectionHeader(writer,0x50,"__classnames__",0x110,classEnd,classEnd,classEnd,classEnd);
                    SectionHeader(writer,0x90,"__types__",typesBase,0,0,0,0);
                    SectionHeader(writer,0xd0,"__data__",dataBase,localStart,globalStart,virtualStart,end);
                    if(output.Length>MaximumBytes)throw new InvalidDataException("HKX output exceeds size limit.");return output.ToArray();
                }
            }
            static void Pad(BinaryWriter writer,int alignment,byte value){while(writer.BaseStream.Position%alignment!=0)writer.Write(value);}
            static void SectionHeader(BinaryWriter writer,int address,string name,int start,int local,int global,int virtuals,int end)
            {
                writer.BaseStream.Position=address;var bytes=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(bytes,0);bytes[19]=0xff;writer.Write(bytes);
                writer.Write(start);writer.Write(local);writer.Write(global);writer.Write(virtuals);writer.Write(end);writer.Write(end);writer.Write(end);writer.Write(new byte[16]);
            }
        }
        public static void AssertEquivalent(XDocument before,XDocument after)
        {
            var a=Normalize(before);var b=Normalize(after);Compare(a,b,"hkpackfile");
        }
        static XElement Normalize(XDocument document)
        {
            var root=new XElement(document.Root);var objects=root.Elements("hksection").Elements("hkobject").ToArray();
            var schema=ForLayout((string)root.Attribute("nativeLayout")??"8,1,1,1");
            root.Attribute("nativeLayout")?.Remove(); // local header-layout extension is not a Java/Havok XML field
            foreach(var obj in objects)NormalizeNarrowValues(obj,schema.Get((string)obj.Attribute("class")),schema);
            var ids=objects.Select((o,i)=>new{Id=(string)o.Attribute("name"),Value="#"+(i+1).ToString(C)}).ToDictionary(o=>o.Id,o=>o.Value);
            foreach(var obj in objects)NormalizeReferences(obj,schema.Get((string)obj.Attribute("class")),ids,schema);
            foreach(var obj in root.Descendants("hkobject"))
            {
                string id=(string)obj.Attribute("name");if(id!=null&&ids.ContainsKey(id))obj.SetAttributeValue("name",ids[id]);
                foreach(var node in obj.Nodes().OfType<XComment>().ToArray())node.Remove();
                var elements=obj.Elements().OrderBy(e=>(string)e.Attribute("name"),StringComparer.Ordinal).ToArray();obj.RemoveNodes();obj.Add(elements);
            }
            return root;
        }
        static void NormalizeReferences(XElement obj,Class cls,Dictionary<string,string> ids,Schema schema)
        {
            foreach(var m in cls.Members.Where(m=>!m.Ignored))
            {
                var p=obj.Elements("hkparam").FirstOrDefault(e=>(string)e.Attribute("name")==m.Name);if(p==null)continue;
                if(m.Type=="TYPE_STRUCT"||(IsArray(m.Type)&&m.Subtype=="TYPE_STRUCT"))foreach(var child in p.Elements("hkobject"))NormalizeReferences(child,schema.Get(m.Target),ids,schema);
                else if(m.Type=="TYPE_POINTER"||(IsArray(m.Type)&&m.Subtype=="TYPE_POINTER"))p.Value=string.Join(" ",Tokens(p.Value).Select(v=>v=="null"?v:ids[v]));
            }
        }
        static void NormalizeNarrowValues(XElement obj,Class cls,Schema schema)
        {
            foreach(var m in cls.Members.Where(m=>!m.Ignored))
            {
                var p=obj.Elements("hkparam").FirstOrDefault(e=>(string)e.Attribute("name")==m.Name);if(p==null)continue;
                if(m.Type=="TYPE_STRUCT"){foreach(var child in p.Elements("hkobject"))NormalizeNarrowValues(child,schema.Get(m.Target),schema);continue;}
                if(IsArray(m.Type)&&m.Subtype=="TYPE_STRUCT"){foreach(var child in p.Elements("hkobject"))NormalizeNarrowValues(child,schema.Get(m.Target),schema);continue;}
                string type=IsArray(m.Type)?m.Subtype:m.Type;
                if(type=="TYPE_INT8"||type=="TYPE_CHAR"||type=="TYPE_INT16")
                {
                    int bits=type=="TYPE_INT16"?16:8;
                    p.Value=string.Join(" ",Tokens(p.Value).Select(v=>{int n=Int(v);if(n<-(1<<(bits-1))||n>=(1<<bits))throw new InvalidDataException("Narrow integer outside bit range.");return (n&((1<<bits)-1)).ToString(C);}));
                }
                else if(type=="TYPE_UINT32")
                {
                    p.Value=string.Join(" ",Tokens(p.Value).Select(v=>{long n=long.Parse(v,C);if(n<int.MinValue||n>uint.MaxValue)throw new InvalidDataException("UInt32 outside native bit range.");return unchecked((int)n).ToString(C);}));
                }
            }
        }
        static void Compare(XElement a,XElement b,string path)
        {
            if(a.Name!=b.Name||!a.Attributes().OrderBy(x=>x.Name.ToString()).Select(x=>x.Name+"="+x.Value).SequenceEqual(b.Attributes().OrderBy(x=>x.Name.ToString()).Select(x=>x.Name+"="+x.Value)))throw new InvalidDataException("HKX roundtrip attributes differ: "+path);
            var ac=a.Elements().ToArray();var bc=b.Elements().ToArray();if(ac.Length!=bc.Length)throw new InvalidDataException("HKX roundtrip child count differs: "+path);
            if(ac.Length==0)
            {
                string x=string.Join(" ",Tokens(a.Value)),y=string.Join(" ",Tokens(b.Value));
                if(x!=y)
                {
                    string[] ax=Tokens(x.Replace("("," ").Replace(")"," ")),bx=Tokens(y.Replace("("," ").Replace(")"," "));
                    if(ax.Length!=bx.Length)throw new InvalidDataException("HKX roundtrip values differ: "+path);
                    for(int i=0;i<ax.Length;i++)
                    {
                        decimal ai,bi;float af,bf;
                        if(Regex.IsMatch(ax[i],@"^-?\d+$")&&Regex.IsMatch(bx[i],@"^-?\d+$")&&decimal.TryParse(ax[i],NumberStyles.Integer,C,out ai)&&decimal.TryParse(bx[i],NumberStyles.Integer,C,out bi)) {if(ai==bi)continue;}
                        else if(float.TryParse(ax[i],NumberStyles.Float,C,out af)&&float.TryParse(bx[i],NumberStyles.Float,C,out bf)&&af==bf)continue;
                        throw new InvalidDataException("HKX roundtrip value differs: "+path+" ("+ax[i]+" vs "+bx[i]+")");
                    }
                }
            }
            for(int i=0;i<ac.Length;i++)Compare(ac[i],bc[i],path+"/"+((string)ac[i].Attribute("name")??ac[i].Name.ToString()));
        }
    }
}
