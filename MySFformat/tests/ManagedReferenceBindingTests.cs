// Asset-free authored Skin reference tests. Run with the built Editor EXE path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;

class ManagedReferenceBindingTests
{
    static int checks;
    static XElement P(string key,object value){return new XElement("hkparam",new XAttribute("name",key),value);}
    static XElement O(string cls,params XElement[] fields){return new XElement("hkobject",new XAttribute("class",cls),fields);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    static int Main(string[] args)
    {
        try
        {
            string dir=Path.GetDirectoryName(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var path=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
            var asm=Assembly.LoadFrom(args[0]);var binder=asm.GetType("MySFformat.Cloth.Preview.ManagedReferenceBinding");var build=binder.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(bool boneSpace in new[]{false,true})
            foreach(bool tangents in new[]{false,true})
            {
                var skeleton=O("hkaSkeleton",P("name","Master"),P("bones",O("hkaBone",P("name","Root"))),P("parentIndices","-1"),P("referencePose","(3 4 5 0)(0 0 0 1)(1 1 1 0)"));
                var set=O("hclTransformSetDefinition",P("name","Master"),P("numTransforms",1));
                var owner=O("hclClothData",P("transformSetDefinitions","#set"),P("bufferDefinitions","#buffer"));
                var blend=O("Blend");int influence=boneSpace?2:1;
                for(int i=0;i<(boneSpace?8:16);i++)blend.Add(P("vertexIndices"+(i+1),Math.Min(i,2)));
                for(int i=0;i<16;i++)blend.Add(P("boneIndices"+(i+1),0));
                var local=O("LocalPN");
                for(int i=0;i<16;i++)
                {
                    int vertex=Math.Min(boneSpace?i/2:i,2);
                    if(boneSpace)local.Add(P("localPosition"+(i+1),i%2==0?"("+(vertex*.5).ToString(System.Globalization.CultureInfo.InvariantCulture)+" 0 0 1)":"(0 0 0 0)"));
                    else for(int j=0;j<4;j++)local.Add(P("localPosition"+(i*4+j+1),new[]{vertex*8192,0,0,12416}[j]));
                    for(int j=0;j<4;j++)local.Add(P("localNormal"+(i*4+j+1),new[]{0,0,boneSpace&&i%2==1?0:16384,12416}[j]));
                }
                var deform=O("Deformer",P("controlBytes",boneSpace?"2":"3"),P("fourBlendEntries",""),P("threeBlendEntries",""),P("twoBlendEntries",boneSpace?(object)blend:""),P("oneBlendEntries",boneSpace?(object)"":blend));
                string suffix=tangents?"PNT":"PN";
                var skin=O((boneSpace?"hclBoneSpaceSkin":"hclObjectSpaceSkin")+suffix+"Operator",P("outputBufferIndex",0),P("transformSetIndex",0),P("transformSubset","0"),P(boneSpace?"boneSpaceDeformer":"objectSpaceDeformer",deform),P("local"+suffix+"s",local),P("localUnpacked"+suffix+"s",""));
                if(!boneSpace)skin.Add(P("boneFromSkinMeshTransforms","(1 0 0 0)(0 1 0 0)(0 0 1 0)(0 0 0 1)"));
                var move=O("hclMoveParticlesOperator",P("simClothIndex",0),P("refBufferIdx",0),P("vertexParticlePairs",O("Pair",P("vertexIndex",2),P("particleIndex",0))));
                var constraint=O("hclLocalRangeConstraintSet",P("referenceMeshBufferIdx",0),P("localConstraints",O("Record",P("referenceVertex",2))));
                var objects=new Dictionary<string,XElement>{{"#set",set},{"#buffer",O("hclScratchBufferDefinition",P("numVertices",3))},{"#skeleton",skeleton}};
                var doc=FormatterServices.GetUninitializedObject(asm.GetType("MySFformat.ClothDocument"));doc.GetType().GetProperty("Objects").SetValue(doc,objects,null);
                var domain=Activator.CreateInstance(asm.GetType("MySFformat.ClothDomain"));domain.GetType().GetField("Owner").SetValue(domain,owner);domain.GetType().GetField("Fixed").SetValue(domain,new[]{true});domain.GetType().GetField("Constraints").SetValue(domain,new List<XElement>{constraint});
                Func<object> run=()=>build.Invoke(null,new object[]{doc,domain,0,new[]{skin,move}});var result=run();
                var positions=(double[][])binder.GetField("Positions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(result);
                var normals=(double[][])binder.GetField("Normals",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(result);
                var pins=(Dictionary<int,double[]>)binder.GetField("FixedTargets",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(result);
                Check(Math.Abs(positions[2][0]-4)<1e-6&&Math.Abs(positions[2][1]-4)<1e-6&&Math.Abs(positions[2][2]-5)<1e-6,"authored "+(boneSpace?"BoneSpace weighted-W":"ObjectSpace packed")+" Skin evaluates rest skeleton");
                Check(pins[0].SequenceEqual(positions[2]),"compact reference index 2 maps to fixed particle 0 without particle-index substitution");
                Check(Math.Abs(normals[2][2]-1)<1e-6,"reference normals decoded and transformed");
                constraint.Descendants("hkparam").Single(p=>(string)p.Attribute("name")=="referenceVertex").Value="3";
                bool refused=false;try{run();}catch(TargetInvocationException e){refused=e.InnerException is NotSupportedException;}Check(refused,"unwritten/out-of-range reference refused, never replaced with rest");
                constraint.Descendants("hkparam").Single(p=>(string)p.Attribute("name")=="referenceVertex").Value="2";
                skin.SetAttributeValue("class","hclMeshMeshDeformOperator");refused=false;try{run();}catch(TargetInvocationException e){refused=e.InnerException is NotSupportedException;}Check(refused,"static Skin evaluator does not fake MeshMeshDeform; it must execute through the graph");
            }
            Console.WriteLine("ALL PASS "+checks);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
