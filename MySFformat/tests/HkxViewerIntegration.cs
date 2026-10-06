using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
class HkxViewerIntegration {
 static BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static void Check(bool x,string s){if(!x)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static string Positions(object viewer){var a=(Array)viewer.GetType().GetField("triVertices",flags).GetValue(viewer);return String.Join(";",a.Cast<object>().Select(x=>x.GetType().GetField("Position").GetValue(x).ToString()));}
 [STAThread] static int Main(string[] args){object viewer=null;Exception failure=null;Thread render=null;try{
 Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
 Application.ThreadException+=(s,e)=>{failure=e.Exception;Console.Error.WriteLine("UI EXCEPTION: "+e.Exception);};
 string dir=Path.GetDirectoryName(Path.GetFullPath(args[0]));AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
 var asm=Assembly.LoadFrom(args[0]);var prog=asm.GetType("MySFformat.Program");var field=prog.GetField("targetFlver",flags);
 field.SetValue(null,field.FieldType.GetMethod("Read",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy,null,new[]{typeof(string)},null).Invoke(null,new object[]{args[1]}));
 prog.GetField("flverName",flags).SetValue(null,args[1]);prog.GetField("loadTexture",flags).SetValue(null,false);prog.GetMethod("resetPoses",flags).Invoke(null,null);
 var vt=asm.GetType("MySFformat.Mono3D");Form owner=null;dynamic player=null;int stage=0;string before=null;var elapsed=Stopwatch.StartNew();var stageClock=Stopwatch.StartNew();
 using(var main=new Form{Text="HKX synchronous integration test",Width=400,Height=150}){
 prog.GetField("nodeUIForm",flags).SetValue(null,main);
 render=new Thread(()=>{try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>{failure=e.Exception;Console.Error.WriteLine("RENDER UI EXCEPTION: "+e.Exception);};viewer=Activator.CreateInstance(vt);prog.GetField("mono",flags).SetValue(null,viewer);prog.GetMethod("updateVertices",flags).Invoke(null,null);var w=vt.GetProperty("Window").GetValue(viewer,null);owner=(Form)Form.FromHandle((IntPtr)w.GetType().GetProperty("Handle").GetValue(w,null));vt.GetMethod("Run",Type.EmptyTypes).Invoke(viewer,null);}catch(Exception e){failure=e;}});
 render.SetApartmentState(ApartmentState.MTA);
 using(var timer=new System.Windows.Forms.Timer{Interval=100}){
 Action finish=()=>{timer.Stop();if(player!=null&&!((Form)player).InvokeRequired)player.Close();if(viewer!=null)vt.GetMethod("Exit").Invoke(viewer,null);main.Close();};
 timer.Tick+=(s,e)=>{try{
 if(failure!=null)throw failure;if(elapsed.Elapsed.TotalSeconds>20)throw new TimeoutException("Viewer integration timed out at stage "+stage);
 if(stage==0){if(viewer==null||owner==null||vt.GetProperty("GraphicsDevice").GetValue(viewer,null)==null)return;stage=1;stageClock.Restart();owner.BeginInvoke(new Action(()=>{try{var menu=(ToolStripMenuItem)owner.MainMenuStrip.Items.Cast<ToolStripItem>().Single(x=>x.Text=="Animation");var entry=menu.DropDownItems[0];Check(entry.Text.StartsWith("Load HKX"),"real Animation menu contains HKX");entry.PerformClick();entry.PerformClick();}catch(Exception ex){failure=ex;}}));}
 else if(stage==1){player=prog.GetField("hkxPlaybackForm",flags).GetValue(null);if(player==null)return;Check(!((Form)player).InvokeRequired,"player is dispatched to main STA UI, not MonoGame window thread");Check(((Form)player).Visible,"repeated menu open uses one visible player");Check(((Form)player).Owner!=owner,"player is not owned by MonoGame window");player.LoadFiles(args[2],args[3]);stage=2;stageClock.Restart();}
 else if(stage==2){if(stageClock.ElapsedMilliseconds<400)return;before=Positions(viewer);Check(before.Length>0,"viewer has triangle vertices");((Button)((Form)player).Controls.Find("playPause",true)[0]).PerformClick();stage=3;stageClock.Restart();}
 else if(stage==3){if(stageClock.ElapsedMilliseconds<450)return;player.Pause();Check(player.Playback.Frame>0,"main STA timer advances HKX during real MonoGame rendering");Check(before!=Positions(viewer),"render thread consumes animated pose and changes mesh");owner.BeginInvoke(new Action(()=>{try{owner.ClientSize=new System.Drawing.Size(820,620);owner.ClientSize=new System.Drawing.Size(800,600);}catch(Exception ex){failure=ex;}}));stage=4;stageClock.Restart();}
 else if(stage==4){if(stageClock.ElapsedMilliseconds<300)return;Check(failure==null,"viewer resize with player open has no unhandled exception");finish();}
 }catch(Exception ex){failure=ex;finish();}};
 main.Shown+=(s,e)=>{render.Start();timer.Start();};Application.Run(main);
 }
 if(render!=null&&!render.Join(5000))throw new TimeoutException("Viewer did not exit");if(failure!=null)throw failure;
 }
 return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(viewer!=null&&(render==null||!render.IsAlive))((IDisposable)viewer).Dispose();}}
}
