using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
class HkxUiIntegration {
 static void Check(bool x,string s){if(!x)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static Control Find(Form f,string n){return f.Controls.Find(n,true).Single();}
 static void Pump(int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
 [STAThread] static int Main(string[] args){try{
 string dir=Path.GetDirectoryName(Path.GetFullPath(args[0]));AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
 var asm=Assembly.LoadFrom(args[0]);var prog=asm.GetType("MySFformat.Program");
 var field=prog.GetField("targetFlver",BindingFlags.Public|BindingFlags.Static);
 var model=field.FieldType.GetMethod("Read",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy,null,new[]{typeof(string)},null).Invoke(null,new object[]{args[1]});field.SetValue(null,model);
 var type=asm.GetType("MySFformat.HkxPlaybackForm");
 using(var form=(Form)Activator.CreateInstance(type)){
 dynamic f=form;form.Show();f.LoadFiles(args[2],args[3]);Pump(50);
 Check(f.Playback.LastFrame>0,"real clip loaded into playback window");
 var slider=(TrackBar)Find(form,"frameSlider");slider.Value=20;Check(f.Playback.Frame==20,"slider scrubs real clip");
 ((NumericUpDown)Find(form,"frameNumber")).Value=25;Check(f.Playback.Frame==25,"numeric frame seeks real clip");
 ((Button)Find(form,"nextFrame")).PerformClick();Check(f.Playback.Frame==26,"next frame button");
 ((Button)Find(form,"previousFrame")).PerformClick();Check(f.Playback.Frame==25,"previous frame button");
 ((Button)Find(form,"playPause")).PerformClick();Pump(150);Check(f.Playback.Frame!=25,"WinForms timer actually advances animation");
 ((Button)Find(form,"playPause")).PerformClick();int paused=f.Playback.Frame;Pump(100);Check(f.Playback.Frame==paused,"pause button stops timer");
 ((Button)Find(form,"stop")).PerformClick();Check(f.Playback.Frame==0&&!f.Playback.Playing,"stop resets frame zero");
 var preview=prog.GetField("HkxWorldPose",BindingFlags.NonPublic|BindingFlags.Static);Check(preview.GetValue(null)!=null,"preview matrices applied");
 ((Button)Find(form,"bindPose")).PerformClick();Check(preview.GetValue(null)==null,"bind pose clears preview");
 f.SeekFrame(35);try{f.LoadFiles(args[2],"does-not-exist.hkx");throw new Exception("invalid load succeeded");}catch(FileNotFoundException){}
 Check(f.Playback.Frame==35,"failed load preserves last valid clip");
 using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[4]);}
 foreach(string n in new[]{"loadSkeleton","loadAnimation","playPause","stop","previousFrame","nextFrame","frameSlider","frameNumber","speed","loop","bindPose"}){var c=Find(form,n);Check(c.Visible&&c.Parent.ClientRectangle.Contains(c.Bounds),"control visible without clipping: "+n);}
 form.Close();Check(preview.GetValue(null)==null,"closing window restores static preview");
 }
 return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
