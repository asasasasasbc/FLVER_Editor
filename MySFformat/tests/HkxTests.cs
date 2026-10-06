using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Numerics;
class HkxTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static int Main(string[] args){try{
 var asm=Assembly.LoadFrom(args[0]);var t=asm.GetType("MySFformat.HkxAnimationClip");
 Check(t!=null,"HKX loader is present");
 dynamic clip=t.GetMethod("Load").Invoke(null,new object[]{args[1],args[2],args[3]});
 Check(clip.NodeCount>=100,"actual Bloodborne skeleton decoded");
 Check(clip.Duration>0f,"actual animation has duration");
 Matrix4x4[] first=clip.SampleWorld(0f),mid=clip.SampleWorld(clip.Duration*0.5f),last=clip.SampleWorld(clip.Duration);
 Check(first.Length==clip.NodeCount,"world matrix per skeleton node");
 Check(first.Where((m,i)=>!m.Equals(mid[i])).Any(),"real animation changes pose");
 Check(last.All(m=>!float.IsNaN(m.M11)&&!float.IsInfinity(m.M41)),"endpoint pose finite");
 Check(((Matrix4x4[])clip.SampleWorld(-1f)).SequenceEqual(first),"negative time clamps");
 Check(((Matrix4x4[])clip.SampleWorld(clip.Duration+10f)).SequenceEqual(last),"end time clamps");
 var bt=asm.GetType("MySFformat.HkxPoseBinding");Check(bt!=null,"FLVER name-based pose binding is present");
 string[] names=clip.Names; string bone=names.First(n=>!String.IsNullOrEmpty(n));
 dynamic binding=Activator.CreateInstance(bt,new object[]{clip,new[]{bone,"unmatched_child"},new[]{-1,0},new[]{Matrix4x4.Identity,Matrix4x4.CreateTranslation(0,0.1f,0)}});
 Matrix4x4[] bound=binding.Sample(0f);int bi=Array.IndexOf(names,bone);
 Check(bound[0].Equals(first[bi]),"HKX binds by name rather than FLVER index");
 Check(bound[1].Equals(Matrix4x4.CreateTranslation(0,0.1f,0)*bound[0]),"unmatched helper follows animated parent");
 Check(binding.MatchedCount==1,"binding reports matched bone count");
 Console.WriteLine("Nodes="+clip.NodeCount+" Duration="+clip.Duration);
 return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
