using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Numerics;
using System.Collections;
using System.Globalization;
using System.Web.Script.Serialization;
class HkxPerformanceTests {
 static BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static Type program;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(string n,object v){program.GetField(n,F).SetValue(null,v);}
 static void Check(bool x,string s){if(!x)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static double Median(Action action,int count){action();var values=new double[count];for(int i=0;i<count;i++){var w=Stopwatch.StartNew();action();values[i]=w.Elapsed.TotalMilliseconds;}Array.Sort(values);return values[count/2];}
 [STAThread] static int Main(string[] args){object viewer=null;try{
 string dir=Path.GetDirectoryName(Path.GetFullPath(args[0]));AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
 var asm=Assembly.LoadFrom(args[0]);program=asm.GetType("MySFformat.Program");var field=program.GetField("targetFlver",F);
 object model=field.FieldType.GetMethod("Read",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy,null,new[]{typeof(string)},null).Invoke(null,new object[]{args[1]});field.SetValue(null,model);
 Set("flverName",args[1]);Set("loadTexture",true);Set("boneDisplay",true);Set("dummyDisplay",true);program.GetMethod("resetPoses",F).Invoke(null,null);
 viewer=Activator.CreateInstance(asm.GetType("MySFformat.Mono3D"));Set("mono",viewer);
 var load=asm.GetType("MySFformat.HkxAnimationClip").GetMethod("Load");dynamic clip=load.Invoke(null,new object[]{args[2],args[3],Path.Combine(dir,"tools/HavokToolset")});
 dynamic binding=program.GetMethod("BindHkx",F).Invoke(null,new object[]{clip});
 var sample=binding.GetType().GetMethod("Sample",new[]{typeof(float)});var set=program.GetMethod("SetHkxPreview",F);var update=program.GetMethod("updateVertices",F);var consume=program.GetMethod("ConsumeHkxRefresh",F);
 float time=clip.Duration*0.53f;Action apply=()=>set.Invoke(null,new object[]{model,sample.Invoke(binding,new object[]{time})});
 double sampling=Median(()=>sample.Invoke(binding,new object[]{time}),31);
 apply();double full=Median(()=>update.Invoke(null,null),7);
 double refresh=Median(()=>{apply();consume.Invoke(null,null);},11);
 int meshCount=((ICollection)model.GetType().GetProperty("Meshes").GetValue(model,null)).Count;
 var result=new{model=args[1],animation=args[3],meshes=meshCount,sample_ms=sampling,full_update_ms=full,animation_refresh_ms=refresh,tri_vertices=((Array)Get(viewer,"triVertices")).Length};
 Console.WriteLine(new JavaScriptSerializer().Serialize(result));File.WriteAllText(args[4],new JavaScriptSerializer().Serialize(result));
 string fixture=args[4]+".vertices";bool compare=args.Length>5;string baseline=compare?args[5]+".vertices":fixture;
 using(var stream=File.Open(baseline,compare?FileMode.Open:FileMode.Create,compare?FileAccess.Read:FileAccess.Write)){
 var writer=compare?null:new BinaryWriter(stream);var reader=compare?new BinaryReader(stream):null;float maxError=0;int checkedVertices=0;
 foreach(float factor in new[]{0f,0.23f,0.53f,1f}){
 time=clip.Duration*factor;apply();consume.Invoke(null,null);
 foreach(string name in new[]{"triVertices","vertices"}){
 Array array=(Array)Get(viewer,name);if(compare)Check(reader.ReadInt32()==array.Length,name+" count unchanged");else writer.Write(array.Length);
 foreach(var v in array){object pos=Get(v,"Position"),color=Get(v,"Color");uint packed=(uint)color.GetType().GetProperty("PackedValue").GetValue(color,null);
 foreach(string axis in new[]{"X","Y","Z"}){float value=(float)Get(pos,axis);if(compare){float delta=Math.Abs(reader.ReadSingle()-value);CheckFinite(value);maxError=Math.Max(maxError,delta);}else writer.Write(value);}
 if(compare){uint old=reader.ReadUInt32();for(int c=0;c<4;c++)if(Math.Abs((int)((old>>(8*c))&255)-(int)((packed>>(8*c))&255))>1)throw new Exception("color changed");}else writer.Write(packed);checkedVertices++;
 }
 }
 }
 if(compare){Check(maxError<0.0001f,"optimized positions agree with pre-change reference; max error="+maxError.ToString("G6",CultureInfo.InvariantCulture));Console.WriteLine("Compared vertices="+checkedVertices);}
 }
 if(compare){var old=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(args[5])) as System.Collections.Generic.Dictionary<string,object>;double baselineMs=Convert.ToDouble(old["animation_refresh_ms"]);Console.WriteLine("Measured speedup="+(baselineMs/refresh).ToString("F2",CultureInfo.InvariantCulture));Check(refresh<baselineMs*0.45,"animation refresh at least 55% faster than original measured baseline");}
 return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(viewer!=null)((IDisposable)viewer).Dispose();}}
 static void CheckFinite(float x){if(float.IsNaN(x)||float.IsInfinity(x))throw new Exception("non-finite vertex");}
}
