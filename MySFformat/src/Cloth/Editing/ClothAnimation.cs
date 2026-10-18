using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using MySFformat.Cloth.Preview;

namespace MySFformat
{
    internal sealed class ClothAnimationPathSettings
    {
        public string SkeletonPath {get;set;} = "";
        public string AnimationPath {get;set;} = "";
        public decimal Speed {get;set;} = 1;
        public bool Loop {get;set;} = true;
        public bool DriveNativeCloth {get;set;} = true;
        internal static ClothAnimationPathSettings Load()
        {
            try
            {
                return AppSettings.Load<ClothAnimationPathSettings>("ClothAnimation");
            }
            catch(Exception ex){Console.WriteLine("Failed to load Cloth animation paths: "+ex.Message);}
            return new ClothAnimationPathSettings();
        }
        internal void Save()
        {
            try{AppSettings.Save("ClothAnimation",this);}
            catch(Exception ex){Console.WriteLine("Failed to save Cloth animation paths: "+ex.Message);}
        }
    }
    public sealed partial class ClothInspectorForm
    {
        readonly ClothAnimationPathSettings animationPathSettings=ClothAnimationPathSettings.Load();
        readonly TextBox animationSkeletonPath=new TextBox {Name="clothAnimationSkeleton",ReadOnly=true,Width=300};
        readonly TextBox animationFilePath=new TextBox {Name="clothAnimationFile",ReadOnly=true,Width=300};
        readonly Button animationPlay=new Button {Name="playClothAnimation",Text="Play HKX",AutoSize=true};
        readonly Button animationPause=new Button {Name="pauseClothAnimation",Text="Pause HKX",AutoSize=true,Enabled=false};
        readonly CheckBox animationLink=new CheckBox {Name="linkClothAnimation",Text="Drive Cloth Physics",Checked=true,AutoSize=true};
        readonly CheckBox animationLoop=new CheckBox {Name="loopClothAnimation",Text="Loop",Checked=true,AutoSize=true};
        readonly NumericUpDown animationSpeed=new NumericUpDown {Name="clothAnimationSpeed",Minimum=.05m,Maximum=4,DecimalPlaces=2,Value=1,Increment=.25m,Width=60};
        readonly NumericUpDown animationFrame=new NumericUpDown {Name="clothAnimationFrame",Minimum=0,Width=85};
        readonly TrackBar animationTimeline=new TrackBar {Name="clothAnimationTimeline",Dock=DockStyle.Top,TickStyle=TickStyle.None,Minimum=0,Maximum=1};
        readonly TextBox animationStatus=new TextBox {Name="clothAnimationStatus",Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,Dock=DockStyle.Fill};
        readonly System.Windows.Forms.Timer animationTimer=new System.Windows.Forms.Timer {Interval=16};
        readonly Stopwatch animationClock=new Stopwatch();
        HkxAnimationClip clothAnimation;
        HkxPoseBinding[] animationBindings;
        XElement[] animationSkeletons;
        ClothDocument animationBoundDocument;
        Matrix4x4[] animationReferenceWorld;
        string[] animationBoneNames;
        bool animationUpdating,animationLoading,animationRest=true,animationRestarting;
        int animationLoadGeneration;
        public HkxPlaybackState ClothAnimationPlayback {get;private set;}
        bool HasLinkedAnimation {get{return clothAnimation!=null&&!animationRest&&animationLink.Checked;}}

