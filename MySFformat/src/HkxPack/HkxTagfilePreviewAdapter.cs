// Preview-only 2018 -> 2014 CPU compatibility view. Not an ER asset exporter.
// Keep all physical operators/constraints; reject unsupported active features.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MySFformat
{
    public sealed class HkxTagfilePreviewAdapter
    {
        readonly Dictionary<string,XElement> schema;
        readonly Dictionary<string,XElement> objects;
        readonly Queue<string> pending=new Queue<string>();
        readonly HashSet<string> enqueued=new HashSet<string>();
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        HkxTagfilePreviewAdapter(XDocument xml)
        {
            if((string)xml.Root.Attribute("sourceFormat")!="TAG0-20180100")throw new ArgumentException("Expected ER TAG0 view.");
            using(var input=typeof(HkxPackfileCodec).Assembly.GetManifestResourceStream("MySFformat.NativeDs3ClothSchema.xml"))
            {if(input==null)throw new InvalidDataException("Missing compatibility schema.");schema=XDocument.Load(input).Root.Elements("class").ToDictionary(c=>(string)c.Attribute("name"));}
            objects=xml.Root.Element("hksection").Elements("hkobject").ToDictionary(o=>(string)o.Attribute("name"));
        }
        static XElement F(XElement o,string name){return o.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")==name);}
        static string V(XElement o,string name){var p=F(o,name);if(p==null)throw new NotSupportedException("Missing ER preview input: "+(string)o.Attribute("class")+"."+name);return p.Value.Trim();}
        static void RequireEmpty(XElement p,string reason){if(p!=null&&((int?)p.Attribute("numelements")!=0||p.HasElements||p.Value.Trim().Length!=0))throw new NotSupportedException(reason);}
        IEnumerable<XElement> Members(string cls)
        {
            XElement c;if(!schema.TryGetValue(cls,out c))throw new NotSupportedException("2014 HCT compatibility class is not implemented: "+cls);
            string parent=(string)c.Attribute("parent");if(!string.IsNullOrEmpty(parent))foreach(var m in Members(parent))yield return m;
            foreach(var m in c.Elements("members").Elements("member"))yield return m;
        }
        void Reference(string text)
        {
            foreach(string id in text.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
            {if(id=="null")continue;if(!objects.ContainsKey(id))throw new InvalidDataException("Unknown ER object reference.");if(enqueued.Add(id))pending.Enqueue(id);}
        }
        XElement Prepared(XElement original)
        {
            var result=new XElement(original);string cls=(string)result.Attribute("class");
            // hkPackedVector3[16] is exactly the old short[64] storage. Preserve
            // all four words per vector; do not decode/requantize or drop weights.
            if(cls.Contains("DeformerLocalBlock"))
            {
                foreach(string prefix in new[]{"localPosition","localNormal","localTangent","localBiTangent"})
                {
                    var packed=Enumerable.Range(1,16).Select(i=>F(result,prefix+i.ToString(C))).ToArray();
                    if(packed[0]==null||packed[0].Elements("hkobject").All(o=>(string)o.Attribute("class")!="hkPackedVector3"))continue;
                    var words=new List<string>();
                    foreach(var p in packed)
                    {
                        if(p==null)throw new InvalidDataException("Incomplete ER packed vector block.");var vector=p.Elements("hkobject").Single();
                        for(int i=1;i<=4;i++)words.Add(V(vector,"values"+i.ToString(C)));
                    }
                    foreach(var p in packed)p.Remove();for(int i=0;i<words.Count;i++)result.Add(new XElement("hkparam",new XAttribute("name",prefix+(i+1).ToString(C)),words[i]));
                }
            }
            return result;
        }
        bool Ignorable(string cls,XElement p)
        {
            string key=(string)p.Attribute("name");
            if(key=="propertyBag"){if(p.Value.Trim()!="null")throw new NotSupportedException("Nonempty ER property bag.");return true;}
            if(key=="constraintId")return true; // stable author handle, not a solver operation
            if(key=="operatorID"||key=="usedBuffers"||key=="usedTransformSets")return cls.EndsWith("Operator",StringComparison.Ordinal);
            if(cls=="hclClothState"&&key=="dependencyGraph")return true; // preserve serial operators, omit job scheduling only
            if(cls=="hclClothData"&&key=="stateTransitions"){RequireEmpty(p,"ER state transitions are not supported by 2014 preview.");return true;}
            if(cls=="hclSimulateOperator"&&key=="simulateOpConfigs")return true;
            if(cls=="hclSimClothData")
            {
                if(new[]{"simOpIds","transferMotionEnabled","landscapeCollisionEnabled","pinchDetectionEnabled"}.Contains(key))return true;
                if(key=="virtualCollisionPointsData")
                {var data=p.Elements("hkobject").Single();if(V(data,"numVCPoints")!="0"||V(data,"numLandscapeVCPoints")!="0")throw new NotSupportedException("Active ER virtual collision points require a 2018 solver; they are not omitted.");return true;}
            }
            if(cls=="hclSimClothDataLandscapeCollisionData"&&key=="collisionTolerance")return true;
            if((cls=="hclObjectSpaceDeformer"||cls=="hclBoneSpaceDeformer")&&new[]{"fiveBlendEntries","sixBlendEntries","sevenBlendEntries","eightBlendEntries"}.Contains(key))
            {RequireEmpty(p,"ER 5–8 bone skinning influences require a 2018 deformer; weights are not truncated.");return true;}
            if(key=="customSkinDeform"){if(p.Value.Trim()!="false")throw new NotSupportedException("ER custom skin deform is not supported by 2014 preview.");return true;}
            if(cls=="hclBendStiffnessConstraintSet"&&(key=="clampBendStiffness"||key=="maxRestPoseHeightSq"))return true; // explicitly labeled 2014 bend approximation
            if(cls=="hclCollidable")
            {
                if(key=="userData")return true;
                if(key=="enabled"){if(p.Value.Trim()!="true")throw new NotSupportedException("Disabled ER collidables are not supported by 2014 preview.");return true;}
                if(key=="virtualCollisionPointCollisionEnabled"){if(p.Value.Trim()!="false")throw new NotSupportedException("ER virtual collision point collider mode is not supported.");return true;}
            }
            return false;
        }
        XElement Record(XElement original,string cls,XElement sim=null)
        {
            var source=Prepared(original);if(cls=="hclSimClothData")sim=source;
            if(!schema.ContainsKey(cls))throw new NotSupportedException("No reviewed 2014 compatibility class for "+cls);
            var result=new XElement("hkobject",new XAttribute("class",cls),new XAttribute("name",(string)source.Attribute("name")),new XAttribute("signature",(string)schema[cls].Attribute("signature")));
            var used=new HashSet<string>();XElement config=null;
            if(cls=="hclSimulateOperator")
            {
                var configs=F(source,"simulateOpConfigs");if(configs==null||configs.Elements().Count()!=1)throw new NotSupportedException("2014 compatibility preview requires exactly one ER simulation config.");
                config=configs.Elements().Single();if(V(config,"useAllInstanceCollidables")!="true")throw new NotSupportedException("ER per-config collider subsets are not supported.");
                RequireEmpty(F(config,"instanceCollidablesUsed"),"ER per-config collider subsets are not supported.");
            }
            foreach(var m in Members(cls))
            {
                if(((string)m.Attribute("flags")??"").Contains("SERIALIZE_IGNORED"))continue;
                int count=int.Parse((string)m.Attribute("arrsize"),C);
                for(int i=0;i<Math.Max(1,count);i++)
                {
                    string key=(string)m.Attribute("name")+(count>0?(i+1).ToString(C):"");var input=F(source,key);if(input!=null)used.Add(key);
                    if(input==null&&config!=null)input=F(config,key);
                    if(input==null&&cls=="hclSimClothDataOverridableSimulationInfo"&&sim!=null)
                    {
                        if(new[]{"transferMotionEnabled","landscapeCollisionEnabled","pinchDetectionEnabled"}.Contains(key))input=F(sim,key);
                        else if(key=="collisionTolerance")input=F(F(sim,"landscapeCollisionData").Elements().Single(),key);
                        else if(key=="subSteps")input=new XElement("hkparam",new XAttribute("name",key),"0"); // runtime uses the preserved operator substeps
                    }
                    if(input==null&&(cls=="hclObjectSpaceDeformer"||cls=="hclBoneSpaceDeformer")&&key=="batchSizeSpu")input=new XElement("hkparam",new XAttribute("name",key),"0"); // CPU preview has no SPU batches
                    if(input==null)throw new NotSupportedException("Missing 2014 compatibility field: "+cls+"."+key);
                    var output=new XElement("hkparam",new XAttribute("name",key));result.Add(output);string kind=(string)m.Attribute("vtype"),sub=(string)m.Attribute("vsubtype"),target=(string)m.Attribute("ctype");
                    if(kind=="TYPE_ARRAY")
                    {
                        output.SetAttributeValue("numelements",(int?)input.Attribute("numelements")??throw new InvalidDataException("Missing ER array count."));
                        if(sub=="TYPE_STRUCT")foreach(var row in input.Elements("hkobject")){var mapped=Record(row,target,sim);mapped.Attributes().Remove();output.Add(mapped);}
                        else if(sub=="TYPE_STRINGPTR")output.Add(input.Elements().Select(e=>new XElement(e)));
                        else{output.Value=input.Value.Trim();if(sub=="TYPE_POINTER")Reference(output.Value);}
                    }
                    else if(kind=="TYPE_STRUCT")output.Add(Record(input.Elements("hkobject").Single(),target,sim));
                    else
                    {
                        output.Value=input.Value.Trim();if(kind=="TYPE_POINTER")Reference(output.Value);
                        if(kind=="TYPE_ENUM")
                        {
                            var enumeration=m.Ancestors("class").Single().Elements("enums").Elements("enum").SingleOrDefault(e=>(string)e.Attribute("name")== (string)m.Attribute("etype"));
                            var entry=enumeration==null?null:enumeration.Elements("enumitem").SingleOrDefault(e=>(string)e.Attribute("value")==output.Value);if(entry!=null)output.Value=(string)entry.Attribute("name");
                        }
                    }
                }
            }
            foreach(var extra in source.Elements("hkparam").Where(p=>!used.Contains((string)p.Attribute("name"))))
                if(!Ignorable(cls,extra))throw new NotSupportedException("Unmapped ER compatibility field: "+cls+"."+(string)extra.Attribute("name"));
            return result;
        }
        public static XDocument Create(XDocument source)
        {
            var adapter=new HkxTagfilePreviewAdapter(source);var root=adapter.objects.Single(p=>(string)p.Value.Attribute("class")=="hkRootLevelContainer");
            adapter.Reference(root.Key);var section=new XElement("hksection",new XAttribute("name","__data__"));
            while(adapter.pending.Count>0){string id=adapter.pending.Dequeue();var original=adapter.objects[id];section.Add(adapter.Record(original,(string)original.Attribute("class")));}
            // All original physical operators and constraints must remain reachable.
            foreach(var owner in adapter.objects.Values.Where(o=>(string)o.Attribute("class")=="hclClothData"))
                foreach(string id in V(owner,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
                    if(!adapter.enqueued.Contains(id))throw new InvalidDataException("Compatibility conversion lost a physical operator.");
            foreach(var simulation in adapter.objects.Values.Where(o=>(string)o.Attribute("class")=="hclSimClothData"))
                foreach(string id in V(simulation,"staticConstraintSets").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
                    if(!adapter.enqueued.Contains(id))throw new InvalidDataException("Compatibility conversion lost a constraint set.");
            return new XDocument(new XElement("hkpackfile",new XAttribute("classversion",11),new XAttribute("contentsversion","hk_2014.1.0-r1"),new XAttribute("nativeLayout","8,1,0,1"),section));
        }
    }
}
