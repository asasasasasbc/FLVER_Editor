using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class PlaybackTests {
 static void Check(bool x,string s){if(!x)throw new Exception(s);Console.WriteLine("PASS "+s);}
 [STAThread] static int Main(string[] args){try{
 var a=Assembly.LoadFrom(args[0]);var t=a.GetType("MySFformat.HkxPlaybackState");Check(t!=null,"playback timeline exists");
 dynamic p=Activator.CreateInstance(t,new object[]{1.0f,60});
 p.SeekFrame(30);Check(p.Frame==30&&Math.Abs(p.Time-0.5)<0.00001,"seek exact frame");
 p.SeekFrame(-1);Check(p.Frame==0,"seek clamps start");p.SeekFrame(999);Check(p.Frame==60,"seek clamps endpoint");
 p.Loop=false;p.Play();p.Advance(2f);Check(!p.Playing&&p.Frame==60,"non-loop playback stops at end");
 p.SeekFrame(0);p.Speed=0.5f;p.Play();p.Advance(0.5f);Check(p.Frame==15,"half-speed clock");
 p.Pause();p.Advance(0.5f);Check(p.Frame==15,"pause freezes frame");
 p.Loop=true;p.Speed=1f;p.SeekFrame(54);p.Play();p.Advance(0.2f);Check(p.Frame==6,"loop wraps timeline");
 var ft=a.GetType("MySFformat.HkxPlaybackForm");Check(ft!=null,"playback window exists");
 using(var form=(Form)Activator.CreateInstance(ft)){
 foreach(string name in new[]{"loadSkeleton","loadAnimation","playPause","stop","previousFrame","nextFrame","frameSlider","frameNumber","speed","loop","bindPose"})
 Check(form.Controls.Find(name,true).Length==1,"window control "+name);
 }
 return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
