using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsFormats;

namespace MySFformat
{
    public sealed class ClothSetupWizardForm : Form
    {
        readonly FLVER2 snapshot;
        readonly TabControl pages=new TabControl{Dock=DockStyle.Fill};
        readonly Button back=new Button{Text="Back",AutoSize=true},next=new Button{Text="Next",AutoSize=true},save=new Button{Text="Save New Folder…",AutoSize=true,Enabled=false};
        readonly ComboBox mesh=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=580},bone=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=360};
        readonly CheckBox assign=new CheckBox{Text="Assign every selected-mesh vertex to this bone (weight 1)",AutoSize=true};
        readonly ClothCanvas canvas=new ClothCanvas{Dock=DockStyle.Fill,ShowColliders=false,ShowRange=false};
        readonly Label summary=new Label{AutoSize=true,MaximumSize=new Size(700,0)},pinCount=new Label{AutoSize=true};
        readonly TextBox displayName=new TextBox{Text="Setup_Cloth",Width=300},mtd=new TextBox{Text=@"N:\SPRJ\data\Material\mtd\parts\P[ARSN]_al_Cloth.mtd",Width=610};
        readonly TextBox review=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
        readonly ToolTip tips=new ToolTip{AutoPopDelay=20000};
        readonly NumericUpDown mass=Number(.8m,.001m,1000),gravity=Number(-9.81m,-100,100),damping=Number(.6m,0,1),radius=Number(.003m,.0001m,.1m),range=Number(.4m,.001m,10),strength=Number(1,.001m,1),bend=Number(.006m,0,1),rows=Number(1,1,100);
        ClothSetupMesh geometry;
        ClothDomain domain;
        ClothSetupResult result;
        int analyzedMesh=-1;
        bool working,navigating;
        public ClothSetupWizardForm(FLVER2 model)
        {
            if(model==null)throw new InvalidOperationException("Open a FLVER model first.");
            snapshot=FLVER2.Read(model.Write());Text="Cloth Setup Wizard — Bloodborne Prototype";Width=980;Height=790;MinimumSize=new Size(820,650);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};var cancel=new Button{Text="Close",AutoSize=true};footer.Controls.AddRange(new Control[]{cancel,save,next,back});Controls.Add(pages);Controls.Add(footer);cancel.Click+=(s,e)=>{if(!working)Close();};back.Click+=(s,e)=>{if(pages.SelectedIndex>0){result=null;GoPage(pages.SelectedIndex-1);UpdateButtons();}};next.Click+=async(s,e)=>await Advance();save.Click+=(s,e)=>Save();
            var first=Page("1. Mesh");var choose=Column();first.Controls.Add(choose);choose.Controls.Add(Note("Create one single-bone Cloth domain from a current FLVER snapshot. Other meshes are retained. No body collision, Binder packaging or game launch. Input is never modified."));choose.Controls.Add(mesh);choose.Controls.Add(summary);
            for(int i=0;i<snapshot.Meshes.Count;i++){var m=snapshot.Meshes[i];mesh.Items.Add(i+" — "+(m.NodeIndex>=0&&m.NodeIndex<snapshot.Nodes.Count?snapshot.Nodes[m.NodeIndex].Name:"Unnamed")+" — "+m.Vertices.Count+" display vertices");}
            mesh.SelectedIndexChanged+=(s,e)=>{try{Analyze();}catch(Exception ex){geometry=null;summary.Text=ex.Message;}UpdateButtons();};if(mesh.Items.Count>0)mesh.SelectedIndex=mesh.Items.Count-1;
            var second=Page("2. Support / Fixed Points");var controls=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
            for(int i=0;i<snapshot.Nodes.Count;i++)bone.Items.Add(i+" — "+snapshot.Nodes[i].Name);int spine=snapshot.Nodes.FindIndex(n=>n.Name=="Spine2");if(bone.Items.Count>0)bone.SelectedIndex=spine>=0?spine:0;
            controls.Controls.AddRange(new Control[]{new Label{Text="Support bone",AutoSize=true},bone,assign});controls.SetFlowBreak(assign,true);
            var top=new Button{Text="Fix Top Y Rows",AutoSize=true};var fix=new Button{Text="Fix Selected",AutoSize=true};var release=new Button{Text="Release Selected",AutoSize=true};var clear=new Button{Text="Clear Fixed",AutoSize=true};var reset=new Button{Text="Reset View",AutoSize=true};
            controls.Controls.AddRange(new Control[]{rows,top,fix,release,clear,reset,pinCount});second.Controls.Add(canvas);second.Controls.Add(controls);
            tips.SetToolTip(top,"Replace the fixed set with the highest N distinct authored Y rows. Use manual selection for rotated meshes.\n用作者坐标Y最高的N排替换固定集合；旋转后的网格请手动选择。");
            tips.SetToolTip(fix,"Make selected simulation particles fixed; mappings, mass and constraints rebuild together during generation.\n将选中模拟粒子设为固定；生成时同步重建映射、质量和约束。");
            tips.SetToolTip(release,"Make selected particles dynamic in this setup only.\n仅在本次设置中将选中粒子恢复为动态。");
            tips.SetToolTip(save,"Save a new timestamped folder containing the FLVER copy, native HKX, CLM2, XML and report. Never modifies the currently open model.\n在新时间戳目录保存FLVER副本、原生HKX、CLM2、XML和报告，不修改当前模型。");
            top.Click+=(s,e)=>{if(domain==null)return;System.Array.Clear(domain.Fixed,0,domain.Fixed.Length);foreach(int i in geometry.TopRows((int)rows.Value))domain.Fixed[i]=true;PinsChanged();};fix.Click+=(s,e)=>{if(domain==null)return;foreach(int i in canvas.SelectedParticles)domain.Fixed[i]=true;PinsChanged();};release.Click+=(s,e)=>{if(domain==null)return;foreach(int i in canvas.SelectedParticles)domain.Fixed[i]=false;PinsChanged();};clear.Click+=(s,e)=>{if(domain==null)return;System.Array.Clear(domain.Fixed,0,domain.Fixed.Length);PinsChanged();};reset.Click+=(s,e)=>canvas.ResetView();
            tips.SetToolTip(canvas,"Right-click selects particles; Ctrl/Shift+right adds, Alt+right removes. Left drag rotates; middle or Shift+left drag pans; wheel zooms. Yellow: fixed. Fix Selected / Release Selected changes the generated setup.\n右键选择粒子，Ctrl/Shift+右键添加、Alt+右键移除；左键旋转，中键或Shift+左键平移，滚轮缩放。黄色为固定；用Fix/Release按钮修改生成设置。");tips.SetToolTip(assign,"Off: validate existing uniform single-bone weights. On: explicitly assign weight 1 in the generated FLVER copy only.\n关闭时验证已有单骨权重；开启时仅在生成的FLVER副本中明确重绑为单骨权重1。");
            var physics=Page("3. Physics");var values=Column();physics.Controls.Add(values);values.Controls.Add(Note("Basic no-collision preset. Bend uses a cross-edge link approximation. All range values use authored coordinates (meters in this lesson)."));Add(values,"Total dynamic mass",mass);Add(values,"Gravity Y",gravity);Add(values,"Damping per second",damping);Add(values,"Particle radius",radius);Add(values,"Local range",range);Add(values,"Normalized edge strength",strength);Add(values,"Normalized bend strength",bend);
            var changes=Page("4. Planned Changes");var plan=Column();changes.Controls.Add(plan);plan.Controls.Add(Note("Only the selected mesh gets a new display node, a cloned material with the chosen MTD, and a two-buffer Cloth output layout. Positions, UVs and triangle topology are retained. Other meshes and existing nodes are retained.\r\n\r\nEmpty CLM2 records will be generated for ALL meshes. Their runtime/game behavior is not certified. Texture compatibility must be verified by you. No automatic texture or Binder replacement."));Add(plan,"Unique display node",displayName);Add(plan,"Bloodborne Cloth MTD",mtd);plan.Controls.Add(Note("Next constructs and validates a complete temporary result. Nothing is applied to the current Editor model."));
            var last=Page("5. Validate / Save");last.Controls.Add(review);pages.Selecting+=(s,e)=>{if(!navigating&&!pages.Disposing&&!Disposing&&!IsDisposed)e.Cancel=true;};pages.SelectedIndexChanged+=(s,e)=>{if(!Disposing&&!IsDisposed)UpdateButtons();};UpdateButtons();
        }
        TabPage Page(string text){var p=new TabPage(text);pages.TabPages.Add(p);return p;}
        static FlowLayoutPanel Column(){return new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12)};}
        static Label Note(string text){return new Label{Text=text,AutoSize=true,MaximumSize=new Size(730,0),Margin=new Padding(3,8,3,12)};}
        static NumericUpDown Number(decimal value,decimal min,decimal max){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,DecimalPlaces=4,Increment=.001m,Width=110};}
        static void Add(FlowLayoutPanel panel,string label,Control value){panel.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(3,8,3,2)});panel.Controls.Add(value);}
        void Analyze()
        {
            if(mesh.SelectedIndex<0)return;if(analyzedMesh==mesh.SelectedIndex&&geometry!=null)return;result=null;geometry=ClothSetupBuilder.Analyze(snapshot,mesh.SelectedIndex);analyzedMesh=mesh.SelectedIndex;
            domain=new ClothDomain{Positions=geometry.Positions,Fixed=new bool[geometry.Positions.Length],Triangles=geometry.Triangles,Ranges=new float[geometry.Positions.Length]};foreach(int i in geometry.TopRows(1))domain.Fixed[i]=true;canvas.Domain=domain;canvas.SetSelection(new int[0]);canvas.ResetView();PinsChanged();
            summary.Text="Display vertices: "+geometry.DisplayVertices+"\r\nWelded particles: "+geometry.Positions.Length+"\r\nBase triangles: "+geometry.Triangles.Length/3+"\r\nConnected / nondegenerate welded topology: PASS";
        }
        void PinsChanged(){result=null;pinCount.Text=domain.Fixed.Count(v=>v)+" fixed / "+domain.Fixed.Count(v=>!v)+" dynamic";canvas.Invalidate();}
        void GoPage(int index){navigating=true;try{pages.SelectedIndex=index;}finally{navigating=false;}}
        void UpdateButtons(){back.Enabled=!working&&pages.SelectedIndex>0;next.Enabled=!working&&geometry!=null&&pages.SelectedIndex<4;save.Enabled=!working&&pages.SelectedIndex==4&&result!=null;next.Text=pages.SelectedIndex==3?"Generate / Validate":"Next";}
        async Task Advance()
        {
            try
            {
                if(pages.SelectedIndex==0)Analyze();if(geometry==null)throw new InvalidOperationException("Select a supported mesh.");
                if(pages.SelectedIndex==1&&(bone.SelectedIndex<0||!domain.Fixed.Any(v=>v)||domain.Fixed.All(v=>v)))throw new InvalidOperationException("Select a support bone and at least one fixed and one dynamic particle.");
                if(pages.SelectedIndex==3)
                {
                    var options=new ClothSetupOptions{MeshIndex=mesh.SelectedIndex,SupportBoneIndex=bone.SelectedIndex,AssignSingleBone=assign.Checked,FixedParticles=Enumerable.Range(0,domain.Fixed.Length).Where(i=>domain.Fixed[i]).ToArray(),TotalMass=(float)mass.Value,GravityY=(float)gravity.Value,Damping=(float)damping.Value,Radius=(float)radius.Value,Range=(float)range.Value,StretchStrength=(float)strength.Value,BendStrength=(float)bend.Value,DisplayName=displayName.Text.Trim(),MaterialMtd=mtd.Text.Trim()};
                    working=true;pages.Enabled=false;UpdateButtons();try{result=await Task.Run(()=>ClothSetupBuilder.Build(snapshot,options));if(IsDisposed)return;review.Text=result.Report;}finally{working=false;if(!IsDisposed)pages.Enabled=true;}
                }
                if(!IsDisposed)GoPage(pages.SelectedIndex+1);
            }
            catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.GetBaseException().Message,"Cloth setup not generated",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            finally{if(!IsDisposed)UpdateButtons();}
        }
        void Save()
        {
            if(result==null)return;
            try
            {
                string parent=ClothSetupFolderPicker.Show(this);if(string.IsNullOrEmpty(parent))return;
                string output=Path.Combine(parent,"ClothSetup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));result.SaveNewFolder(output,"Setup_Cloth");review.Text=result.Report+"\r\nSaved: "+output+"\r\nOpen Setup_Cloth.flver in Editor, then Load Cloth / CLM2 for preview. The original model remains open and unchanged.";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Setup not saved",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        protected override void OnFormClosing(FormClosingEventArgs e){if(working)e.Cancel=true;base.OnFormClosing(e);}
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
}
