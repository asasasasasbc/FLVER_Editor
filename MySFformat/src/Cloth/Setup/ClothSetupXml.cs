using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace MySFformat
{
    // New runtime graphs are constructed from audited field definitions, not game assets.
    internal sealed class ClothSetupXml
    {
        readonly Dictionary<string,XElement> classes;
        readonly XElement section=new XElement("hksection",new XAttribute("name","__data__"));
        int next=1;
        internal ClothSetupXml()
        {
            using(var stream=typeof(HkxPackfileCodec).Assembly.GetManifestResourceStream("MySFformat.NativeClothSchema.xml"))
                classes=XDocument.Load(stream).Root.Elements("class").ToDictionary(e=>(string)e.Attribute("name"));
        }
        internal XDocument Document {get{
            foreach(var o in section.Descendants("hkobject").Where(o=>o.Parent.Name=="hkparam"&&o.Attribute("class")!=null))o.SetAttributeValue("name",(string)o.Parent.Attribute("name"));
            return new XDocument(new XElement("hkpackfile",new XAttribute("classversion","11"),new XAttribute("contentsversion","hk_2014.1.0-r1"),new XAttribute("nativeLayout","8,1,1,1"),section));}}
        internal XElement New(string type)
        {
            var node=Inline(type);node.SetAttributeValue("name","#"+next++);section.Add(node);return node;
        }
        internal XElement Inline(string type)
        {
            var definition=classes[type];var node=new XElement("hkobject",new XAttribute("class",type),new XAttribute("signature","0x"+Convert.ToUInt32(((string)definition.Attribute("signature")).Substring(2),16).ToString("x",CultureInfo.InvariantCulture)));
            var parent=(string)definition.Attribute("parent");
            if(!string.IsNullOrEmpty(parent))node.Add(Inline(parent).Elements().Select(e=>new XElement(e)));
            foreach(var member in definition.Elements("members").Elements("member"))
            {
                if(((string)member.Attribute("flags")??"").Contains("SERIALIZE_IGNORED"))continue;
                int count=(int)member.Attribute("arrsize");
                for(int i=0;i<Math.Max(1,count);i++)
                {
                    string name=(string)member.Attribute("name")+(count>0?(i+1).ToString():"");
                    var p=new XElement("hkparam",new XAttribute("name",name));node.Add(p);
                    string t=(string)member.Attribute("vtype");
                    if(t=="TYPE_STRUCT")p.Add(Inline((string)member.Attribute("ctype")));
                    else if(t=="TYPE_ARRAY"||t=="TYPE_SIMPLEARRAY"||t=="TYPE_RELARRAY")p.SetAttributeValue("numelements",0);
                    else if(t=="TYPE_BOOL")p.Value="false";
                    else if(t=="TYPE_STRINGPTR"||t=="TYPE_CSTRING")p.Value="";
                    else if(t=="TYPE_POINTER")p.Value="null";
                    else if(t=="TYPE_VECTOR4")p.Value="(0 0 0 0)";
                    else if(t=="TYPE_QUATERNION")p.Value="(0 0 0 1)";
                    else if(t=="TYPE_QSTRANSFORM")p.Value="(0 0 0 1)(0 0 0 1)(1 1 1 0)";
                    else if(t=="TYPE_MATRIX4"||t=="TYPE_TRANSFORM")p.Value="(1 0 0 0)(0 1 0 0)(0 0 1 0)(0 0 0 1)";
                    else p.Value="0";
                }
            }
            return node;
        }
        internal static XElement F(XElement o,string n){return ClothDocument.Field(o,n);}
        internal static string Ref(XElement o){return (string)o.Attribute("name");}
        internal static string S(object value){return Convert.ToString(value,CultureInfo.InvariantCulture);}
        internal static void Set(XElement o,string n,object value){F(o,n).Value=S(value);}
        internal static void Array(XElement o,string n,IEnumerable<object> values)
        {
            var a=values.ToArray();var p=F(o,n);p.RemoveNodes();p.SetAttributeValue("numelements",a.Length);
            if(a.Length>0)p.Value=string.Join(" ",a.Select(S));
        }
        internal static void Records(XElement o,string n,IEnumerable<XElement> values)
        {
            var a=values.ToArray();var p=F(o,n);p.RemoveNodes();p.SetAttributeValue("numelements",a.Length);p.Add(a);
        }
        internal static XElement Row(params object[] pairs)
        {
            var row=new XElement("hkobject");for(int i=0;i<pairs.Length;i+=2)row.Add(new XElement("hkparam",new XAttribute("name",pairs[i]),S(pairs[i+1])));return row;
        }
    }
}
