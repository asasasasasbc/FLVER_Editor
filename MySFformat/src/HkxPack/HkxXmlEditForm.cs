using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace MySFformat
{
    public sealed class HkxXmlEditForm : Form
    {
        readonly ClothDocument source;
        readonly TextBox xml=new TextBox {Name="editHkxXml",Dock=DockStyle.Fill,Multiline=true,WordWrap=false,ScrollBars=ScrollBars.Both,MaxLength=0,AcceptsTab=true,Font=new Font("Consolas",9)};
        readonly ToolTip tips=new ToolTip {AutoPopDelay=20000,InitialDelay=400,ShowAlways=true};
        public ClothDocument Result {get;private set;}
        public HkxXmlEditForm(ClothDocument document)
        {
            source=document;Text="Edit HKX XML — Native BB / DS3 Cloth";Width=1080;Height=780;MinimumSize=new Size(750,500);StartPosition=FormStartPosition.CenterParent;
            xml.Text=document.Xml.ToString();var note=new Label {Dock=DockStyle.Top,Height=54,Text="Advanced author-data editing. Keep object IDs/references and required schema fields consistent.\nApply validates cloth dimensions, native packing and full readback; it changes memory only.\nUnknown fields remain unknown. Use Save HKX Copy to write a separate file; no game Binder is overwritten."};
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=40};var apply=new Button {Name="applyHkxEdit",Text="Validate / Apply",AutoSize=true};var cancel=new Button {Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);Controls.Add(xml);Controls.Add(note);Controls.Add(buttons);CancelButton=cancel;
            tips.SetToolTip(xml,"Edit the complete packfile XML, including supported solver/constraint/particle fields. Missing fields, unresolved refs, bad counts and unsupported classes are rejected.\n编辑完整packfile XML及支持的解算/约束/粒子字段；缺字段、悬空引用、错误计数和未支持类会被拒绝。");
            tips.SetToolTip(note,"Pure C# schema-driven read/write; not Havok simulation or runtime validation.\n纯C#按已验证结构读写，不是Havok解算或运行时验证。");
            tips.SetToolTip(apply,"Pack and reread in memory, compare all known fields, then apply the validated document without saving any file.\n在内存打包/读回、比较全部已知字段后应用文档，不保存任何文件。");
            tips.SetToolTip(cancel,"Discard this dialog's edits; loaded source and in-memory document remain unchanged.\n放弃本弹窗修改，不改变源文件和原内存文档。");
            apply.Click+=(s,e)=>{try{ValidateAndApply();}catch(Exception ex){MessageBox.Show(this,ex.Message,"HKX Validation Failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}};
        }
        public void ValidateAndApply()
        {
            using(var text=new StringReader(xml.Text))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=128*1024*1024}))
            {
                var document=ClothDocument.FromXml(XDocument.Load(reader),source.Source);
                HkxPackfileCodec.Pack(document.Xml);Result=document;DialogResult=DialogResult.OK;Close();
            }
        }
        protected override void Dispose(bool disposing){if(disposing){tips.Dispose();xml.Font.Dispose();}base.Dispose(disposing);}
    }
}
