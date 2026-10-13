using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MySFformat
{
    public sealed partial class ClothInspectorForm
    {
        // 选区属性常驻在右侧，但没有选中粒子时隐藏；不再弹出 Modify 对话框。
        readonly GroupBox selectedParticleEditor = new GroupBox
        {
            Name = "selectedParticleEditor", Text = "Selected Particles",
            Dock = DockStyle.Bottom, AutoSize = true, Visible = false
        };
        readonly ComboBox selectedParticleField = new ComboBox
        {
            Name = "selectedParticleField", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150
        };
        readonly TextBox selectedParticleValue = new TextBox { Name = "selectedParticleValue", Width = 90 };
        readonly Label selectedParticleInfo = new Label { Name = "selectedParticleInfo", AutoSize = true, MaximumSize = new Size(400, 0) };
        readonly Button particleEyedropper = new Button { Name = "sampleParticleValue", Text = "Pick Value", AutoSize = true };
        readonly LinkLabel verificationIndicator = new LinkLabel
        {
            Name = "clothVerificationStatus", Text = "? Verify", AutoSize = true,
            Padding = new Padding(3, 7, 3, 0), LinkColor = Color.Gray
        };
        bool selectedValueUpdating, selectedValueDirty;
        int[] inlineParticleTargets = new int[0];

        void InitializeSelectionEditor(SplitContainer split)
        {
            selectedParticleField.Items.AddRange(new object[] { "maximumDistance", "mass", "radius", "friction" });
            selectedParticleField.SelectedIndex = 0;
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            row.Controls.AddRange(new Control[] { selectedParticleField, selectedParticleValue, particleEyedropper });
            row.Controls.Add(selectedParticleInfo);
            row.SetFlowBreak(particleEyedropper, true);
            selectedParticleEditor.Controls.Add(row);
            split.Panel2.Controls.Add(selectedParticleEditor);
            split.Panel2.Controls.SetChildIndex(selectedParticleEditor,1);
            // 保留批量倍率操作，但移到选区面板的右键菜单，不再占常驻按钮位置。
            var advanced = new ContextMenuStrip();
            advanced.Items.Add("Advanced Set / Multiply…", null, (s, e) => ShowModifySelection());
            selectedParticleEditor.ContextMenuStrip = advanced;
            Disposed += (s, e) => advanced.Dispose();
            tips.SetToolTip(selectedParticleEditor,
                "Edit values and Apply Changes. Right-click this panel for advanced set/multiply operations.\n" +
                "直接改值并Apply Changes；右键此面板可进行高级设置/倍率操作。");

            tips.SetToolTip(selectedParticleValue,
                "Mixed means selected particles have different values. Enter one value, then Apply Changes to set it for the selection.\n" +
                "Mixed表示选区值不一致。输入统一值后点Apply Changes应用到选区；不需要弹窗Modify。");
            tips.SetToolTip(particleEyedropper,
                "Pick this field from a particle in the viewport without changing the target selection. Apply Changes commits it; Esc cancels picking.\n" +
                "在视图点击来源粒子，读取当前字段但保留目标选区；Apply Changes提交，Esc取消吸管。");
            selectedParticleField.SelectedIndexChanged += (s, e) =>
            {
                canvas.PickValueRequested = null;
                particleEyedropper.Text = "Pick Value";
                RefreshSelectionEditor(true);
            };
            selectedParticleValue.TextChanged += (s, e) =>
            {
                if (selectedValueUpdating) return;
                selectedValueDirty = true;
                UpdateEditState();
            };
            selectedParticleValue.Validating += (s, e) =>
            {
                if (!selectedValueDirty || selectedValueUpdating) return;
                try
                {
                    string field = (string)selectedParticleField.SelectedItem;
                    double value = PositiveNumber(selectedParticleValue.Text, field != "mass");
                    // 选区在输入尚未提交时被锁定，不能误写到后来点击的另一组选区。
                    StageSelectedParticleValues(field, value);
                    selectedValueDirty = false;
                    UpdateEditState();
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    selectedParticleInfo.Text = ex.Message;
                }
            };
            particleEyedropper.Click += (s, e) =>
            {
                if (canvas.PickValueRequested != null)
                {
                    CancelParticleEyedropper();
                    return;
                }
                particleEyedropper.Text = "Cancel Pick";
                canvas.PickValueRequested = SampleParticleValue;
                selectedParticleInfo.Text = "Click a source particle · target selection retained";
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && canvas.PickValueRequested != null)
                {
                    CancelParticleEyedropper();
                    e.Handled = true;
                }
            };
        }

        double ReadParticleField(int id, string field)
        {
            var domain = Document.Domains[editingDomain];
            if (field == "maximumDistance")
                return RangeRows(domain, id)
                    .Select(row => double.Parse(ClothDocument.Value(row, field), EditCulture))
                    .DefaultIfEmpty(double.NaN).Max();
            var particle = ClothDocument.Field(StagedObject(domain.Sim), "particleDatas").Elements().ElementAt(id);
            return double.Parse(ClothDocument.Value(particle, field), EditCulture);
        }

        void RefreshSelectionEditor(bool force = false)
        {
            selectedParticleEditor.Visible = Document != null && canvas.SelectedParticles.Count > 0;
            if (!selectedParticleEditor.Visible || stagedXml == null || editingDomain < 0) return;
            if (selectedValueDirty && !force) return;
            inlineParticleTargets = SelectedParticleIds;
            string field = (string)selectedParticleField.SelectedItem;
            var values = inlineParticleTargets.Select(id => ReadParticleField(id, field)).ToArray();
            bool missing = values.Any(double.IsNaN);
            bool mixed = missing || values.Any(v => v != values[0]);
            bool fixedMass = field == "mass" && inlineParticleTargets.Any(id => Document.Domains[editingDomain].Fixed[id]);
            selectedParticleValue.ReadOnly = missing || fixedMass;
            particleEyedropper.Enabled = !missing && !fixedMass;
            selectedValueUpdating = true;
            try
            {
                selectedParticleValue.Text = mixed ? "Mixed" : values[0].ToString("R", CultureInfo.InvariantCulture);
                selectedValueDirty = false;
                selectedParticleInfo.Text = inlineParticleTargets.Length + " particles · " +
                    (fixedMass ? "Fixed particle mass is read-only; choose dynamic particles" : missing ? "Some particles have no LocalRange record" :
                    mixed ? string.Format(CultureInfo.InvariantCulture, "Mixed: {0:G6} – {1:G6}", values.Min(), values.Max()) : "Same value") +
                    " · edit value → Apply Changes";
            }
            finally { selectedValueUpdating = false; }
        }

        // 吸管只读取属性。它不替换选区，也不立即改作者文档或运行中的物理状态。
        public void SampleParticleValue(int id)
        {
            string field = (string)selectedParticleField.SelectedItem;
            double value = ReadParticleField(id, field);
            if (double.IsNaN(value) || (field == "mass" && value <= 0))
            {
                selectedParticleInfo.Text = double.IsNaN(value) ?
                    "Source has no LocalRange record; choose another particle" :
                    "Source mass is not positive; choose a dynamic source particle";
                return;
            }
            canvas.PickValueRequested = null;
            particleEyedropper.Text = "Pick Value";
            selectedParticleValue.Text = value.ToString("R", CultureInfo.InvariantCulture);
            selectedParticleInfo.Text = "Value sampled from particle " + id + " · Apply Changes to commit";
        }

        void CancelParticleEyedropper()
        {
            canvas.PickValueRequested = null;
            particleEyedropper.Text = "Pick Value";
            if (!selectedValueDirty) RefreshSelectionEditor();
        }

        void ResetSelectionEditor()
        {
            selectedValueDirty = false;
            inlineParticleTargets = new int[0];
            selectedParticleEditor.Visible = false;
            CancelParticleEyedropper();
        }

        // 状态图标只表示静态数据校验；不能用绿色勾声称游戏物理已通过。
        void RefreshVerificationIndicator(string report = null)
        {
            if (Document == null)
            {
                verificationIndicator.Text = "? Verify";
                verificationIndicator.LinkColor = Color.Gray;
                return;
            }
            try { report = report ?? VerifyClothes(); }
            catch (Exception ex) { report = "[FAIL] " + ex.Message; }
            bool failed = report.Contains("[FAIL]");
            bool incomplete = report.Contains("[WARN]") || report.Contains("[SKIP]");
            verificationIndicator.Text = failed ? "✗ Verify" : incomplete ? "⚠ Verify" : "✓ Verify";
            verificationIndicator.LinkColor = failed ? Color.Firebrick : incomplete ? Color.DarkGoldenrod : Color.ForestGreen;
            tips.SetToolTip(verificationIndicator,
                "Static checks only; click for details. Green: passed; amber: warnings/skipped; red: failures.\n" +
                "仅静态校验，点图标查看详情；绿勾通过，黄色警告/未检查，红叉失败，不代表游戏验证。\n" +
                report.Substring(0, Math.Min(1200, report.Length)));
        }
    }
}
