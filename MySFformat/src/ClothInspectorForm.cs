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
        // 职责划分：本文件负责界面组装、文档展示与预览生命周期。
        // 文件弹窗在 ClothInspectorActions；作者编辑在 ClothEditing；
        // 选区属性/吸管在 ClothSelectionEditor；HKX 复用逻辑在 ClothAnimation。
        // 所有原生 DLL 调用均留在独立宿主，本 Form 不加载厂商 DLL。
        readonly ComboBox domains = new ComboBox { Name="clothDomains", DropDownStyle=ComboBoxStyle.DropDownList, Width=420 };
        readonly TextBox summary = new TextBox { Name="clothSummary", Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false };
        readonly TreeView objects = new TreeView { Dock=DockStyle.Fill, Name="clothObjects" };
        readonly TextBox xml = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false };
        readonly ClothCanvas canvas = new ClothCanvas { Name="clothCanvas", Dock=DockStyle.Fill };
        readonly DataGridView particles = Grid("clothParticles");
        readonly DataGridView collisions = Grid("clothColliders");
        readonly DataGridView constraints = Grid("clothConstraints");
        readonly NumericUpDown factor = new NumericUpDown { Name="rangeFactor", Minimum=0.1m, Maximum=4, DecimalPlaces=2, Increment=0.1m, Value=1, Width=65 };
        readonly TextBox status = new TextBox { Name="clothStatus", Dock=DockStyle.Bottom, Height=44, Multiline=true, ReadOnly=true, WordWrap=true, ScrollBars=ScrollBars.Vertical, TabStop=false };
        readonly Button load = new Button { Name="loadCloth", Text="Load Cloth…", AutoSize=true };
        readonly ToolTip tips = new ToolTip { AutoPopDelay=20000,InitialDelay=400,ReshowDelay=100,ShowAlways=true };
        readonly TextBox clmSource=new TextBox {Name="clm2Source",ReadOnly=true,Width=410};
        readonly ComboBox nativeLayout=new ComboBox {Name="nativeLayout",DropDownStyle=ComboBoxStyle.DropDownList,Width=230};
        readonly Label nativeLayoutLabel=new Label {Name="nativeLayoutLabel",Text="Layout version:",AutoSize=true,Padding=new Padding(4,7,0,0)};
        bool updatingLayout;
        bool clmAutomaticallyLoaded;
        readonly Button previewPlay=new Button {Name="playClothPreview",Text="Play Cloth",AutoSize=true};
        readonly Button previewReset=new Button {Name="resetClothPreview",Text="Reset Cloth",AutoSize=true};
        readonly Button previewStop=new Button {Name="stopClothPreview",Text="Stop Cloth",AutoSize=true};
        readonly Button viewReset=new Button {Name="resetClothView",Text="Reset View",AutoSize=true};
        readonly CheckBox gravityOverride=new CheckBox {Name="previewGravityOverride",Text="Override gravity",AutoSize=true};
        readonly NumericUpDown gravityX = EnvironmentNumber("previewGravityX", -100, 100, 0);
        readonly NumericUpDown gravityY = EnvironmentNumber("previewGravityY", -100, 100, -9.81m);
        readonly NumericUpDown gravityZ = EnvironmentNumber("previewGravityZ", -100, 100, 0);
        readonly CheckBox windEnabled=new CheckBox {Name="previewWindEnabled",Text="Wind Enabled (Native HCT)",AutoSize=true,Checked=true};
        readonly NumericUpDown windX = EnvironmentNumber("previewWindX", -1, 1, 1);
        readonly NumericUpDown windY = EnvironmentNumber("previewWindY", -1, 1, 0);
        readonly NumericUpDown windZ = EnvironmentNumber("previewWindZ", -1, 1, 0);
        readonly NumericUpDown windSpeed = EnvironmentNumber("previewWindSpeed", 0, 50, 3);
        readonly NumericUpDown windDrag = EnvironmentNumber("previewWindDrag", 0, 100, 1);
        PreviewEnvironment appliedEnvironment;
        readonly ComboBox previewBackend=new ComboBox {Name="clothPreviewBackend",DropDownStyle=ComboBoxStyle.DropDownList,Width=255};
        readonly Label previewBackendNote=new Label {Name="clothPreviewBackendNote",AutoSize=true,MaximumSize=new Size(440,0),Padding=new Padding(4)};
        readonly CheckBox allowUnmodeledConstraints=new CheckBox {Name="allowUnmodeledClothConstraints",Text="Allow unmodeled constraints to be omitted (approximate)",AutoSize=true};
        readonly Button previewRuntime=new Button {Name="chooseClothRuntime",Text="HCT Runtime…",AutoSize=true};
        readonly TextBox previewRuntimePath=new TextBox {Name="clothRuntimePath",ReadOnly=true,Width=280};
        readonly Label previewState=new Label {Name="clothPreviewState",AutoSize=true,MaximumSize=new Size(900,0),Padding=new Padding(4,7,0,0)};
        readonly System.Windows.Forms.Timer previewTimer=new System.Windows.Forms.Timer {Interval=16};
        readonly System.Diagnostics.Stopwatch previewClock=new System.Diagnostics.Stopwatch();
        IClothPreviewSession previewSession;
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
            // 固定分行：文件/显示、HKX版本、CLM2、物理。不能依赖 FlowLayout 自动换行。
            Text="Cloth Inspector";Width=1200;Height=850;MinimumSize=new Size(900,650);
            var top=new FlowLayoutPanel { Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents=true, Padding=new Padding(5) };
            var setup=new Button{Name="clothSetupWizard",Text="Cloth Setup Wizard",AutoSize=true};
            top.Controls.Add(setup);top.Controls.Add(load);top.Controls.Add(verificationIndicator);top.Controls.Add(domains);
            tips.SetToolTip(setup,"Create an experimental Bloodborne single-bone Cloth setup from a FLVER snapshot; save a new FLVER/HKX/CLM2 set. No collision, source changes or game launch.\n从FLVER快照生成实验性血源单骨布料设置，另存FLVER/HKX/CLM2；无人体碰撞，不改源文件，不启动游戏。");
            setup.Click+=(s,e)=>{try{if(applyingAuthor||previewPreparing)throw new InvalidOperationException("Wait for the current Cloth operation to finish.");using(var wizard=new ClothSetupWizardForm(Program.targetFlver))wizard.ShowDialog(this);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Cloth Setup Wizard",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
            var factorLabel=new Label { Text="Export range scale", AutoSize=true, Padding=new Padding(0,7,0,0) };top.Controls.Add(factorLabel);top.Controls.Add(factor);
            var save=new Button { Name="saveRangeCopy",Text="Save Range XML Copy…",AutoSize=true };top.Controls.Add(save);
            foreach(var spec in new[]{"Particles","Colliders","Range Sphere"})
            {
                var c=new CheckBox { Text=spec,AutoSize=true,Checked=true };top.Controls.Add(c);
                tips.SetToolTip(c,spec=="Particles"?"Show fixed (yellow) and dynamic (cyan) particles.\n显示固定（黄色）和动态（青色）粒子。":spec=="Colliders"?"Show supported collider shapes at the current preview pose.\n显示当前预览姿态的碰撞形状。":"Show the selected particle's maximumDistance sphere. Normal limits and other constraints are not represented.\n显示选中粒子的maximumDistance范围球；不代表法线限位和其他约束的合成可达范围。");
                c.CheckedChanged+=(s,e)=> { if(spec=="Particles")canvas.ShowParticles=c.Checked;if(spec=="Colliders")canvas.ShowColliders=c.Checked;if(spec=="Range Sphere")canvas.ShowRange=c.Checked;canvas.Invalidate(); };
            }
            viewReset.Click+=(s,e)=>canvas.ResetView();
            var second=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            var loadClm=new Button {Name="loadClm2",Text="Load CLM2…",AutoSize=true};
            var editClm=new Button {Name="editClm2",Text="Edit CLM2…",AutoSize=true};
            var saveClm=new Button {Name="saveClm2",Text="Save CLM2…",AutoSize=true};
            verificationIndicator.LinkClicked+=(s,e)=>OnVerifyClothesClick(s,e);
            second.Controls.AddRange(new Control[]{loadClm,editClm,saveClm,clmSource});
            var third=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            var editHkx=new Button {Name="editHkx",Text="Edit HKX XML…",AutoSize=true};
            var saveHkx=new Button {Name="saveHkx",Text="Save HKX Copy…",AutoSize=true};
            var nativeLabel=new Label {Text="Native C# · BB/DS3 read-write · Sekiro/ER TAG0 read-only",AutoSize=true,Padding=new Padding(4,7,0,0)};
            nativeLayout.Items.AddRange(new object[]{"8,1,1,1 (BB sample layout)","8,1,0,1 (DS3 sample layout)"});nativeLayout.SelectedIndex=0;
            third.Controls.AddRange(new Control[]{editHkx,saveHkx,nativeLayoutLabel,nativeLayout,nativeLabel});
            tips.SetToolTip(editHkx,"Open an advanced full-XML editor on a clone, changes memory only, press save HKX Copy to save to the harddrive.\n在克隆上弹窗编辑完整XML；只修改内存，记得点Save存硬盘。");
            tips.SetToolTip(saveHkx,"Write a new BB/DS3 cloth HKX with the selected header layout. Export range scale affects only the selected domain.\n按所选包头布局另存BB/DS3布料HKX；范围倍率只影响所选域。");
            tips.SetToolTip(nativeLabel,"In-process C# with audited BB/DS3 schemas. Unsupported classes/SDKs and invalid data fail closed. Not a Havok runtime solver.\n进程内C#使用已验证BB/DS3结构；未支持类/SDK和错误数据明确拒绝，不是Havok运行时解算器。");
            tips.SetToolTip(nativeLayout,
                "Preserve the binary layout rule from HKX. This is not an infallible game-ID detector. " +
                "Legacy XML without nativeLayout defaults to the BB sample layout; select the DS3 layout explicitly if needed. " +
                "Changing it marks the document edited.\n" +
                "保留HKX包头布局规则，并非可靠游戏ID探测。旧XML缺nativeLayout时默认BB样例布局；" +
                "DS3需明确选择。手动切换会标记文档修改。");
            tips.SetToolTip(nativeLayoutLabel,tips.GetToolTip(nativeLayout));
            nativeLayout.SelectedIndexChanged += OnNativeLayoutChanged;
            clmSource.Text="No CLM2 loaded";
            tips.SetToolTip(loadClm,"Load a CLM2 as an independent companion document; existing data is kept if parsing fails.\n独立载入CLM2配套文档；解析失败时保留之前的数据。");
            tips.SetToolTip(editClm,"Open a separate editor for known mesh records and Unk00/Unk02 signed Int16 fields. Entry semantics remain unknown.\n弹窗编辑已知mesh记录和Unk00/Unk02有符号16位字段；条目含义仍未确认。");
            tips.SetToolTip(saveClm,"Save edited CLM2 to a new file. Sources and existing files are never overwritten.\n将修改后的CLM2另存到新文件，不覆盖源文件或已有文件。");
            tips.SetToolTip(verificationIndicator,"Check CLM2 mesh counts, HKX display bindings and dimensions against the current FLVER.\n对照当前FLVER检查CLM2网格数量、HKX显示绑定和维数。");
            tips.SetToolTip(clmSource,"Loaded CLM2 source and unsaved-edit state. This is independent of the selected HKX/XML source.\n显示CLM2来源和未保存修改状态，与HKX/XML来源独立。");
            var fourth=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            fourth.Controls.AddRange(new Control[]{previewPlay,previewStop,previewReset,viewReset,previewRuntime,previewRuntimePath,previewState});
            previewRuntimePath.Text=ClothPreviewSession.DefaultRuntime;previewState.Text="Cloth preview off";
            tips.SetToolTip(previewPlay,"Play or pause the selected solver. Both solvers accept linked HKX animation.\n播放或暂停当前解算器；两种解算器均可联动HKX动画。");
            tips.SetToolTip(previewReset,"Stop the owned host, restore the rest view and start a fresh simulation from current in-memory data.\n停止本窗口宿主、恢复rest视图，再从当前内存数据重新初始化解算。");
            tips.SetToolTip(previewStop,"End this window's owned host and restore rest data without restarting; environment controls are retained.\n结束本窗口宿主、恢复rest数据，不重新启动；保留预览环境控件值。");
            tips.SetToolTip(previewRuntime,"Select hctPreviewPlugin.dll with the standard file picker; its parent runtime root is detected automatically. Six local HCT DLLs are required for native preview only.\n用标准文件选择窗口选择hctPreviewPlugin.dll，自动识别运行时根目录；仅原生解算预览需要6个本机HCT DLL。");
            tips.SetToolTip(previewRuntimePath,"Local optional proprietary runtime. Editor read/write works without it. FLVER_CLOTH_RUNTIME overrides the saved setting at startup.\n可选本机专有运行时；普通读写不依赖它。启动时FLVER_CLOTH_RUNTIME优先于保存设置。");
            tips.SetToolTip(previewState,"Simulation time, frame count and step cost.\n模拟时间、帧数和单步耗时。");
            previewPlay.Click+=async (s,e)=>{if(previewPlaying)PauseClothPreview();else await StartClothPreviewAsync();};
            previewReset.Click+=async (s,e)=>{StopClothPreview();await StartClothPreviewAsync();};
            previewStop.Click+=(s,e)=>StopClothPreview();
            previewRuntime.Click += OnChooseRuntimeClick;
            previewTimer.Tick+=(s,e)=>PreviewTick();
            // 环境和 HKX 动画使用独立 tab，不把更多参数堆到主工具栏。
            var environmentRow=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(5,0,5,5)};
            previewBackend.Items.AddRange(new object[]{"Native HCT","OpenPhysicsHKX (built-in, experimental)"});previewBackend.SelectedIndex=0;
            environmentRow.Controls.Add(new Label {Name="clothSolverLabel",Text="Solver",AutoSize=true,Padding=new Padding(2,5,0,0)});environmentRow.Controls.Add(previewBackend);environmentRow.SetFlowBreak(previewBackend,true);
            environmentRow.Controls.Add(allowUnmodeledConstraints);environmentRow.SetFlowBreak(allowUnmodeledConstraints,true);
            tips.SetToolTip(allowUnmodeledConstraints,"Allow OpenPhysicsHKX to skip unsupported constraint classes; names remain in status. Does not enable wind or unsupported mappings. Off by default.\n允许OpenPhysicsHKX跳过未支持约束，状态持续列名；不启用风或未支持映射，默认关闭。");
            tips.SetToolTip(previewBackend,"Native HCT uses a user-provided runtime. OpenPhysicsHKX uses the bundled C# DLL; no runtime folder is needed. Select and Apply Environment. Unsupported assets fail explicitly.\nHCT需要自备运行时；OpenPhysicsHKX使用自带C# DLL，无需选择目录。选择后点Apply Environment；不支持的资源会明确提示。");
            Action<string,Control> environmentField=(label,control)=>{environmentRow.Controls.Add(new Label {Text=label,AutoSize=true,Padding=new Padding(2,5,0,0)});environmentRow.Controls.Add(control);};
            environmentRow.Controls.Add(gravityOverride);environmentField("Gx",gravityX);environmentField("Gy",gravityY);environmentField("Gz",gravityZ);environmentRow.Controls.Add(windEnabled);environmentField("Wx",windX);environmentField("Wy",windY);environmentField("Wz",windZ);environmentField("Speed",windSpeed);environmentField("Max drag",windDrag);
            var applyEnvironment=new Button {Name="applyClothEnvironment",Text="Apply Environment",AutoSize=true};var defaultsEnvironment=new Button {Name="defaultClothEnvironment",Text="File Gravity / Wind Off",AutoSize=true};environmentRow.Controls.Add(applyEnvironment);environmentRow.Controls.Add(defaultsEnvironment);
            var testWind=new Button {Name="testClothWind",Text="Wind Test (+X)",AutoSize=true};environmentRow.Controls.Add(testWind);
            testWind.Click+=(s,e)=>{windEnabled.Checked=true;windX.Value=1;windY.Value=windZ.Value=0;windSpeed.Value=20;windDrag.Value=5;editState.Text="Wind test preset pending · click Apply Environment, then Play if stopped";};
            tips.SetToolTip(testWind,"Stage a stronger diagnostic wind (X+, speed 20, max drag 5). Click Apply Environment. Not a game preset; constraints may limit motion.\n准备较强诊断风（+X，速度20、最大阻力5），再点Apply Environment；不是游戏预设，运动仍受约束限制。");
            foreach(var control in new Control[]{gravityOverride,gravityX,gravityY,gravityZ})tips.SetToolTip(control,"Override gravity XYZ in model coordinates for this preview. File Settings and exports are unchanged; Apply restarts the simulation.\n在模型坐标系下临时覆盖预览重力；不改文件设置和导出内容，Apply会重启模拟。");
            foreach(var control in new Control[]{windEnabled,windX,windY,windZ,windSpeed,windDrag})tips.SetToolTip(control,"Use native HCT wind controls: normalized XYZ direction, constant min=max speed, native maximum drag. Requires native wind/cloth participation; no fabricated particle offsets. Preview only.\n使用HCT原生风接口：归一化XYZ方向、min=max恒定风速及原生最大阻力；仍受布料风参与配置影响，不伪造粒子偏移，仅影响预览。");
            tips.SetToolTip(applyEnvironment,"Validate and apply preview-only settings; restart a running/paused host and preserve its play/pause state. No changes are saved to source files.\n验证并应用预览设置；重启当前宿主并保留播放/暂停状态，不保存到源文件。");
            tips.SetToolTip(defaultsEnvironment,"Use gravity from File Settings and disable preview wind.\n使用文件设置中的重力，关闭预览风。");
            applyEnvironment.Click+=async (s,e)=>await ApplyPreviewEnvironmentAsync();defaultsEnvironment.Click+=async (s,e)=>{gravityOverride.Checked=false;gravityX.Value=0;gravityY.Value=-9.81m;gravityZ.Value=0;windEnabled.Checked=false;windX.Value=1;windY.Value=windZ.Value=0;windSpeed.Value=3;windDrag.Value=1;await ApplyPreviewEnvironmentAsync();};
            var tools=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=4};
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<4;i++)tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));top.Dock=DockStyle.Fill;
            top.Controls.Remove(factorLabel);top.Controls.Remove(factor);top.Controls.Remove(save);
            nativeLabel.Visible=false;
            tools.Controls.Add(top,0,0);tools.Controls.Add(third,0,1);tools.Controls.Add(second,0,2);tools.Controls.Add(fourth,0,3);
            var split=new SplitContainer { Size=new Size(1150,640), Dock=DockStyle.Fill, SplitterDistance=540 };
            split.Panel1.Controls.Add(canvas);
            var tabs=new TabControl { Name="clothTabs",Dock=DockStyle.Fill,Multiline=true };
            var parameters=new TabPage("File Settings / Binding");parameters.Controls.Add(summary);tabs.TabPages.Add(parameters);
            foreach(var item in new[]{Tuple.Create("Particles / Range",particles),Tuple.Create("Colliders",collisions),Tuple.Create("Constraints",constraints)})
            {var page=new TabPage(item.Item1);page.Controls.Add(item.Item2);tabs.TabPages.Add(page);}
            var raw=new TabPage("Objects / Raw XML");var rawSplit=new SplitContainer {Size=new Size(550,600),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=220};rawSplit.Panel1.Controls.Add(objects);rawSplit.Panel2.Controls.Add(xml);raw.Controls.Add(rawSplit);tabs.TabPages.Add(raw);
            var environmentPage=new TabPage("Preview Environment") {AutoScroll=true};environmentRow.Dock=DockStyle.Top;environmentPage.Controls.Add(environmentRow);
            environmentRow.Controls.Add(previewBackendNote);
            fourth.Controls.Remove(previewRuntime);fourth.Controls.Remove(previewRuntimePath);environmentRow.Controls.Add(previewRuntime);environmentRow.Controls.Add(previewRuntimePath);tabs.TabPages.Add(environmentPage);
            InitializeMeshPreview(environmentRow);
            var exportRow=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true};exportRow.Controls.AddRange(new Control[]{factorLabel,factor,save});raw.Controls.Add(exportRow);
            split.Panel2.Controls.Add(tabs);Controls.Add(split);Controls.Add(tools);Controls.Add(status);
            InitializeEditing(split,parameters);
            InitializeSelectionEditor(split);
            InitializeClothAnimation(tabs);
            previewBackend.SelectedIndexChanged+=(s,e)=>{RefreshBackendControls();if(!populatingEditing){previewEnvironmentDirty=true;UpdateEditState();}};
            allowUnmodeledConstraints.CheckedChanged+=(s,e)=>{if(!populatingEditing){previewEnvironmentDirty=true;UpdateEditState();}};
            RefreshBackendControls();
            // 底部状态文本只占两行左右，长消息可滚动，不挤占布料视图。
            status.Text="Load a Cloth HKX, parts Binder, or XML.";
            tips.SetToolTip(load,"Load BB / DS3 / Sekiro / ER cloth HKX, parts Binder, or XML using the native C# codec. TAG0 author data is read-only.\n使用纯C#载入布料HKX、装备Binder或XML；只狼/法环TAG0作者数据只读。");
            tips.SetToolTip(domains,"Choose one simulation domain; only this domain is affected by range export.\n选择模拟域；范围导出只修改当前选择的域。");
            tips.SetToolTip(factor,"Scale LocalRange distances on export only (0.10–4.00). Original data and current view do not change.\n只在导出时缩放LocalRange距离（0.10–4.00）；不修改源数据或当前视图。");
            tips.SetToolTip(factorLabel,tips.GetToolTip(factor));
            tips.SetToolTip(save,"Save an XML copy with scaled LocalRange in the selected domain. Does not overwrite the source; reload the copy to inspect it.\n另存所选域LocalRange缩放后的XML副本，不覆盖源文件；重新载入副本即可查看修改。");
            tips.SetToolTip(viewReset,"Reset the viewport camera.\n重置视角。");
            tips.SetToolTip(canvas,"Left drag rotates; middle or Shift+left drag pans; right selects/brushes; wheel zooms; F focuses. Ctrl/Shift+right adds, Alt+right removes.\n左键拖动旋转，中键或Shift+左键拖动平移，右键选择/刷选，滚轮缩放，F聚焦；Ctrl/Shift+右键添加、Alt+右键移除。");
            tips.SetToolTip(summary,"Serialized parameters, solver schedule, bone subsets, and selected particle details. A bone subset is not per-particle skin weighting.\n查看文件参数、解算调度、骨集合和选中粒子详情；骨subset不是每个粒子的蒙皮权重。");
            tips.SetToolTip(particles,"Select a row to highlight its particle. Fixed state comes from invMass=0, not the fixedParticles array length.\n选择行高亮对应粒子；按invMass=0判定固定状态，而非fixedParticles数组长度。");
            tips.SetToolTip(collisions,"Supported collider endpoints/radii in skeleton rest space. Unsupported shapes remain available in Raw XML.\n查看支持的碰撞体rest端点/半径；未支持形状仍可在原始XML中检查。");
            tips.SetToolTip(constraints,"Inspect constraint records directly; stiffness fields from different constraint classes are not interchangeable.\n直接查看约束条目；不同约束类的刚度字段不能混用。");
            tips.SetToolTip(objects,"Select a referenced simulation, constraint, operator, collider, or shape object.\n选择引用的模拟、约束、算子、碰撞体或形状对象。");
            tips.SetToolTip(xml,"Read-only source fields of the selected object; no edits are silently saved.\n只读查看选中对象的原始字段，不会静默保存修改。");
            tips.SetToolTip(status,
                "Load, edit and export messages.\n载入、编辑和导出消息。");
            // 文件弹窗单独放到 ClothInspectorActions，避免构造函数挤满长 lambda。
            load.Click += OnLoadClothClick;
            save.Click += OnSaveRangeClick;
            loadClm.Click += OnLoadClm2Click;
            editClm.Click += OnEditClm2Click;
            saveClm.Click += OnSaveClm2Click;
            editHkx.Click += OnEditHkxClick;
            saveHkx.Click += OnSaveHkxClick;
            domains.SelectedIndexChanged += (s, e) =>
            {
                if (GuardDomainChange()) ShowDomain();
            };
            objects.AfterSelect += (s, e) =>
            {
                var item = e.Node.Tag as System.Xml.Linq.XElement;
                if (item != null) xml.Text = item.ToString();
            };
            canvas.SelectionChanged += () =>
            {
                SynchronizeParticleSelection();
                ShowDomainSummary();
            };
        }
        public void LoadClm2File(string path){var document=Clm2Document.Load(path);StopClothPreview();Clm2=document;clmAutomaticallyLoaded=false;RefreshClmLabel();}
        public void ConfigurePreviewRuntime(string folder){ClothPreviewSession.ValidateRuntime(folder);StopClothPreview();previewRuntimePath.Text=Path.GetFullPath(folder);}
        static NumericUpDown EnvironmentNumber(string name,decimal minimum,decimal maximum,decimal value){return new NumericUpDown{Name=name,Minimum=minimum,Maximum=maximum,Value=value,DecimalPlaces=2,Increment=.1m,Width=62};}
        public PreviewEnvironment ReadPreviewEnvironment()
        {
            var settings=new PreviewEnvironment{Backend=previewBackend.SelectedIndex==1?"OpenPhysicsHKX":Convert.ToString(previewBackend.SelectedItem),AllowUnmodeledConstraints=allowUnmodeledConstraints.Checked,OverrideGravity=gravityOverride.Checked,GravityX=(float)gravityX.Value,GravityY=(float)gravityY.Value,GravityZ=(float)gravityZ.Value,WindEnabled=previewBackend.SelectedIndex!=1&&windEnabled.Checked,WindX=(float)windX.Value,WindY=(float)windY.Value,WindZ=(float)windZ.Value,WindSpeed=(float)windSpeed.Value,WindMaxDrag=(float)windDrag.Value};settings.Validate();return settings;
        }
        public void ConfigurePreviewBackend(string backend)
        {
            if(backend!="Native HCT"&&backend!="OpenPhysicsHKX"&&backend!="Native HCT (ER compatibility)"&&backend!="Native HCT (Sekiro compatibility)")throw new ArgumentException("Unknown solver.");
            if(backend!="Native HCT"&&backend!="OpenPhysicsHKX"&&(Document==null||!Document.IsTagfile||Document.TagfileBackend!=backend))throw new InvalidOperationException("Load the matching Sekiro/ER TAG0 Cloth document first.");
            previewBackend.SelectedIndex=backend=="OpenPhysicsHKX"?1:previewBackend.Items.IndexOf(backend);
        }
        void RefreshBackendControls()
        {
            bool managed=previewBackend.SelectedIndex==1;
            previewRuntime.Enabled=previewRuntimePath.Enabled=!managed;
            allowUnmodeledConstraints.Enabled=managed;
            foreach(var control in new Control[]{windEnabled,windX,windY,windZ,windSpeed,windDrag})control.Enabled=!managed;
            foreach(var control in Controls.Find("testClothWind",true))control.Enabled=!managed;
            previewBackendNote.Text=managed?
                "Built-in C# solver. HKX animation updates reference meshes, fixed points and capsule colliders at 60 Hz. BendLink, Transition, VolumeMx and mesh-to-mesh deformation use approximations. Wind, friction, collision-mask filtering and animated collider scaling are not supported.":
                "Native HCT uses the optional local runtime. Wind is enabled by default. Apply Environment restarts the active solver; Stop retains settings.";
            tips.SetToolTip(previewBackendNote,managed?
                "OpenPhysicsHKX runs through the bundled C# Core DLL. HKX animation updates reference meshes, fixed points and colliders; no HCT runtime is needed.\nOpenPhysicsHKX使用自带C# Core DLL，HKX动画驱动参考网格、固定点和碰撞体，无需HCT运行时。":
                 "Native HCT physics runs in an isolated helper process.\nHCT原生物理在独立宿主进程运行。");
            if(previewBackend.SelectedIndex==2)
            {
                bool sekiro=Document!=null&&Document.IsSekiroTagfile;string game=sekiro?"Sekiro":"ER";
                previewBackendNote.Text=game+(sekiro?" 2016":" 2018")+" → HCT 2014 CPU compatibility approximation, NOT the "+game+" runtime. Preserves mapped operators and constraints; uses 2014 bending. Active unsupported features are refused. No game-compatible TAG0 HKX export.";
                tips.SetToolTip(previewBackendNote,"Explicit cross-version physics preview; not game-runtime equivalence. No rest-binding substitution.\n显式跨版本物理近似预览，不是游戏原版解算器；不使用rest绑定替代，不支持的活动特性会拒绝。");
            }
        }
        public async Task ApplyPreviewEnvironmentAsync()
        {
            if (applyingAuthor)
            {
                previewState.Text = "Author Apply in progress; environment not applied.";
                return;
            }
            try
            {
                ReadPreviewEnvironment();
                bool restart = previewSession != null || previewPreparing;
                bool paused = previewSession != null && !previewPlaying;
                bool resumeAnimation = HasLinkedAnimation && ClothAnimationPlayback.Playing;
                StopClothPreview();
                if (restart)
                {
                    if (resumeAnimation) ClothAnimationPlayback.Play();
                    await StartClothPreviewAsync();
                    if (paused && previewSession != null) PauseClothPreview();
                }
                else previewState.Text = "Preview environment ready for next Play · no author data changed";
                previewEnvironmentDirty = false;
                UpdateEditState();
            }
            catch (Exception ex) { previewState.Text = "Environment not applied: " + ex.GetBaseException().Message; }
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
            if (InvokeRequired)
            {
                var done = new TaskCompletionSource<bool>();
                BeginInvoke(new Action(async () =>
                {
                    try { await StartClothPreviewAsync(forcedEnvironment); done.TrySetResult(true); }
                    catch (Exception ex) { done.TrySetException(ex); }
                }));
                await done.Task;
                return;
            }
            // DoEvents 测试宿主也必须安装 WinForms 同步上下文；await 后不能到线程池创建 UI timer。
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            if (previewPreparing || IsDisposed) return;
            if (previewSession != null)
            {
                previewPlaying = true;
                previewClock.Restart();
                previewTimer.Start();
                RefreshPreviewState();
                return;
            }
            int generation = ++previewGeneration;
            previewPreparing = true;
            previewPlay.Enabled = false;
            previewReset.Enabled = true;
            previewCancellation = new CancellationTokenSource();
            var cancel = previewCancellation.Token;
            try
            {
                if (Document == null) throw new InvalidOperationException("Load Cloth first.");
                var environment = forcedEnvironment == null ? ReadPreviewEnvironment() : forcedEnvironment.Clone();
                bool managed=environment.Backend=="OpenPhysicsHKX";
                if(environment.Backend!="Native HCT"&&environment.Backend!="Native HCT (ER compatibility)"&&environment.Backend!="Native HCT (Sekiro compatibility)"&&!managed)throw new InvalidOperationException("Unknown solver.");
                if (!managed&&Program.targetFlver == null) throw new InvalidOperationException("Load the matching FLVER first.");
                if (Clm2 == null) AutoLoadClm2Companion();

                // 先快照内存数据，builder/宿主只消费副本；导出文件和原模型不变。
                var document = new System.Xml.Linq.XDocument(Document.Xml);
                var clm = Clm2 == null ? null : Clm2.Clone();
                var model = Program.targetFlver;
                string runtime = previewRuntimePath.Text;
                if(!managed)ClothPreviewSession.ValidateRuntime(runtime);
                previewModel = model;
                var expected = Document.Domains.ToDictionary(d => d.Name, d => d.Positions.Length);
                byte[] geometry = managed?null:model.Write();
                byte[] seams = managed||clm==null?null:clm.Serialize();
                var progress = new Progress<string>(message =>
                {
                    if (!IsDisposed && generation == previewGeneration) previewState.Text = message;
                });
                IClothPreviewSession session;
                if(managed)session=await ManagedClothPreviewSession.CreateAsync(document,environment,cancel,progress);
                else session=await ClothPreviewSession.CreateAsync(document.ToString(), geometry, seams,runtime,cancel,progress,environment);

                // 关闭、重载或更换模型后，旧的异步启动结果必须回收，不能重新接管窗口。
                if (IsDisposed || generation != previewGeneration || cancel.IsCancellationRequested ||
                    !ReferenceEquals(model, Program.targetFlver))
                {
                    await Task.Run(() => session.Dispose());
                    return;
                }
                if (!session.DomainNames.OrderBy(s => s).SequenceEqual(expected.Keys.OrderBy(s => s)))
                {
                    await Task.Run(() => session.Dispose());
                    throw new InvalidDataException("Preview domain names do not match the loaded document.");
                }
                ClothPreviewFrame animatedInitial = null;
                try
                {
                    if (HasLinkedAnimation)
                    {
                        ValidateAnimationSession(session);
                        animatedInitial = await session.StepAsync(1, BuildAnimationPoseBatch(1, false));
                    }
                }
                catch { await Task.Run(() => session.Dispose()); throw; }
                if (IsDisposed || generation != previewGeneration || cancel.IsCancellationRequested ||
                    !ReferenceEquals(model, Program.targetFlver))
                {
                    await Task.Run(() => session.Dispose());
                    return;
                }
                appliedEnvironment = environment;
                previewSession = session;
                previewPreparing = false;
                previewPlaying = true;
                previewAccumulator = 0;
                previewClock.Restart();
                previewTimer.Start();
                if (animatedInitial != null)
                {
                    lastPreview = animatedInitial;
                    ApplyPreviewPositions();
                }
                RefreshPreviewState();
                if (forcedEnvironment == null) previewEnvironmentDirty = false;
                UpdateEditState();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && generation == previewGeneration)
                {
                    StopClothPreview();
                    previewState.Text = "Cloth preview unavailable: " + ex.GetBaseException().Message;
                }
            }
            finally
            {
                if (!IsDisposed && generation == previewGeneration)
                {
                    previewPreparing = false;
                    previewPlay.Enabled = true;
                }
            }
        }
        public void PauseClothPreview()
        {
            previewPlaying = false;
            previewClock.Reset();
            previewAccumulator = 0;
            if (HasLinkedAnimation)
            {
                PauseAnimationClock();
                RefreshAnimationLabels();
            }
            RefreshPreviewState();
        }
        internal void RequestStopForModelChange()
        {
            if (IsDisposed || !IsHandleCreated) return;
            Action stop = () =>
            {
                if (IsDisposed) return;
                RefreshVerificationIndicator();
                InvalidateMeshPreview();
                if (previewSession == null && !previewPreparing) return;
                StopClothPreview();
                previewState.Text = "Preview stopped: FLVER edit/refresh invalidated its snapshot.";
            };
            if (InvokeRequired)
            {
                try { BeginInvoke(stop); }
                catch (InvalidOperationException) { /* 窗口关闭竞态，无需重新打开。 */ }
            }
            else stop();
        }
        public void StopClothPreview()
        {
            if (HasLinkedAnimation)
            {
                PauseAnimationClock();
                RefreshAnimationLabels();
            }
            previewGeneration++;
            previewPlaying = false;
            previewPreparing = false;
            previewTimer.Stop();
            previewClock.Reset();
            previewAccumulator = 0;
            if(previewCancellation!=null)
            {
                // 子进程先退出时，.NET 的管道取消回调可能遇到已关闭的句柄。
                // 仅忽略这一类回收竞态，不吞掉其他取消异常。
                try{previewCancellation.Cancel();}
                catch(AggregateException ex){if(!ex.Flatten().InnerExceptions.All(e=>e is ObjectDisposedException))throw;}
                finally{previewCancellation.Dispose();previewCancellation=null;}
            }
            // 回收的只是本窗口拥有的宿主。观察旧请求异常，避免取消后产生未观察任务。
            var retired = previewSession;
            previewSession = null;
            if (retired != null) Task.Run(() => retired.Dispose());
            if (pendingPreview != null)
            {
                pendingPreview.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                pendingPreview = null;
            }
            lastPreview = null;
            previewModel = null;
            canvas.SimulationPositions = null;
            canvas.PreviewLabel = null;
            canvas.Invalidate();
            previewPlay.Enabled = true;
            previewPlay.Text = "Play Cloth";
            previewState.Text = "Cloth preview off · rest pose";
        }
        void RefreshPreviewState()
        {
            previewPlay.Enabled = !previewPreparing;
            previewPlay.Text = previewPlaying ? "Pause Cloth" : "Play Cloth";
            if (previewSession == null) return;

            string playback = previewPlaying ? "playing" : "paused";
            string wind = appliedEnvironment.WindEnabled ?
                appliedEnvironment.WindSpeed.ToString("G3", CultureInfo.InvariantCulture) : "off";
            previewState.Text = string.Format(CultureInfo.InvariantCulture,
                previewSession.DisplayName+" {0} · frame {1} · {2:F3} s · {3:F3} ms/batch · gravity {4} · wind {5}",
                playback, PreviewFrameNumber, PreviewFrameNumber / 60.0,
                lastPreview == null ? 0 : lastPreview.SolveMilliseconds,
                appliedEnvironment.OverrideGravity ? "preview override" : "file", wind)+
                (string.IsNullOrEmpty(previewSession.Notice)?"":" · "+previewSession.Notice);

            string windDescription = appliedEnvironment.WindEnabled ?
                string.Format(CultureInfo.InvariantCulture,
                    "Wind ({0:G3}, {1:G3}, {2:G3}) speed {3:G3} drag {4:G3}",
                    appliedEnvironment.WindX, appliedEnvironment.WindY, appliedEnvironment.WindZ,
                    appliedEnvironment.WindSpeed, appliedEnvironment.WindMaxDrag) : "Wind off";
            canvas.PreviewLabel = previewSession.DisplayName.ToUpperInvariant()+" · " + playback + " · frame " + PreviewFrameNumber +
                "\n" + windDescription + " · preview only"+(string.IsNullOrEmpty(previewSession.Notice)?"":"\n"+previewSession.Notice);
            canvas.Invalidate();
        }
        void ApplyPreviewPositions()
        {
            if (lastPreview == null || canvas.Domain == null) return;
            Vector3[] positions;
            if (!lastPreview.Domains.TryGetValue(canvas.Domain.Name, out positions) ||
                positions.Length != canvas.Domain.Positions.Length)
                throw new InvalidDataException("Native preview particle topology mismatch");
            canvas.SimulationPositions = positions;
            System.Collections.Generic.List<ClothCollider> colliders;
            if(lastPreview.Colliders!=null&&lastPreview.Colliders.TryGetValue(canvas.Domain.Name,out colliders))canvas.AnimationColliders=colliders;
            canvas.Invalidate();
        }
        void PreviewTick()
        {
            try
            {
                if (previewSession == null) return;
                if (!ReferenceEquals(previewModel, Program.targetFlver))
                {
                    StopClothPreview();
                    previewState.Text = "Preview stopped: current FLVER changed.";
                    return;
                }
                if (pendingPreview != null && pendingPreview.IsCompleted)
                {
                    var completed = pendingPreview;
                    pendingPreview = null;
                    lastPreview = completed.GetAwaiter().GetResult();
                    ApplyPreviewPositions();
                    RefreshPreviewState();
                }
                if (!previewPlaying) return;

                // 固定物理步长；UI 卡顿时最多补四步，不把巨大的 elapsed 传给 DLL。
                double elapsed = previewClock.Elapsed.TotalSeconds;
                previewClock.Restart();
                previewAccumulator = Math.Min(4.0 / 60, previewAccumulator + elapsed);
                if (pendingPreview != null || previewAccumulator < 1.0 / 60) return;
                int steps = Math.Min(4, (int)(previewAccumulator * 60));
                previewAccumulator -= steps / 60.0;
                var poses = BuildAnimationPoseBatch(steps);
                // 动画循环可能刚启动重建，不能把请求交给已经退休的宿主。
                if (previewSession == null || animationRestarting) return;
                pendingPreview = previewSession.StepAsync(steps, poses);
            }
            catch (Exception ex)
            {
                StopClothPreview();
                previewState.Text = "Cloth preview stopped: " + ex.GetBaseException().Message;
            }
        }
        public void SaveClm2Copy(string path){if(Clm2==null)throw new InvalidOperationException("Load CLM2 first.");Clm2.SaveCopy(path);}
        public string VerifyClothes(){return Clm2Document.Verify(Clm2,Document,Program.targetFlver,Program.orgFileName);}
        void RefreshClmLabel(){clmSource.Text=Clm2==null?"No CLM2 loaded":Clm2.Source+(Clm2.Edited?" [in-memory edits]":"");RefreshVerificationIndicator();}
        static DataGridView Grid(string name)
        {return new DataGridView {Name=name,Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false};}
        public void LoadFile(string path) { SetDocument(ClothDocument.Load(path)); }
        public void ExportHkxCopy(string path)
        {
            if (Document == null) throw new InvalidOperationException("Load Cloth first.");
            if (editingDirty || selectedValueDirty || applyingAuthor)
                throw new InvalidOperationException("Apply or discard pending changes before export.");
            Document.SaveHkxCopy(path, domains.SelectedIndex, (double)factor.Value);
        }
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
                status.Text="Auto-loaded: "+source;
                return true;
            }
            catch (ClothBinderSelectionException ex)
            {
                if (!IsDisposed)
                {
                    // 没有 SetDocument，就不会触发配套 CLM2 加载；也不清空已有手动比较数据。
                    status.Text = "Automatic loading skipped: " + ex.Message;
                    MessageBox.Show(this, ex.Message, "Cloth — Manual Selection Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return false;
            }
            catch(Exception ex){if(!IsDisposed)status.Text="Cloth auto-load failed: "+ex.Message+"\nUse Load Cloth to choose a valid file. No source data was modified.";return false;}
            finally{if(!IsDisposed){load.Enabled=true;UseWaitCursor=false;}}
        }
        public void ExportRangeCopy(string path)
        {
            if (editingDirty || selectedValueDirty || applyingAuthor)
                throw new InvalidOperationException("Apply or discard pending changes before export.");
            Document.SaveRangeCopy(domains.SelectedIndex, (double)factor.Value, path);
        }
        void SetDocument(ClothDocument document)
        {
            StopClothPreview();
            ResetEditingForLoad();
            ResetSelectionEditor();
            ResetClothAnimationForDocument();
            Document=document;AutoLoadClm2Companion();domains.Items.Clear();domains.Items.AddRange(document.Domains.Cast<object>().ToArray());domains.SelectedIndex=0;
            updatingLayout=true;
            if(document.IsTagfile)
            {
                string label=document.NativeLayout+" ("+(document.IsSekiroTagfile?"Sekiro":"ER")+", read-only)";
                if(nativeLayout.Items.Count==2)nativeLayout.Items.Add(label);else nativeLayout.Items[2]=label;
            }
            if(!document.IsTagfile&&nativeLayout.Items.Count>2)nativeLayout.Items.RemoveAt(2);
            nativeLayout.SelectedIndex=document.IsTagfile?2:document.NativeLayout=="8,1,0,1"?1:0;updatingLayout=false;
            if(document.IsTagfile)
            {
                if(previewBackend.Items.Count==2)previewBackend.Items.Add(document.TagfileBackend);
                else if(Convert.ToString(previewBackend.Items[2])!=document.TagfileBackend)
                {if(previewBackend.SelectedIndex==2)previewBackend.SelectedIndex=0;previewBackend.Items[2]=document.TagfileBackend;}
            }
            if(!document.IsTagfile&&previewBackend.Items.Count>2){if(previewBackend.SelectedIndex==2)previewBackend.SelectedIndex=0;previewBackend.Items.RemoveAt(2);}
            RefreshBackendControls();
            Text="Cloth Inspector — "+Path.GetFileName(document.Source)+(document.Edited?" [in-memory edits]":"");
        }
        void ShowDomain()
        {
            if(Document==null||domains.SelectedIndex<0)return;
            populatingEditing=true;
            var grids=new[]{particles,collisions,constraints,authorParameters,constraintParameters};
            var sizes=grids.Select(g=>g.AutoSizeColumnsMode).ToArray();
            foreach(var grid in grids){grid.SuspendLayout();grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;}
            objects.BeginUpdate();
            try
            {
            if(!editingDirty)stagedXml=new System.Xml.Linq.XDocument(Document.Xml);
            var domain=Document.Domains[domains.SelectedIndex];canvas.Domain=domain;canvas.Selected=-1;canvas.ResetView();
            canvas.SelectedParticles.Clear();
            canvas.SimulationPositions=null;ApplyPreviewPositions();
            UpdateAnimationCanvas();
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
            AddDomainRows(particles,rows.Select((row,i)=>new object[]{i,domain.Fixed[i],ClothDocument.Value(row,"mass"),ClothDocument.Value(row,"invMass"),ClothDocument.Value(row,"radius"),ClothDocument.Value(row,"friction"),domain.Ranges[i],i<masks.Length?masks[i].ToString(CultureInfo.InvariantCulture):"Unknown"}));
            particles.ClearSelection();
            collisions.Columns.Clear();foreach(var name in new[]{"Bone","Shape","Endpoint A (rest)","Endpoint B (rest)","Radius A","Radius B"})collisions.Columns.Add(name,name);
            AddDomainRows(collisions,domain.Colliders.Select(c=>new object[]{c.Bone,c.Shape,c.Start,c.End,c.RadiusA,c.RadiusB}));
            constraints.Columns.Clear();foreach(var name in new[]{"Constraint Class","Name","Record","Fields"})constraints.Columns.Add(name,name);
            AddDomainRows(constraints,domain.Constraints.SelectMany(c=>c.Elements().Where(p=>p.Elements().Any()).SelectMany(p=>p.Elements().Select((row,i)=>new object[]{(string)c.Attribute("class"),ClothDocument.Value(c,"name"),i,string.Join("; ",row.Elements().Select(v=>(string)v.Attribute("name")+"="+v.Value.Trim()))}))));
            populatingEditing=false;PopulateEditing();
            RefreshVerificationIndicator();
            }
            finally
            {
                populatingEditing=false;objects.EndUpdate();
                for(int i=0;i<grids.Length;i++){grids[i].AutoSizeColumnsMode=sizes[i];grids[i].ResumeLayout();}
            }
        }
        static void AddDomainRows(DataGridView grid,System.Collections.Generic.IEnumerable<object[]> values)
        {
            var rows=values.Select(v=>{var row=new DataGridViewRow();row.CreateCells(grid,v);return row;}).ToArray();
            if(rows.Length>0)grid.Rows.AddRange(rows);
        }
        void ShowDomainSummary()
        {
            if(Document==null||domains.SelectedIndex<0)return;
            var d=Document.Domains[domains.SelectedIndex];var text=new StringBuilder();
            text.AppendLine("Source: "+Document.Source).AppendLine(d.Name);
            text.AppendLine(Document.IsTagfile?Document.NativeLayout+" ("+(Document.IsSekiroTagfile?"Sekiro":"ER")+") — read-only author data":"Havok64 2014.1 native header layout: "+Document.NativeLayout+" (not a game-ID guarantee)");
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
            if(canvas.SelectedParticles.Count>1)
            {
                var ids=SelectedParticleIds;
                text.AppendLine("\r\nSelected particles: "+ids.Length+"; fixed: "+ids.Count(i=>d.Fixed[i])+"; dynamic: "+ids.Count(i=>!d.Fixed[i]));
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,"Selected author ranges: {0:G6} – {1:G6}",ids.Min(i=>d.Ranges[i]),ids.Max(i=>d.Ranges[i])));
                text.AppendLine("Edit the Selected Particles panel; Mixed means values differ. All selected range circles are drawn translucently.");
            }
            else if(canvas.Selected>=0)
            {
                int i=canvas.Selected;text.AppendLine("\r\nSelected particle "+i+" "+(d.Fixed[i]?"Fixed":"Dynamic")+" rest="+d.Positions[i]+" range="+d.Ranges[i].ToString("G6"));
                var rows=ClothDocument.Field(d.Sim,"particleDatas").Elements().ToArray();text.AppendLine(rows[i].ToString());
                var masks=ClothDocument.Numbers(ClothDocument.Value(d.Sim,"staticCollisionMasks"));if(i<masks.Length)text.AppendLine("collisionMask: "+masks[i]);
            }
            text.AppendLine("Range circles show saved maximumDistance limits, not combined constraints.");
            text.AppendLine("Orange colliders follow the displayed skeleton pose when animation is linked; their table remains author data.");
            text.AppendLine("Source files/current FLVER remain unchanged. Save HKX Copy writes a validated new file.");
            summary.Text = text.ToString();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopClothPreview();
                DisposeClothAnimation();
                previewTimer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    public sealed partial class ClothCanvas : Panel
    {
        public ClothDomain Domain;
        public Vector3[] SimulationPositions;
        public string PreviewLabel;
        public Vector3[] AnimationBones;
        public int[] AnimationParents;
        public System.Collections.Generic.List<ClothCollider> AnimationColliders;
        Vector3[] DisplayPositions {get{return SimulationPositions!=null&&Domain!=null&&SimulationPositions.Length==Domain.Positions.Length?SimulationPositions:Domain.Positions;}}
        public int Selected=-1;
        public Action<int> PickValueRequested;
        public bool ShowParticles=true,ShowColliders=true,ShowRange=true;
        public int ClothRenderMode; // 0: points/lines (default), 1: solid, 2: both
        public event Action SelectionChanged;
        // Front view from -Z, matching the main Viewer's initial side.
        float yaw=0,pitch=0,zoom=1,span=1;Vector3 center;Point down,last;bool drag;
        public ClothCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(34,38,46);
            InitializeSelectionInput();
            MouseWheel += (s, e) =>
            {
                zoom = Math.Max(.1f, Math.Min(10, zoom * (e.Delta > 0 ? 1.15f : .87f)));
                Invalidate();
            };
            Resize += (s, e) => Invalidate();
        }
        public void ResetView()
        {
            yaw = 0;
            pitch = 0;
            zoom = 1;
            pan = PointF.Empty;
            if (Domain != null)
            {
                var points = Domain.Positions.Concat(Domain.Colliders.SelectMany(c => new[] { c.Start, c.End })).ToArray();
                var min = points.Aggregate(Vector3.Min);
                var max = points.Aggregate(Vector3.Max);
                center = (min + max) * .5f;
                span = Math.Max(.1f, (max - min).Length());
            }
            Invalidate();
        }
        float PixelScale {get{return Math.Min(Width,Height)*.8f*zoom/span;}}
        float cachedViewYaw=float.NaN,cachedViewPitch=float.NaN;
        Matrix4x4 cachedViewTransform;
        Matrix4x4 ViewTransform
        {
            get
            {
                if(cachedViewYaw==yaw&&cachedViewPitch==pitch)return cachedViewTransform;
                float sy=(float)Math.Sin(yaw),cy=(float)Math.Cos(yaw);
                float sp=(float)Math.Sin(pitch),cp=(float)Math.Cos(pitch);
                // Match Mono3D.DrawGround, including its handedness conversion:
                // FLVER (X,Y,Z) -> Viewer (X,Z,Y), then MonoGame Z-up LookAt.
                // pitch is camera elevation, NOT a right-handed model rotation.
                var eye=new Microsoft.Xna.Framework.Vector3(sy*cp,-cy*cp,sp);
                // Same as world Z-up for elevations within +/-90 degrees;
                // the explicit camera-up remains defined at the poles as well.
                var up=new Microsoft.Xna.Framework.Vector3(-sy*sp,cy*sp,cp);
                var v=Microsoft.Xna.Framework.Matrix.CreateLookAt(eye,Microsoft.Xna.Framework.Vector3.Zero,up);
                var view=new Matrix4x4(v.M11,v.M12,v.M13,v.M14,v.M21,v.M22,v.M23,v.M24,
                    v.M31,v.M32,v.M33,v.M34,v.M41,v.M42,v.M43,v.M44);
                var xzy=new Matrix4x4(1,0,0,0,0,0,1,0,0,1,0,0,0,0,0,1);
                cachedViewTransform=xzy*view;
                cachedViewYaw=yaw;cachedViewPitch=pitch;
                return cachedViewTransform;
            }
        }
        PointF ProjectViewPoint(Vector3 point)
        {
            return new PointF(Width*.5f+pan.X+point.X*PixelScale,Height*.5f+pan.Y-point.Y*PixelScale);
        }
        PointF Project(Vector3 p)
        {
            return ProjectViewPoint(Vector3.Transform(p-center,ViewTransform));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (Domain == null)
            {
                graphics.DrawString("Load Cloth data to inspect rest particles / colliders", Font, Brushes.White, 12, 12);
                return;
            }
            // 各绘制层的具体实现位于 ClothCanvasRendering，便于独立调整透明度与样式。
            var positions = DisplayPositions;
            DrawFlverMeshes(graphics);
            DrawWorldAxes(graphics);
            DrawClothTriangles(graphics, positions);
            DrawClothColliders(graphics);
            DrawAnimationSkeleton(graphics);
            DrawClothParticles(graphics, positions);
            DrawSelectedRanges(graphics);
            DrawSelectionOverlay(graphics);
            DrawAxisIndicator(graphics);
            graphics.DrawString(PreviewLabel ??
                "REST VIEW · Left rotate · Middle / Shift+Left pan · Right select · Wheel zoom\n" +
                "Yellow: fixed; cyan: dynamic; magenta: selected; orange: colliders",
                Font, Brushes.White, 8, 8);
        }
    }
    static partial class Program
    {
        public static ClothInspectorForm clothInspectorForm;
        internal static void OpenClothInspector()
        {
            var ui = nodeUIForm;
            if (ui == null || ui.IsDisposed || !ui.IsHandleCreated) return;
            // 始终在现有 WinForms STA 上打开，不能阻塞 MonoGame 的消息循环。
            ui.BeginInvoke(new Action(async () =>
            {
                if (ui.IsDisposed) return;
                if (clothInspectorForm == null || clothInspectorForm.IsDisposed)
                    clothInspectorForm = new ClothInspectorForm();
                var window = clothInspectorForm;
                if (!window.Visible) EditorWindowPlacement.ShowCentered(window, ui);
                window.BringToFront();
                await window.TryAutoLoadForModelAsync(orgFileName);
            }));
        }
    }
}
