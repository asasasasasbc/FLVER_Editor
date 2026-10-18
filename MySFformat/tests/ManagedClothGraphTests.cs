// Asset-free schedule/triangle-frame tests; takes the built Editor EXE path.
using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Numerics;using System.Xml.Linq;using System.Runtime.Serialization;
class ManagedClothGraphTests
{
    static Assembly asm;static Type graphType,docType,domainType;static int checks;
    static XElement P(string k,object v){return new XElement("hkparam",new XAttribute("name",k),v);}
    static XElement O(string cls,params XElement[] fields){return new XElement("hkobject",new XAttribute("class",cls),fields);}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static string Text(double[][] points){return string.Join(" ",points.Select(p=>"("+string.Join(" ",p.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+" 1)"));}
    static object Document(XElement owner,XElement[] ops,double[][][] poses,int[][] triangles,int[] types,int[] subtypes,int[] counts,XElement[][] constraints)
    {
        var objects=new Dictionary<string,XElement>();var domains=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(domainType));
        for(int i=0;i<ops.Length;i++)objects.Add("#op"+i,ops[i]);
        for(int i=0;i<poses.Length;i++)
        {
            var sim=O("hclSimClothData",P("simClothPoses","#pose"+i));sim.SetAttributeValue("name","#sim"+i);objects.Add("#sim"+i,sim);objects.Add("#pose"+i,O("hclSimClothPose",P("positions",Text(poses[i]))));
            var domain=Activator.CreateInstance(domainType);domainType.GetField("Owner").SetValue(domain,owner);domainType.GetField("Sim").SetValue(domain,sim);domainType.GetField("Positions").SetValue(domain,poses[i].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray());domainType.GetField("Fixed").SetValue(domain,new bool[poses[i].Length]);domainType.GetField("Triangles").SetValue(domain,triangles[i]);domainType.GetField("Constraints").SetValue(domain,constraints[i].ToList());domains.Add(domain);
        }
        for(int i=0;i<types.Length;i++)objects.Add("#buffer"+i,O("hclBufferDefinition",P("type",types[i]),P("subType",subtypes[i]),P("numVertices",counts[i])));
        objects.Add("#state",O("hclClothState",P("operators",string.Join(" ",Enumerable.Range(0,ops.Length)))));
        owner.Add(P("operators",string.Join(" ",Enumerable.Range(0,ops.Length).Select(i=>"#op"+i))),P("simClothDatas",string.Join(" ",Enumerable.Range(0,poses.Length).Select(i=>"#sim"+i))),P("bufferDefinitions",string.Join(" ",Enumerable.Range(0,types.Length).Select(i=>"#buffer"+i))),P("clothStateDatas","#state"));
        var doc=FormatterServices.GetUninitializedObject(docType);docType.GetProperty("Objects").SetValue(doc,objects,null);docType.GetProperty("Domains").SetValue(doc,domains,null);return doc;
    }
    static object Graph(object document,XElement owner){return Activator.CreateInstance(graphType,BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{document,(object)owner},null);}
    static void Execute(object graph,Func<int,object,double[][]> step){graphType.GetMethod("Execute",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{step});}
    static double[][] Buffer(object snapshot,int id){var buffers=(IDictionary)snapshot.GetType().GetField("Buffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(snapshot);var data=buffers[id];return (double[][])data.GetType().GetProperty("Positions").GetValue(data,null);}
    static double[][] Normals(object snapshot,int id){var buffers=(IDictionary)snapshot.GetType().GetField("Buffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(snapshot);var data=buffers[id];return (double[][])data.GetType().GetProperty("Normals").GetValue(data,null);}
    static XElement Constraint(int buffer){return O("hclLocalRangeConstraintSet",P("referenceMeshBufferIdx",buffer),P("applyNormalComponent","false"),P("localConstraints",O("Record",P("referenceVertex",0))));}
    static int Main(string[] args)
    {
        try
        {
            var dir=Path.GetDirectoryName(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};asm=Assembly.LoadFrom(args[0]);graphType=asm.GetType("MySFformat.Cloth.Preview.ManagedClothGraph");docType=asm.GetType("MySFformat.ClothDocument");domainType=asm.GetType("MySFformat.ClothDomain");
            var tri=new[]{new[]{0.0,0.0,0.0},new[]{1.0,0.0,0.0},new[]{0.0,1.0,0.0}};var point=new[]{new[]{0.0,0.0,0.0}};
            var owner=O("hclClothData");var ops=new[]{O("hclSimulateOperator",P("simClothIndex",0)),O("hclSimulateOperator",P("simClothIndex",1)),O("hclSimulateOperator",P("simClothIndex",2))};
            var doc=Document(owner,ops,new[]{tri,tri,point},new[]{new[]{0,1,2},new[]{0,1,2},new int[0]},new[]{1,1,1},new[]{0,1,2},new[]{3,3,1},new[]{new XElement[0],new XElement[0],new[]{Constraint(0),Constraint(1)}});var graph=Graph(doc,owner);double a=0,b=0;
            Execute(graph,(id,snapshot)=>{if(id==0)return tri.Select(p=>new[]{p[0]+2,p[1],p[2]}).ToArray();if(id==1)return tri.Select(p=>new[]{p[0]+7,p[1],p[2]}).ToArray();a=Buffer(snapshot,0)[0][0];b=Buffer(snapshot,1)[0][0];return point;});
            Check(a==2&&b==7,"multiple same-index reference buffers stay distinct and read same-frame upstream results");
            owner=O("hclClothData");ops=new[]{O("hclSimulateOperator",P("simClothIndex",2)),O("hclSimulateOperator",P("simClothIndex",0)),O("hclSimulateOperator",P("simClothIndex",1))};
            doc=Document(owner,ops,new[]{tri,tri,point},new[]{new[]{0,1,2},new[]{0,1,2},new int[0]},new[]{1,1,1},new[]{0,1,2},new[]{3,3,1},new[]{new XElement[0],new XElement[0],new[]{Constraint(0),Constraint(1)}});graph=Graph(doc,owner);
            Func<int,object,double[][]> readBeforeWrite=(id,snapshot)=>{if(id==0)return tri.Select(p=>new[]{p[0]+2,p[1],p[2]}).ToArray();if(id==1)return tri.Select(p=>new[]{p[0]+7,p[1],p[2]}).ToArray();a=Buffer(snapshot,0)[0][0];b=Buffer(snapshot,1)[0][0];return point;};
            Execute(graph,readBeforeWrite);Check(a==0&&b==0,"authored read-before-write order is not silently reordered");Execute(graph,readBeforeWrite);Check(a==2&&b==7,"read-before-write edges retain actual previous-frame simulation output");
            foreach(string mode in new[]{"SCALE_NORMAL_APPLY","SCALE_NORMAL_IGNORE","SCALE_NORMAL_INVERT"})
            {
                var matrix=(Matrix4x4)graphType.GetMethod("TriangleFrame",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Vector3.Zero,new Vector3(3,0,0),new Vector3(0,3,0),mode});
                float expected=mode=="SCALE_NORMAL_APPLY"?3:mode=="SCALE_NORMAL_IGNORE"?1:1f/3;Check(Math.Abs(matrix.M33-expected)<1e-6&&matrix.M41==1&&matrix.M42==1,"centroid frame preserves authored normal-scale mode "+mode);
            }
            var sharedOwner=O("hclClothData");var sharedDoc=Document(sharedOwner,new[]{O("hclSimulateOperator",P("simClothIndex",0))},new[]{tri},new[]{new[]{0,1,2}},new[]{1},new[]{0},new[]{3},new[]{new XElement[0]});
            var sharedDomains=(IList)docType.GetProperty("Domains").GetValue(sharedDoc,null);var otherOwner=new XElement(sharedOwner);var otherDomain=Activator.CreateInstance(domainType);
            foreach(var field in domainType.GetFields(BindingFlags.Public|BindingFlags.Instance))field.SetValue(otherDomain,field.GetValue(sharedDomains[0]));domainType.GetField("Owner").SetValue(otherDomain,otherOwner);sharedDomains.Add(otherDomain);
            int sharedId= -1;Execute(Graph(sharedDoc,otherOwner),(id,snapshot)=>{sharedId=id;return tri;});Check(sharedId==1,"owners sharing a SimClothData declaration retain independent domain identity");
            foreach(bool packed in new[]{false,true})
            {
                owner=O("hclClothData");XElement mesh;
                if(!packed)mesh=O("hclMeshMeshDeformOperator",P("inputBufferIdx",0),P("outputBufferIdx",1),P("inputTrianglesSubset",""),P("scaleNormalBehaviour","SCALE_NORMAL_IGNORE"),P("startVertex",0),P("endVertex",0),P("deformNormals","true"),P("partialDeform","false"),P("triangleVertexStartForVertex","0 1"),P("triangleVertexPairs",O("Pair",P("triangleIndex",0),P("weight",1),P("localPosition","(0 0 0 1)"),P("localNormal","(0 0 1 0)"))));
                else
                {
                    var blend=O("Blend");var local=O("Local");for(int i=1;i<=16;i++){blend.Add(P("vertexIndices"+i,0),P("boneIndices"+i,0));local.Add(P("localPosition"+i,"(0.2 0.3 0.4 1)"),P("localNormal"+i,"(0 0 1 0)"));}
                    // Inverse of the centroid basis for triangle (0,0),(1,0),(0,1).
                    mesh=O("hclObjectSpaceMeshMeshDeformPNOperator",P("inputBufferIdx",0),P("outputBufferIdx",1),P("inputTrianglesSubset",""),P("scaleNormalBehaviour","SCALE_NORMAL_IGNORE"),P("triangleFromMeshTransforms","(-1 1 0 0)(-2 -1 0 0)(0 0 1 0)(1 0 0 1)"),P("objectSpaceDeformer",O("Deformer",P("controlBytes","3"),P("fourBlendEntries",""),P("threeBlendEntries",""),P("twoBlendEntries",""),P("oneBlendEntries",blend))),P("localPNs",""),P("localUnpackedPNs",local));
                }
                ops=new[]{O("hclSimulateOperator",P("simClothIndex",0)),mesh,O("hclSimulateOperator",P("simClothIndex",1))};
                doc=Document(owner,ops,new[]{tri,point},new[]{new[]{0,1,2},new int[0]},new[]{1,2,1},new[]{0,0,1},new[]{3,1,1},new[]{new XElement[0],new[]{Constraint(1)}});graph=Graph(doc,owner);double[] observed=null;
                Execute(graph,(id,snapshot)=>{if(id==0)return tri.Select(p=>new[]{p[0]+2,p[1]+3,p[2]+4}).ToArray();observed=Buffer(snapshot,1)[0];return point;});
                var expected=packed?new[]{2.2,3.3,4.4}:new[]{2+1.0/3,3+1.0/3,4.0};Check(observed.Zip(expected,(x,y)=>Math.Abs(x-y)<2e-6).All(x=>x),(packed?"ObjectSpace":"Legacy")+" mesh deformation uses centroid frame and updated parent positions");
                Execute(graph,(id,snapshot)=>{if(id==0)return tri.Select(p=>new[]{p[0]+5,p[1],p[2]}).ToArray();observed=Buffer(snapshot,1)[0];return point;});Check(Math.Abs(observed[0]-(packed?5.2:5+1.0/3))<2e-6&&observed[2]<(packed?.401:.001),"deformer targets update each frame rather than remaining baked at rest");
                double[] observedNormal=null;Execute(graph,(id,snapshot)=>{if(id==0)return tri.Select(p=>new[]{p[0],-p[2],p[1]}).ToArray();observed=Buffer(snapshot,1)[0];observedNormal=Normals(snapshot,1)[0];return point;});
                expected=packed?new[]{.2,-.4,.3}:new[]{1.0/3,0.0,1.0/3};Check(observed.Zip(expected,(x,y)=>Math.Abs(x-y)<2e-6).All(x=>x)&&Math.Abs(observedNormal[1]+1)<1e-6,"triangle-frame positions and normals rotate with the live upstream triangle");
                bool refused=false;try{Execute(graph,(id,snapshot)=>id==0?tri.Select(p=>new[]{0.0,0.0,0.0}).ToArray():point);}catch(TargetInvocationException e){refused=e.InnerException is NotSupportedException;}Check(refused,"collapsed unit-normal triangle refuses undefined normalization instead of substituting rest");
            }
            Console.WriteLine("ALL PASS "+checks);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
