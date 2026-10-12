using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MySFformat
{
    public sealed class Clm2EditForm : Form
    {
        readonly Clm2Document working;
        readonly ListBox meshes=new ListBox { Name="clmMeshes",Dock=DockStyle.Fill };
        readonly DataGridView entries=new DataGridView {Name="clmEntries",Dock=DockStyle.Fill,AllowUserToAddRows=true,AllowUserToDeleteRows=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=true};
        readonly Label info=new Label {Dock=DockStyle.Top,Height=58};
        readonly ToolTip tips=new ToolTip {AutoPopDelay=20000,InitialDelay=400,ShowAlways=true};
        int current=-1;bool updating;
        public Clm2Document Result {get;private set;}
        public Clm2EditForm(Clm2Document document)
        {
            working=document.Clone();Text="Edit CLM2 — Known Layout, Unknown Entry Semantics";Width=780;Height=580;MinimumSize=new Size(620,440);StartPosition=FormStartPosition.CenterParent;
            var split=new SplitContainer {Size=new Size(760,460),Dock=DockStyle.Fill,SplitterDistance=200};split.Panel1.Controls.Add(meshes);split.Panel2.Controls.Add(entries);
            entries.Columns.Add("Unk00","Unk00 (Int16)");entries.Columns.Add("Unk02","Unk02 (Int16)");foreach(DataGridViewColumn column in entries.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
            entries.DefaultValuesNeeded+=(s,e)=>{e.Row.Cells[0].Value=0;e.Row.Cells[1].Value=0;};
            entries.DataError+=(s,e)=>{e.ThrowException=false;};
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.LeftToRight};
            var add=new Button {Name="addClmMesh",Text="Add Mesh",AutoSize=true};var remove=new Button {Name="removeClmMesh",Text="Remove Last Mesh",AutoSize=true};var ok=new Button {Name="applyClmEdit",Text="Apply Edits",AutoSize=true};var cancel=new Button {Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
            buttons.Controls.AddRange(new Control[]{add,remove,ok,cancel});Controls.Add(split);Controls.Add(buttons);Controls.Add(info);CancelButton=cancel;
            tips.SetToolTip(meshes,"CLM2 mesh records correspond to FLVER mesh order, not HKX simulation domains.\nCLM2网格记录对应FLVER网格顺序，不是HKX模拟域。");
            tips.SetToolTip(entries,"Edit only the two known signed 16-bit fields (-32768 to 32767). Their runtime meaning is not confirmed. Add/delete rows using the grid.\n仅编辑已知的两个有符号16位字段（-32768至32767）；运行时含义未确认。可在表格中添加/删除条目。");
            tips.SetToolTip(add,"Append an empty mesh record. Existing mesh indices remain unchanged.\n在末尾追加空mesh记录，不改变已有mesh索引。");
            tips.SetToolTip(remove,"Remove only the final mesh record, with confirmation. Its entries are removed too.\n经确认后只删除最后一个mesh记录，同时删除该mesh的条目。");
            tips.SetToolTip(ok,"Apply validated edits to the in-memory CLM2 document only. Use Save CLM2 in the parent window to write a new file.\n校验后只应用到内存中的CLM2文档；需要主窗口Save CLM2才能另存文件。");
            tips.SetToolTip(cancel,"Discard this dialog's changes; the loaded CLM2 is unchanged.\n放弃本弹窗修改，已载入CLM2不变。");
            tips.SetToolTip(info,"Known CLM2 structure comes from SoulsFormats. Mesh order follows FLVER; entry semantics remain unknown.\n已知CLM2结构来自SoulsFormats；mesh顺序对应FLVER，条目语义仍未知。");
            add.Click+=(s,e)=>{if(!CommitRows())return;if(working.Meshes.Count>=10000){MessageBox.Show(this,"Mesh-count limit reached.");return;}working.Meshes.Add(new List<Clm2Entry>());RefreshMeshes(working.Meshes.Count-1);};
            remove.Click+=(s,e)=>{if(working.Meshes.Count==0||!CommitRows())return;if(MessageBox.Show(this,"Remove the last mesh and all its entries?","Remove Mesh",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;working.Meshes.RemoveAt(working.Meshes.Count-1);RefreshMeshes(Math.Min(current,working.Meshes.Count-1));};
            ok.Click+=(s,e)=>{ApplyEdits();};
            meshes.SelectedIndexChanged+=(s,e)=>{if(updating)return;int selected=meshes.SelectedIndex;if(!CommitRows()){updating=true;meshes.SelectedIndex=current;updating=false;return;}ShowMesh(selected);};
            RefreshMeshes(working.Meshes.Count>0?0:-1);
        }
        public bool ApplyEdits()
        {
            if(!CommitRows())return false;
            try{working.Serialize();working.Edited=true;Result=working;DialogResult=DialogResult.OK;Close();return true;}
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Invalid CLM2",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
        }
        bool CommitRows()
        {
            if(current<0)return true;entries.EndEdit();var rows=new List<Clm2Entry>();
            foreach(DataGridViewRow row in entries.Rows)
            {
                if(row.IsNewRow)continue;short a,b;
                if(!short.TryParse(Convert.ToString(row.Cells[0].Value),NumberStyles.Integer,CultureInfo.InvariantCulture,out a)||!short.TryParse(Convert.ToString(row.Cells[1].Value),NumberStyles.Integer,CultureInfo.InvariantCulture,out b))
                {MessageBox.Show(this,"Both entry fields must be integers from -32768 to 32767.","Invalid Entry",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
                rows.Add(new Clm2Entry{Unk00=a,Unk02=b});
            }
            working.Meshes[current]=rows;
            bool previous=updating;updating=true;meshes.Items[current]="Mesh "+current+" ("+rows.Count+" entries)";updating=previous;
            return true;
        }
        void RefreshMeshes(int index)
        {
            updating=true;meshes.Items.Clear();for(int i=0;i<working.Meshes.Count;i++)meshes.Items.Add("Mesh "+i+" ("+working.Meshes[i].Count+" entries)");meshes.SelectedIndex=index;updating=false;ShowMesh(index);
        }
        void ShowMesh(int index)
        {
            current=index;entries.Rows.Clear();entries.Enabled=index>=0;
            if(index>=0)foreach(var row in working.Meshes[index])entries.Rows.Add(row.Unk00,row.Unk02);
            info.Text="Meshes: "+working.Meshes.Count+". Each record corresponds to a FLVER mesh, not a cloth domain.\nUnk00 / Unk02 are signed Int16 fields; their runtime meaning is unknown.\nEdits apply in memory only. Save CLM2 separately to write a new file.";
        }
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
}
