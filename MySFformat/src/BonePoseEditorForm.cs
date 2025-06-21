// BonePoseEditorForm.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics; // For System.Numerics.Vector3
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsFormats; // For FLVER.Node

namespace MySFformat
{
    public partial class BonePoseEditorForm : Form
    {
        private List<FLVER.Node> _flverNodes; // Reference to original FLVER nodes for names and reset
        private List<FLVER.Node> _poseNodesRef;  // Reference to Program.poseNodes
        private int _selectedBoneIndex = -1;

        public Action OnPoseNeedsUpdate; // Delegate to trigger 3D view refresh

        public BonePoseEditorForm(List<FLVER.Node> flverNodes, List<FLVER.Node> poseNodes)
        {
            InitializeComponent();
            _flverNodes = flverNodes;
            _poseNodesRef = poseNodes; // Store the reference

            PopulateBoneList();
        }

        private void PopulateBoneList()
        {
            lstBones.Items.Clear();
            if (_flverNodes != null)
            {
                for (int i = 0; i < _flverNodes.Count; i++)
                {
                    lstBones.Items.Add($"[{i}] {_flverNodes[i].Name}");
                }
            }
        }

        private void lstBones_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstBones.SelectedIndex >= 0 && lstBones.SelectedIndex < _poseNodesRef.Count)
            {
                _selectedBoneIndex = lstBones.SelectedIndex;
                Program.checkingBoneIndex = _selectedBoneIndex; // For 3D view highlight
                LoadBoneData(_selectedBoneIndex);
                Program.updateVertices();
            }
            else
            {
                _selectedBoneIndex = -1;
                Program.checkingBoneIndex = -1;
                Program.updateVertices();
                ClearInputFields();
            }
        }

        private void LoadBoneData(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _poseNodesRef.Count) return;

            var bonePose = _poseNodesRef[boneIndex];

            // Translation
            txtPosX.Text = bonePose.Translation.X.ToString("F6");
            txtPosY.Text = bonePose.Translation.Y.ToString("F6");
            txtPosZ.Text = bonePose.Translation.Z.ToString("F6");

            // Rotation (convert radians to degrees for display)
            txtRotX.Text = RadToDeg(bonePose.Rotation.X).ToString("F3");
            txtRotY.Text = RadToDeg(bonePose.Rotation.Y).ToString("F3");
            txtRotZ.Text = RadToDeg(bonePose.Rotation.Z).ToString("F3");

            // Scale (usually not modified for pose, but good to show)
            txtScaleX.Text = bonePose.Scale.X.ToString("F6");
            txtScaleY.Text = bonePose.Scale.Y.ToString("F6");
            txtScaleZ.Text = bonePose.Scale.Z.ToString("F6");
        }

        private void ClearInputFields()
        {
            txtPosX.Text = ""; txtPosY.Text = ""; txtPosZ.Text = "";
            txtRotX.Text = ""; txtRotY.Text = ""; txtRotZ.Text = "";
            txtScaleX.Text = ""; txtScaleY.Text = ""; txtScaleZ.Text = "";
        }

        private float DegToRad(float degrees) => degrees * ((float)Math.PI / 180.0f);
        private float RadToDeg(float radians) => radians * (180.0f / (float)Math.PI);

        private void btnApplyChanges_Click(object sender, EventArgs e)
        {
            if (_selectedBoneIndex < 0 || _selectedBoneIndex >= _poseNodesRef.Count)
            {
                MessageBox.Show("Please select a bone first.", "No Bone Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var bonePose = _poseNodesRef[_selectedBoneIndex];

                bonePose.Translation = new System.Numerics.Vector3(
                    float.Parse(txtPosX.Text),
                    float.Parse(txtPosY.Text),
                    float.Parse(txtPosZ.Text)
                );

                bonePose.Rotation = new System.Numerics.Vector3(
                    DegToRad(float.Parse(txtRotX.Text)),
                    DegToRad(float.Parse(txtRotY.Text)),
                    DegToRad(float.Parse(txtRotZ.Text))
                );

                // If scale editing is enabled:
                // bonePose.Scale = new System.Numerics.Vector3(
                //     float.Parse(txtScaleX.Text),
                //     float.Parse(txtScaleY.Text),
                //     float.Parse(txtScaleZ.Text)
                // );

                Program.updateVertices();
            }
            catch (FormatException ex)
            {
                MessageBox.Show("Invalid input format. Please enter valid numbers.\n" + ex.Message, "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnResetSelectedBone_Click(object sender, EventArgs e)
        {
            if (_selectedBoneIndex < 0 || _selectedBoneIndex >= _poseNodesRef.Count || _selectedBoneIndex >= _flverNodes.Count)
            {
                MessageBox.Show("Please select a bone first.", "No Bone Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var originalBone = _flverNodes[_selectedBoneIndex];
            var poseBoneToReset = _poseNodesRef[_selectedBoneIndex];

            poseBoneToReset.Translation = originalBone.Translation;
            poseBoneToReset.Rotation = originalBone.Rotation;
            poseBoneToReset.Scale = originalBone.Scale;
            // ParentIndex, ChildIndices etc., are structural and should not change for a pose edit.
            // They are copied initially when poseNodes is created.

            LoadBoneData(_selectedBoneIndex);
            Program.updateVertices();
        }

        private void btnResetAllPoses_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show("Are you sure you want to reset all bone poses to their original FLVER state?",
                                                   "Confirm Reset All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.Yes)
            {
                Program.resetPoses(); // This re-initializes Program.poseNodes
                                      // _poseNodesRef should now point to the newly reset list if Program.poseNodes was handled correctly

                if (_selectedBoneIndex != -1 && _selectedBoneIndex < _poseNodesRef.Count) // Check bounds after reset
                {
                    LoadBoneData(_selectedBoneIndex);
                }
                else
                {
                    _selectedBoneIndex = -1; // Deselect if index is now invalid
                    Program.checkingBoneIndex = -1;
                    ClearInputFields();
                }
                Program.updateVertices();
            }
        }

        // --- Add InitializeComponent() in BonePoseEditorForm.Designer.cs ---
        // For now, I'll put a simplified manual one here.
        // You'll need: ListBox (lstBones), GroupBox (grpBoneData),
        // Labels and TextBoxes for PosX/Y/Z, RotX/Y/Z, ScaleX/Y/Z,
        // Buttons (btnApplyChanges, btnResetSelectedBone, btnResetAllPoses)

        private System.Windows.Forms.ListBox lstBones;
        private System.Windows.Forms.GroupBox grpBoneData;
        private System.Windows.Forms.TextBox txtPosZ;
        private System.Windows.Forms.Label lblPosZ;
        private System.Windows.Forms.TextBox txtPosY;
        private System.Windows.Forms.Label lblPosY;
        private System.Windows.Forms.TextBox txtPosX;
        private System.Windows.Forms.Label lblPosX;
        private System.Windows.Forms.Label lblTranslation;
        private System.Windows.Forms.TextBox txtRotZ;
        private System.Windows.Forms.Label lblRotZ;
        private System.Windows.Forms.TextBox txtRotY;
        private System.Windows.Forms.Label lblRotY;
        private System.Windows.Forms.TextBox txtRotX;
        private System.Windows.Forms.Label lblRotX;
        private System.Windows.Forms.Label lblRotation;
        private System.Windows.Forms.Label lblScale;
        private System.Windows.Forms.TextBox txtScaleX;
        private System.Windows.Forms.Label lblScaleX;
        private System.Windows.Forms.TextBox txtScaleY;
        private System.Windows.Forms.Label lblScaleY;
        private System.Windows.Forms.TextBox txtScaleZ;
        private System.Windows.Forms.Label lblScaleZ;
        private System.Windows.Forms.Button btnApplyChanges;
        private System.Windows.Forms.Button btnResetSelectedBone;
        private System.Windows.Forms.Button btnResetAllPoses;

        private void InitializeComponent()
        {
            this.lstBones = new System.Windows.Forms.ListBox();
            this.grpBoneData = new System.Windows.Forms.GroupBox();
            this.lblTranslation = new System.Windows.Forms.Label();
            this.lblPosX = new System.Windows.Forms.Label();
            this.txtPosX = new System.Windows.Forms.TextBox();
            this.lblPosY = new System.Windows.Forms.Label();
            this.txtPosY = new System.Windows.Forms.TextBox();
            this.lblPosZ = new System.Windows.Forms.Label();
            this.txtPosZ = new System.Windows.Forms.TextBox();
            this.lblRotation = new System.Windows.Forms.Label();
            this.lblRotX = new System.Windows.Forms.Label();
            this.txtRotX = new System.Windows.Forms.TextBox();
            this.lblRotY = new System.Windows.Forms.Label();
            this.txtRotY = new System.Windows.Forms.TextBox();
            this.lblRotZ = new System.Windows.Forms.Label();
            this.txtRotZ = new System.Windows.Forms.TextBox();
            this.lblScale = new System.Windows.Forms.Label();
            this.lblScaleX = new System.Windows.Forms.Label();
            this.txtScaleX = new System.Windows.Forms.TextBox();
            this.lblScaleY = new System.Windows.Forms.Label();
            this.txtScaleY = new System.Windows.Forms.TextBox();
            this.lblScaleZ = new System.Windows.Forms.Label();
            this.txtScaleZ = new System.Windows.Forms.TextBox();
            this.btnApplyChanges = new System.Windows.Forms.Button();
            this.btnResetSelectedBone = new System.Windows.Forms.Button();
            this.btnResetAllPoses = new System.Windows.Forms.Button();
            this.grpBoneData.SuspendLayout();
            this.SuspendLayout();
            // 
            // lstBones
            // 
            this.lstBones.FormattingEnabled = true;
            this.lstBones.Location = new System.Drawing.Point(12, 12);
            this.lstBones.Name = "lstBones";
            this.lstBones.Size = new System.Drawing.Size(200, 329);
            this.lstBones.TabIndex = 0;
            this.lstBones.SelectedIndexChanged += new System.EventHandler(this.lstBones_SelectedIndexChanged);
            // 
            // grpBoneData
            // 
            this.grpBoneData.Controls.Add(this.txtScaleZ);
            this.grpBoneData.Controls.Add(this.lblScaleZ);
            this.grpBoneData.Controls.Add(this.txtScaleY);
            this.grpBoneData.Controls.Add(this.lblScaleY);
            this.grpBoneData.Controls.Add(this.txtScaleX);
            this.grpBoneData.Controls.Add(this.lblScaleX);
            this.grpBoneData.Controls.Add(this.lblScale);
            this.grpBoneData.Controls.Add(this.txtRotZ);
            this.grpBoneData.Controls.Add(this.lblRotZ);
            this.grpBoneData.Controls.Add(this.txtRotY);
            this.grpBoneData.Controls.Add(this.lblRotY);
            this.grpBoneData.Controls.Add(this.txtRotX);
            this.grpBoneData.Controls.Add(this.lblRotX);
            this.grpBoneData.Controls.Add(this.lblRotation);
            this.grpBoneData.Controls.Add(this.txtPosZ);
            this.grpBoneData.Controls.Add(this.lblPosZ);
            this.grpBoneData.Controls.Add(this.txtPosY);
            this.grpBoneData.Controls.Add(this.lblPosY);
            this.grpBoneData.Controls.Add(this.txtPosX);
            this.grpBoneData.Controls.Add(this.lblPosX);
            this.grpBoneData.Controls.Add(this.lblTranslation);
            this.grpBoneData.Location = new System.Drawing.Point(220, 12);
            this.grpBoneData.Name = "grpBoneData";
            this.grpBoneData.Size = new System.Drawing.Size(250, 270);
            this.grpBoneData.TabIndex = 1;
            this.grpBoneData.TabStop = false;
            this.grpBoneData.Text = "Selected Bone Pose";
            // 
            // lblTranslation
            // 
            this.lblTranslation.AutoSize = true;
            this.lblTranslation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTranslation.Location = new System.Drawing.Point(6, 25);
            this.lblTranslation.Name = "lblTranslation";
            this.lblTranslation.Size = new System.Drawing.Size(74, 13);
            this.lblTranslation.TabIndex = 0;
            this.lblTranslation.Text = "Translation:";
            // 
            // lblPosX
            // 
            this.lblPosX.AutoSize = true;
            this.lblPosX.Location = new System.Drawing.Point(23, 48);
            this.lblPosX.Name = "lblPosX";
            this.lblPosX.Size = new System.Drawing.Size(17, 13);
            this.lblPosX.TabIndex = 1;
            this.lblPosX.Text = "X:";
            // 
            // txtPosX
            // 
            this.txtPosX.Location = new System.Drawing.Point(46, 45);
            this.txtPosX.Name = "txtPosX";
            this.txtPosX.Size = new System.Drawing.Size(70, 20);
            this.txtPosX.TabIndex = 2;
            // 
            // lblPosY
            // 
            this.lblPosY.AutoSize = true;
            this.lblPosY.Location = new System.Drawing.Point(130, 48);
            this.lblPosY.Name = "lblPosY";
            this.lblPosY.Size = new System.Drawing.Size(17, 13);
            this.lblPosY.TabIndex = 3;
            this.lblPosY.Text = "Y:";
            // 
            // txtPosY
            // 
            this.txtPosY.Location = new System.Drawing.Point(153, 45);
            this.txtPosY.Name = "txtPosY";
            this.txtPosY.Size = new System.Drawing.Size(70, 20);
            this.txtPosY.TabIndex = 4;
            // 
            // lblPosZ
            // 
            this.lblPosZ.AutoSize = true;
            this.lblPosZ.Location = new System.Drawing.Point(23, 74);
            this.lblPosZ.Name = "lblPosZ";
            this.lblPosZ.Size = new System.Drawing.Size(17, 13);
            this.lblPosZ.TabIndex = 5;
            this.lblPosZ.Text = "Z:";
            // 
            // txtPosZ
            // 
            this.txtPosZ.Location = new System.Drawing.Point(46, 71);
            this.txtPosZ.Name = "txtPosZ";
            this.txtPosZ.Size = new System.Drawing.Size(70, 20);
            this.txtPosZ.TabIndex = 6;
            // 
            // lblRotation
            // 
            this.lblRotation.AutoSize = true;
            this.lblRotation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblRotation.Location = new System.Drawing.Point(6, 105);
            this.lblRotation.Name = "lblRotation";
            this.lblRotation.Size = new System.Drawing.Size(95, 13);
            this.lblRotation.TabIndex = 7;
            this.lblRotation.Text = "Rotation (Deg):";
            // 
            // lblRotX
            // 
            this.lblRotX.AutoSize = true;
            this.lblRotX.Location = new System.Drawing.Point(23, 128);
            this.lblRotX.Name = "lblRotX";
            this.lblRotX.Size = new System.Drawing.Size(17, 13);
            this.lblRotX.TabIndex = 8;
            this.lblRotX.Text = "X:";
            // 
            // txtRotX
            // 
            this.txtRotX.Location = new System.Drawing.Point(46, 125);
            this.txtRotX.Name = "txtRotX";
            this.txtRotX.Size = new System.Drawing.Size(70, 20);
            this.txtRotX.TabIndex = 9;
            // 
            // lblRotY
            // 
            this.lblRotY.AutoSize = true;
            this.lblRotY.Location = new System.Drawing.Point(130, 128);
            this.lblRotY.Name = "lblRotY";
            this.lblRotY.Size = new System.Drawing.Size(17, 13);
            this.lblRotY.TabIndex = 10;
            this.lblRotY.Text = "Y:";
            // 
            // txtRotY
            // 
            this.txtRotY.Location = new System.Drawing.Point(153, 125);
            this.txtRotY.Name = "txtRotY";
            this.txtRotY.Size = new System.Drawing.Size(70, 20);
            this.txtRotY.TabIndex = 11;
            // 
            // lblRotZ
            // 
            this.lblRotZ.AutoSize = true;
            this.lblRotZ.Location = new System.Drawing.Point(23, 154);
            this.lblRotZ.Name = "lblRotZ";
            this.lblRotZ.Size = new System.Drawing.Size(17, 13);
            this.lblRotZ.TabIndex = 12;
            this.lblRotZ.Text = "Z:";
            // 
            // txtRotZ
            // 
            this.txtRotZ.Location = new System.Drawing.Point(46, 151);
            this.txtRotZ.Name = "txtRotZ";
            this.txtRotZ.Size = new System.Drawing.Size(70, 20);
            this.txtRotZ.TabIndex = 13;
            // 
            // lblScale
            // 
            this.lblScale.AutoSize = true;
            this.lblScale.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblScale.Location = new System.Drawing.Point(6, 185);
            this.lblScale.Name = "lblScale";
            this.lblScale.Size = new System.Drawing.Size(43, 13);
            this.lblScale.TabIndex = 14;
            this.lblScale.Text = "Scale:";
            // 
            // lblScaleX
            // 
            this.lblScaleX.AutoSize = true;
            this.lblScaleX.Location = new System.Drawing.Point(23, 208);
            this.lblScaleX.Name = "lblScaleX";
            this.lblScaleX.Size = new System.Drawing.Size(17, 13);
            this.lblScaleX.TabIndex = 15;
            this.lblScaleX.Text = "X:";
            // 
            // txtScaleX
            // 
            this.txtScaleX.Location = new System.Drawing.Point(46, 205);
            this.txtScaleX.Name = "txtScaleX";
            this.txtScaleX.ReadOnly = true;
            this.txtScaleX.Size = new System.Drawing.Size(70, 20);
            this.txtScaleX.TabIndex = 16;
            // 
            // lblScaleY
            // 
            this.lblScaleY.AutoSize = true;
            this.lblScaleY.Location = new System.Drawing.Point(130, 208);
            this.lblScaleY.Name = "lblScaleY";
            this.lblScaleY.Size = new System.Drawing.Size(17, 13);
            this.lblScaleY.TabIndex = 17;
            this.lblScaleY.Text = "Y:";
            // 
            // txtScaleY
            // 
            this.txtScaleY.Location = new System.Drawing.Point(153, 205);
            this.txtScaleY.Name = "txtScaleY";
            this.txtScaleY.ReadOnly = true;
            this.txtScaleY.Size = new System.Drawing.Size(70, 20);
            this.txtScaleY.TabIndex = 18;
            // 
            // lblScaleZ
            // 
            this.lblScaleZ.AutoSize = true;
            this.lblScaleZ.Location = new System.Drawing.Point(23, 234);
            this.lblScaleZ.Name = "lblScaleZ";
            this.lblScaleZ.Size = new System.Drawing.Size(17, 13);
            this.lblScaleZ.TabIndex = 19;
            this.lblScaleZ.Text = "Z:";
            // 
            // txtScaleZ
            // 
            this.txtScaleZ.Location = new System.Drawing.Point(46, 231);
            this.txtScaleZ.Name = "txtScaleZ";
            this.txtScaleZ.ReadOnly = true;
            this.txtScaleZ.Size = new System.Drawing.Size(70, 20);
            this.txtScaleZ.TabIndex = 20;
            // 
            // btnApplyChanges
            // 
            this.btnApplyChanges.Location = new System.Drawing.Point(220, 288);
            this.btnApplyChanges.Name = "btnApplyChanges";
            this.btnApplyChanges.Size = new System.Drawing.Size(110, 23);
            this.btnApplyChanges.TabIndex = 2;
            this.btnApplyChanges.Text = "Apply Changes";
            this.btnApplyChanges.UseVisualStyleBackColor = true;
            this.btnApplyChanges.Click += new System.EventHandler(this.btnApplyChanges_Click);
            // 
            // btnResetSelectedBone
            // 
            this.btnResetSelectedBone.Location = new System.Drawing.Point(336, 288);
            this.btnResetSelectedBone.Name = "btnResetSelectedBone";
            this.btnResetSelectedBone.Size = new System.Drawing.Size(134, 23);
            this.btnResetSelectedBone.TabIndex = 3;
            this.btnResetSelectedBone.Text = "Reset Selected Bone";
            this.btnResetSelectedBone.UseVisualStyleBackColor = true;
            this.btnResetSelectedBone.Click += new System.EventHandler(this.btnResetSelectedBone_Click);
            // 
            // btnResetAllPoses
            // 
            this.btnResetAllPoses.Location = new System.Drawing.Point(220, 317);
            this.btnResetAllPoses.Name = "btnResetAllPoses";
            this.btnResetAllPoses.Size = new System.Drawing.Size(250, 23);
            this.btnResetAllPoses.TabIndex = 4;
            this.btnResetAllPoses.Text = "Reset All Poses";
            this.btnResetAllPoses.UseVisualStyleBackColor = true;
            this.btnResetAllPoses.Click += new System.EventHandler(this.btnResetAllPoses_Click);
            // 
            // BonePoseEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 353);
            this.Controls.Add(this.btnResetAllPoses);
            this.Controls.Add(this.btnResetSelectedBone);
            this.Controls.Add(this.btnApplyChanges);
            this.Controls.Add(this.grpBoneData);
            this.Controls.Add(this.lstBones);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BonePoseEditorForm";
            this.Text = "Bone Pose Editor";
            this.grpBoneData.ResumeLayout(false);
            this.grpBoneData.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}