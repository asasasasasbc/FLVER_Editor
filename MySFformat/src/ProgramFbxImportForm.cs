// FbxImportForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MySFformat
{
    public class ProgramFbxImportForm : Form
    {
        public FbxImportSettings Settings { get; private set; }

        // Controls
        private TextBox txtImportPath;
        private Button btnChooseImportFile;
        private CheckBox chkUseBoneConversion;
        private TextBox txtBoneConversionPath;
        private Button btnChooseBoneFile;
        private Button btnBoneHelp;
        private Button btnAxisHelp;
        private CheckBox chkMirrorTertiary;
        private CheckBox chkBlenderTan;
        private CheckBox chkSetTexture;
        private CheckBox chkSetLOD;
        private CheckBox chkImportBones;
        private Button btnOk;
        private Button btnCancel;
        private GroupBox grpPrimaryAxis;
        private GroupBox grpSecondaryAxis;
        private Dictionary<FbxImportSettings.Axis, RadioButton> primaryAxisRadios;
        private Dictionary<FbxImportSettings.Axis, RadioButton> secondaryAxisRadios;

        private string axisHelp = @"This defines how vertex coordinates from the FBX are mapped.
Primary Axis maps to the output X-axis.
Secondary Axis maps to the output Y-axis.
The third axis is calculated automatically.
Recommended for FBX exported directly from Flver Editor: 
Primary: X, Secondary: Y, Mirror Z Axis: Yes
For FBX exported from Blender：
Primary: -Z, Secondary: X, Mirror Z Axis: Yes
If you REALLY want old-FLVER Editor's switch YZ axis values function, try:
Primary: X, Secondary: Z, Mirror Z Axis: Yes
---
定义了FBX中的顶点坐标如何被映射,是以前Flver编辑器导入模型切换YZ轴的升级功能。
主轴（Primary Axis）映射到Flver的X轴。
次轴（Secondary Axis）映射到Flver的Y轴。
第三轴将自动计算。
Flver编辑器默认FBX导出的情况建议如下：
Primary: X, Secondary: Y, Mirror Z Axis: Yes
Blender4.X默认FBX导出的情况建议如下(无Armature骨架的情况)：
Primary: -Z, Secondary: X, Mirror Z Axis: Yes
Blender4.X默认FBX导出的情况建议如下(有来自FLVER Editor导出的FBX Armature骨架的情况)：
Primary: X, Secondary: Y, Mirror Z Axis: Yes
如果你需要旧版的Flver编辑器导入模型切换YZ轴功能，建议配置如下：
Primary: X, Secondary: Z, Mirror Z Axis: Yes
";
        private string boneHelp = @"The bone conversion file (.ini) maps bone names from 
the FBX to bone names in the target FLVER skeleton.
Useful for importing different bone weights from different skeletons.
Format:
FBX_Bone_Name_1
FLVER_Bone_Name_1
FBX_Bone_Name_2
FLVER_Bone_Name_2
---
骨骼转换文件（.ini）将导入FBX中的骨骼名称映射到目标FLVER骨架中的骨骼名称。
从与Flver不同的骨架中导入骨骼权重用。
文件格式:
FBX_Bone_Name_1
FLVER_Bone_Name_1
FBX_Bone_Name_2
FLVER_Bone_Name_2
";
        public ProgramFbxImportForm(FbxImportSettings initialSettings)
        {
            this.Settings = initialSettings;
            InitializeComponent();
            PopulateControlsFromSettings();
        }

        private void InitializeComponent()
        {
            // Form properties
            this.Text = "Import FBX Options";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(500, 545);
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int yPos = 15;
            int xMargin = 15;

            // Import File Path
            var lblImportPath = new Label { Text = "Import file path:", Location = new Point(xMargin, yPos), AutoSize = true };
            this.Controls.Add(lblImportPath);
            yPos += 25;
            txtImportPath = new TextBox { Location = new Point(xMargin, yPos), Size = new Size(380, 20) };
            btnChooseImportFile = new Button { Text = "Choose...", Location = new Point(405, yPos - 2), Size = new Size(80, 24) };
            this.Controls.Add(txtImportPath);
            this.Controls.Add(btnChooseImportFile);
            yPos += 35;

            // Bone Conversion
            chkUseBoneConversion = new CheckBox { Text = "Use Bone convertion file", Location = new Point(xMargin, yPos), AutoSize = true };
            btnBoneHelp = new Button { Text = "?", Location = new Point(180, yPos - 2), Size = new Size(24, 24), Tag = boneHelp };
            this.Controls.Add(chkUseBoneConversion);
            this.Controls.Add(btnBoneHelp);
            yPos += 30;
            txtBoneConversionPath = new TextBox { Location = new Point(xMargin, yPos), Size = new Size(380, 20) };
            btnChooseBoneFile = new Button { Text = "Choose...", Location = new Point(405, yPos - 2), Size = new Size(80, 24) };
            this.Controls.Add(txtBoneConversionPath);
            this.Controls.Add(btnChooseBoneFile);
            yPos += 45;

            // Axis Settings
            var lblAxis = new Label { Text = "Import Axis:", Location = new Point(xMargin, yPos), AutoSize = true };
            btnAxisHelp = new Button { Text = "?", Location = new Point(100, yPos - 2), Size = new Size(24, 24), Tag = axisHelp };
            this.Controls.Add(lblAxis);
            this.Controls.Add(btnAxisHelp);
            yPos += 25;

            // Radio Button Groups for Axis
            grpPrimaryAxis = new GroupBox { Text = "Primary (maps to X)", Location = new Point(xMargin, yPos), Size = new Size(470, 50) };
            grpSecondaryAxis = new GroupBox { Text = "Secondary (maps to Y)", Location = new Point(xMargin, yPos + 60), Size = new Size(470, 50) };
            primaryAxisRadios = CreateAxisRadioButtons(grpPrimaryAxis);
            secondaryAxisRadios = CreateAxisRadioButtons(grpSecondaryAxis);
            this.Controls.Add(grpPrimaryAxis);
            this.Controls.Add(grpSecondaryAxis);
            yPos += 125;
            // ** New Tertiary Mirror Checkbox **
            chkMirrorTertiary = new CheckBox { Text = "Mirror Tertiary (Z) Axis", Location = new Point(xMargin + 10, yPos), AutoSize = true };
            this.Controls.Add(chkMirrorTertiary);
            yPos += 35;

            chkBlenderTan = new CheckBox { Text = "Blender Tangents", Location = new Point(xMargin + 10, yPos), AutoSize = true };
            this.Controls.Add(chkBlenderTan);
            yPos += 35;

            // Other Options
            chkSetTexture = new CheckBox { Text = "Auto set texture paths", Location = new Point(xMargin, yPos), AutoSize = true };
            yPos += 30;
            chkSetLOD = new CheckBox { Text = "Set LOD levels (for viewing far away)", Location = new Point(xMargin, yPos), AutoSize = true };
            yPos += 30;
            chkImportBones = new CheckBox { Text = "Import and Override Bones (Experimental)", Location = new Point(xMargin, yPos), AutoSize = true };
            this.Controls.Add(chkSetTexture);
            this.Controls.Add(chkSetLOD);
            this.Controls.Add(chkImportBones);
            yPos += 45;

            // OK / Cancel Buttons
            btnOk = new Button { Text = "OK", Location = new Point(this.ClientSize.Width - 180, this.ClientSize.Height - 40), Size = new Size(80, 25) };
            btnCancel = new Button { Text = "Cancel", Location = new Point(this.ClientSize.Width - 90, this.ClientSize.Height - 40), Size = new Size(80, 25) };
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            // Event Handlers
            btnChooseImportFile.Click += (s, e) => ChooseFile(txtImportPath, "FBX Files|*.fbx|All Files|*.*");
            chkUseBoneConversion.CheckedChanged += OnUseBoneConversionChanged;
            btnChooseBoneFile.Click += (s, e) => ChooseFile(txtBoneConversionPath, "INI Files|*.ini|All Files|*.*");
            btnOk.Click += OnOkClick;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            btnBoneHelp.Click += ShowHelp;
            btnAxisHelp.Click += ShowHelp;
        }

        private Dictionary<FbxImportSettings.Axis, RadioButton> CreateAxisRadioButtons(GroupBox parent)
        {
            var dict = new Dictionary<FbxImportSettings.Axis, RadioButton>();
            var axes = Enum.GetValues(typeof(FbxImportSettings.Axis)).Cast<FbxImportSettings.Axis>().ToArray();
            int radioX = 10;
            for (int i = 0; i < axes.Length; i++)
            {
                var axis = axes[i];
                string text = axis.ToString().Replace("Neg", "-");
                var radio = new RadioButton { Text = text, Location = new Point(radioX, 20), AutoSize = true, Tag = axis };
                parent.Controls.Add(radio);
                dict[axis] = radio;
                radioX += 75;
            }
            return dict;
        }

        private void PopulateControlsFromSettings()
        {
            txtImportPath.Text = Settings.ImportFilePath;
            chkUseBoneConversion.Checked = Settings.UseBoneConversion;
            txtBoneConversionPath.Text = Settings.BoneConversionFilePath;

            if (primaryAxisRadios.ContainsKey(Settings.PrimaryAxis))
                primaryAxisRadios[Settings.PrimaryAxis].Checked = true;
            if (secondaryAxisRadios.ContainsKey(Settings.SecondaryAxis))
                secondaryAxisRadios[Settings.SecondaryAxis].Checked = true;
            chkMirrorTertiary.Checked = Settings.MirrorTertiaryAxis; // Populate new checkbox
            chkBlenderTan.Checked = Settings.blenderTan;

            chkSetTexture.Checked = Settings.SetTexture;
            chkSetLOD.Checked = Settings.SetLOD;
            chkImportBones.Checked = Settings.ImportAndOverrideBones;

            // Manually trigger to set initial enabled state
            OnUseBoneConversionChanged(null, null);
        }

        private void OnUseBoneConversionChanged(object sender, EventArgs e)
        {
            txtBoneConversionPath.Enabled = chkUseBoneConversion.Checked;
            btnChooseBoneFile.Enabled = chkUseBoneConversion.Checked;
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            // Update settings object from controls
            Settings.ImportFilePath = txtImportPath.Text;
            Settings.UseBoneConversion = chkUseBoneConversion.Checked;
            Settings.BoneConversionFilePath = txtBoneConversionPath.Text;

            Settings.PrimaryAxis = primaryAxisRadios.First(kvp => kvp.Value.Checked).Key;
            Settings.SecondaryAxis = secondaryAxisRadios.First(kvp => kvp.Value.Checked).Key;
            Settings.MirrorTertiaryAxis = chkMirrorTertiary.Checked; // Save new checkbox state
            Settings.blenderTan = chkBlenderTan.Checked;

            Settings.SetTexture = chkSetTexture.Checked;
            Settings.SetLOD = chkSetLOD.Checked;
            Settings.ImportAndOverrideBones = chkImportBones.Checked;

            this.DialogResult = DialogResult.OK;
        }

        private void ChooseFile(TextBox targetTextBox, string filter)
        {
            using (var ofd = new OpenFileDialog { Filter = filter })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    targetTextBox.Text = ofd.FileName;
                }
            }
        }

        private void ShowHelp(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is string helpText)
            {
                MessageBox.Show(helpText, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}