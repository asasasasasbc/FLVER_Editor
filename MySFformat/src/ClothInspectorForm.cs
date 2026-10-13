using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using MySFformat.Cloth.Preview;

namespace MySFformat
{
    public sealed partial class ClothInspectorForm : Form
    {
        readonly ComboBox domains = new ComboBox { Name="clothDomains", DropDownStyle=ComboBoxStyle.DropDownList, Width=420 };
        readonly TextBox summary = new TextBox { Name="clothSummary", Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false };
        readonly TreeView objects = new TreeView { Dock=DockStyle.Fill, Name="clothObjects" };
        readonly TextBox xml = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false };
        readonly ClothCanvas canvas = new ClothCanvas { Name="clothCanvas", Dock=DockStyle.Fill };
        readonly DataGridView particles = Grid("clothParticles");
        readonly DataGridView collisions = Grid("clothColliders");
        readonly DataGridView constraints = Grid("clothConstraints");
        readonly NumericUpDown factor = new NumericUpDown { Name="rangeFactor", Minimum=0.1m, Maximum=4, DecimalPlaces=2, Increment=0.1m, Value=1, Width=65 };
        readonly Label status = new Label { Name="clothStatus", Dock=DockStyle.Bottom, Height=62 };
        readonly Button load = new Button { Name="loadCloth", Text="Load Cloth…", AutoSize=true };
        readonly ToolTip tips = new ToolTip { AutoPopDelay=20000,InitialDelay=400,ReshowDelay=100,ShowAlways=true };
        readonly TextBox clmSource=new TextBox {Name="clm2Source",ReadOnly=true,Width=410};
        readonly ComboBox nativeLayout=new ComboBox {Name="nativeLayout",DropDownStyle=ComboBoxStyle.DropDownList,Width=230};
        bool updatingLayout;
        bool clmAutomaticallyLoaded;
        readonly Button previewPlay=new Button {Name="playClothPreview",Text="Play Cloth",AutoSize=true};
        readonly Button previewReset=new Button {Name="resetClothPreview",Text="Reset Cloth",AutoSize=true};
        readonly Button previewStop=new Button {Name="stopClothPreview",Text="Stop Cloth",AutoSize=true};
        readonly CheckBox gravityOverride=new CheckBox {Name="previewGravityOverride",Text="Override gravity",AutoSize=true};
        readonly NumericUpDown gravityX=EnvironmentNumber("previewGravityX",-100,100,0),gravityY=EnvironmentNumber("previewGravityY",-100,100,-9.81m),gravityZ=EnvironmentNumber("previewGravityZ",-100,100,0);
        readonly CheckBox windEnabled=new CheckBox {Name="previewWindEnabled",Text="Wind Enabled (Native HCT)",AutoSize=true,Checked=true};
        readonly NumericUpDown windX=EnvironmentNumber("previewWindX",-1,1,1),windY=EnvironmentNumber("previewWindY",-1,1,0),windZ=EnvironmentNumber("previewWindZ",-1,1,0);
        readonly NumericUpDown windSpeed=EnvironmentNumber("previewWindSpeed",0,50,3),windDrag=EnvironmentNumber("previewWindDrag",0,100,1);
        PreviewEnvironment appliedEnvironment;
        readonly Button previewRuntime=new Button {Name="chooseClothRuntime",Text="HCT Runtime…",AutoSize=true};
        readonly TextBox previewRuntimePath=new TextBox {Name="clothRuntimePath",ReadOnly=true,Width=280};
        readonly Label previewState=new Label {Name="clothPreviewState",AutoSize=true,MaximumSize=new Size(900,0),Padding=new Padding(4,7,0,0)};
        readonly System.Windows.Forms.Timer previewTimer=new System.Windows.Forms.Timer {Interval=16};
        readonly System.Diagnostics.Stopwatch previewClock=new System.Diagnostics.Stopwatch();
        ClothPreviewSession previewSession;
        Task<ClothPreviewFrame> pendingPreview;
        ClothPreviewFrame lastPreview;
        CancellationTokenSource previewCancellation;
        SoulsFormats.FLVER2 previewModel;
        bool previewPlaying,previewPreparing;
        int previewGeneration;
        double previewAccumulator;
        public int PreviewFrameNumber {get{return lastPreview==null?0:lastPreview.Frame;}}
        public bool IsPreviewPlaying {get{return previewPlaying;}}
        public int PreviewProcessId {get{return previewSession==null?0:previewSession.ProcessId;}}
        public string PreviewStatus {get{return previewState.Text;}}
        public Clm2Document Clm2 {get;private set;}
        public ClothDocument Document { get; private set; }
        public ClothInspectorForm()
        {
            Text="Cloth Inspector — Rest / Native Preview, Not Live Game";Width=1200;Height=850;MinimumSize=new Size(900,650);
            var top=new FlowLayoutPanel { Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents=true, Padding=new Padding(5) };
            top.Controls.Add(load);top.Controls.Add(domains);
            var factorLabel=new Label { Text="Export range scale", AutoSize=true, Padding=new Padding(0,7,0,0) };top.Controls.Add(factorLabel);top.Controls.Add(factor);
            var save=new Button { Name="saveRangeCopy",Text="Save Range XML Copy…",AutoSize=true };top.Controls.Add(save);
            foreach(var spec in new[]{"Particles","Colliders","Range Sphere"})
            {
                var c=new CheckBox { Text=spec,AutoSize=true,Checked=true };top.Controls.Add(c);
                tips.SetToolTip(c,spec=="Particles"?"Show fixed (yellow) and dynamic (cyan) particles.\n显示固定（黄色）和动态（青色）粒子。":spec=="Colliders"?"Show supported collider shapes at skeleton rest pose, not the live game pose.\n显示支持的碰撞形状在骨架rest姿态的位置，不是游戏实时姿态。":"Show the selected particle's maximumDistance sphere. Normal limits and other constraints are not represented.\n显示选中粒子的maximumDistance范围球；不代表法线限位和其他约束的合成可达范围。");
                c.CheckedChanged+=(s,e)=> { if(spec=="Particles")canvas.ShowParticles=c.Checked;if(spec=="Colliders")canvas.ShowColliders=c.Checked;if(spec=="Range Sphere")canvas.ShowRange=c.Checked;canvas.Invalidate(); };
            }
            var reset=new Button {Text="Reset View",AutoSize=true};top.Controls.Add(reset);reset.Click+=(s,e)=>canvas.ResetView();
            var second=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            var loadClm=new Button {Name="loadClm2",Text="Load CLM2…",AutoSize=true};
            var editClm=new Button {Name="editClm2",Text="Edit CLM2…",AutoSize=true};
            var saveClm=new Button {Name="saveClm2",Text="Save CLM2…",AutoSize=true};
            var verify=new Button {Name="verifyClothes",Text="Verify Clothes",AutoSize=true};
            second.Controls.AddRange(new Control[]{loadClm,editClm,saveClm,verify,clmSource});
            var third=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            var editHkx=new Button {Name="editHkx",Text="Edit HKX XML…",AutoSize=true};
            var saveHkx=new Button {Name="saveHkx",Text="Save HKX Copy…",AutoSize=true};
            var nativeLabel=new Label {Text="Native C# · BB / DS3 · Full readback gate · No Java",AutoSize=true,Padding=new Padding(4,7,0,0)};
            nativeLayout.Items.AddRange(new object[]{"8,1,1,1 (BB sample layout)","8,1,0,1 (DS3 sample layout)"});nativeLayout.SelectedIndex=0;
            third.Controls.AddRange(new Control[]{editHkx,saveHkx,nativeLayout,nativeLabel});
            tips.SetToolTip(editHkx,"Open an advanced full-XML editor on a clone. Validate/Apply uses the native C# pack/read gate and changes memory only.\n在克隆上弹窗编辑完整XML；Validate/Apply使用纯C#打包/读回校验，只修改内存。");
            tips.SetToolTip(saveHkx,"Write a new BB/DS3 cloth HKX with the selected header layout. Export range scale affects only the selected domain. Full schema/pack/readback validation runs before output creation.\n按所选包头布局另存BB/DS3布料HKX；范围倍率只影响所选域，创建输出前完成结构/打包/读回校验。");
            tips.SetToolTip(nativeLabel,"In-process C# with audited BB/DS3 schemas. Unsupported classes/SDKs and invalid data fail closed. Not a Havok runtime solver.\n进程内C#使用已验证BB/DS3结构；未支持类/SDK和错误数据明确拒绝，不是Havok运行时解算器。");
            tips.SetToolTip(nativeLayout,"Preserve the binary layout rule from HKX. This is not an infallible game-ID detector. Legacy XML without nativeLayout defaults to the BB sample layout; select the DS3 layout explicitly if needed. Changing it marks the document edited.\n保留HKX包头布局规则，并非可靠游戏ID探测。旧XML缺nativeLayout时默认BB样例布局；DS3需明确选择。手动切换会标记文档修改。");
            nativeLayout.SelectedIndexChanged+=(s,e)=>{if(updatingLayout||Document==null)return;StopClothPreview();undoDocuments.Push(ClothDocument.FromXml(Document.Xml,Document.Source,Document.Edited));redoDocuments.Clear();Document.SetNativeLayout(nativeLayout.SelectedIndex==1?"8,1,0,1":"8,1,1,1");stagedXml=new System.Xml.Linq.XDocument(Document.Xml);PopulateEditing();ShowDomainSummary();Text="Cloth Inspector — "+Path.GetFileName(Document.Source)+" [in-memory edits]";};
            clmSource.Text="No CLM2 loaded";
            tips.SetToolTip(loadClm,"Load a CLM2 as an independent companion document; existing data is kept if parsing fails.\n独立载入CLM2配套文档；解析失败时保留之前的数据。");
            tips.SetToolTip(editClm,"Open a separate editor for known mesh records and Unk00/Unk02 signed Int16 fields. Entry semantics remain unknown.\n弹窗编辑已知mesh记录和Unk00/Unk02有符号16位字段；条目含义仍未确认。");
            tips.SetToolTip(saveClm,"Save edited CLM2 to a new file. Sources and existing files are never overwritten.\n将修改后的CLM2另存到新文件，不覆盖源文件或已有文件。");
            tips.SetToolTip(verify,"Compare CLM2 mesh count with the current in-memory FLVER; inspect HKX display bindings and dimensions separately. Cloth domain count is not a CLM2 mesh count.\n对照CLM2与当前内存FLVER的mesh数量，另检HKX显示绑定和维数；布料域数不等于CLM2 mesh数。");
            tips.SetToolTip(clmSource,"Loaded CLM2 source and unsaved-edit state. This is independent of the selected HKX/XML source.\n显示CLM2来源和未保存修改状态，与HKX/XML来源独立。");
            var fourth=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            fourth.Controls.AddRange(new Control[]{previewPlay,previewStop,previewReset,previewRuntime,previewRuntimePath,previewState});
            previewRuntimePath.Text=ClothPreviewSession.DefaultRuntime;previewState.Text="Native preview off · constant skeleton pose · source data unchanged";
            tips.SetToolTip(previewPlay,"Run native Cloth in an isolated offline host, or pause it. This first version keeps the skeleton at its authored reference pose; no animation is connected.\n在独立离线宿主解算布料，或暂停；当前骨架保持作者参考姿态，尚未联动动画。");
            tips.SetToolTip(previewReset,"Stop the owned host, restore the rest view and start a fresh simulation from current in-memory data.\n停止本窗口宿主、恢复rest视图，再从当前内存数据重新初始化解算。");
            tips.SetToolTip(previewStop,"End this window's owned host and restore rest data without restarting; environment controls are retained.\n结束本窗口宿主、恢复rest数据，不重新启动；保留预览环境控件值。");
            tips.SetToolTip(previewRuntime,"Select hctPreviewPlugin.dll with the standard file picker; its parent runtime root is detected automatically. Six local HCT DLLs are required for native preview only.\n用标准文件选择窗口选择hctPreviewPlugin.dll，自动识别运行时根目录；仅原生解算预览需要6个本机HCT DLL。");
            tips.SetToolTip(previewRuntimePath,"Local optional proprietary runtime. Editor read/write works without it. FLVER_CLOTH_RUNTIME overrides the saved setting at startup.\n可选本机专有运行时；普通读写不依赖它。启动时FLVER_CLOTH_RUNTIME优先于保存设置。");
            tips.SetToolTip(previewState,"Simulation time/frame and native step cost, not Viewer FPS or game certification. Current HKX, FLVER and CLM2 are snapshotted without modifying originals.\n解算时间/帧及耗时，不是Viewer帧率或游戏验证；HKX、FLVER、CLM2以内存副本送入宿主，不改原文件。");
            previewPlay.Click+=async (s,e)=>{if(previewPlaying)PauseClothPreview();else await StartClothPreviewAsync();};
            previewReset.Click+=async (s,e)=>{StopClothPreview();await StartClothPreviewAsync();};
            previewStop.Click+=(s,e)=>StopClothPreview();
            previewRuntime.Click+=(s,e)=>{
                string initial=string.IsNullOrWhiteSpace(previewRuntimePath.Text)?"":Path.Combine(previewRuntimePath.Text,"tools");
                using(var dialog=new OpenFileDialog {Title="Select HCT hctPreviewPlugin.dll",Filter="HCT preview plugin (hctPreviewPlugin.dll)|hctPreviewPlugin.dll|DLL files (*.dll)|*.dll",FileName="hctPreviewPlugin.dll",InitialDirectory=Directory.Exists(initial)?initial:"",AutoUpgradeEnabled=true,CheckFileExists=true})
                {if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{ConfigurePreviewRuntimeFile(dialog.FileName);ClothPreviewSession.SaveRuntimeSetting(previewRuntimePath.Text);}catch(Exception ex){previewState.Text="Runtime selection failed: "+ex.Message;}}
            };
            previewTimer.Tick+=(s,e)=>PreviewTick();
            var environmentRow=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            Action<string,Control> environmentField=(label,control)=>{environmentRow.Controls.Add(new Label {Text=label,AutoSize=true,Padding=new Padding(2,5,0,0)});environmentRow.Controls.Add(control);};
            environmentRow.Controls.Add(gravityOverride);environmentField("Gx",gravityX);environmentField("Gy",gravityY);environmentField("Gz",gravityZ);environmentRow.Controls.Add(windEnabled);environmentField("Wx",windX);environmentField("Wy",windY);environmentField("Wz",windZ);environmentField("Speed",windSpeed);environmentField("Max drag",windDrag);
            var applyEnvironment=new Button {Name="applyClothEnvironment",Text="Apply Environment",AutoSize=true};var defaultsEnvironment=new Button {Name="defaultClothEnvironment",Text="File Gravity / Wind Off",AutoSize=true};environmentRow.Controls.Add(applyEnvironment);environmentRow.Controls.Add(defaultsEnvironment);
            var testWind=new Button {Name="testClothWind",Text="Wind Test (+X)",AutoSize=true};environmentRow.Controls.Add(testWind);
            testWind.Click+=(s,e)=>{windEnabled.Checked=true;windX.Value=1;windY.Value=windZ.Value=0;windSpeed.Value=20;windDrag.Value=5;editState.Text="Wind test preset pending · click Apply Environment, then Play if stopped";};
            tips.SetToolTip(testWind,"Stage a stronger diagnostic wind (X+, speed 20, max drag 5). Click Apply Environment. Not a game preset; constraints may limit motion.\n准备较强诊断风（+X，速度20、最大阻力5），再点Apply Environment；不是游戏预设，运动仍受约束限制。");
            foreach(var control in new Control[]{gravityOverride,gravityX,gravityY,gravityZ})tips.SetToolTip(control,"Preview gravity XYZ in authored coordinates. Only a temporary runtime HKX copy changes; author XML/HKX and export settings do not. Apply restarts the owned host.\n作者坐标系下的预览重力XYZ；只修改临时运行副本，不改作者XML/HKX或导出配置。Apply会重启本窗口宿主。");
            foreach(var control in new Control[]{windEnabled,windX,windY,windZ,windSpeed,windDrag})tips.SetToolTip(control,"Use native HCT wind controls: normalized XYZ direction, constant min=max speed, native maximum drag. Requires native wind/cloth participation; no fabricated particle offsets. Preview only.\n使用HCT原生风接口：归一化XYZ方向、min=max恒定风速及原生最大阻力；仍受布料风参与配置影响，不伪造粒子偏移，仅影响预览。");
            tips.SetToolTip(applyEnvironment,"Validate and apply preview-only settings; restart a running/paused host and preserve its play/pause state. No changes are saved to source files.\n验证并应用预览设置；重启当前宿主并保留播放/暂停状态，不保存到源文件。");
            tips.SetToolTip(defaultsEnvironment,"Restore file-authored gravity and disable preview wind, applying only to this preview.\n恢复文件作者重力，关闭预览风，仅影响本次预览。");
            applyEnvironment.Click+=async (s,e)=>await ApplyPreviewEnvironmentAsync();defaultsEnvironment.Click+=async (s,e)=>{gravityOverride.Checked=false;gravityX.Value=0;gravityY.Value=-9.81m;gravityZ.Value=0;windEnabled.Checked=false;windX.Value=1;windY.Value=windZ.Value=0;windSpeed.Value=3;windDrag.Value=1;await ApplyPreviewEnvironmentAsync();};
            var tools=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=3};
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<3;i++)tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));top.Dock=DockStyle.Fill;
            top.Controls.Remove(factorLabel);top.Controls.Remove(factor);top.Controls.Remove(save);
            foreach(Control control in third.Controls.Cast<Control>().ToArray()){third.Controls.Remove(control);top.Controls.Add(control);}
            nativeLabel.Visible=false;
            tools.Controls.Add(top,0,0);tools.Controls.Add(second,0,1);tools.Controls.Add(fourth,0,2);
            var split=new SplitContainer { Size=new Size(1150,640), Dock=DockStyle.Fill, SplitterDistance=540 };
            split.Panel1.Controls.Add(canvas);
            var tabs=new TabControl { Name="clothTabs",Dock=DockStyle.Fill,Multiline=true };
            var parameters=new TabPage("Parameters / Binding");parameters.Controls.Add(summary);tabs.TabPages.Add(parameters);
            foreach(var item in new[]{Tuple.Create("Particles / Range",particles),Tuple.Create("Colliders",collisions),Tuple.Create("Constraints",constraints)})
            {var page=new TabPage(item.Item1);page.Controls.Add(item.Item2);tabs.TabPages.Add(page);}
            var raw=new TabPage("Objects / Raw XML");var rawSplit=new SplitContainer {Size=new Size(550,600),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=220};rawSplit.Panel1.Controls.Add(objects);rawSplit.Panel2.Controls.Add(xml);raw.Controls.Add(rawSplit);tabs.TabPages.Add(raw);
            var environmentPage=new TabPage("Preview Environment") {AutoScroll=true};environmentRow.Dock=DockStyle.Top;environmentPage.Controls.Add(environmentRow);
            environmentRow.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(440,0),Text="Preview only. Wind uses native HCT stepping and is enabled by default. Apply to restart the active preview; Stop retains settings. Constraint ranges still limit movement.",Padding=new Padding(4)});
            fourth.Controls.Remove(previewRuntime);fourth.Controls.Remove(previewRuntimePath);environmentRow.Controls.Add(previewRuntime);environmentRow.Controls.Add(previewRuntimePath);tabs.TabPages.Add(environmentPage);
            var exportRow=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true};exportRow.Controls.AddRange(new Control[]{factorLabel,factor,save});raw.Controls.Add(exportRow);
            split.Panel2.Controls.Add(tabs);Controls.Add(split);Controls.Add(tools);Controls.Add(status);
            InitializeEditing(split,parameters);
            status.Text="Load BB / DS3 _c.hkx, parts Binder, or XML. Yellow: fixed (invMass=0); cyan: dynamic; orange: colliders.\nDrag to rotate, wheel to zoom, click a particle for its range sphere. Serialized rest data cannot confirm or rule out game-world wind.";
            tips.SetToolTip(load,"Load BB / DS3 cloth HKX, parts Binder, or XML using the native C# codec. No Java/Jar is needed. Source files remain read-only.\n使用纯C#编解码器载入BB/DS3布料HKX、装备Binder或XML，不依赖Java/Jar；源文件只读。");
            tips.SetToolTip(domains,"Choose one simulation domain; only this domain is affected by range export.\n选择模拟域；范围导出只修改当前选择的域。");
            tips.SetToolTip(factor,"Scale LocalRange distances on export only (0.10–4.00). Original data and current view do not change.\n只在导出时缩放LocalRange距离（0.10–4.00）；不修改源数据或当前视图。");
            tips.SetToolTip(factorLabel,tips.GetToolTip(factor));
            tips.SetToolTip(save,"Save a new author XML with scaled LocalRange in the selected domain. Never overwrites a source; not a playable HKX/Binder. Reload the copy to inspect it.\n另存所选域LocalRange缩放后的作者XML，不覆盖源文件；不能直接作为游戏HKX/Binder。重新载入副本即可查看修改。");
            tips.SetToolTip(reset,"Reset the rest-data camera without changing any cloth parameters.\n重置rest数据视角，不修改布料参数。");
            tips.SetToolTip(canvas,"Left select/brush, right rotate, middle pan, wheel zoom, F focus. Ctrl/Shift add, Alt remove. Native preview uses a constant authored skeleton.\n左键选择/刷选，右键旋转，中键平移，滚轮缩放，F聚焦；Ctrl/Shift添加、Alt移除；原生预览使用静态作者骨架。");
            tips.SetToolTip(summary,"Serialized parameters, solver schedule, bone subsets, and selected particle details. A bone subset is not per-particle skin weighting.\n查看文件参数、解算调度、骨集合和选中粒子详情；骨subset不是每个粒子的蒙皮权重。");
            tips.SetToolTip(particles,"Select a row to highlight its particle. Fixed state comes from invMass=0, not the fixedParticles array length.\n选择行高亮对应粒子；按invMass=0判定固定状态，而非fixedParticles数组长度。");
            tips.SetToolTip(collisions,"Supported collider endpoints/radii in skeleton rest space. Unsupported shapes remain available in Raw XML.\n查看支持的碰撞体rest端点/半径；未支持形状仍可在原始XML中检查。");
            tips.SetToolTip(constraints,"Inspect constraint records directly; stiffness fields from different constraint classes are not interchangeable.\n直接查看约束条目；不同约束类的刚度字段不能混用。");
            tips.SetToolTip(objects,"Select a referenced simulation, constraint, operator, collider, or shape object.\n选择引用的模拟、约束、算子、碰撞体或形状对象。");
            tips.SetToolTip(xml,"Read-only source fields of the selected object; no edits are silently saved.\n只读查看选中对象的原始字段，不会静默保存修改。");
            tips.SetToolTip(status,"Author edit/export messages. Native preview is a separate offline solver, not live-game verification.\n作者编辑/导出消息；原生预览是独立离线求解器，不代表游戏验证。");
            load.Click+=async (s,e)=> {
                using(var dialog=new OpenFileDialog { Filter="Cloth (*.xml;*.hkx;*.dcx)|*.xml;*.hkx;*.dcx",Title="Select BB / DS3 Cloth Data (Not Animation HKX)" })
                {if(dialog.ShowDialog(this)!=DialogResult.OK)return;load.Enabled=false;UseWaitCursor=true;
                    try { var loaded=await Task.Run(()=>ClothDocument.Load(dialog.FileName));if(!IsDisposed)SetDocument(loaded); }
                    catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,"Cloth Load Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}
                    finally {if(!IsDisposed){load.Enabled=true;UseWaitCursor=false;}}
                }
            };
            save.Click+=(s,e)=> {
                if(Document==null)return;
                using(var dialog=new SaveFileDialog { Filter="Author XML|*.xml",FileName="cloth-range-copy.xml",OverwritePrompt=true })
                {if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try{ExportRangeCopy(dialog.FileName);MessageBox.Show(this,"XML copy saved. Source files are unchanged.\nThis is not a playable HKX: pack and validate before game use.\nReload the exported XML to inspect the scaled ranges.","Export Complete");}
                    catch(Exception ex){MessageBox.Show(this,ex.Message,"Export Failed");}
                }
            };
            domains.SelectedIndexChanged+=(s,e)=>{if(!GuardDomainChange())return;ShowDomain();};
            objects.AfterSelect+=(s,e)=>{var o=e.Node.Tag as System.Xml.Linq.XElement;if(o!=null)xml.Text=o.ToString();};
            canvas.SelectionChanged+=()=>{SynchronizeParticleSelection();ShowDomainSummary();};
            loadClm.Click+=(s,e)=>{using(var dialog=new OpenFileDialog {Filter="CLM2 files|*.clm2;*.clm",Title="Load CLM2"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{LoadClm2File(dialog.FileName);}catch(Exception ex){MessageBox.Show(this,ex.Message,"CLM2 Load Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}}};
            editClm.Click+=(s,e)=>{if(Clm2==null){MessageBox.Show(this,"Load CLM2 first.","Edit CLM2");return;}using(var dialog=new Clm2EditForm(Clm2)){if(dialog.ShowDialog(this)==DialogResult.OK){StopClothPreview();Clm2=dialog.Result;RefreshClmLabel();}}};
            saveClm.Click+=(s,e)=>{if(Clm2==null){MessageBox.Show(this,"Load CLM2 first.","Save CLM2");return;}using(var dialog=new SaveFileDialog {Filter="CLM2 file|*.clm2",FileName=Path.GetFileNameWithoutExtension(Clm2.Source)+"-edited.clm2"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{SaveClm2Copy(dialog.FileName);status.Text="Saved CLM2 copy: "+dialog.FileName+"\nSource files unchanged. Run Verify Clothes against the intended FLVER/HKX before game testing.";}catch(Exception ex){MessageBox.Show(this,ex.Message,"CLM2 Save Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}}};
            verify.Click+=(s,e)=>{
                string report;try{report=VerifyClothes();}catch(Exception ex){report="[FAIL] Verification could not complete: "+ex.Message;}
                using(var window=new Form {Text="Verify Clothes — Layout and Binding Checks",Size=new Size(950,650),StartPosition=FormStartPosition.CenterParent})
                {var output=new TextBox {Name="clothesVerification",Text=report,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill};window.Controls.Add(output);var reportTips=new ToolTip {AutoPopDelay=20000,ShowAlways=true};reportTips.SetToolTip(output,"PASS/FAIL/WARN/SKIP describe static checks only, not gameplay certification. Current FLVER may contain unsaved edits.\nPASS/FAIL/WARN/SKIP只描述静态检查，不代表游戏验证；当前FLVER可能有未保存修改。");window.Disposed+=(a,b)=>reportTips.Dispose();window.ShowDialog(this);}
            };
            editHkx.Click+=(s,e)=>{if(editingDirty||applyingAuthor){status.Text="Apply or discard pending changes before advanced XML editing.";return;}if(Document==null){MessageBox.Show(this,"Load Cloth first.","Edit HKX");return;}using(var dialog=new HkxXmlEditForm(Document)){int index=domains.SelectedIndex;if(dialog.ShowDialog(this)==DialogResult.OK){SetDocument(dialog.Result);domains.SelectedIndex=Math.Min(index,Document.Domains.Count-1);status.Text="Validated HKX edits applied in memory. Source files are unchanged.";}}};
            saveHkx.Click+=(s,e)=>{
                if(Document==null){MessageBox.Show(this,"Load Cloth first.","Save HKX");return;}
                using(var dialog=new SaveFileDialog {Filter="BB / DS3 Cloth HKX|*.hkx",FileName="cloth-edited_c.hkx"})
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try{UseWaitCursor=true;ExportHkxCopy(dialog.FileName);status.Text="Native HKX copy saved after full schema/readback validation: "+dialog.FileName+"\nSource files unchanged. No Binder was repacked; gameplay remains untested.";}
                    catch(Exception ex){MessageBox.Show(this,ex.Message,"HKX Save Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}
                    finally{UseWaitCursor=false;}
                }
            };
        }
        public void LoadClm2File(string path){var document=Clm2Document.Load(path);StopClothPreview();Clm2=document;clmAutomaticallyLoaded=false;RefreshClmLabel();}
        public void ConfigurePreviewRuntime(string folder){ClothPreviewSession.ValidateRuntime(folder);StopClothPreview();previewRuntimePath.Text=Path.GetFullPath(folder);}
        static NumericUpDown EnvironmentNumber(string name,decimal minimum,decimal maximum,decimal value){return new NumericUpDown{Name=name,Minimum=minimum,Maximum=maximum,Value=value,DecimalPlaces=2,Increment=.1m,Width=62};}
        public PreviewEnvironment ReadPreviewEnvironment()
        {
            var settings=new PreviewEnvironment{OverrideGravity=gravityOverride.Checked,GravityX=(float)gravityX.Value,GravityY=(float)gravityY.Value,GravityZ=(float)gravityZ.Value,WindEnabled=windEnabled.Checked,WindX=(float)windX.Value,WindY=(float)windY.Value,WindZ=(float)windZ.Value,WindSpeed=(float)windSpeed.Value,WindMaxDrag=(float)windDrag.Value};settings.Validate();return settings;
        }
        public async Task ApplyPreviewEnvironmentAsync()
        {
            if(applyingAuthor){previewState.Text="Author Apply in progress; environment not applied.";return;}
            try{ReadPreviewEnvironment();bool restart=previewSession!=null||previewPreparing,paused=previewSession!=null&&!previewPlaying;StopClothPreview();if(restart){await StartClothPreviewAsync();if(paused&&previewSession!=null)PauseClothPreview();}else previewState.Text="Preview environment ready for next Play · no author data changed";previewEnvironmentDirty=false;UpdateEditState();}
            catch(Exception ex){previewState.Text="Environment not applied: "+ex.GetBaseException().Message;}
        }
        public void ConfigurePreviewRuntimeFile(string file)
        {
            if(!Path.GetFileName(file).Equals("hctPreviewPlugin.dll",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select hctPreviewPlugin.dll, not a Cloth filter/setup DLL.");
            var tools=Path.GetDirectoryName(Path.GetFullPath(file));var parent=Directory.GetParent(tools);if(parent==null)throw new InvalidOperationException("Invalid HCT runtime folder.");ConfigurePreviewRuntime(parent.FullName);
        }
        void AutoLoadClm2Companion()
        {
            // Keep a manually selected comparison or unsaved CLM2 edits intact.
            if(Clm2!=null&&(!clmAutomaticallyLoaded||Clm2.Edited))return;
            try{Clm2=Clm2Document.LoadCompanion(Document.Source);clmAutomaticallyLoaded=Clm2!=null;RefreshClmLabel();}
            catch(Exception ex){clmSource.Text=(Clm2==null?"No CLM2 loaded":Clm2.Source)+" [companion load failed]";status.Text="HKX loaded; CLM2 companion could not load: "+ex.Message+"\nPrevious CLM2, if present, was preserved.";}
        }
        public async Task StartClothPreviewAsync(PreviewEnvironment forcedEnvironment=null)
        {
            if(InvokeRequired)
            {
                var done=new TaskCompletionSource<bool>();BeginInvoke(new Action(async()=>{try{await StartClothPreviewAsync(forcedEnvironment);done.TrySetResult(true);}catch(Exception ex){done.TrySetException(ex);}}));await done.Task;return;
            }
            // Also support automation hosts pumping DoEvents without Application.Run.
            // Await continuations must return to this form's STA, not a pool thread.
            if(!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            if(previewPreparing||IsDisposed)return;
            if(previewSession!=null){previewPlaying=true;previewClock.Restart();previewTimer.Start();RefreshPreviewState();return;}
            int generation=++previewGeneration;previewPreparing=true;previewPlay.Enabled=false;previewReset.Enabled=true;previewCancellation=new CancellationTokenSource();var cancel=previewCancellation.Token;
            try
            {
                if(Document==null)throw new InvalidOperationException("Load Cloth first.");if(Program.targetFlver==null)throw new InvalidOperationException("Load the matching FLVER first.");
                if(Clm2==null)AutoLoadClm2Companion();
                var document=new System.Xml.Linq.XDocument(Document.Xml);var clm=Clm2==null?null:Clm2.Clone();var model=Program.targetFlver;
                string runtime=previewRuntimePath.Text;ClothPreviewSession.ValidateRuntime(runtime);previewModel=model;
                var expected=Document.Domains.ToDictionary(d=>d.Name,d=>d.Positions.Length);string snapshot=document.ToString();byte[] geometry=model.Write(),seams=clm==null?null:clm.Serialize();
                var progress=new Progress<string>(message=>{if(!IsDisposed&&generation==previewGeneration)previewState.Text=message;});
                var environment=forcedEnvironment==null?ReadPreviewEnvironment():forcedEnvironment.Clone();var session=await ClothPreviewSession.CreateAsync(snapshot,geometry,seams,runtime,cancel,progress,environment);
                if(IsDisposed||generation!=previewGeneration||cancel.IsCancellationRequested||!Object.ReferenceEquals(model,Program.targetFlver)){await Task.Run(()=>session.Dispose());return;}
                if(!session.DomainNames.OrderBy(s=>s).SequenceEqual(expected.Keys.OrderBy(s=>s))){await Task.Run(()=>session.Dispose());throw new InvalidDataException("Native preview domain names do not match the loaded document.");}
                appliedEnvironment=environment;previewSession=session;previewPreparing=false;previewPlaying=true;previewAccumulator=0;previewClock.Restart();previewTimer.Start();RefreshPreviewState();
                if(forcedEnvironment==null)previewEnvironmentDirty=false;UpdateEditState();
            }
            catch(Exception ex){if(!IsDisposed&&generation==previewGeneration){StopClothPreview();previewState.Text="Native preview unavailable: "+ex.GetBaseException().Message;}}
            finally{if(!IsDisposed&&generation==previewGeneration){previewPreparing=false;previewPlay.Enabled=true;}}
        }
        public void PauseClothPreview(){previewPlaying=false;previewClock.Reset();previewAccumulator=0;RefreshPreviewState();}
        internal void RequestStopForModelChange()
        {
            if(IsDisposed||!IsHandleCreated)return;
            Action stop=()=>{if(!IsDisposed&&(previewSession!=null||previewPreparing)){StopClothPreview();previewState.Text="Preview stopped: FLVER edit/refresh invalidated its snapshot.";}};
            if(InvokeRequired){try{BeginInvoke(stop);}catch(InvalidOperationException){}}else stop();
        }
        public void StopClothPreview()
        {
            previewGeneration++;previewPlaying=false;previewPreparing=false;previewTimer.Stop();previewClock.Reset();previewAccumulator=0;
            if(previewCancellation!=null){previewCancellation.Cancel();previewCancellation.Dispose();previewCancellation=null;}
            var retired=previewSession;previewSession=null;if(retired!=null)Task.Run(()=>retired.Dispose());
            if(pendingPreview!=null){pendingPreview.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);pendingPreview=null;}
            lastPreview=null;previewModel=null;canvas.SimulationPositions=null;canvas.PreviewLabel=null;canvas.Invalidate();previewPlay.Enabled=true;previewPlay.Text="Play Cloth";previewState.Text="Native preview off · rest data restored · sources unchanged";
        }
        void RefreshPreviewState()
        {
            previewPlay.Enabled=!previewPreparing;previewPlay.Text=previewPlaying?"Pause Cloth":"Play Cloth";
            if(previewSession!=null){previewState.Text=string.Format(CultureInfo.InvariantCulture,"Native Cloth {0} · frame {1} · {2:F3} s · {3:F3} ms/batch · gravity {4} · wind {5}",previewPlaying?"playing":"paused",PreviewFrameNumber,PreviewFrameNumber/60.0,lastPreview==null?0:lastPreview.SolveMilliseconds,appliedEnvironment.OverrideGravity?"preview override":"file",appliedEnvironment.WindEnabled?appliedEnvironment.WindSpeed.ToString("G3",CultureInfo.InvariantCulture):"off");canvas.PreviewLabel="NATIVE CLOTH · "+(previewPlaying?"playing":"paused")+" · frame "+PreviewFrameNumber+"\nConstant skeleton · "+(appliedEnvironment.WindEnabled?string.Format(CultureInfo.InvariantCulture,"Wind ({0:G3}, {1:G3}, {2:G3}) speed {3:G3} drag {4:G3}",appliedEnvironment.WindX,appliedEnvironment.WindY,appliedEnvironment.WindZ,appliedEnvironment.WindSpeed,appliedEnvironment.WindMaxDrag):"Wind off")+" · preview only";canvas.Invalidate();}
        }
        void ApplyPreviewPositions()
        {
            if(lastPreview==null||canvas.Domain==null)return;Vector3[] positions;
            if(!lastPreview.Domains.TryGetValue(canvas.Domain.Name,out positions)||positions.Length!=canvas.Domain.Positions.Length)throw new InvalidDataException("Native preview particle topology mismatch");
            canvas.SimulationPositions=positions;canvas.Invalidate();
        }
        void PreviewTick()
        {
            try
            {
                if(previewSession==null)return;if(!Object.ReferenceEquals(previewModel,Program.targetFlver)){StopClothPreview();previewState.Text="Preview stopped: current FLVER changed.";return;}
                if(pendingPreview!=null&&pendingPreview.IsCompleted){var completed=pendingPreview;pendingPreview=null;lastPreview=completed.GetAwaiter().GetResult();ApplyPreviewPositions();RefreshPreviewState();}
                if(!previewPlaying)return;double elapsed=previewClock.Elapsed.TotalSeconds;previewClock.Restart();previewAccumulator=Math.Min(4.0/60,previewAccumulator+elapsed);
                if(pendingPreview==null&&previewAccumulator>=1.0/60){int steps=Math.Min(4,(int)(previewAccumulator*60));previewAccumulator-=steps/60.0;pendingPreview=previewSession.StepAsync(steps);}
            }
            catch(Exception ex){StopClothPreview();previewState.Text="Native preview stopped: "+ex.GetBaseException().Message;}
        }
        public void SaveClm2Copy(string path){if(Clm2==null)throw new InvalidOperationException("Load CLM2 first.");Clm2.SaveCopy(path);}
        public string VerifyClothes(){return Clm2Document.Verify(Clm2,Document,Program.targetFlver,Program.orgFileName);}
        void RefreshClmLabel(){clmSource.Text=Clm2==null?"No CLM2 loaded":Clm2.Source+(Clm2.Edited?" [in-memory edits]":"");}
        static DataGridView Grid(string name)
        {return new DataGridView {Name=name,Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false};}
        public void LoadFile(string path) { SetDocument(ClothDocument.Load(path)); }
        public void ExportHkxCopy(string path){if(Document==null)throw new InvalidOperationException("Load Cloth first.");if(editingDirty||applyingAuthor)throw new InvalidOperationException("Apply or discard pending changes before export.");Document.SaveHkxCopy(path,domains.SelectedIndex,(double)factor.Value);}
        public static string FindAutoSource(string modelPath)
        {
            if(string.IsNullOrWhiteSpace(modelPath)||!File.Exists(modelPath))return null;
            string full=Path.GetFullPath(modelPath);
            if(Path.GetExtension(full).Equals(".flver",StringComparison.OrdinalIgnoreCase))
            {
                string hkx=Path.Combine(Path.GetDirectoryName(full),Path.GetFileNameWithoutExtension(full)+"_c.hkx");
                return File.Exists(hkx)?hkx:null;
            }
            // A directly opened parts Binder can supply its own cloth without extraction by the user.
            return Path.GetExtension(full).Equals(".dcx",StringComparison.OrdinalIgnoreCase)?full:null;
        }
        public async Task<bool> TryAutoLoadForModelAsync(string modelPath)
        {
            if(Document!=null||!load.Enabled)return false; // Do not discard a manual comparison document on reopening.
            string source=FindAutoSource(modelPath);
            if(source==null){status.Text="No matching <FLVER name>_c.hkx found next to the model. Use Load Cloth to select data manually.";return false;}
            load.Enabled=false;UseWaitCursor=true;status.Text="Auto-loading cloth: "+source;
            try
            {
                var document=await Task.Run(()=>ClothDocument.Load(source));
                if(IsDisposed)return false;SetDocument(document);
                status.Text="Auto-loaded: "+source+"\nSources unchanged. Edit author tables or Play Cloth for offline native preview; no live-game input.";
                return true;
            }
            catch(Exception ex){if(!IsDisposed)status.Text="Cloth auto-load failed: "+ex.Message+"\nUse Load Cloth to choose a valid file. No source data was modified.";return false;}
            finally{if(!IsDisposed){load.Enabled=true;UseWaitCursor=false;}}
        }
        public void ExportRangeCopy(string path) { if(editingDirty||applyingAuthor)throw new InvalidOperationException("Apply or discard pending changes before export.");Document.SaveRangeCopy(domains.SelectedIndex,(double)factor.Value,path); }
        void SetDocument(ClothDocument document)
        {
            StopClothPreview();
            ResetEditingForLoad();
            Document=document;AutoLoadClm2Companion();domains.Items.Clear();domains.Items.AddRange(document.Domains.Cast<object>().ToArray());domains.SelectedIndex=0;
            updatingLayout=true;nativeLayout.SelectedIndex=document.NativeLayout=="8,1,0,1"?1:0;updatingLayout=false;
            Text="Cloth Inspector — "+Path.GetFileName(document.Source)+(document.Edited?" [in-memory edits]":"")+" (Rest / Native Preview, Not Live Game)";
        }
        void ShowDomain()
        {
            if(Document==null||domains.SelectedIndex<0)return;
            populatingEditing=true;
            if(!editingDirty)stagedXml=new System.Xml.Linq.XDocument(Document.Xml);
            var domain=Document.Domains[domains.SelectedIndex];canvas.Domain=domain;canvas.Selected=-1;canvas.ResetView();
            canvas.SelectedParticles.Clear();
            canvas.SimulationPositions=null;ApplyPreviewPositions();
            objects.Nodes.Clear();
            Action<System.Xml.Linq.XElement> add=o=>objects.Nodes.Add(new TreeNode((string)o.Attribute("class")+" "+(string)o.Attribute("name")){Tag=o});
            add(domain.Sim);foreach(var c in domain.Constraints)add(c);
            foreach(var reference in ClothDocument.Value(domain.Owner,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))add(Document.Objects[reference]);
            foreach(var reference in ClothDocument.Value(domain.Sim,"perInstanceCollidables").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
            {var collider=Document.Objects[reference];add(collider);add(Document.Objects[ClothDocument.Value(collider,"shape")]);}
            if(objects.Nodes.Count>0)objects.SelectedNode=objects.Nodes[0];ShowDomainSummary();
            particles.Columns.Clear();foreach(var name in new[]{"Particle ID","Fixed","mass","invMass","radius","friction","maximumDistance","collisionMask"})particles.Columns.Add(name,name);
            var masks=ClothDocument.Numbers(ClothDocument.Value(domain.Sim,"staticCollisionMasks"));
            var rows=ClothDocument.Field(domain.Sim,"particleDatas").Elements().ToArray();
            for(int i=0;i<rows.Length;i++)particles.Rows.Add(i,domain.Fixed[i],ClothDocument.Value(rows[i],"mass"),ClothDocument.Value(rows[i],"invMass"),ClothDocument.Value(rows[i],"radius"),ClothDocument.Value(rows[i],"friction"),domain.Ranges[i],i<masks.Length?masks[i].ToString(CultureInfo.InvariantCulture):"Unknown");
            particles.ClearSelection();
            collisions.Columns.Clear();foreach(var name in new[]{"Bone","Shape","Endpoint A (rest)","Endpoint B (rest)","Radius A","Radius B"})collisions.Columns.Add(name,name);
            foreach(var c in domain.Colliders)collisions.Rows.Add(c.Bone,c.Shape,c.Start,c.End,c.RadiusA,c.RadiusB);
            constraints.Columns.Clear();foreach(var name in new[]{"Constraint Class","Name","Record","Fields"})constraints.Columns.Add(name,name);
            foreach(var c in domain.Constraints)
            foreach(var p in c.Elements().Where(p=>p.Elements().Any()))
            {int i=0;foreach(var row in p.Elements()){int index=constraints.Rows.Add((string)c.Attribute("class"),ClothDocument.Value(c,"name"),i,string.Join("; ",row.Elements().Select(v=>(string)v.Attribute("name")+"="+v.Value.Trim())));constraints.Rows[index].Tag=ClothDocument.Field(StagedObject(c),(string)p.Attribute("name")).Elements().ElementAt(i++);}}
            populatingEditing=false;PopulateEditing();
        }
        void ShowDomainSummary()
        {
            if(Document==null||domains.SelectedIndex<0)return;
            var d=Document.Domains[domains.SelectedIndex];var text=new StringBuilder();
            text.AppendLine("Source: "+Document.Source).AppendLine(d.Name);
            text.AppendLine("Havok64 2014.1 native header layout: "+Document.NativeLayout+" (not a game-ID guarantee)");
            text.AppendLine("Particles: "+d.Positions.Length+"; fixed (invMass=0): "+d.Fixed.Count(x=>x)+"; triangles: "+d.Triangles.Length/3);
            text.AppendLine("fixedParticles array count: "+ClothDocument.Field(d.Sim,"fixedParticles").Attribute("numelements").Value);
            text.AppendLine("Visualized collider shapes: "+d.Colliders.Count+" / "+ClothDocument.Field(d.Sim,"perInstanceCollidables").Attribute("numelements").Value+"; unsupported shapes: see Raw XML.");
            text.AppendLine("Original assets may have an empty fixedParticles array; fixed state uses invMass.");
            foreach(var p in ClothDocument.Field(d.Sim,"simulationInfo").Elements().Single().Elements())text.AppendLine((string)p.Attribute("name")+": "+p.Value.Trim());
            text.AppendLine("maxCollisionPairs: "+ClothDocument.Value(d.Sim,"maxCollisionPairs"));
            text.AppendLine("LocalRange maximumDistance (largest): "+d.Ranges.Max().ToString("G6",CultureInfo.InvariantCulture));
            foreach(var c in d.Constraints)text.AppendLine("Constraint: "+(string)c.Attribute("class")+" / "+ClothDocument.Value(c,"name"));
            var bones=ClothDocument.Field(Document.Objects.Values.First(o=>(string)o.Attribute("class")=="hkaSkeleton"),"bones").Elements().Select(o=>ClothDocument.Value(o,"name")).ToArray();
            foreach(var reference in ClothDocument.Value(d.Owner,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
            {
                var op=Document.Objects[reference];string cls=(string)op.Attribute("class");
                if(cls=="hclSimulateOperator")foreach(var p in op.Elements())text.AppendLine("Solver "+(string)p.Attribute("name")+": "+p.Value.Trim());
                var subset=op.Elements().FirstOrDefault(p=>(string)p.Attribute("name")=="transformSubset");
                if(subset!=null)text.AppendLine("Skin bone subset (not per-particle weights): "+string.Join(", ",ClothDocument.Numbers(subset.Value).Select(i=>bones[(int)i])));
            }
            foreach(var c in d.Colliders)text.AppendLine("Collider: "+c.Bone+" / "+c.Shape+" R="+c.RadiusA.ToString("F4")+" / "+c.RadiusB.ToString("F4"));
            if(canvas.Selected>=0)
            {
                int i=canvas.Selected;text.AppendLine("\r\nSelected particle "+i+" "+(d.Fixed[i]?"Fixed":"Dynamic")+" rest="+d.Positions[i]+" range="+d.Ranges[i].ToString("G6"));
                var rows=ClothDocument.Field(d.Sim,"particleDatas").Elements().ToArray();text.AppendLine(rows[i].ToString());
                var masks=ClothDocument.Numbers(ClothDocument.Value(d.Sim,"staticCollisionMasks"));if(i<masks.Length)text.AppendLine("collisionMask: "+masks[i]);
            }
            text.AppendLine("\r\nNative C# BB / DS3 read/write. Play Cloth optionally runs an isolated native solver at a constant authored skeleton pose, not live-game or animation input.\r\nRange spheres show maximumDistance at authored reference centers only, not combined constraints. Orange wireframes show reference colliders.\r\nSource files/current FLVER remain unchanged. Save HKX Copy writes a new file after full field readback validation; export range scale affects only the selected domain.");summary.Text=text.ToString();
        }
        protected override void Dispose(bool disposing) { if(disposing){StopClothPreview();previewTimer.Dispose();tips.Dispose();}base.Dispose(disposing); }
    }
    public sealed partial class ClothCanvas : Panel
    {
        public ClothDomain Domain;
        public Vector3[] SimulationPositions;
        public string PreviewLabel;
        Vector3[] DisplayPositions {get{return SimulationPositions!=null&&Domain!=null&&SimulationPositions.Length==Domain.Positions.Length?SimulationPositions:Domain.Positions;}}
        public int Selected=-1;
        public bool ShowParticles=true,ShowColliders=true,ShowRange=true;
        public event Action SelectionChanged;
        float yaw=.35f,pitch=.1f,zoom=1,span=1;Vector3 center;Point down,last;bool drag;
        public ClothCanvas()
        {
            DoubleBuffered=true;BackColor=Color.FromArgb(24,27,34);
            InitializeSelectionInput();
            MouseWheel+=(s,e)=>{zoom=Math.Max(.1f,Math.Min(10,zoom*(e.Delta>0?1.15f:.87f)));Invalidate();};Resize+=(s,e)=>Invalidate();
        }
        public void ResetView()
        {
            yaw=.35f;pitch=.1f;zoom=1;pan=PointF.Empty;
            if(Domain!=null){var points=Domain.Positions.Concat(Domain.Colliders.SelectMany(c=>new[]{c.Start,c.End})).ToArray();var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);center=(min+max)*.5f;span=Math.Max(.1f,(max-min).Length());}Invalidate();
        }
        float PixelScale {get{return Math.Min(Width,Height)*.8f*zoom/span;}}
        PointF Project(Vector3 p)
        {var q=Vector3.Transform(p-center,Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateRotationX(pitch));return new PointF(Width*.5f+pan.X+q.X*PixelScale,Height*.5f+pan.Y-q.Y*PixelScale);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if(Domain==null){g.DrawString("Load Cloth data to inspect rest particles / colliders",Font,Brushes.White,12,12);return;}
            var display=DisplayPositions;
            using(var edge=new Pen(Color.FromArgb(100,150,175)))for(int i=0;i<Domain.Triangles.Length;i+=3){var a=Project(display[Domain.Triangles[i]]);var b=Project(display[Domain.Triangles[i+1]]);var c=Project(display[Domain.Triangles[i+2]]);g.DrawLine(edge,a,b);g.DrawLine(edge,b,c);g.DrawLine(edge,c,a);}
            if(ShowColliders)using(var pen=new Pen(Color.FromArgb(180,255,150,50)))foreach(var c in Domain.Colliders)
            {
                // Sample spheres + cone side rings in world rest space, not screen-radius impostors.
                var axis=c.End-c.Start;var unit=axis.LengthSquared()>1e-12f?Vector3.Normalize(axis):Vector3.UnitY;
                var u=Vector3.Normalize(Vector3.Cross(unit,Math.Abs(unit.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var v=Vector3.Cross(unit,u);
                for(int j=0;j<32;j++){float t=j*(float)Math.PI/16,t2=(j+1)*(float)Math.PI/16;var dir=u*(float)Math.Cos(t)+v*(float)Math.Sin(t);var next=u*(float)Math.Cos(t2)+v*(float)Math.Sin(t2);g.DrawLine(pen,Project(c.Start+dir*c.RadiusA),Project(c.Start+next*c.RadiusA));g.DrawLine(pen,Project(c.End+dir*c.RadiusB),Project(c.End+next*c.RadiusB));if(j%8==0){g.DrawLine(pen,Project(c.Start+dir*c.RadiusA),Project(c.End+dir*c.RadiusB));}}
                foreach(var endpoint in new[]{Tuple.Create(c.Start,c.RadiusA),Tuple.Create(c.End,c.RadiusB)})foreach(var basis in new[]{Tuple.Create(u,v),Tuple.Create(u,unit),Tuple.Create(v,unit)})for(int j=0;j<32;j++){float t=j*(float)Math.PI/16,t2=(j+1)*(float)Math.PI/16;g.DrawLine(pen,Project(endpoint.Item1+endpoint.Item2*(basis.Item1*(float)Math.Cos(t)+basis.Item2*(float)Math.Sin(t))),Project(endpoint.Item1+endpoint.Item2*(basis.Item1*(float)Math.Cos(t2)+basis.Item2*(float)Math.Sin(t2))));}
            }
            if(ShowParticles)for(int i=0;i<display.Length;i++){var p=Project(display[i]);g.FillEllipse(Domain.Fixed[i]?Brushes.Gold:Brushes.Cyan,p.X-2,p.Y-2,4,4);}
            foreach(int id in SelectedParticles)if(id>=0&&id<display.Length){var p=Project(display[id]);g.FillEllipse(Brushes.White,p.X-3,p.Y-3,6,6);g.DrawEllipse(Pens.Magenta,p.X-5,p.Y-5,10,10);}
            if(Selected>=0){var p=Project(display[Selected]);g.FillEllipse(Brushes.White,p.X-4,p.Y-4,8,8);if(ShowRange&&Domain.Ranges[Selected]>0){var anchor=Project(Domain.Positions[Selected]);float radius=Domain.Ranges[Selected]*PixelScale;g.DrawEllipse(Pens.Lime,anchor.X-radius,anchor.Y-radius,radius*2,radius*2);}}
            DrawSelectionOverlay(g);
            g.DrawString(PreviewLabel??"REST VIEW · Left select · Right rotate · Middle pan · Wheel zoom\nYellow: fixed; cyan: dynamic; magenta: selected; orange: colliders",Font,Brushes.White,8,8);
        }
    }
    static partial class Program
    {
        public static ClothInspectorForm clothInspectorForm;
        internal static void OpenClothInspector()
        {
            var ui=nodeUIForm;if(ui==null||ui.IsDisposed||!ui.IsHandleCreated)return;
            ui.BeginInvoke(new Action(async ()=>{if(ui.IsDisposed)return;if(clothInspectorForm==null||clothInspectorForm.IsDisposed)clothInspectorForm=new ClothInspectorForm();var window=clothInspectorForm;if(!window.Visible)window.Show(ui);window.BringToFront();await window.TryAutoLoadForModelAsync(orgFileName);}));
        }
    }
}
