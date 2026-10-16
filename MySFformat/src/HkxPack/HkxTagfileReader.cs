// TAG0 20180100 reader. Format informed by The12thAvenger/HKLib (MIT;
// see tools/Cloth/LICENSE-HKLib.txt). No generated SDK classes or runtime DLL.
// The file's own TYPE metadata is bounded and interpreted, never executed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MySFformat
{
    public sealed class HkxTagfileReader
    {
        sealed class Section { public int Start,End; }
        sealed class Field { public string Name; public int Flags,Offset,Type; }
        sealed class TypeInfo
        {
            public string Name; public int Parent,Format=-1,Size=-1,Sub=-1;
            public List<Field> Fields=new List<Field>();
        }
        sealed class Item { public int Type,Kind,Offset,Count; }
        readonly byte[] bytes;
        TypeInfo[] types;Item[] items;
        int cursor,limit,dataStart,dataEnd,budget=4000000;
        readonly HashSet<int> pointers=new HashSet<int>();
        readonly HashSet<int> patches=new HashSet<int>();
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        public static bool IsTagfile(byte[] data){return data!=null&&data.Length>=8&&Encoding.ASCII.GetString(data,4,4)=="TAG0";}
        public HkxTagfileReader(byte[] data){bytes=data??throw new ArgumentNullException("data");}
        void Bounds(int p,long n){if(p<0||n<0||p+(long)n>bytes.Length)throw new InvalidDataException("TAG0 access outside file.");}
        uint U32(int p){Bounds(p,4);return BitConverter.ToUInt32(bytes,p);}
        ulong U64(int p){Bounds(p,8);return BitConverter.ToUInt64(bytes,p);}
        int Index(uint value,int count){if(value>=count)throw new InvalidDataException("TAG0 index outside table.");return (int)value;}
        Dictionary<string,Section> Sections(int start,int end)
        {
            var result=new Dictionary<string,Section>();
            for(int p=start;p<end;)
            {
                Bounds(p,8);uint word=((uint)bytes[p]<<24)|((uint)bytes[p+1]<<16)|((uint)bytes[p+2]<<8)|bytes[p+3];
                int length=(int)(word&0x3fffffff);if(length<8||p+(long)length>end)throw new InvalidDataException("Invalid TAG0 section length.");
                string name=Encoding.ASCII.GetString(bytes,p+4,4);if(result.ContainsKey(name))throw new InvalidDataException("Duplicate TAG0 section.");
                result.Add(name,new Section{Start=p+8,End=p+length});p+=length;
            }
            return result;
        }
        Section Required(Dictionary<string,Section> sections,string name){Section s;if(!sections.TryGetValue(name,out s))throw new NotSupportedException("Required TAG0 section missing: "+name);return s;}
        void Seek(Section s){cursor=s.Start;limit=s.End;}
        int Var()
        {
            if(cursor>=limit)throw new InvalidDataException("Truncated TAG0 variable integer.");
            int first=bytes[cursor++],bits,size;
            if(first<128){bits=7;size=1;}else if(first<192){bits=14;size=2;}else if(first<224){bits=21;size=3;}
            else if(first<232){bits=27;size=4;}else if(first<240){bits=35;size=5;}
            else if(first<248){bits=59;size=8;}else if(first==248){bits=40;size=6;}else if(first==249){bits=64;size=9;}
            else throw new InvalidDataException("Invalid TAG0 variable integer prefix.");
            if(cursor+(long)size-1>limit)throw new InvalidDataException("Truncated TAG0 variable integer.");
            ulong value=(ulong)(first&((1<<(bits%8))-1));
            for(int i=1;i<size;i++){if(value>(ulong)int.MaxValue>>8)throw new InvalidDataException("Oversized TAG0 metadata integer.");value=(value<<8)|bytes[cursor++];}
            if(value>int.MaxValue)throw new InvalidDataException("Oversized TAG0 metadata integer.");return (int)value;
        }
        string[] Strings(Section s)
        {
            var result=new List<string>();int p=s.Start;var utf8=new UTF8Encoding(false,true);
            while(p<s.End){int end=p;while(end<s.End&&bytes[end]!=0)end++;if(end==s.End)throw new InvalidDataException("Unterminated TAG0 metadata string.");result.Add(utf8.GetString(bytes,p,end-p));p=end+1;}
            return result.ToArray();
        }
        TypeInfo T(int id){if(id<=0||id>=types.Length)throw new InvalidDataException("Invalid TAG0 type.");return types[id];}
        int Effective(int id,int which)
        {
            var seen=new HashSet<int>();
            while(id!=0){if(!seen.Add(id))throw new InvalidDataException("Cyclic TAG0 type inheritance.");var t=T(id);int v=which==0?t.Format:which==1?t.Size:t.Sub;if(v>=0)return v;id=t.Parent;}
            return which==2?0:throw new InvalidDataException("Incomplete TAG0 type description.");
        }
        IEnumerable<Field> Fields(int id,int depth=0)
        {
            if(depth>64)throw new InvalidDataException("TAG0 inheritance too deep.");var t=T(id);
            if(t.Parent!=0)foreach(var f in Fields(t.Parent,depth+1))yield return f;
            foreach(var f in t.Fields)yield return f;
        }
        bool Derives(int id,int expected)
        {
            for(int i=0;i<64&&id!=0;i++){if(id==expected)return true;id=T(id).Parent;}return false;
        }
        bool NumericAggregate(int id)
        {
            string n=T(id).Name;return n.StartsWith("hkVector4",StringComparison.Ordinal)||n.StartsWith("hkQuaternion",StringComparison.Ordinal)||n.StartsWith("hkMatrix",StringComparison.Ordinal)||n.StartsWith("hkTransform",StringComparison.Ordinal)||n.StartsWith("hkQsTransform",StringComparison.Ordinal);
        }
        void DataBounds(int address,int size){if(address<dataStart||size<0||address+(long)size>dataEnd)throw new InvalidDataException("TAG0 value outside DATA section.");Bounds(address,size);}
        Item Pointer(int address,int expected,bool array)
        {
            DataBounds(address,8);pointers.Add(address);ulong index=U64(address);if(index==0)return null;
            if(index>=(ulong)items.Length)throw new InvalidDataException("Invalid TAG0 item reference.");var item=items[(int)index];
            if(array?item.Kind!=2:item.Kind!=1||item.Count!=1)throw new InvalidDataException("TAG0 pointer/array item kind mismatch.");
            if(expected!=0&&!Derives(item.Type,expected)&&!Derives(expected,item.Type))throw new InvalidDataException("TAG0 pointer type mismatch.");
            return item;
        }
        string Scalar(int type,int p)
        {
            int format=Effective(type,0),kind=format&15,size=Effective(type,1);DataBounds(p,size);
            if((format&256)!=0&&(kind==4||kind==5))throw new NotSupportedException("Big-endian TAG0 scalar.");
            if(kind==2){ulong b=size==1?bytes[p]:size==4?U32(p):size==8?U64(p):throw new NotSupportedException("TAG0 boolean width.");if(b>1)throw new InvalidDataException("Invalid TAG0 boolean.");return b==0?"false":"true";}
            if(kind==4)
            {
                bool signed=(format&512)!=0;
                if(size==1)return signed?unchecked((sbyte)bytes[p]).ToString(C):bytes[p].ToString(C);
                if(size==2)return signed?BitConverter.ToInt16(bytes,p).ToString(C):BitConverter.ToUInt16(bytes,p).ToString(C);
                if(size==4)return signed?BitConverter.ToInt32(bytes,p).ToString(C):U32(p).ToString(C);
                if(size==8)return signed?BitConverter.ToInt64(bytes,p).ToString(C):U64(p).ToString(C);
            }
            if(kind==5)
            {
                double value;if(size==4)value=BitConverter.ToSingle(bytes,p);else if(size==8)value=BitConverter.ToDouble(bytes,p);
                else if(size==2){var lane=new byte[4];lane[2]=bytes[p];lane[3]=bytes[p+1];value=BitConverter.ToSingle(lane,0);}else throw new NotSupportedException("TAG0 float width.");
                if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidDataException("Nonfinite TAG0 scalar.");
                return size==4?((float)value).ToString("R",C):value.ToString("R",C);
            }
            throw new NotSupportedException("Unsupported TAG0 scalar: "+T(type).Name);
        }
        string String(int type,int p)
        {
            int format=Effective(type,0);if((format&16)!=0){int n=format>>16;DataBounds(p,n);return new UTF8Encoding(false,true).GetString(bytes,p,n);}
            var item=Pointer(p,0,true);if(item==null)return "";
            if(Effective(item.Type,1)!=1||item.Count<1||bytes[item.Offset+item.Count-1]!=0)throw new InvalidDataException("Invalid TAG0 string item.");
            return new UTF8Encoding(false,true).GetString(bytes,item.Offset,item.Count-1);
        }
        string Reference(int type,int p)
        {
            var item=Pointer(p,Effective(type,2),false);if(item==null)return "null";
            int index=checked((int)U64(p));return "#"+index.ToString(C);
        }
        string Flat(int type,int p,int depth)
        {
            if(depth>64)throw new InvalidDataException("TAG0 aggregate too deep.");int kind=Effective(type,0)&15;
            if(kind==7)return string.Join(" ",Fields(type).Where(f=>(f.Flags&1)==0).Select(f=>Flat(f.Type,p+f.Offset,depth+1)));
            if(kind==8){int n=Effective(type,0)>>8,sub=Effective(type,2),stride=Effective(sub,1);if(n<1||n>256)throw new InvalidDataException("Invalid fixed aggregate length.");return string.Join(" ",Enumerable.Range(0,n).Select(i=>Flat(sub,p+i*stride,depth+1)));}
            return Scalar(type,p);
        }
        string NumericText(int type,int p,int depth)
        {
            var lanes=Flat(type,p,depth).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(lanes.Length%4!=0)throw new InvalidDataException("TAG0 numeric aggregate lane count.");
            return string.Concat(Enumerable.Range(0,lanes.Length/4).Select(i=>"("+string.Join(" ",lanes.Skip(i*4).Take(4))+")"));
        }
        string Name(int type){return T(type).Name.Replace("::","");}
        XElement Record(int type,int p,string name,int depth)
        {
            if(depth>64||--budget<0)throw new InvalidDataException("TAG0 expansion limit.");DataBounds(p,Effective(type,1));
            var obj=new XElement("hkobject",new XAttribute("class",Name(type)),new XAttribute("name",name));
            foreach(var f in Fields(type))
            {
                if((f.Flags&1)!=0)continue;
                int size=Effective(f.Type,1);if(f.Offset<0||f.Offset+(long)size>Effective(type,1))throw new InvalidDataException("TAG0 field outside record.");
                AddField(obj,f.Name,f.Type,p+f.Offset,depth+1);
            }
            if(obj.Elements().Select(e=>(string)e.Attribute("name")).Distinct().Count()!=obj.Elements().Count())throw new InvalidDataException("Duplicate expanded TAG0 field.");
            return obj;
        }
        void AddField(XElement obj,string name,int type,int p,int depth)
        {
            if(depth>64||--budget<0)throw new InvalidDataException("TAG0 expansion limit.");
            int format=Effective(type,0),kind=format&15;DataBounds(p,Effective(type,1));
            if(kind==8&&(format>>8)>0&&!NumericAggregate(type))
            {
                int n=format>>8,sub=Effective(type,2),stride=Effective(sub,1);if(n>4096)throw new InvalidDataException("Oversized TAG0 fixed array.");
                for(int i=0;i<n;i++)AddField(obj,name+(i+1).ToString(C),sub,p+i*stride,depth+1);return;
            }
            var field=new XElement("hkparam",new XAttribute("name",name));obj.Add(field);
            if(NumericAggregate(type)){field.Value=NumericText(type,p,depth+1);return;}
            if(kind==2||kind==4||kind==5){field.Value=Scalar(type,p);return;}
            if(kind==3){field.Value=String(type,p);return;}
            if(kind==6){field.Value=Reference(type,p);return;}
            if(kind==7){field.Add(Record(type,p,name,depth+1));return;}
            if(kind==8)
            {
                if(T(type).Name=="hkPropertyBag"){if(U64(p)!=0)throw new NotSupportedException("Nonempty TAG0 property bag.");pointers.Add(p);field.Value="null";return;}
                if(T(type).Name!="hkArray")throw new NotSupportedException("Unsupported TAG0 container: "+T(type).Name);
                if(U64(p+8)!=0)throw new InvalidDataException("Nonzero TAG0 array reserved words.");int sub=Effective(type,2);var item=Pointer(p,sub,true);
                int count=item==null?0:item.Count;field.SetAttributeValue("numelements",count);
                if(item==null)return;var tokens=new List<string>();int stride=Effective(item.Type,1),elementKind=Effective(item.Type,0)&15;
                for(int i=0;i<count;i++)
                {
                    int address=item.Offset+i*stride;
                    if(NumericAggregate(item.Type))tokens.Add(NumericText(item.Type,address,depth+1));
                    else if(elementKind==7)field.Add(Record(item.Type,address,name,depth+1));
                    else if(elementKind==6)tokens.Add(Reference(item.Type,address));
                    else if(elementKind==3)field.Add(new XElement("hkcstring",String(item.Type,address)));
                    else if(elementKind==8){var nested=new XElement("hkobject",new XAttribute("class",Name(item.Type)),new XAttribute("name",name));AddField(nested,"items",item.Type,address,depth+1);field.Add(nested);}
                    else tokens.Add(Scalar(item.Type,address));
                }
                if(tokens.Count>0)field.Value=string.Join(" ",tokens);return;
            }
            throw new NotSupportedException("Unsupported TAG0 field: "+T(type).Name);
        }
        public XDocument Read()
        {
            if(bytes.Length>128*1024*1024||!IsTagfile(bytes))throw new InvalidDataException("Invalid or oversized TAG0 file.");
            var top=Sections(0,bytes.Length);var root=Required(top,"TAG0");var chunks=Sections(root.Start,root.End);var sdk=Required(chunks,"SDKV");
            if(sdk.End-sdk.Start!=8||Encoding.ASCII.GetString(bytes,sdk.Start,8)!="20180100")throw new NotSupportedException("Only TAG0 SDK 20180100 Cloth is supported.");
            var data=Required(chunks,"DATA");dataStart=data.Start;dataEnd=data.End;
            var meta=Required(chunks,"TYPE");var typeSections=Sections(meta.Start,meta.End);var names=Strings(Required(typeSections,"TSTR"));var fields=Strings(Required(typeSections,"FSTR"));
            Seek(Required(typeSections,"TNA1"));int count=Var();if(count<2||count>8192)throw new InvalidDataException("Unsafe TAG0 type count.");types=new TypeInfo[count];types[0]=new TypeInfo{Name="NULL"};
            for(int i=1;i<count;i++){int n=Var();if(n>=names.Length)throw new InvalidDataException("Invalid TAG0 type name index.");types[i]=new TypeInfo{Name=names[n]};int parameters=Var();if(parameters>32)throw new InvalidDataException("Too many TAG0 template parameters.");for(int j=0;j<parameters;j++){int key=Var(),value=Var();if(key>=names.Length)throw new InvalidDataException("Invalid TAG0 template name.");}}
            Seek(Required(typeSections,"TBDY"));var definitions=new HashSet<int>();
            for(int i=1;i<count;i++)
            {
                int id=Var();if(id==0)break;if(!definitions.Add(id))throw new InvalidDataException("Duplicate TAG0 type body.");var t=T(id);t.Parent=Var();if(t.Parent>=count)throw new InvalidDataException("Invalid TAG0 parent.");int optional=Var();if((optional&~127)!=0)throw new NotSupportedException("TAG0 attributes are not supported.");
                if((optional&1)!=0)t.Format=Var();if((optional&2)!=0)t.Sub=Var();if((optional&4)!=0)Var();if((optional&8)!=0){t.Size=Var();Var();}if((optional&16)!=0)Var();
                if((optional&32)!=0){int members=Var()&65535;if(members>4096)throw new InvalidDataException("Too many TAG0 fields.");for(int j=0;j<members;j++){int key=Var();if(key>=fields.Length)throw new InvalidDataException("Invalid TAG0 field name.");t.Fields.Add(new Field{Name=fields[key],Flags=Var(),Offset=Var(),Type=Var()});}}
                if((optional&64)!=0){int interfaces=Var();if(interfaces>256)throw new InvalidDataException("Too many TAG0 interfaces.");for(int j=0;j<interfaces;j++){Var();Var();}}
            }
            var index=Required(chunks,"INDX");var indices=Sections(index.Start,index.End);var itemSection=Required(indices,"ITEM");if((itemSection.End-itemSection.Start)%12!=0)throw new InvalidDataException("Invalid TAG0 ITEM length.");
            int itemCount=(itemSection.End-itemSection.Start)/12;if(itemCount<2||itemCount>100000)throw new InvalidDataException("Unsafe TAG0 item count.");items=new Item[itemCount];long expanded=0;
            for(int i=0;i<itemCount;i++)
            {
                int p=itemSection.Start+i*12;uint word=U32(p);int type=(int)(word&0xffffff),kind=(int)(word>>28),offset=checked((int)U32(p+4)),n=checked((int)U32(p+8));
                if(i==0){if(word!=0||offset!=0||n!=0)throw new InvalidDataException("Nonzero TAG0 null item.");items[i]=new Item();continue;}
                if((word&0x0f000000)!=0||(kind!=1&&kind!=2)||n<1||n>2000000)throw new InvalidDataException("Invalid TAG0 item descriptor.");
                int stride=Effective(type,1);if(stride<1||offset+(long)n*stride>dataEnd-dataStart)throw new InvalidDataException("TAG0 item outside DATA.");expanded+=n;if(expanded>4000000)throw new InvalidDataException("Oversized TAG0 object graph.");
                items[i]=new Item{Type=type,Kind=kind,Offset=dataStart+offset,Count=n};
            }
            var section=new XElement("hksection",new XAttribute("name","__data__"));
            for(int i=1;i<items.Length;i++)if(items[i].Kind==1)
            {
                if(items[i].Count!=1||(Effective(items[i].Type,0)&15)!=7)throw new NotSupportedException("TAG0 top-level item is not a record.");
                section.Add(Record(items[i].Type,items[i].Offset,"#"+i.ToString(C),0));
            }
            if(items[1].Kind!=1||T(items[1].Type).Name!="hkRootLevelContainer")throw new InvalidDataException("TAG0 root is not hkRootLevelContainer.");
            // Every serialized patch slot must have been visited by a typed pointer read.
            var patchSection=Required(indices,"PTCH");int patch=patchSection.Start;
            while(patch<patchSection.End)
            {
                if(patch+8>patchSection.End)throw new InvalidDataException("Truncated TAG0 patch group.");T(checked((int)U32(patch)));int n=checked((int)U32(patch+4));patch+=8;
                if(n<0||patch+(long)n*4>patchSection.End)throw new InvalidDataException("Invalid TAG0 patch count.");
                for(int j=0;j<n;j++){int address=checked(dataStart+(int)U32(patch));patch+=4;DataBounds(address,4);if(!patches.Add(address)||!pointers.Contains(address))throw new InvalidDataException("Unvisited or duplicate TAG0 pointer patch at DATA+"+(address-dataStart).ToString(C)+".");}
            }
            foreach(int p in pointers)if(U64(p)!=0&&!patches.Contains(p))throw new InvalidDataException("TAG0 pointer is missing its patch entry.");
            return new XDocument(new XElement("hkpackfile",new XAttribute("classversion",11),new XAttribute("contentsversion","hk_2018.1.0"),new XAttribute("sourceFormat","TAG0-20180100"),section));
        }
    }
}