        void InitializeClothAnimation(TabControl tabs)
        {
            var page=new TabPage("HKX Animation") {Name="clothAnimationTab",AutoScroll=true};tabs.TabPages.Add(page);
            var files=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(4)};
            var skeleton=new Button {Name="loadClothAnimationSkeleton",Text="Skeleton HKX…",AutoSize=true};
            var loadAnimation=new Button {Name="loadClothAnimation",Text="Load Animation HKX…",AutoSize=true};
            files.Controls.AddRange(new Control[]{skeleton,animationSkeletonPath,loadAnimation,animationFilePath});
            files.SetFlowBreak(animationSkeletonPath,true);
            var playback=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(4)};
            var stop=new Button {Name="stopClothAnimation",Text="Stop HKX",AutoSize=true};
            var previous=new Button {Name="previousClothAnimationFrame",Text="◀",AutoSize=true};
            var next=new Button {Name="nextClothAnimationFrame",Text="▶",AutoSize=true};
            var rest=new Button {Name="clothAnimationRest",Text="Rest Pose",AutoSize=true};
            playback.Controls.AddRange(new Control[]{animationPlay,animationPause,stop,previous,next,animationFrame,new Label {Text="Speed",AutoSize=true,Padding=new Padding(0,6,0,0)},animationSpeed,animationLoop,animationLink,rest});
            page.Controls.Add(animationStatus);page.Controls.Add(animationTimeline);page.Controls.Add(playback);page.Controls.Add(files);
            animationStatus.Text="Load Cloth, then select the character skeleton.hkx and an animation HKX.\r\nDrive Cloth Physics sends bone poses to the selected solver at 60 Hz. Seeking or looping starts a fresh simulation at that pose.";
            tips.SetToolTip(skeleton,"Choose the character skeleton for animation decoding, not the equipment _c.hkx.\n选择解码动画用的角色骨架，不是装备_c.hkx。");
            tips.SetToolTip(loadAnimation,"Decode using the existing HavokToolset adapter, then map bones by exact name to the Cloth skeletons. Failed loads preserve the previous clip.\n复用现有HavokToolset解码，按精确骨名映射到布料骨架；失败保留旧动画。");
            tips.SetToolTip(animationLink,"Drive the selected solver with HKX animation. Unmatched bones keep their local offsets. Uncheck for animation only.\n用HKX动画驱动当前解算器；未匹配骨骼保留局部偏移，取消则仅预览动画。");
            tips.SetToolTip(animationTimeline,"Seek pauses playback and resets linked physics; previous frames are not replayed.\n跳帧会暂停播放并重置联动物理，不回放此前全部动画帧。");
            tips.SetToolTip(animationPlay,"Play or resume HKX. Linked Cloth also starts.\n播放或继续HKX；联动Cloth也会启动。");
            tips.SetToolTip(animationPause,"Pause at the current frame without resetting physics history. Linked Cloth pauses too.\n停在当前帧，不重置物理历史；联动Cloth也会暂停。");
            tips.SetToolTip(rest,"Keep the clip loaded, pause HKX and restore Rest Pose; recreate active native history.\n保留动画、暂停播放、恢复初始姿态并重建当前解算历史。");
            tips.SetToolTip(animationSpeed,"Animation time multiplier. Physics always uses fixed 1/60 steps; speed changes sampled poses, not solver dt.\n动画时间倍率；物理步长始终1/60，仅改变采样姿态。");
            tips.SetToolTip(stop,"Pause and seek frame 0. Rest Pose is a separate operation.\n暂停并回到第0帧；恢复作者姿态请用Rest Pose。");
            tips.SetToolTip(previous,tips.GetToolTip(animationTimeline));tips.SetToolTip(next,tips.GetToolTip(animationTimeline));tips.SetToolTip(animationFrame,tips.GetToolTip(animationTimeline));
            tips.SetToolTip(animationLoop,"Looping starts a fresh simulation to avoid carrying velocity across the end-to-start jump.\n循环时重新初始化模拟，避免末帧到首帧跳变携带旧速度。");
            string defaultSkeleton=Path.Combine(HkxPlaybackForm.BloodborneRoot,@"c0000-anibnd-dcx\chr\c0000\hkx\skeleton.hkx");
            if(!string.IsNullOrWhiteSpace(animationPathSettings.SkeletonPath))animationSkeletonPath.Text=animationPathSettings.SkeletonPath;
            else if(File.Exists(defaultSkeleton))animationSkeletonPath.Text=defaultSkeleton;
            animationFilePath.Text=animationPathSettings.AnimationPath??"";
            animationSpeed.Value=Math.Max(animationSpeed.Minimum,Math.Min(animationSpeed.Maximum,animationPathSettings.Speed));
            animationLoop.Checked=animationPathSettings.Loop;animationLink.Checked=animationPathSettings.DriveNativeCloth;
            skeleton.Click+=async(s,e)=>{
                using(var dialog=new OpenFileDialog {Title="Select character skeleton.hkx",Filter="HKX skeleton|*.hkx",AutoUpgradeEnabled=true})
                {
                    RestoreAnimationDialogPath(dialog,animationSkeletonPath.Text,"skeleton.hkx");
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    if(clothAnimation==null){animationSkeletonPath.Text=dialog.FileName;animationPathSettings.SkeletonPath=dialog.FileName;animationPathSettings.Save();}
                    else await TryLoadClothAnimationAsync(dialog.FileName,animationFilePath.Text);
                }
            };
            loadAnimation.Click+=async(s,e)=>{
                if(!File.Exists(animationSkeletonPath.Text)){animationStatus.Text="Choose a character skeleton.hkx first.";return;}
                using(var dialog=new OpenFileDialog {Title="Select animation HKX (not Cloth)",Filter="HKX animation|*.hkx",AutoUpgradeEnabled=true})
                {RestoreAnimationDialogPath(dialog,animationFilePath.Text,"");if(dialog.ShowDialog(this)==DialogResult.OK)await TryLoadClothAnimationAsync(animationSkeletonPath.Text,dialog.FileName);}
            };
            animationPlay.Click+=async(s,e)=>await PlayClothAnimationAsync();stop.Click+=async(s,e)=>await SeekClothAnimationAsync(0);
            animationPause.Click+=(s,e)=>PauseClothAnimation();
            previous.Click+=async(s,e)=>await SeekClothAnimationAsync(ClothAnimationPlayback==null?0:ClothAnimationPlayback.Frame-1);
            next.Click+=async(s,e)=>await SeekClothAnimationAsync(ClothAnimationPlayback==null?0:ClothAnimationPlayback.Frame+1);
            rest.Click+=async(s,e)=>{PauseClothAnimation();animationRest=true;canvas.AnimationBones=null;canvas.AnimationColliders=null;await RestartForAnimationDiscontinuityAsync(false);RefreshAnimationLabels();UpdateMeshPreview();canvas.Invalidate();};
            animationTimeline.ValueChanged+=async(s,e)=>{if(!animationUpdating)await SeekClothAnimationAsync(animationTimeline.Value);};
            animationFrame.ValueChanged+=async(s,e)=>{if(!animationUpdating)await SeekClothAnimationAsync((int)animationFrame.Value);};
            animationSpeed.ValueChanged+=(s,e)=>{if(ClothAnimationPlayback!=null)ClothAnimationPlayback.Speed=(float)animationSpeed.Value;animationPathSettings.Speed=animationSpeed.Value;animationPathSettings.Save();};
            animationLoop.CheckedChanged+=(s,e)=>{if(ClothAnimationPlayback!=null)ClothAnimationPlayback.Loop=animationLoop.Checked;animationPathSettings.Loop=animationLoop.Checked;animationPathSettings.Save();};
            animationLink.CheckedChanged+=async(s,e)=>{animationPathSettings.DriveNativeCloth=animationLink.Checked;animationPathSettings.Save();PauseClothAnimation();await RestartForAnimationDiscontinuityAsync(false);RefreshAnimationLabels();};
            animationTimer.Tick+=(s,e)=>{
                if(ClothAnimationPlayback==null||!ClothAnimationPlayback.Playing||HasLinkedAnimation)return;
                try{float elapsed=(float)animationClock.Elapsed.TotalSeconds;animationClock.Restart();ClothAnimationPlayback.Advance(Math.Min(.1f,elapsed));UpdateAnimationCanvas();RefreshAnimationLabels();if(!ClothAnimationPlayback.Playing)animationTimer.Stop();}
                catch(Exception ex){PauseClothAnimation();animationStatus.Text="Animation paused: "+ex.Message;}
            };
        }
        static void RestoreAnimationDialogPath(OpenFileDialog dialog,string path,string fallback)
        {
            dialog.FileName=fallback;
            if(string.IsNullOrWhiteSpace(path))return;
            try
            {
                string full=Path.GetFullPath(path);string directory=Path.GetDirectoryName(full);
                if(Directory.Exists(directory))dialog.InitialDirectory=directory;
                dialog.FileName=Path.GetFileName(full);
            }
            catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
        }
        async Task TryLoadClothAnimationAsync(string skeleton,string animation)
        {try{await LoadClothAnimationAsync(skeleton,animation);}catch(Exception ex){if(!IsDisposed)animationStatus.Text="Animation load failed (previous clip retained): "+ex.GetBaseException().Message;}}
        public async Task LoadClothAnimationAsync(string skeleton,string animation)
        {
            if(Document==null)throw new InvalidOperationException("Load Cloth first.");if(animationLoading||animationRestarting||applyingAuthor)throw new InvalidOperationException("Another operation is in progress.");
            if(!(System.Threading.SynchronizationContext.Current is WindowsFormsSynchronizationContext))System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            PauseClothAnimation();int generation=++animationLoadGeneration;var document=Document;animationLoading=true;animationPlay.Enabled=false;
            try
            {
                var clip=await Task.Run(()=>HkxAnimationClip.Load(skeleton,animation,Program.HkxToolsPath));
                if(IsDisposed||generation!=animationLoadGeneration||!ReferenceEquals(document,Document))return;
                SetClothAnimationClip(clip);animationSkeletonPath.Text=Path.GetFullPath(skeleton);animationFilePath.Text=Path.GetFullPath(animation);
                animationPathSettings.SkeletonPath=animationSkeletonPath.Text;animationPathSettings.AnimationPath=animationFilePath.Text;animationPathSettings.Save();
                await RestartForAnimationDiscontinuityAsync(false);
            }
            finally{animationLoading=false;if(!IsDisposed)animationPlay.Enabled=true;}
        }
        public void SetClothAnimationClip(HkxAnimationClip clip)
        {
            if(Document==null)throw new InvalidOperationException("Load Cloth first.");
            var skeletons=Document.Objects.Values.Where(o=>(string)o.Attribute("class")=="hkaSkeleton").ToArray();
            var bindings=skeletons.Select(s=>CreateAnimationBinding(clip,s)).ToArray();
            PauseClothAnimation();clothAnimation=clip;animationSkeletons=skeletons;animationBindings=bindings;animationBoundDocument=Document;
            ReadAnimationReference(skeletons[0]);
            ClothAnimationPlayback=new HkxPlaybackState(clip.Duration,HkxAnimationClip.SampleRate) {Speed=(float)animationSpeed.Value,Loop=animationLoop.Checked};animationRest=false;
            animationUpdating=true;animationTimeline.Maximum=ClothAnimationPlayback.LastFrame;animationFrame.Maximum=ClothAnimationPlayback.LastFrame;animationUpdating=false;
            UpdateAnimationCanvas();RefreshAnimationLabels();
        }
        static HkxPoseBinding CreateAnimationBinding(HkxAnimationClip clip,XElement skeleton)
        {
            string[] names=ClothDocument.Field(skeleton,"bones").Elements().Select(o=>ClothDocument.Value(o,"name")).ToArray();
            int[] parents=ClothDocument.Numbers(ClothDocument.Value(skeleton,"parentIndices")).Select(v=>v>=32768?(int)v-65536:(int)v).ToArray();
            var values=ClothDocument.Numbers(ClothDocument.Value(skeleton,"referencePose"));if(values.Length!=names.Length*12)throw new InvalidDataException("Cloth reference-pose count mismatch");
            var local=new Matrix4x4[names.Length];for(int i=0;i<names.Length;i++){int p=i*12;local[i]=Matrix4x4.CreateScale((float)values[p+8],(float)values[p+9],(float)values[p+10])*Matrix4x4.CreateFromQuaternion(new Quaternion((float)values[p+4],(float)values[p+5],(float)values[p+6],(float)values[p+7]))*Matrix4x4.CreateTranslation((float)values[p],(float)values[p+1],(float)values[p+2]);}
            return new HkxPoseBinding(clip,names,parents,local);
        }
        void EnsureAnimationBindings()
        {
            if(clothAnimation==null)return;if(ReferenceEquals(animationBoundDocument,Document))return;
            var skeletons=Document.Objects.Values.Where(o=>(string)o.Attribute("class")=="hkaSkeleton").ToArray();
            var bindings=skeletons.Select(s=>CreateAnimationBinding(clothAnimation,s)).ToArray();animationSkeletons=skeletons;animationBindings=bindings;animationBoundDocument=Document;
            ReadAnimationReference(skeletons[0]);
        }
        void ValidateAnimationSession(IClothPreviewSession session)
        {
            if(HasLinkedAnimation&&!session.SupportsAnimation)throw new NotSupportedException("This Cloth session has no supported animation skeleton binding.");
            if(!HasLinkedAnimation)return;EnsureAnimationBindings();var names=session.SkeletonNames;var bones=session.SkeletonBones;
            if(names.Length!=animationSkeletons.Length)throw new InvalidDataException("Animated skeleton count mismatch");
            for(int s=0;s<names.Length;s++)if(names[s]!=ClothDocument.Value(animationSkeletons[s],"name")||!bones[s].SequenceEqual(ClothDocument.Field(animationSkeletons[s],"bones").Elements().Select(b=>ClothDocument.Value(b,"name"))))throw new InvalidDataException("Animated skeleton order/name mismatch");
        }
        static float[] FlattenAnimationPose(Matrix4x4[] pose)
        {
            return pose.SelectMany(m=>{Matrix4x4 inv;if(!Matrix4x4.Invert(m,out inv))throw new InvalidDataException("Singular animated transform");var values=new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44};if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new InvalidDataException("Nonfinite animation pose");return values;}).ToArray();
        }
        float[][][] BuildAnimationPoseBatch(int steps,bool advance=true)
        {
            if(!HasLinkedAnimation)return null;EnsureAnimationBindings();var result=new float[steps][][];
            for(int p=0;p<steps;p++)
            {
                if(advance&&ClothAnimationPlayback.Playing){float previous=ClothAnimationPlayback.Time;ClothAnimationPlayback.Advance(1f/60);if(ClothAnimationPlayback.Time<previous){ClothAnimationPlayback.SeekFrame(0);RestartAnimationLoop();return null;}}
                result[p]=animationBindings.Select(b=>FlattenAnimationPose(b.Sample(ClothAnimationPlayback.Time))).ToArray();
            }
            UpdateAnimationCanvas();RefreshAnimationLabels();return result;
        }
        async void RestartAnimationLoop()
        {try{await RestartForAnimationDiscontinuityAsync(true);}catch(Exception ex){if(!IsDisposed){PauseClothAnimation();animationStatus.Text="Loop restart failed: "+ex.Message;}}}
        public async Task PlayClothAnimationAsync()
        {
            if(ClothAnimationPlayback==null||animationLoading||animationRestarting||applyingAuthor)return;
            if(ClothAnimationPlayback.Playing)return;
            animationRest=false;EnsureAnimationBindings();bool atEnd=ClothAnimationPlayback.Time>=ClothAnimationPlayback.Duration;ClothAnimationPlayback.Play();
            if(HasLinkedAnimation){if(atEnd&&previewSession!=null)await RestartForAnimationDiscontinuityAsync(true);else await StartClothPreviewAsync();if(previewSession==null){PauseClothAnimation();animationStatus.Text=previewState.Text;return;}}
            else{animationClock.Restart();animationTimer.Start();}
            UpdateAnimationCanvas();RefreshAnimationLabels();
        }
        public void PauseClothAnimation()
        {
            PauseAnimationClock();if(HasLinkedAnimation&&previewPlaying)PauseClothPreview();RefreshAnimationLabels();
        }
        void PauseAnimationClock(){animationTimer.Stop();animationClock.Reset();if(ClothAnimationPlayback!=null)ClothAnimationPlayback.Pause();}
        public async Task SeekClothAnimationAsync(int frame)
        {
            if(ClothAnimationPlayback==null||animationLoading||animationRestarting||applyingAuthor)return;
            PauseClothAnimation();ClothAnimationPlayback.SeekFrame(frame);animationRest=false;UpdateAnimationCanvas();RefreshAnimationLabels();await RestartForAnimationDiscontinuityAsync(false);
        }
        async Task RestartForAnimationDiscontinuityAsync(bool resume)
        {
            if(animationRestarting||IsDisposed)return;
            bool restart=previewSession!=null||previewPreparing;var environment=previewSession==null?null:appliedEnvironment.Clone();
            animationRestarting=true;animationPlay.Enabled=false;
            try
            {
                if(restart){StopClothPreview();if(resume&&ClothAnimationPlayback!=null)ClothAnimationPlayback.Play();await StartClothPreviewAsync(environment);if(!resume&&previewSession!=null)PauseClothPreview();}
                if(!IsDisposed){UpdateAnimationCanvas();RefreshAnimationLabels();}
            }
            finally{animationRestarting=false;if(!IsDisposed){animationPlay.Enabled=!animationLoading;RefreshAnimationLabels();}}
        }
        void ResetClothAnimationForDocument()
        {
            animationLoadGeneration++;PauseAnimationClock();clothAnimation=null;ClothAnimationPlayback=null;animationBindings=null;animationBoundDocument=null;canvas.AnimationBones=null;canvas.AnimationColliders=null;
            animationPause.Enabled=false;
            UpdateMeshPreview();
            if(!IsDisposed)animationStatus.Text="Cloth document changed. Load an animation for its skeleton.";
        }
        // 作者碰撞体已经处于参考世界坐标。显示动画姿态时，先消去参考骨变换，
        // 再乘当前骨变换；不改作者坐标，也不猜逐粒子蒙皮权重。
        void ReadAnimationReference(XElement skeleton)
        {
            animationBoneNames=ClothDocument.Field(skeleton,"bones").Elements().Select(b=>ClothDocument.Value(b,"name")).ToArray();
            var parents=ClothDocument.Numbers(ClothDocument.Value(skeleton,"parentIndices")).Select(v=>v>=32768?(int)v-65536:(int)v).ToArray();
            var values=ClothDocument.Numbers(ClothDocument.Value(skeleton,"referencePose"));
            var local=new Matrix4x4[parents.Length];
            for(int i=0;i<local.Length;i++)
            {
                int p=i*12;
                local[i]=Matrix4x4.CreateScale((float)values[p+8],(float)values[p+9],(float)values[p+10])*
                    Matrix4x4.CreateFromQuaternion(new Quaternion((float)values[p+4],(float)values[p+5],(float)values[p+6],(float)values[p+7]))*
                    Matrix4x4.CreateTranslation((float)values[p],(float)values[p+1],(float)values[p+2]);
            }
            animationReferenceWorld=new Matrix4x4[parents.Length];
            var marks=new byte[parents.Length];
            Action<int> resolve=null;
            resolve=i=>
            {
                if(marks[i]==2)return;
                if(marks[i]==1)throw new InvalidDataException("Cyclic reference skeleton");
                marks[i]=1;
                if(parents[i]>=0)resolve(parents[i]);
                animationReferenceWorld[i]=parents[i]<0?local[i]:local[i]*animationReferenceWorld[parents[i]];
                marks[i]=2;
            };
            for(int i=0;i<parents.Length;i++)resolve(i);
        }
        void UpdateAnimationCanvas()
        {
            if(clothAnimation==null||animationRest||Document==null){canvas.AnimationBones=null;canvas.AnimationColliders=null;UpdateMeshPreview();canvas.Invalidate();return;}
            EnsureAnimationBindings();var pose=animationBindings[0].Sample(ClothAnimationPlayback.Time);canvas.AnimationBones=pose.Select(m=>new Vector3(m.M41,m.M42,m.M43)).ToArray();
            canvas.AnimationParents=ClothDocument.Numbers(ClothDocument.Value(animationSkeletons[0],"parentIndices")).Select(v=>v>=32768?(int)v-65536:(int)v).ToArray();
            canvas.AnimationColliders=null;
            if(HasLinkedAnimation&&canvas.Domain!=null)
            {
                canvas.AnimationColliders=canvas.Domain.Colliders.Select(c=>
                {
                    int bone=Array.IndexOf(animationBoneNames,c.Bone);
                    Matrix4x4 inverse;
                    if(bone<0||!Matrix4x4.Invert(animationReferenceWorld[bone],out inverse))return c;
                    var transform=inverse*pose[bone];
                    return new ClothCollider {Bone=c.Bone,Shape=c.Shape,Start=Vector3.Transform(c.Start,transform),
                        End=Vector3.Transform(c.End,transform),RadiusA=c.RadiusA,RadiusB=c.RadiusB};
                }).ToList();
            }
            UpdateMeshPreview();
            if(previewSession is ManagedClothPreviewSession&&lastPreview!=null)ApplyPreviewPositions();
            canvas.Invalidate();
        }
        void RefreshAnimationLabels()
        {
            if(ClothAnimationPlayback==null||IsDisposed)return;animationUpdating=true;animationTimeline.Value=ClothAnimationPlayback.Frame;animationFrame.Value=ClothAnimationPlayback.Frame;animationUpdating=false;
            animationPause.Enabled=ClothAnimationPlayback.Playing&&!animationRestarting&&!animationLoading;
            animationStatus.Text=string.Format("Frame {0}/{1} · {2:F3}/{3:F3} s · {4}\r\nMapped bones: {5}. Unmatched bones keep their local offsets.\r\n{6}\r\nSeek/loop starts fresh physics at that pose. Range circles show saved limits; colliders follow animation.",ClothAnimationPlayback.Frame,ClothAnimationPlayback.LastFrame,ClothAnimationPlayback.Time,ClothAnimationPlayback.Duration,animationRest?"Rest Pose":HasLinkedAnimation?"Cloth linked":"Animation only",string.Join(", ",animationBindings.Select(b=>b.MatchedCount+" matched / "+b.UnmatchedNames.Length+" unmatched")),string.Join(", ",animationBindings.SelectMany(b=>b.UnmatchedNames).Distinct().Take(12)));
        }
        void DisposeClothAnimation(){animationLoadGeneration++;animationTimer.Stop();animationTimer.Dispose();}
    }
}
