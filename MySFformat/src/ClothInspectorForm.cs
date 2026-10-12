using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MySFformat
{
    public sealed class ClothInspectorForm : Form
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
        public Clm2Document Clm2 {get;private set;}
        public ClothDocument Document { get; private set; }
        public ClothInspectorForm()
        {
            Text="Cloth Inspector — Rest Configuration, Not Game Simulation";Width=1200;Height=820;MinimumSize=new Size(900,650);
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
            var editHkx=new Button {Name="editHkx",Text="Edit HKX…",AutoSize=true};
            var saveHkx=new Button {Name="saveHkx",Text="Save HKX Copy…",AutoSize=true};
            var nativeLabel=new Label {Text="Native C# · BB / DS3 · Full readback gate · No Java",AutoSize=true,Padding=new Padding(4,7,0,0)};
            nativeLayout.Items.AddRange(new object[]{"8,1,1,1 (BB sample layout)","8,1,0,1 (DS3 sample layout)"});nativeLayout.SelectedIndex=0;
            third.Controls.AddRange(new Control[]{editHkx,saveHkx,nativeLayout,nativeLabel});
            tips.SetToolTip(editHkx,"Open an advanced full-XML editor on a clone. Validate/Apply uses the native C# pack/read gate and changes memory only.\n在克隆上弹窗编辑完整XML；Validate/Apply使用纯C#打包/读回校验，只修改内存。");
            tips.SetToolTip(saveHkx,"Write a new BB/DS3 cloth HKX with the selected header layout. Export range scale affects only the selected domain. Full schema/pack/readback validation runs before output creation.\n按所选包头布局另存BB/DS3布料HKX；范围倍率只影响所选域，创建输出前完成结构/打包/读回校验。");
            tips.SetToolTip(nativeLabel,"In-process C# with audited BB/DS3 schemas. Unsupported classes/SDKs and invalid data fail closed. Not a Havok runtime solver.\n进程内C#使用已验证BB/DS3结构；未支持类/SDK和错误数据明确拒绝，不是Havok运行时解算器。");
            tips.SetToolTip(nativeLayout,"Preserve the binary layout rule from HKX. This is not an infallible game-ID detector. Legacy XML without nativeLayout defaults to the BB sample layout; select the DS3 layout explicitly if needed. Changing it marks the document edited.\n保留HKX包头布局规则，并非可靠游戏ID探测。旧XML缺nativeLayout时默认BB样例布局；DS3需明确选择。手动切换会标记文档修改。");
            nativeLayout.SelectedIndexChanged+=(s,e)=>{if(updatingLayout||Document==null)return;Document.SetNativeLayout(nativeLayout.SelectedIndex==1?"8,1,0,1":"8,1,1,1");ShowDomainSummary();Text="Cloth Inspector — "+Path.GetFileName(Document.Source)+" [in-memory edits]";};
            clmSource.Text="No CLM2 loaded";
            tips.SetToolTip(loadClm,"Load a CLM2 as an independent companion document; existing data is kept if parsing fails.\n独立载入CLM2配套文档；解析失败时保留之前的数据。");
            tips.SetToolTip(editClm,"Open a separate editor for known mesh records and Unk00/Unk02 signed Int16 fields. Entry semantics remain unknown.\n弹窗编辑已知mesh记录和Unk00/Unk02有符号16位字段；条目含义仍未确认。");
            tips.SetToolTip(saveClm,"Save edited CLM2 to a new file. Sources and existing files are never overwritten.\n将修改后的CLM2另存到新文件，不覆盖源文件或已有文件。");
            tips.SetToolTip(verify,"Compare CLM2 mesh count with the current in-memory FLVER; inspect HKX display bindings and dimensions separately. Cloth domain count is not a CLM2 mesh count.\n对照CLM2与当前内存FLVER的mesh数量，另检HKX显示绑定和维数；布料域数不等于CLM2 mesh数。");
            tips.SetToolTip(clmSource,"Loaded CLM2 source and unsaved-edit state. This is independent of the selected HKX/XML source.\n显示CLM2来源和未保存修改状态，与HKX/XML来源独立。");
            var tools=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=3};
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));tools.RowStyles.Add(new RowStyle(SizeType.AutoSize));top.Dock=DockStyle.Fill;tools.Controls.Add(top,0,0);tools.Controls.Add(second,0,1);tools.Controls.Add(third,0,2);
            var split=new SplitContainer { Size=new Size(1150,640), Dock=DockStyle.Fill, SplitterDistance=540 };
            split.Panel1.Controls.Add(canvas);
            var tabs=new TabControl { Dock=DockStyle.Fill };
            var parameters=new TabPage("Parameters / Binding");parameters.Controls.Add(summary);tabs.TabPages.Add(parameters);
            foreach(var item in new[]{Tuple.Create("Particles / Range",particles),Tuple.Create("Colliders",collisions),Tuple.Create("Constraints",constraints)})
            {var page=new TabPage(item.Item1);page.Controls.Add(item.Item2);tabs.TabPages.Add(page);}
            var raw=new TabPage("Objects / Raw XML");var rawSplit=new SplitContainer {Size=new Size(550,600),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=220};rawSplit.Panel1.Controls.Add(objects);rawSplit.Panel2.Controls.Add(xml);raw.Controls.Add(rawSplit);tabs.TabPages.Add(raw);
            split.Panel2.Controls.Add(tabs);Controls.Add(split);Controls.Add(tools);Controls.Add(status);
            status.Text="Load BB / DS3 _c.hkx, parts Binder, or XML. Yellow: fixed (invMass=0); cyan: dynamic; orange: colliders.\nDrag to rotate, wheel to zoom, click a particle for its range sphere. Serialized rest data cannot confirm or rule out game-world wind.";
            tips.SetToolTip(load,"Load BB / DS3 cloth HKX, parts Binder, or XML using the native C# codec. No Java/Jar is needed. Source files remain read-only.\n使用纯C#编解码器载入BB/DS3布料HKX、装备Binder或XML，不依赖Java/Jar；源文件只读。");
            tips.SetToolTip(domains,"Choose one simulation domain; only this domain is affected by range export.\n选择模拟域；范围导出只修改当前选择的域。");
            tips.SetToolTip(factor,"Scale LocalRange distances on export only (0.10–4.00). Original data and current view do not change.\n只在导出时缩放LocalRange距离（0.10–4.00）；不修改源数据或当前视图。");
            tips.SetToolTip(factorLabel,tips.GetToolTip(factor));
            tips.SetToolTip(save,"Save a new author XML with scaled LocalRange in the selected domain. Never overwrites a source; not a playable HKX/Binder. Reload the copy to inspect it.\n另存所选域LocalRange缩放后的作者XML，不覆盖源文件；不能直接作为游戏HKX/Binder。重新载入副本即可查看修改。");
            tips.SetToolTip(reset,"Reset the rest-data camera without changing any cloth parameters.\n重置rest数据视角，不修改布料参数。");
            tips.SetToolTip(canvas,"Drag to rotate; wheel to zoom; click a particle to inspect its rest range. No Havok solver or live-game forces are evaluated.\n拖动旋转、滚轮缩放、单击粒子检查rest范围；不运行Havok解算，也不评估游戏实时施力。");
            tips.SetToolTip(summary,"Serialized parameters, solver schedule, bone subsets, and selected particle details. A bone subset is not per-particle skin weighting.\n查看文件参数、解算调度、骨集合和选中粒子详情；骨subset不是每个粒子的蒙皮权重。");
            tips.SetToolTip(particles,"Select a row to highlight its particle. Fixed state comes from invMass=0, not the fixedParticles array length.\n选择行高亮对应粒子；按invMass=0判定固定状态，而非fixedParticles数组长度。");
            tips.SetToolTip(collisions,"Supported collider endpoints/radii in skeleton rest space. Unsupported shapes remain available in Raw XML.\n查看支持的碰撞体rest端点/半径；未支持形状仍可在原始XML中检查。");
            tips.SetToolTip(constraints,"Inspect constraint records directly; stiffness fields from different constraint classes are not interchangeable.\n直接查看约束条目；不同约束类的刚度字段不能混用。");
            tips.SetToolTip(objects,"Select a referenced simulation, constraint, operator, collider, or shape object.\n选择引用的模拟、约束、算子、碰撞体或形状对象。");
            tips.SetToolTip(xml,"Read-only source fields of the selected object; no edits are silently saved.\n只读查看选中对象的原始字段，不会静默保存修改。");
            tips.SetToolTip(status,"Rest inspection only. Global wind and runtime parameter overrides still require in-game diagnostics.\n仅检查rest数据；全局风和运行时参数覆盖仍需要游戏内诊断。");
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
            domains.SelectedIndexChanged+=(s,e)=>ShowDomain();
            objects.AfterSelect+=(s,e)=>{var o=e.Node.Tag as System.Xml.Linq.XElement;if(o!=null)xml.Text=o.ToString();};
            canvas.SelectionChanged+=()=>ShowDomainSummary();
            particles.CellClick+=(s,e)=>{if(e.RowIndex<0)return;canvas.Selected=Convert.ToInt32(particles.Rows[e.RowIndex].Cells[0].Value);canvas.Invalidate();ShowDomainSummary();};
            loadClm.Click+=(s,e)=>{using(var dialog=new OpenFileDialog {Filter="CLM2 files|*.clm2;*.clm",Title="Load CLM2"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{LoadClm2File(dialog.FileName);}catch(Exception ex){MessageBox.Show(this,ex.Message,"CLM2 Load Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}}};
            editClm.Click+=(s,e)=>{if(Clm2==null){MessageBox.Show(this,"Load CLM2 first.","Edit CLM2");return;}using(var dialog=new Clm2EditForm(Clm2)){if(dialog.ShowDialog(this)==DialogResult.OK){Clm2=dialog.Result;RefreshClmLabel();}}};
            saveClm.Click+=(s,e)=>{if(Clm2==null){MessageBox.Show(this,"Load CLM2 first.","Save CLM2");return;}using(var dialog=new SaveFileDialog {Filter="CLM2 file|*.clm2",FileName=Path.GetFileNameWithoutExtension(Clm2.Source)+"-edited.clm2"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{SaveClm2Copy(dialog.FileName);status.Text="Saved CLM2 copy: "+dialog.FileName+"\nSource files unchanged. Run Verify Clothes against the intended FLVER/HKX before game testing.";}catch(Exception ex){MessageBox.Show(this,ex.Message,"CLM2 Save Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}}};
            verify.Click+=(s,e)=>{
                string report;try{report=VerifyClothes();}catch(Exception ex){report="[FAIL] Verification could not complete: "+ex.Message;}
                using(var window=new Form {Text="Verify Clothes — Layout and Binding Checks",Size=new Size(950,650),StartPosition=FormStartPosition.CenterParent})
                {var output=new TextBox {Name="clothesVerification",Text=report,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill};window.Controls.Add(output);var reportTips=new ToolTip {AutoPopDelay=20000,ShowAlways=true};reportTips.SetToolTip(output,"PASS/FAIL/WARN/SKIP describe static checks only, not gameplay certification. Current FLVER may contain unsaved edits.\nPASS/FAIL/WARN/SKIP只描述静态检查，不代表游戏验证；当前FLVER可能有未保存修改。");window.Disposed+=(a,b)=>reportTips.Dispose();window.ShowDialog(this);}
            };
            editHkx.Click+=(s,e)=>{if(Document==null){MessageBox.Show(this,"Load Cloth first.","Edit HKX");return;}using(var dialog=new HkxXmlEditForm(Document)){int index=domains.SelectedIndex;if(dialog.ShowDialog(this)==DialogResult.OK){SetDocument(dialog.Result);domains.SelectedIndex=Math.Min(index,Document.Domains.Count-1);status.Text="Validated HKX edits applied in memory. Source files are unchanged.";}}};
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
        public void LoadClm2File(string path){var document=Clm2Document.Load(path);Clm2=document;RefreshClmLabel();}
        public void SaveClm2Copy(string path){if(Clm2==null)throw new InvalidOperationException("Load CLM2 first.");Clm2.SaveCopy(path);}
        public string VerifyClothes(){return Clm2Document.Verify(Clm2,Document,Program.targetFlver,Program.orgFileName);}
        void RefreshClmLabel(){clmSource.Text=Clm2==null?"No CLM2 loaded":Clm2.Source+(Clm2.Edited?" [in-memory edits]":"");}
        static DataGridView Grid(string name)
        {return new DataGridView {Name=name,Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false};}
        public void LoadFile(string path) { SetDocument(ClothDocument.Load(path)); }
        public void ExportHkxCopy(string path){if(Document==null)throw new InvalidOperationException("Load Cloth first.");Document.SaveHkxCopy(path,domains.SelectedIndex,(double)factor.Value);}
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
                status.Text="Auto-loaded: "+source+"\nRest configuration only. Sources unchanged; game-world wind and runtime overrides are not evaluated.";
                return true;
            }
            catch(Exception ex){if(!IsDisposed)status.Text="Cloth auto-load failed: "+ex.Message+"\nUse Load Cloth to choose a valid file. No source data was modified.";return false;}
            finally{if(!IsDisposed){load.Enabled=true;UseWaitCursor=false;}}
        }
        public void ExportRangeCopy(string path) { Document.SaveRangeCopy(domains.SelectedIndex,(double)factor.Value,path); }
        void SetDocument(ClothDocument document)
        {
            Document=document;domains.Items.Clear();domains.Items.AddRange(document.Domains.Cast<object>().ToArray());domains.SelectedIndex=0;
            updatingLayout=true;nativeLayout.SelectedIndex=document.NativeLayout=="8,1,0,1"?1:0;updatingLayout=false;
            Text="Cloth Inspector — "+Path.GetFileName(document.Source)+(document.Edited?" [in-memory edits]":"")+" (Author Rest, Not Game Simulation)";
        }
        void ShowDomain()
        {
            if(Document==null||domains.SelectedIndex<0)return;
            var domain=Document.Domains[domains.SelectedIndex];canvas.Domain=domain;canvas.Selected=-1;canvas.ResetView();
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
            {int i=0;foreach(var row in p.Elements())constraints.Rows.Add((string)c.Attribute("class"),ClothDocument.Value(c,"name"),i++,string.Join("; ",row.Elements().Select(v=>(string)v.Attribute("name")+"="+v.Value.Trim())));}
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
            text.AppendLine("\r\nNative C# BB / DS3 read/write. No game-runtime reads, Havok simulation, or cloth playback.\r\nRange spheres show maximumDistance only, not combined constraints. Orange wireframes show rest colliders.\r\nSource files/current FLVER remain unchanged. Save HKX Copy writes a new file after full field readback validation; export range scale affects only the selected domain.");summary.Text=text.ToString();
        }
        protected override void Dispose(bool disposing) { if(disposing)tips.Dispose();base.Dispose(disposing); }
    }
    public sealed class ClothCanvas : Panel
    {
        public ClothDomain Domain;
        public int Selected=-1;
        public bool ShowParticles=true,ShowColliders=true,ShowRange=true;
        public event Action SelectionChanged;
        float yaw=.35f,pitch=.1f,zoom=1,span=1;Vector3 center;Point down,last;bool drag;
        public ClothCanvas()
        {
            DoubleBuffered=true;BackColor=Color.FromArgb(24,27,34);
            MouseDown+=(s,e)=>{down=last=e.Location;drag=false;Capture=true;};
            MouseMove+=(s,e)=>{if(!Capture)return;int dx=e.X-last.X,dy=e.Y-last.Y;if(Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>4)drag=true;yaw+=dx*.01f;pitch+=dy*.01f;last=e.Location;Invalidate();};
            MouseUp+=(s,e)=>{Capture=false;if(drag||Domain==null)return;float best=12*12;Selected=-1;for(int i=0;i<Domain.Positions.Length;i++){var p=Project(Domain.Positions[i]);float dist=(p.X-e.X)*(p.X-e.X)+(p.Y-e.Y)*(p.Y-e.Y);if(dist<best){best=dist;Selected=i;}}Invalidate();if(SelectionChanged!=null)SelectionChanged();};
            MouseWheel+=(s,e)=>{zoom=Math.Max(.1f,Math.Min(10,zoom*(e.Delta>0?1.15f:.87f)));Invalidate();};Resize+=(s,e)=>Invalidate();
        }
        public void ResetView()
        {
            yaw=.35f;pitch=.1f;zoom=1;
            if(Domain!=null){var points=Domain.Positions.Concat(Domain.Colliders.SelectMany(c=>new[]{c.Start,c.End})).ToArray();var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);center=(min+max)*.5f;span=Math.Max(.1f,(max-min).Length());}Invalidate();
        }
        float PixelScale {get{return Math.Min(Width,Height)*.8f*zoom/span;}}
        PointF Project(Vector3 p)
        {var q=Vector3.Transform(p-center,Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateRotationX(pitch));return new PointF(Width*.5f+q.X*PixelScale,Height*.5f-q.Y*PixelScale);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if(Domain==null){g.DrawString("Load Cloth data to inspect rest particles / colliders",Font,Brushes.White,12,12);return;}
            using(var edge=new Pen(Color.FromArgb(100,150,175)))for(int i=0;i<Domain.Triangles.Length;i+=3){var a=Project(Domain.Positions[Domain.Triangles[i]]);var b=Project(Domain.Positions[Domain.Triangles[i+1]]);var c=Project(Domain.Positions[Domain.Triangles[i+2]]);g.DrawLine(edge,a,b);g.DrawLine(edge,b,c);g.DrawLine(edge,c,a);}
            if(ShowColliders)using(var pen=new Pen(Color.FromArgb(180,255,150,50)))foreach(var c in Domain.Colliders)
            {
                // Sample spheres + cone side rings in world rest space, not screen-radius impostors.
                var axis=c.End-c.Start;var unit=axis.LengthSquared()>1e-12f?Vector3.Normalize(axis):Vector3.UnitY;
                var u=Vector3.Normalize(Vector3.Cross(unit,Math.Abs(unit.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var v=Vector3.Cross(unit,u);
                for(int j=0;j<32;j++){float t=j*(float)Math.PI/16,t2=(j+1)*(float)Math.PI/16;var dir=u*(float)Math.Cos(t)+v*(float)Math.Sin(t);var next=u*(float)Math.Cos(t2)+v*(float)Math.Sin(t2);g.DrawLine(pen,Project(c.Start+dir*c.RadiusA),Project(c.Start+next*c.RadiusA));g.DrawLine(pen,Project(c.End+dir*c.RadiusB),Project(c.End+next*c.RadiusB));if(j%8==0){g.DrawLine(pen,Project(c.Start+dir*c.RadiusA),Project(c.End+dir*c.RadiusB));}}
                foreach(var endpoint in new[]{Tuple.Create(c.Start,c.RadiusA),Tuple.Create(c.End,c.RadiusB)})foreach(var basis in new[]{Tuple.Create(u,v),Tuple.Create(u,unit),Tuple.Create(v,unit)})for(int j=0;j<32;j++){float t=j*(float)Math.PI/16,t2=(j+1)*(float)Math.PI/16;g.DrawLine(pen,Project(endpoint.Item1+endpoint.Item2*(basis.Item1*(float)Math.Cos(t)+basis.Item2*(float)Math.Sin(t))),Project(endpoint.Item1+endpoint.Item2*(basis.Item1*(float)Math.Cos(t2)+basis.Item2*(float)Math.Sin(t2))));}
            }
            if(ShowParticles)for(int i=0;i<Domain.Positions.Length;i++){var p=Project(Domain.Positions[i]);g.FillEllipse(Domain.Fixed[i]?Brushes.Gold:Brushes.Cyan,p.X-2,p.Y-2,4,4);}
            if(Selected>=0){var p=Project(Domain.Positions[Selected]);g.FillEllipse(Brushes.White,p.X-4,p.Y-4,8,8);if(ShowRange&&Domain.Ranges[Selected]>0){float radius=Domain.Ranges[Selected]*PixelScale;g.DrawEllipse(Pens.Lime,p.X-radius,p.Y-radius,radius*2,radius*2);}}
            g.DrawString("REST ONLY · Click a particle for its range sphere\nYellow: fixed; cyan: dynamic; orange: colliders",Font,Brushes.White,8,8);
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
