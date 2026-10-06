using System;using System.Reflection;
class CameraSettingsTests {
 static int passed;
 static void Check(bool b,string text){if(!b)throw new Exception(text);passed++;}
 static void Main(string[] a){try{
  var assembly=Assembly.LoadFrom(a[0]);var type=assembly.GetType("MySFformat.ViewerCameraSettings");Check(type!=null,"Missing ViewerCameraSettings helper");
  var parse=type.GetMethod("Parse",BindingFlags.Public|BindingFlags.Static);
  var valid="{\"camera\":[0,1,-4],\"target\":[0,1,0],\"renderMode\":\"BothNoTex\",\"showBones\":true}";
  var setting=parse.Invoke(null,new object[]{valid});var c=(float[])type.GetField("camera").GetValue(setting);Check(c[2]==-4&&c[1]==1,"FLVER Y-up/Z-forward values must remain explicit");
  foreach(string invalid in new[]{"{}","{\"camera\":[0,1],\"target\":[0,1,0]}","{\"camera\":[0,1,0],\"target\":[0,1,0]}","{\"camera\":[0,5,0],\"target\":[0,1,0]}","{\"camera\":[0,1,-4],\"target\":[0,1,0],\"renderMode\":\"wrong\"}","{"}) {
   bool rejected=false;try{parse.Invoke(null,new object[]{invalid});}catch(TargetInvocationException){rejected=true;}Check(rejected,"Invalid camera should be rejected without mutating live view: "+invalid);
  }
  Console.WriteLine("PASS camera helper assertions: "+passed);
 }catch(Exception e){Console.WriteLine("FAIL: "+e.Message);Environment.Exit(1);}}
}
