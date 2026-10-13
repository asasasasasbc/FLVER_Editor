using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using MySFformat.Cloth.Preview;

namespace MySFformat
{
    public sealed partial class ClothInspectorForm
    {
        // 文件按钮事件集中放在这里，主 Form 只组装布局，不混入长篇对话框逻辑。
        async void OnLoadClothClick(object sender, EventArgs args)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Cloth (*.xml;*.hkx;*.dcx)|*.xml;*.hkx;*.dcx",
                Title = "Select BB / DS3 Cloth Data (Not Animation HKX)"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                load.Enabled = false;
                UseWaitCursor = true;
                try
                {
                    var document = await Task.Run(() => ClothDocument.Load(dialog.FileName));
                    if (!IsDisposed) SetDocument(document);
                }
                catch (Exception ex)
                {
                    if (!IsDisposed) MessageBox.Show(this, ex.Message, "Cloth Load Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        load.Enabled = true;
                        UseWaitCursor = false;
                    }
                }
            }
        }

        void OnSaveRangeClick(object sender, EventArgs args)
        {
            if (Document == null) return;
            using (var dialog = new SaveFileDialog { Filter = "Author XML|*.xml", FileName = "cloth-range-copy.xml" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ExportRangeCopy(dialog.FileName);
                    status.Text = "Author XML copy saved: " + dialog.FileName +
                        "\r\nSource unchanged. This is not a playable HKX/Binder; pack and validate before game use.";
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export Failed"); }
            }
        }

        void OnEditHkxClick(object sender, EventArgs args)
        {
            if (editingDirty || selectedValueDirty || applyingAuthor)
            {
                status.Text = "Apply or discard pending changes before advanced XML editing.";
                return;
            }
            if (Document == null)
            {
                MessageBox.Show(this, "Load Cloth first.", "Edit HKX");
                return;
            }
            using (var dialog = new HkxXmlEditForm(Document))
            {
                int domain = domains.SelectedIndex;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetDocument(dialog.Result);
                domains.SelectedIndex = Math.Min(domain, Document.Domains.Count - 1);
                status.Text = "Validated HKX edits applied in memory. Source files are unchanged.";
            }
        }

        void OnSaveHkxClick(object sender, EventArgs args)
        {
            if (Document == null)
            {
                MessageBox.Show(this, "Load Cloth first.", "Save HKX");
                return;
            }
            using (var dialog = new SaveFileDialog { Filter = "BB / DS3 Cloth HKX|*.hkx", FileName = "cloth-edited_c.hkx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    UseWaitCursor = true;
                    ExportHkxCopy(dialog.FileName);
                    status.Text = "Native HKX copy saved after schema/readback validation: " + dialog.FileName +
                        "\r\nSource unchanged. No Binder was repacked; gameplay remains untested.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "HKX Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { UseWaitCursor = false; }
            }
        }

        void OnLoadClm2Click(object sender, EventArgs args)
        {
            using (var dialog = new OpenFileDialog { Filter = "CLM2 files|*.clm2;*.clm", Title = "Load CLM2" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { LoadClm2File(dialog.FileName); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "CLM2 Load Failed"); }
            }
        }

        void OnEditClm2Click(object sender, EventArgs args)
        {
            if (Clm2 == null)
            {
                MessageBox.Show(this, "Load CLM2 first.", "Edit CLM2");
                return;
            }
            using (var dialog = new Clm2EditForm(Clm2))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                StopClothPreview();
                Clm2 = dialog.Result;
                RefreshClmLabel();
            }
        }

        void OnSaveClm2Click(object sender, EventArgs args)
        {
            if (Clm2 == null)
            {
                MessageBox.Show(this, "Load CLM2 first.", "Save CLM2");
                return;
            }
            using (var dialog = new SaveFileDialog
            {
                Filter = "CLM2 file|*.clm2", FileName = Path.GetFileNameWithoutExtension(Clm2.Source) + "-edited.clm2"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SaveClm2Copy(dialog.FileName);
                    status.Text = "Saved CLM2 copy: " + dialog.FileName +
                        "\r\nSource unchanged. Verify against the intended FLVER/HKX before game testing.";
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "CLM2 Save Failed"); }
            }
        }

        void OnVerifyClothesClick(object sender, EventArgs args)
        {
            string report;
            try { report = VerifyClothes(); }
            catch (Exception ex) { report = "[FAIL] Verification could not complete: " + ex.Message; }
            RefreshVerificationIndicator(report);
            using (var window = new Form
            {
                Text = "Verify Clothes — Layout and Binding Checks", Size = new Size(950, 650),
                StartPosition = FormStartPosition.CenterParent
            })
            using (var reportTips = new ToolTip { AutoPopDelay = 20000, ShowAlways = true })
            {
                var output = new TextBox
                {
                    Name = "clothesVerification", Text = report, Multiline = true, ReadOnly = true,
                    ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill
                };
                window.Controls.Add(output);
                reportTips.SetToolTip(output,
                    "Static checks only, not gameplay certification. Current FLVER may contain unsaved edits.\n" +
                    "仅静态检查，不代表游戏验证；当前FLVER可能有未保存修改。");
                window.ShowDialog(this);
            }
        }

        void OnNativeLayoutChanged(object sender, EventArgs args)
        {
            if (updatingLayout || Document == null) return;
            StopClothPreview();
            // 先保存旧文档，确保切换包头版本可以撤销；不把它当可靠的游戏 ID。
            undoDocuments.Push(ClothDocument.FromXml(Document.Xml, Document.Source, Document.Edited));
            redoDocuments.Clear();
            Document.SetNativeLayout(nativeLayout.SelectedIndex == 1 ? "8,1,0,1" : "8,1,1,1");
            stagedXml = new XDocument(Document.Xml);
            PopulateEditing();
            ShowDomainSummary();
            RefreshVerificationIndicator();
            Text = "Cloth Inspector — " + Path.GetFileName(Document.Source) + " [in-memory edits]";
        }

        void OnChooseRuntimeClick(object sender, EventArgs args)
        {
            string initial = string.IsNullOrWhiteSpace(previewRuntimePath.Text) ? "" :
                Path.Combine(previewRuntimePath.Text, "tools");
            using (var dialog = new OpenFileDialog
            {
                Title = "Select HCT hctPreviewPlugin.dll",
                Filter = "HCT preview plugin (hctPreviewPlugin.dll)|hctPreviewPlugin.dll|DLL files (*.dll)|*.dll",
                FileName = "hctPreviewPlugin.dll", InitialDirectory = Directory.Exists(initial) ? initial : "",
                AutoUpgradeEnabled = true, CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ConfigurePreviewRuntimeFile(dialog.FileName);
                    ClothPreviewSession.SaveRuntimeSetting(previewRuntimePath.Text);
                }
                catch (Exception ex) { previewState.Text = "Runtime selection failed: " + ex.Message; }
            }
        }
    }
}
