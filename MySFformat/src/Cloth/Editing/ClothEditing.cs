using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MySFformat
{
    public sealed partial class ClothInspectorForm
    {
        readonly DataGridView authorParameters=Grid("clothEditableParameters");
        readonly DataGridView constraintParameters=Grid("clothConstraintParameters");
        readonly Button applyAuthor=new Button {Name="applyClothChanges",Text="Apply Changes",AutoSize=true};
        readonly Button undoAuthor=new Button {Name="undoClothChanges",Text="Undo",AutoSize=true};
        readonly Button redoAuthor=new Button {Name="redoClothChanges",Text="Redo",AutoSize=true};
        readonly Label editState=new Label {Name="clothEditState",AutoSize=true,Padding=new Padding(3,7,0,0)};
        readonly Stack<ClothDocument> undoDocuments=new Stack<ClothDocument>(),redoDocuments=new Stack<ClothDocument>();
        XDocument stagedXml;
        XDocument indexedStagedXml;
        Dictionary<string,XElement> stagedObjects;
        XDocument indexedRangeXml;
        ClothDomain indexedRangeDomain;
        Dictionary<int,XElement[]> stagedRanges;
        bool editingDirty,populatingEditing,syncingSelection,changingDomain,applyingAuthor,previewEnvironmentDirty;
        int editingDomain=-1;
        static readonly CultureInfo EditCulture=CultureInfo.InvariantCulture;

        void InitializeEditing(SplitContainer split,TabPage parameters)
        {
            summary.Dock=DockStyle.Bottom;summary.Height=150;
            parameters.Controls.Add(authorParameters);authorParameters.BringToFront();
            authorParameters.Columns.Add("category","Category");authorParameters.Columns.Add("field","Field");authorParameters.Columns.Add("value","Value");
            authorParameters.ReadOnly=false;authorParameters.Columns[0].ReadOnly=authorParameters.Columns[1].ReadOnly=true;
            authorParameters.CellValidating+=(s,e)=>ValidateParameterCell(e);
            authorParameters.CellValueChanged+=(s,e)=>StageParameterCell(e);
            authorParameters.DataError+=(s,e)=>{e.ThrowException=false;editState.Text="Invalid value; edit not applied";};
            var constraintPage=(TabPage)constraints.Parent;
            var constraintSplit=new SplitContainer {Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,Size=new Size(500,450),SplitterDistance=260};
            constraintPage.Controls.Remove(constraints);constraintSplit.Panel1.Controls.Add(constraints);constraintSplit.Panel2.Controls.Add(constraintParameters);constraintPage.Controls.Add(constraintSplit);
            constraintParameters.Columns.Add("field","Record field");constraintParameters.Columns.Add("value","Value");constraintParameters.ReadOnly=false;constraintParameters.Columns[0].ReadOnly=true;
            constraints.CellClick+=(s,e)=>{if(e.RowIndex>=0)ShowConstraintRecord(e.RowIndex);};
            constraintParameters.CellValidating+=(s,e)=>{
                if(populatingEditing||e.RowIndex<0||e.ColumnIndex!=1||constraintParameters.Rows[e.RowIndex].Cells[1].ReadOnly)return;
                try{ValidateParameter((XElement)constraintParameters.Rows[e.RowIndex].Tag,Convert.ToString(e.FormattedValue));constraintParameters.Rows[e.RowIndex].ErrorText="";}
                catch(Exception ex){e.Cancel=true;constraintParameters.Rows[e.RowIndex].ErrorText=ex.Message;editState.Text=ex.Message;}
            };
            constraintParameters.CellValueChanged+=(s,e)=>{
                if(populatingEditing||e.RowIndex<0||e.ColumnIndex!=1||constraintParameters.Rows[e.RowIndex].Cells[1].ReadOnly)return;
                try{var p=(XElement)constraintParameters.Rows[e.RowIndex].Tag;string value=Convert.ToString(constraintParameters.Rows[e.RowIndex].Cells[1].Value,EditCulture);ValidateParameter(p,value);p.Value=value;editingDirty=true;UpdateEditState();}
                catch(Exception ex){editState.Text=ex.Message;}
            };
            constraintParameters.DataError+=(s,e)=>{e.ThrowException=false;editState.Text="Invalid constraint value; edit not applied";};
            tips.SetToolTip(constraintParameters,"Known scalar stiffness/length fields for the selected record. Particle indices, derived bend weights/curvature and unknown fields stay read-only. Apply commits all pending edits.\n编辑所选记录的已知刚度/长度；粒子索引、派生弯曲权重/曲率和未知字段只读，点Apply统一提交。");
            particles.ReadOnly=false;particles.MultiSelect=true;
            particles.CellValidating+=(s,e)=>ValidateParticleCell(e);
            particles.CellValueChanged+=(s,e)=>StageParticleCell(e);
            particles.SelectionChanged+=(s,e)=>{
                if(syncingSelection||populatingEditing||canvas.Domain==null)return;
                canvas.SetSelection(particles.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.Index));ShowDomainSummary();
            };
            particles.DataError+=(s,e)=>{e.ThrowException=false;editState.Text="Invalid particle value; edit not applied";};
            particles.VisibleChanged+=(s,e)=>ScrollParticleSelectionIntoView();
            particles.SizeChanged+=(s,e)=>ScrollParticleSelectionIntoView();
            var edits=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true};
            var discard=new Button {Name="discardClothChanges",Text="Discard Pending",AutoSize=true};
            edits.Controls.AddRange(new Control[]{applyAuthor,discard,undoAuthor,redoAuthor,editState});split.Panel2.Controls.Add(edits);
            applyAuthor.Click+=async(s,e)=>await ApplyAuthorChangesAsync();
            discard.CausesValidation=false;
            discard.Click+=(s,e)=>{if(Document==null)return;ResetSelectionEditor();stagedXml=new XDocument(Document.Xml);editingDirty=false;ShowDomain();};
            undoAuthor.Click+=async(s,e)=>await RestoreHistoryAsync(false);
            redoAuthor.Click+=async(s,e)=>await RestoreHistoryAsync(true);
            tips.SetToolTip(applyAuthor,"Validate pending edits with full native pack/readback, then commit to memory. Running preview restarts; paused remains paused. Source files are never written.\n完整打包/读回验证后应用到内存；播放自动重启、暂停保持暂停，不写源文件。");
            tips.SetToolTip(authorParameters,"Edit author parameters, not preview overrides. Index arrays, bindings and unknown fields stay read-only in this first version.\n修改作者参数，不是预览覆盖；索引数组、绑定和未知字段本版仍只读。");
            tips.SetToolTip(discard,"Discard only pending table edits, not applied edits.\n仅丢弃尚未Apply的表格修改。");
            tips.SetToolTip(undoAuthor,"Undo one applied author edit; pending changes must be applied or discarded first.\n撤销一次已应用作者修改；先应用或丢弃待提交修改。");
            tips.SetToolTip(redoAuthor,"Redo one undone author edit.\n重做一次已撤销作者修改。");
            var selectionTools=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true};
            var mode=new ComboBox {Name="clothSelectMode",DropDownStyle=ComboBoxStyle.DropDownList,Width=100};mode.Items.AddRange(new object[]{"Click","Box","Circle Brush"});mode.SelectedIndex=0;
            var brush=new NumericUpDown {Name="clothBrushRadius",Minimum=4,Maximum=200,Value=35,Width=55};
            var visible=new CheckBox {Name="clothVisibleSelection",Text="Visible only",AutoSize=true,Checked=true};
            var similar=new Button {Name="clothSelectSimilar",Text="Select Similar…",AutoSize=true};
            var all=new Button {Text="All",AutoSize=true};var invert=new Button {Text="Invert",AutoSize=true};var clear=new Button {Text="Clear",AutoSize=true};var focus=new Button {Text="Focus (F)",AutoSize=true};
            selectionTools.Controls.AddRange(new Control[]{mode,new Label {Text="Radius",AutoSize=true,Padding=new Padding(0,6,0,0)},brush,visible,similar,all,invert,clear,focus});
            split.Panel1.Controls.Add(selectionTools);
            mode.SelectedIndexChanged+=(s,e)=>canvas.SelectionMode=mode.SelectedIndex;
            brush.ValueChanged+=(s,e)=>canvas.BrushRadius=(int)brush.Value;
            visible.CheckedChanged+=(s,e)=>canvas.VisibleOnly=visible.Checked;
            similar.Click+=(s,e)=>ShowSimilarSelection();
            all.Click+=(s,e)=>{if(canvas.Domain!=null)canvas.SetSelection(Enumerable.Range(0,canvas.Domain.Positions.Length));};
            invert.Click+=(s,e)=>{if(canvas.Domain!=null)canvas.SetSelection(Enumerable.Range(0,canvas.Domain.Positions.Length).Except(canvas.SelectedParticles).ToArray());};
            clear.Click+=(s,e)=>canvas.SetSelection(new int[0]);focus.Click+=(s,e)=>canvas.FocusSelection();
            tips.SetToolTip(mode,"Right: select/box/brush. Ctrl or Shift+right: add; Alt+right: remove. Left drag: rotate; middle or Shift+left drag: pan; wheel: zoom.\n右键选择/框选/刷选，Ctrl或Shift+右键添加、Alt+右键移除；左键旋转，中键或Shift+左键平移，滚轮缩放。");
            tips.SetToolTip(visible,"Test front-most cloth triangles for selection. Uncheck for through-selection, including rear particles. Reference colliders do not occlude picks.\n按布料三角面判断前后遮挡；取消则穿透选背面节点，参考碰撞体不遮挡选择。");
            tips.SetToolTip(similar,"Select by explicit properties in this simulation domain; no guessed skin weights.\n按明确属性选择当前模拟域节点，不猜测蒙皮权重。");
            KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F&&!IsTextEditing()){canvas.FocusSelection();e.Handled=true;}};
            foreach(Control c in new Control[]{gravityOverride,gravityX,gravityY,gravityZ,windEnabled,windX,windY,windZ,windSpeed,windDrag})
            {
                var number=c as NumericUpDown;var check=c as CheckBox;
                EventHandler pending=(s,e)=>{if(!populatingEditing){previewEnvironmentDirty=true;UpdateEditState();}};
                if(number!=null)number.ValueChanged+=pending;if(check!=null)check.CheckedChanged+=pending;
            }
            UpdateEditState();
        }
        bool IsTextEditing(){return ActiveControl is TextBoxBase||authorParameters.IsCurrentCellInEditMode||particles.IsCurrentCellInEditMode||constraintParameters.IsCurrentCellInEditMode;}
        void ResetEditingForLoad(){stagedXml=null;editingDirty=false;editingDomain=-1;undoDocuments.Clear();redoDocuments.Clear();}
        bool GuardDomainChange()
        {
            if(changingDomain)return false;
            if(editingDirty&&editingDomain>=0&&domains.SelectedIndex!=editingDomain){changingDomain=true;domains.SelectedIndex=editingDomain;changingDomain=false;editState.Text="Apply or discard pending edits before switching domain";return false;}
            return true;
        }
        XElement StagedObject(XElement original)
        {
            if(!ReferenceEquals(indexedStagedXml,stagedXml))
            {
                stagedObjects=stagedXml.Descendants("hksection").Elements("hkobject").ToDictionary(o=>(string)o.Attribute("name"));
                indexedStagedXml=stagedXml;
            }
            return stagedObjects[(string)original.Attribute("name")];
        }
        void PopulateEditing()
        {
            populatingEditing=true;
            try
            {
                editingDomain=domains.SelectedIndex;if(stagedXml==null)stagedXml=new XDocument(Document.Xml);
                foreach(DataGridViewColumn c in particles.Columns){c.SortMode=DataGridViewColumnSortMode.NotSortable;c.ReadOnly=!new[]{"mass","radius","friction","maximumDistance"}.Contains(c.Name);c.DefaultCellStyle.BackColor=c.ReadOnly?Color.FromArgb(240,240,240):Color.White;}
                var d=Document.Domains[editingDomain];var stagedSim=StagedObject(d.Sim);
                int recordIndex=0;
                foreach(var c in d.Constraints)foreach(var p in c.Elements().Where(p=>p.Elements().Any()))
                {
                    var stagedRows=ClothDocument.Field(StagedObject(c),(string)p.Attribute("name")).Elements().ToArray();
                    foreach(var r in stagedRows)constraints.Rows[recordIndex++].Tag=r;
                }
                foreach(DataGridViewRow r in particles.Rows)
                {
                    if(d.Fixed[r.Index])r.Cells["mass"].ReadOnly=true;
                    if(!RangeRows(d,r.Index).Any())r.Cells["maximumDistance"].ReadOnly=true;
                    foreach(DataGridViewCell cell in r.Cells)if(cell.ReadOnly)cell.Style.BackColor=Color.FromArgb(240,240,240);
                }
                authorParameters.Rows.Clear();
                constraintParameters.Rows.Clear();
                foreach(var p in ClothDocument.Field(stagedSim,"simulationInfo").Elements().Single().Elements())
                    AddParameter("Simulation",p);
                foreach(var reference in ClothDocument.Value(d.Owner,"operators").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
                {
                    var original=Document.Objects[reference];if((string)original.Attribute("class")!="hclSimulateOperator")continue;
                    if(ClothDocument.Numbers(ClothDocument.Value(original,"simClothIndex")).Single()!=Array.IndexOf(ClothDocument.Value(d.Owner,"simClothDatas").Split((char[])null,StringSplitOptions.RemoveEmptyEntries),(string)d.Sim.Attribute("name")))continue;
                    var op=StagedObject(original);
                    foreach(string key in new[]{"subSteps","numberOfSolveIterations","adaptConstraintStiffness"})AddParameter("Solver "+ClothDocument.Value(op,"name"),ClothDocument.Field(ClothDocument.SolverConfig(op),key));
                }
                foreach(var constraint in d.Constraints)
                {
                    var staged=StagedObject(constraint);
                    foreach(var p in staged.Elements().Where(p=>!p.HasElements&&new[]{"stiffness","stretchStiffness","compressionStiffness","bendStiffness"}.Contains((string)p.Attribute("name"))))AddParameter(ClothDocument.Value(staged,"name"),p);
                }
            }
            finally{populatingEditing=false;UpdateEditState();}
        }
        void AddParameter(string category,XElement p)
        {
            string key=(string)p.Attribute("name");string value=p.Value.Trim();bool boolean=value=="true"||value=="false";double number;
            bool scalar=double.TryParse(value,NumberStyles.Float,EditCulture,out number);bool vector=key=="gravity"&&ClothDocument.Numbers(value).Length==4;
            int row=authorParameters.Rows.Add(category,key,value);authorParameters.Rows[row].Tag=p;
            authorParameters.Rows[row].Cells[2].ReadOnly=!(boolean||scalar||vector);
            authorParameters.Rows[row].Cells[2].Style.BackColor=authorParameters.Rows[row].Cells[2].ReadOnly?Color.FromArgb(240,240,240):Color.White;
        }
        void ShowConstraintRecord(int row)
        {
            if(editingDomain<0)return;var record=constraints.Rows[row].Tag as XElement;if(record==null)return;
            populatingEditing=true;
            try
            {
                constraintParameters.Rows.Clear();
                foreach(var p in record.Elements())
                {
                    string key=(string)p.Attribute("name");int index=constraintParameters.Rows.Add(key,p.Value.Trim());constraintParameters.Rows[index].Tag=p;
                    constraintParameters.Rows[index].Cells[1].ReadOnly=!new[]{"stiffness","bendStiffness","restLength","compressionLength","maximumDistance"}.Contains(key)||p.HasElements;
                    constraintParameters.Rows[index].Cells[1].Style.BackColor=constraintParameters.Rows[index].Cells[1].ReadOnly?Color.FromArgb(240,240,240):Color.White;
                }
            }
            finally{populatingEditing=false;}
        }
        IEnumerable<XElement> RangeRows(ClothDomain d,int particle)
        {
            if(!ReferenceEquals(indexedRangeXml,stagedXml)||!ReferenceEquals(indexedRangeDomain,d))
            {
                stagedRanges=d.Constraints.Where(c=>(string)c.Attribute("class")=="hclLocalRangeConstraintSet")
                    .SelectMany(c=>ClothDocument.Field(StagedObject(c),"localConstraints").Elements())
                    .GroupBy(r=>int.Parse(ClothDocument.Value(r,"particleIndex"),EditCulture)).ToDictionary(g=>g.Key,g=>g.ToArray());
                indexedRangeXml=stagedXml;indexedRangeDomain=d;
            }
            XElement[] rows;return stagedRanges.TryGetValue(particle,out rows)?rows:Enumerable.Empty<XElement>();
        }
        static double PositiveNumber(object value,bool zeroAllowed)
        {
            double number;if(!double.TryParse(Convert.ToString(value,EditCulture),NumberStyles.Float,EditCulture,out number)||double.IsNaN(number)||double.IsInfinity(number)||number>float.MaxValue||(zeroAllowed?number<0:number<=0))throw new ArgumentException("Enter a finite "+(zeroAllowed?"nonnegative":"positive")+" number (decimal point: .).");return number;
        }
        void ValidateParticleCell(DataGridViewCellValidatingEventArgs e)
        {
            if(populatingEditing||e.RowIndex<0||particles.Columns[e.ColumnIndex].ReadOnly||particles.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly)return;
            try{PositiveNumber(e.FormattedValue,particles.Columns[e.ColumnIndex].Name!="mass");particles.Rows[e.RowIndex].ErrorText="";}
            catch(Exception ex){e.Cancel=true;particles.Rows[e.RowIndex].ErrorText=ex.Message;editState.Text=ex.Message;}
        }
        void StageParticleCell(DataGridViewCellEventArgs e)
        {
            if(populatingEditing||e.RowIndex<0||e.ColumnIndex<0||stagedXml==null||particles.Columns[e.ColumnIndex].ReadOnly)return;
            try{StageParticle(e.RowIndex,particles.Columns[e.ColumnIndex].Name,PositiveNumber(particles.Rows[e.RowIndex].Cells[e.ColumnIndex].Value,particles.Columns[e.ColumnIndex].Name!="mass"));}
            catch(Exception ex){editState.Text=ex.Message;}
        }
        void StageParticle(int id,string field,double value)
        {
            var d=Document.Domains[editingDomain];if(id<0||id>=d.Positions.Length)throw new ArgumentOutOfRangeException("id");
            if(field=="maximumDistance")
            {
                var ranges=RangeRows(d,id).ToArray();if(ranges.Length==0)throw new InvalidOperationException("Particle "+id+" has no LocalRange record; no new constraint is invented.");
                foreach(var r in ranges)ClothDocument.Field(r,field).Value=value.ToString("R",EditCulture);
            }
            else
            {
                var row=ClothDocument.Field(StagedObject(d.Sim),"particleDatas").Elements().ElementAt(id);
                if(!new[]{"mass","radius","friction"}.Contains(field))throw new ArgumentException("Field is not editable.");
                if(field=="mass"&&d.Fixed[id])throw new InvalidOperationException("Fixed particle mass is read-only; changing fixed-state needs binding/history checks.");
                if(field=="mass"){double inverse=1/value;if(inverse>float.MaxValue||inverse<float.Epsilon)throw new ArgumentException("Mass reciprocal is outside Float32 range.");ClothDocument.Field(row,"invMass").Value=inverse.ToString("R",EditCulture);}
                ClothDocument.Field(row,field).Value=value.ToString("R",EditCulture);
            }
            editingDirty=true;UpdateEditState();
        }
        void ValidateParameterCell(DataGridViewCellValidatingEventArgs e)
        {
            if(populatingEditing||e.RowIndex<0||e.ColumnIndex!=2||authorParameters.Rows[e.RowIndex].Cells[2].ReadOnly)return;
            try{ValidateParameter((XElement)authorParameters.Rows[e.RowIndex].Tag,Convert.ToString(e.FormattedValue));authorParameters.Rows[e.RowIndex].ErrorText="";}
            catch(Exception ex){e.Cancel=true;authorParameters.Rows[e.RowIndex].ErrorText=ex.Message;editState.Text=ex.Message;}
        }
        static void ValidateParameter(XElement p,string value)
        {
            string prior=p.Value.Trim(),key=(string)p.Attribute("name");
            if(prior=="true"||prior=="false"){if(value!="true"&&value!="false")throw new ArgumentException("Enter true or false.");return;}
            if(key=="gravity")
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^\s*\(\s*[-+0-9.eE]+\s+[-+0-9.eE]+\s+[-+0-9.eE]+\s+[-+0-9.eE]+\s*\)\s*$"))throw new ArgumentException("Gravity format: (x y z 0).");
                var n=ClothDocument.Numbers(value);if(n.Length!=4||n.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||Math.Abs(v)>100)||n[3]!=0)throw new ArgumentException("Gravity needs finite XYZ within ±100, and W=0.");return;
            }
            double number=PositiveNumber(value,true);
            bool simulationDefault=key=="subSteps"&&(string)p.Parent.Parent.Attribute("name")=="simulationInfo";
            if(new[]{"subSteps","numberOfSolveIterations"}.Contains(key)&&(number!=Math.Floor(number)||number>64||number<(simulationDefault?0:1)))throw new ArgumentException("Solver count must be an integer "+(simulationDefault?"0":"1")+"..64.");
            if(key=="globalDampingPerSecond"&&number>1)throw new ArgumentException("Damping must be 0..1.");
            if(key.IndexOf("stiffness",StringComparison.OrdinalIgnoreCase)>=0&&number>1)throw new ArgumentException("Stiffness must be 0..1; constraint classes have distinct solver meanings.");
        }
        void StageParameterCell(DataGridViewCellEventArgs e)
        {
            if(populatingEditing||e.RowIndex<0||e.ColumnIndex!=2||stagedXml==null||authorParameters.Rows[e.RowIndex].Cells[2].ReadOnly)return;
            try{var p=(XElement)authorParameters.Rows[e.RowIndex].Tag;string value=Convert.ToString(authorParameters.Rows[e.RowIndex].Cells[2].Value,EditCulture);ValidateParameter(p,value);p.Value=value;editingDirty=true;UpdateEditState();}
            catch(Exception ex){editState.Text=ex.Message;}
        }
        public int[] SelectedParticleIds {get{return canvas.SelectedParticles.OrderBy(i=>i).ToArray();}}
        public void SelectParticles(int[] ids){canvas.SetSelection(ids);}
        void SynchronizeParticleSelection()
        {
            if(syncingSelection)return;syncingSelection=true;
            try
            {
                if(selectedValueDirty&&!inlineParticleTargets.SequenceEqual(SelectedParticleIds))
                {
                    canvas.SetSelection(inlineParticleTargets);
                    selectedParticleInfo.Text="Apply or discard the entered value before changing selection";
                }
                // 设定当前行锚点，防止首次切到粒子 tab 时 DataGridView 自动
                // 选中第 0 行，从而把画布上的多选覆盖成单选。
                if(canvas.SelectedParticles.Count>0)
                {
                    int first=canvas.SelectedParticles.Min();
                    if(first<particles.Rows.Count)particles.CurrentCell=particles.Rows[first].Cells[0];
                }
                particles.ClearSelection();
                foreach(int id in canvas.SelectedParticles)if(id>=0&&id<particles.Rows.Count)particles.Rows[id].Selected=true;
            }
            finally{syncingSelection=false;}
            ScrollParticleSelectionIntoView();
            UpdateEditState();
        }
        void ScrollParticleSelectionIntoView()
        {
            if(!IsHandleCreated||IsDisposed||!particles.Visible||canvas.SelectedParticles.Count==0)return;
            BeginInvoke(new Action(()=>
            {
                if(IsDisposed||!particles.Visible||canvas.SelectedParticles.Count==0)return;
                int first=canvas.SelectedParticles.Min();
                if(first>=particles.Rows.Count)return;
                try{particles.FirstDisplayedScrollingRowIndex=first;}
                catch(InvalidOperationException){/* tab 正在布局时下次可见/尺寸事件再定位。 */}
            }));
        }
        void UpdateEditState()
        {
            bool readOnly=Document!=null&&Document.IsTagfile;
            bool pending=editingDirty||selectedValueDirty;
            applyAuthor.Enabled=pending&&!applyingAuthor;undoAuthor.Enabled=!pending&&!applyingAuthor&&undoDocuments.Count>0;redoAuthor.Enabled=!pending&&!applyingAuthor&&redoDocuments.Count>0;
            load.Enabled=!pending&&!applyingAuthor;domains.Enabled=!pending&&!applyingAuthor;
            nativeLayout.Enabled=!readOnly&&!pending&&!applyingAuthor;authorParameters.Enabled=particles.Enabled=constraintParameters.Enabled=!applyingAuthor;
            particles.ReadOnly=authorParameters.ReadOnly=constraintParameters.ReadOnly=readOnly;
            foreach(string name in new[]{"editHkx","saveHkx","saveRangeCopy","modifyClothSelection","discardClothChanges"})foreach(var control in Controls.Find(name,true))control.Enabled=!readOnly&&!applyingAuthor&&(!editingDirty||name=="modifyClothSelection"||name=="discardClothChanges");
            editState.Text=canvas.SelectedParticles.Count+" selected · "+(pending?"Pending edits — Apply Changes":Document!=null&&Document.Edited?"Applied in memory — save a copy":"No author changes")+(previewEnvironmentDirty?" · Environment pending — Apply Environment":"");
            if(readOnly)editState.Text=Document.NativeLayout+" · read-only inspection / preview · no game HKX export";
            if(readOnly)applyAuthor.Enabled=undoAuthor.Enabled=redoAuthor.Enabled=false;
            selectedParticleEditor.Enabled=!readOnly&&!applyingAuthor;
            RefreshSelectionEditor();
        }
        public async Task ApplyAuthorChangesAsync()
        {
            if(applyingAuthor||Document==null)return;
            if(Document.IsTagfile)throw new NotSupportedException("Sekiro/ER TAG0 author editing/export is not implemented.");
            if(!ValidateChildren()||!particles.EndEdit()||!authorParameters.EndEdit()||!constraintParameters.EndEdit())return;
            if(!editingDirty)return;
            applyingAuthor=true;UpdateEditState();
            try
            {
                var snapshot=new XDocument(stagedXml);string source=Document.Source;var expected=Document;
                var validated=await Task.Run(()=>{var doc=ClothDocument.FromXml(snapshot,source);HkxPackfileCodec.Pack(doc.Xml);return doc;});
                if(IsDisposed||!ReferenceEquals(Document,expected))return;
                undoDocuments.Push(Document);redoDocuments.Clear();await CommitAuthorDocumentAsync(validated);
            }
            catch(Exception ex){if(!IsDisposed){editState.Text="Apply failed: "+ex.GetBaseException().Message;status.Text=editState.Text+"\nNo author data or source files changed.";}}
            finally{applyingAuthor=false;if(!IsDisposed){string message=editState.Text;UpdateEditState();if(message.StartsWith("Apply failed:",StringComparison.Ordinal))editState.Text=message;}}
        }
        async Task CommitAuthorDocumentAsync(ClothDocument document)
        {
            bool restart=previewSession!=null||previewPreparing,paused=previewSession!=null&&!previewPlaying;
            var restartEnvironment=previewSession==null?null:appliedEnvironment.Clone();
            bool resumeAnimation=HasLinkedAnimation&&ClothAnimationPlayback.Playing;
            int domain=domains.SelectedIndex;int[] selected=SelectedParticleIds;
            StopClothPreview();Document=document;editingDirty=false;stagedXml=new XDocument(document.Xml);
            updatingLayout=true;nativeLayout.SelectedIndex=document.NativeLayout=="8,1,0,1"?1:0;updatingLayout=false;
            // Keep the camera and selection across author edits; only the physics history resets.
            var view=canvas.SaveCamera();changingDomain=true;for(int i=0;i<document.Domains.Count;i++)domains.Items[i]=document.Domains[i];domains.SelectedIndex=domain;changingDomain=false;ShowDomain();canvas.RestoreCamera(view);canvas.SetSelection(selected);
            Text="Cloth Inspector — "+System.IO.Path.GetFileName(document.Source)+(document.Edited?" [in-memory edits]":"");
            status.Text="Author changes validated and applied in memory. Save HKX Copy to export; source files unchanged.";
            if(restart){if(resumeAnimation)ClothAnimationPlayback.Play();await StartClothPreviewAsync(restartEnvironment);if(paused&&previewSession!=null)PauseClothPreview();}
            if(!IsDisposed)UpdateEditState();
        }
        async Task RestoreHistoryAsync(bool redo)
        {
            if(editingDirty||applyingAuthor)return;var from=redo?redoDocuments:undoDocuments;var to=redo?undoDocuments:redoDocuments;if(from.Count==0)return;
            applyingAuthor=true;UpdateEditState();
            try{to.Push(Document);await CommitAuthorDocumentAsync(from.Pop());}
            finally{applyingAuthor=false;if(!IsDisposed)UpdateEditState();}
        }
        public void StageSelectedParticleValues(string field,double value,bool multiply=false)
        {
            if(Document!=null&&Document.IsTagfile)throw new NotSupportedException("Sekiro/ER TAG0 author data is read-only.");
            PositiveNumber(value,field!="mass");if(canvas.SelectedParticles.Count==0)throw new InvalidOperationException("Select particles first.");
            var backup=new XDocument(stagedXml);bool wasDirty=editingDirty;
            try
            {
                foreach(int id in SelectedParticleIds)
                {
                    double current=field=="maximumDistance"?RangeRows(Document.Domains[editingDomain],id).Select(r=>double.Parse(ClothDocument.Value(r,field),EditCulture)).DefaultIfEmpty(double.NaN).Max():double.Parse(ClothDocument.Value(ClothDocument.Field(StagedObject(Document.Domains[editingDomain].Sim),"particleDatas").Elements().ElementAt(id),field),EditCulture);
                    double result=multiply?current*value:value;PositiveNumber(result,field!="mass");StageParticle(id,field,result);
                }
            }
            catch{stagedXml=backup;editingDirty=wasDirty;PopulateEditing();throw;}
            populatingEditing=true;
            try{foreach(int id in SelectedParticleIds){particles.Rows[id].Cells[field].Value=field=="maximumDistance"?RangeRows(Document.Domains[editingDomain],id).Max(r=>double.Parse(ClothDocument.Value(r,field),EditCulture)).ToString("R",EditCulture):ClothDocument.Value(ClothDocument.Field(StagedObject(Document.Domains[editingDomain].Sim),"particleDatas").Elements().ElementAt(id),field);}}
            finally{populatingEditing=false;UpdateEditState();}
        }
        void ShowModifySelection()
        {
            using(var dialog=new Form {Text="Modify Selected Particles — Pending Until Apply",Size=new Size(440,210),StartPosition=FormStartPosition.CenterParent})
            {
                var row=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(10)};
                var field=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=170};field.Items.AddRange(new object[]{"maximumDistance","mass","radius","friction"});field.SelectedIndex=0;
                var operation=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=100};operation.Items.AddRange(new object[]{"Set","Multiply"});operation.SelectedIndex=0;
                var value=new TextBox {Text="1",Width=90};var stage=new Button {Text="Stage Changes",AutoSize=true};var message=new Label {AutoSize=true,MaximumSize=new Size(390,0),Text=canvas.SelectedParticles.Count+" particles selected. No fixed-state/topology changes."};
                row.Controls.AddRange(new Control[]{field,operation,value,stage,message});dialog.Controls.Add(row);
                stage.Click+=(s,e)=>{try{StageSelectedParticleValues((string)field.SelectedItem,PositiveNumber(value.Text,(string)field.SelectedItem!="mass"),operation.SelectedIndex==1);dialog.Close();}catch(Exception ex){message.Text=ex.Message;}};
                dialog.ShowDialog(this);
            }
        }
        public void SelectSimilarParticles(string criterion,double tolerance)
        {
            if(canvas.Domain==null||canvas.SelectedParticles.Count==0)throw new InvalidOperationException("Select seed particles first.");
            if(double.IsNaN(tolerance)||double.IsInfinity(tolerance)||tolerance<0)throw new ArgumentException("Tolerance must be finite and nonnegative.");
            var d=canvas.Domain;int[] seeds=SelectedParticleIds;var matches=new HashSet<int>(seeds);
            var rows=ClothDocument.Field(StagedObject(d.Sim),"particleDatas").Elements().ToArray();
            if(criterion=="Constraint membership")
            {
                foreach(var c in d.Constraints)
                {
                    var ids=c.Descendants("hkparam").Where(p=>new[]{"particleA","particleB","particleC","particleD","particleIndex"}.Contains((string)p.Attribute("name"))).Select(p=>int.Parse(p.Value,EditCulture)).Distinct().ToArray();
                    if(ids.Intersect(seeds).Any())matches.UnionWith(ids);
                }
            }
            else
            {
                Func<int,double> value;
                switch(criterion){case "Fixed / Dynamic":value=i=>d.Fixed[i]?0:1;break;case "Range":value=i=>RangeRows(d,i).Select(r=>double.Parse(ClothDocument.Value(r,"maximumDistance"),EditCulture)).DefaultIfEmpty(double.NaN).Max();break;case "Inverse mass":value=i=>double.Parse(ClothDocument.Value(rows[i],"invMass"),EditCulture);break;case "Rest height (Y)":value=i=>d.Positions[i].Y;break;default:throw new ArgumentException("Unknown similarity criterion.");}
                double limit=criterion=="Fixed / Dynamic"?0:tolerance;var seedValues=seeds.Select(value).ToArray();
                for(int i=0;i<d.Positions.Length;i++)if(seedValues.Any(v=>Math.Abs(value(i)-v)<=limit))matches.Add(i);
            }
            canvas.SetSelection(matches);
        }
        void ShowSimilarSelection()
        {
            using(var dialog=new Form {Text="Select Similar — Current Domain",Size=new Size(450,220),StartPosition=FormStartPosition.CenterParent})
            {
                var row=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(10)};
                var criterion=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=220};criterion.Items.AddRange(new object[]{"Fixed / Dynamic","Range","Inverse mass","Rest height (Y)","Constraint membership"});criterion.SelectedIndex=0;
                var tolerance=new TextBox {Text="0.01",Width=90};var select=new Button {Text="Select Similar",AutoSize=true};var note=new Label {AutoSize=true,MaximumSize=new Size(400,0),Text="Match any selected seed. Absolute tolerance; fixed state uses exact match. Constraint membership may select the entire constraint set."};
                row.Controls.AddRange(new Control[]{criterion,new Label {Text="Tolerance",AutoSize=true},tolerance,select,note});dialog.Controls.Add(row);
                select.Click+=(s,e)=>{try{SelectSimilarParticles((string)criterion.SelectedItem,PositiveNumber(tolerance.Text,true));dialog.Close();}catch(Exception ex){note.Text=ex.Message;}};dialog.ShowDialog(this);
            }
        }
    }
}
