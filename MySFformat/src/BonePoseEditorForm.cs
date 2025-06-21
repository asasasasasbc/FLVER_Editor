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
        private List<FLVER.Node> _flverNodes;
        private List<FLVER.Node> _poseNodesRef;
        private int _selectedBoneIndex = -1;

        public Action OnPoseNeedsUpdate;

        private System.Windows.Forms.Timer _updateTimer;
        private bool _needsVisualUpdate = false;
        private bool _isUpdatingControlsProgrammatically = false; // Prevents event feedback

        // --- Constants for TrackBar scaling ---
        private const int SLIDER_RESOLUTION = 1000; // e.g., 1000 steps
        private const float TRANSLATION_RANGE = 2.0f; // Sliders will go from -2.0 to +2.0
        private const float ROTATION_RANGE_DEGREES = 180.0f; // Sliders will go from -180 to +180 degrees

        public BonePoseEditorForm(List<FLVER.Node> flverNodes, List<FLVER.Node> poseNodes)
        {
            InitializeComponent(); // This will now include TrackBars
            _flverNodes = flverNodes;
            _poseNodesRef = poseNodes;

            SetupTrackBars();
            PopulateBoneList();

            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 100; // 100 ms = 0.1 seconds
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void SetupTrackBars()
        {
            // Translation TrackBars
            SetupTrackBar(trkTransX, -TRANSLATION_RANGE, TRANSLATION_RANGE, HandleTranslationSliderScroll);
            SetupTrackBar(trkTransY, -TRANSLATION_RANGE, TRANSLATION_RANGE, HandleTranslationSliderScroll);
            SetupTrackBar(trkTransZ, -TRANSLATION_RANGE, TRANSLATION_RANGE, HandleTranslationSliderScroll);

            // Rotation TrackBars (in Degrees)
            SetupTrackBar(trkRotX, -ROTATION_RANGE_DEGREES, ROTATION_RANGE_DEGREES, HandleRotationSliderScroll);
            SetupTrackBar(trkRotY, -ROTATION_RANGE_DEGREES, ROTATION_RANGE_DEGREES, HandleRotationSliderScroll);
            SetupTrackBar(trkRotZ, -ROTATION_RANGE_DEGREES, ROTATION_RANGE_DEGREES, HandleRotationSliderScroll);
        }

        private void SetupTrackBar(TrackBar trackBar, float minVal, float maxVal, EventHandler onScroll)
        {
            trackBar.Minimum = (int)(minVal * SLIDER_RESOLUTION);
            trackBar.Maximum = (int)(maxVal * SLIDER_RESOLUTION);
            trackBar.TickFrequency = (trackBar.Maximum - trackBar.Minimum) / 20; // 20 ticks
            trackBar.SmallChange = SLIDER_RESOLUTION / 100; // Small step
            trackBar.LargeChange = SLIDER_RESOLUTION / 10;   // Large step
            trackBar.Scroll += onScroll;
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
                Program.checkingBoneIndex = _selectedBoneIndex;
                LoadBoneDataToUI(_selectedBoneIndex);
                RequestVisualUpdate(); // Request update as selection might change highlight
            }
            else
            {
                _selectedBoneIndex = -1;
                Program.checkingBoneIndex = -1;
                ClearInputFieldsAndSliders();
                RequestVisualUpdate();
            }
        }

        private void LoadBoneDataToUI(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _poseNodesRef.Count) return;

            _isUpdatingControlsProgrammatically = true; // Prevent slider events from firing

            var bonePose = _poseNodesRef[boneIndex];

            // Translation
            txtPosX.Text = bonePose.Translation.X.ToString("F6");
            txtPosY.Text = bonePose.Translation.Y.ToString("F6");
            txtPosZ.Text = bonePose.Translation.Z.ToString("F6");
            SetSliderValue(trkTransX, bonePose.Translation.X, TRANSLATION_RANGE);
            SetSliderValue(trkTransY, bonePose.Translation.Y, TRANSLATION_RANGE);
            SetSliderValue(trkTransZ, bonePose.Translation.Z, TRANSLATION_RANGE);


            // Rotation (convert radians to degrees for display and sliders)
            float rotXDeg = RadToDeg(bonePose.Rotation.X);
            float rotYDeg = RadToDeg(bonePose.Rotation.Y);
            float rotZDeg = RadToDeg(bonePose.Rotation.Z);
            txtRotX.Text = rotXDeg.ToString("F3");
            txtRotY.Text = rotYDeg.ToString("F3");
            txtRotZ.Text = rotZDeg.ToString("F3");
            SetSliderValue(trkRotX, rotXDeg, ROTATION_RANGE_DEGREES);
            SetSliderValue(trkRotY, rotYDeg, ROTATION_RANGE_DEGREES);
            SetSliderValue(trkRotZ, rotZDeg, ROTATION_RANGE_DEGREES);

            // Scale
            txtScaleX.Text = bonePose.Scale.X.ToString("F6");
            txtScaleY.Text = bonePose.Scale.Y.ToString("F6");
            txtScaleZ.Text = bonePose.Scale.Z.ToString("F6");

            _isUpdatingControlsProgrammatically = false;
        }

        private void SetSliderValue(TrackBar slider, float value, float range)
        {
            int sliderVal = (int)(Math.Max(-range, Math.Min(range, value)) * SLIDER_RESOLUTION);
            slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, sliderVal));
        }

        private float GetSliderValue(TrackBar slider)
        {
            return (float)slider.Value / SLIDER_RESOLUTION;
        }

        private void ClearInputFieldsAndSliders()
        {
            _isUpdatingControlsProgrammatically = true;
            txtPosX.Text = ""; txtPosY.Text = ""; txtPosZ.Text = "";
            txtRotX.Text = ""; txtRotY.Text = ""; txtRotZ.Text = "";
            txtScaleX.Text = ""; txtScaleY.Text = ""; txtScaleZ.Text = "";

            // Reset sliders to a default position (e.g., 0)
            // Or you could disable them if you prefer
            trkTransX.Value = 0; trkTransY.Value = 0; trkTransZ.Value = 0;
            trkRotX.Value = 0; trkRotY.Value = 0; trkRotZ.Value = 0;
            _isUpdatingControlsProgrammatically = false;
        }

        private float DegToRad(float degrees) => degrees * ((float)Math.PI / 180.0f);
        private float RadToDeg(float radians) => radians * (180.0f / (float)Math.PI);

        private void HandleTranslationSliderScroll(object sender, EventArgs e)
        {
            if (_isUpdatingControlsProgrammatically || _selectedBoneIndex < 0) return;

            var bonePose = _poseNodesRef[_selectedBoneIndex];
            float valX = GetSliderValue(trkTransX);
            float valY = GetSliderValue(trkTransY);
            float valZ = GetSliderValue(trkTransZ);

            bonePose.Translation = new System.Numerics.Vector3(valX, valY, valZ);

            txtPosX.Text = valX.ToString("F6");
            txtPosY.Text = valY.ToString("F6");
            txtPosZ.Text = valZ.ToString("F6");

            RequestVisualUpdate();
        }

        private void HandleRotationSliderScroll(object sender, EventArgs e)
        {
            if (_isUpdatingControlsProgrammatically || _selectedBoneIndex < 0) return;

            var bonePose = _poseNodesRef[_selectedBoneIndex];
            float valXDeg = GetSliderValue(trkRotX);
            float valYDeg = GetSliderValue(trkRotY);
            float valZDeg = GetSliderValue(trkRotZ);

            bonePose.Rotation = new System.Numerics.Vector3(
                DegToRad(valXDeg),
                DegToRad(valYDeg),
                DegToRad(valZDeg)
            );

            txtRotX.Text = valXDeg.ToString("F3");
            txtRotY.Text = valYDeg.ToString("F3");
            txtRotZ.Text = valZDeg.ToString("F3");

            RequestVisualUpdate();
        }

        private void btnApplyChanges_Click(object sender, EventArgs e) // Applies TextBox values
        {
            if (_selectedBoneIndex < 0 || _selectedBoneIndex >= _poseNodesRef.Count)
            {
                MessageBox.Show("Please select a bone first.", "No Bone Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var bonePose = _poseNodesRef[_selectedBoneIndex];

                // Update from TextBoxes
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

                // Reload UI to sync sliders if text input changed them
                LoadBoneDataToUI(_selectedBoneIndex);
                RequestVisualUpdate();
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

            LoadBoneDataToUI(_selectedBoneIndex);
            RequestVisualUpdate();
        }

        private void btnResetAllPoses_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show("Are you sure you want to reset all bone poses to their original FLVER state?",
                                                   "Confirm Reset All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.Yes)
            {
                Program.resetPoses();

                if (_selectedBoneIndex != -1 && _selectedBoneIndex < _poseNodesRef.Count)
                {
                    LoadBoneDataToUI(_selectedBoneIndex);
                }
                else
                {
                    _selectedBoneIndex = -1;
                    Program.checkingBoneIndex = -1;
                    ClearInputFieldsAndSliders();
                }
                RequestVisualUpdate();
            }
        }

        private void RequestVisualUpdate()
        {
            _needsVisualUpdate = true;
            // The timer will pick this up
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_needsVisualUpdate)
            {
                Program.updateVertices();
                OnPoseNeedsUpdate?.Invoke(); // If you have external views listening
                _needsVisualUpdate = false;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            base.OnFormClosed(e);
        }

        // --- Add InitializeComponent() in BonePoseEditorForm.Designer.cs ---
        // You'll need to add TrackBars manually or using the designer.
        // Example TrackBar declarations (add these to your class fields):
        private System.Windows.Forms.TrackBar trkTransX;
        private System.Windows.Forms.TrackBar trkTransY;
        private System.Windows.Forms.TrackBar trkTransZ;
        private System.Windows.Forms.TrackBar trkRotX;
        private System.Windows.Forms.TrackBar trkRotY;
        private System.Windows.Forms.TrackBar trkRotZ;

        // ... (rest of your existing InitializeComponent members) ...
        // Your existing InitializeComponent will need to be MODIFIED
        // to include the creation and layout of these TrackBar controls.
        // For brevity, I'll show a snippet of what to add inside InitializeComponent,
        // assuming your GroupBox `grpBoneData` exists.

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
            this.trkTransX = new System.Windows.Forms.TrackBar(); // Added
            this.lblPosY = new System.Windows.Forms.Label();
            this.txtPosY = new System.Windows.Forms.TextBox();
            this.trkTransY = new System.Windows.Forms.TrackBar(); // Added
            this.lblPosZ = new System.Windows.Forms.Label();
            this.txtPosZ = new System.Windows.Forms.TextBox();
            this.trkTransZ = new System.Windows.Forms.TrackBar(); // Added
            this.lblRotation = new System.Windows.Forms.Label();
            this.lblRotX = new System.Windows.Forms.Label();
            this.txtRotX = new System.Windows.Forms.TextBox();
            this.trkRotX = new System.Windows.Forms.TrackBar();   // Added
            this.lblRotY = new System.Windows.Forms.Label();
            this.txtRotY = new System.Windows.Forms.TextBox();
            this.trkRotY = new System.Windows.Forms.TrackBar();   // Added
            this.lblRotZ = new System.Windows.Forms.Label();
            this.txtRotZ = new System.Windows.Forms.TextBox();
            this.trkRotZ = new System.Windows.Forms.TrackBar();   // Added
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
            ((System.ComponentModel.ISupportInitialize)(this.trkTransX)).BeginInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkTransY)).BeginInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkTransZ)).BeginInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotX)).BeginInit();   // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotY)).BeginInit();   // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotZ)).BeginInit();   // Added
            this.SuspendLayout();
            // 
            // lstBones
            // 
            this.lstBones.FormattingEnabled = true;
            this.lstBones.ItemHeight = 16; // Example, adjust if needed
            this.lstBones.Location = new System.Drawing.Point(12, 12);
            this.lstBones.Name = "lstBones";
            this.lstBones.Size = new System.Drawing.Size(230, 420); // Made taller
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
            this.grpBoneData.Controls.Add(this.trkRotZ);    // Added
            this.grpBoneData.Controls.Add(this.txtRotZ);
            this.grpBoneData.Controls.Add(this.lblRotZ);
            this.grpBoneData.Controls.Add(this.trkRotY);    // Added
            this.grpBoneData.Controls.Add(this.txtRotY);
            this.grpBoneData.Controls.Add(this.lblRotY);
            this.grpBoneData.Controls.Add(this.trkRotX);    // Added
            this.grpBoneData.Controls.Add(this.txtRotX);
            this.grpBoneData.Controls.Add(this.lblRotX);
            this.grpBoneData.Controls.Add(this.lblRotation);
            this.grpBoneData.Controls.Add(this.trkTransZ);  // Added
            this.grpBoneData.Controls.Add(this.txtPosZ);
            this.grpBoneData.Controls.Add(this.lblPosZ);
            this.grpBoneData.Controls.Add(this.trkTransY);  // Added
            this.grpBoneData.Controls.Add(this.txtPosY);
            this.grpBoneData.Controls.Add(this.lblPosY);
            this.grpBoneData.Controls.Add(this.trkTransX);  // Added
            this.grpBoneData.Controls.Add(this.txtPosX);
            this.grpBoneData.Controls.Add(this.lblPosX);
            this.grpBoneData.Controls.Add(this.lblTranslation);
            this.grpBoneData.Location = new System.Drawing.Point(250, 12); // Adjusted X
            this.grpBoneData.Name = "grpBoneData";
            this.grpBoneData.Size = new System.Drawing.Size(320, 370); // Made wider and taller
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
            this.lblPosX.Location = new System.Drawing.Point(10, 50); // Adjusted
            this.lblPosX.Name = "lblPosX";
            this.lblPosX.Size = new System.Drawing.Size(17, 13);
            this.lblPosX.TabIndex = 1;
            this.lblPosX.Text = "X:";
            // 
            // txtPosX
            // 
            this.txtPosX.Location = new System.Drawing.Point(30, 47); // Adjusted
            this.txtPosX.Name = "txtPosX";
            this.txtPosX.Size = new System.Drawing.Size(70, 20);
            this.txtPosX.TabIndex = 2;
            // 
            // trkTransX
            // 
            this.trkTransX.Location = new System.Drawing.Point(105, 45); // Added
            this.trkTransX.Name = "trkTransX";
            this.trkTransX.Size = new System.Drawing.Size(200, 45); // Adjusted width
            this.trkTransX.TabIndex = 3;
            this.trkTransX.TickStyle = System.Windows.Forms.TickStyle.None; // Cleaner look
            // 
            // lblPosY
            // 
            this.lblPosY.AutoSize = true;
            this.lblPosY.Location = new System.Drawing.Point(10, 76); // Adjusted
            this.lblPosY.Name = "lblPosY";
            this.lblPosY.Size = new System.Drawing.Size(17, 13);
            this.lblPosY.TabIndex = 4;
            this.lblPosY.Text = "Y:";
            // 
            // txtPosY
            // 
            this.txtPosY.Location = new System.Drawing.Point(30, 73); // Adjusted
            this.txtPosY.Name = "txtPosY";
            this.txtPosY.Size = new System.Drawing.Size(70, 20);
            this.txtPosY.TabIndex = 5;
            // 
            // trkTransY
            // 
            this.trkTransY.Location = new System.Drawing.Point(105, 71); // Added
            this.trkTransY.Name = "trkTransY";
            this.trkTransY.Size = new System.Drawing.Size(200, 45);
            this.trkTransY.TabIndex = 6;
            this.trkTransY.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // lblPosZ
            // 
            this.lblPosZ.AutoSize = true;
            this.lblPosZ.Location = new System.Drawing.Point(10, 102); // Adjusted
            this.lblPosZ.Name = "lblPosZ";
            this.lblPosZ.Size = new System.Drawing.Size(17, 13);
            this.lblPosZ.TabIndex = 7;
            this.lblPosZ.Text = "Z:";
            // 
            // txtPosZ
            // 
            this.txtPosZ.Location = new System.Drawing.Point(30, 99); // Adjusted
            this.txtPosZ.Name = "txtPosZ";
            this.txtPosZ.Size = new System.Drawing.Size(70, 20);
            this.txtPosZ.TabIndex = 8;
            // 
            // trkTransZ
            // 
            this.trkTransZ.Location = new System.Drawing.Point(105, 97); // Added
            this.trkTransZ.Name = "trkTransZ";
            this.trkTransZ.Size = new System.Drawing.Size(200, 45);
            this.trkTransZ.TabIndex = 9;
            this.trkTransZ.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // lblRotation
            // 
            this.lblRotation.AutoSize = true;
            this.lblRotation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblRotation.Location = new System.Drawing.Point(6, 140); // Adjusted Y
            this.lblRotation.Name = "lblRotation";
            this.lblRotation.Size = new System.Drawing.Size(95, 13);
            this.lblRotation.TabIndex = 10;
            this.lblRotation.Text = "Rotation (Deg):";
            // 
            // lblRotX
            // 
            this.lblRotX.AutoSize = true;
            this.lblRotX.Location = new System.Drawing.Point(10, 165); // Adjusted
            this.lblRotX.Name = "lblRotX";
            this.lblRotX.Size = new System.Drawing.Size(17, 13);
            this.lblRotX.TabIndex = 11;
            this.lblRotX.Text = "X:";
            // 
            // txtRotX
            // 
            this.txtRotX.Location = new System.Drawing.Point(30, 162); // Adjusted
            this.txtRotX.Name = "txtRotX";
            this.txtRotX.Size = new System.Drawing.Size(70, 20);
            this.txtRotX.TabIndex = 12;
            // 
            // trkRotX
            // 
            this.trkRotX.Location = new System.Drawing.Point(105, 160); // Added
            this.trkRotX.Name = "trkRotX";
            this.trkRotX.Size = new System.Drawing.Size(200, 45);
            this.trkRotX.TabIndex = 13;
            this.trkRotX.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // lblRotY
            // 
            this.lblRotY.AutoSize = true;
            this.lblRotY.Location = new System.Drawing.Point(10, 191); // Adjusted
            this.lblRotY.Name = "lblRotY";
            this.lblRotY.Size = new System.Drawing.Size(17, 13);
            this.lblRotY.TabIndex = 14;
            this.lblRotY.Text = "Y:";
            // 
            // txtRotY
            // 
            this.txtRotY.Location = new System.Drawing.Point(30, 188); // Adjusted
            this.txtRotY.Name = "txtRotY";
            this.txtRotY.Size = new System.Drawing.Size(70, 20);
            this.txtRotY.TabIndex = 15;
            // 
            // trkRotY
            // 
            this.trkRotY.Location = new System.Drawing.Point(105, 186); // Added
            this.trkRotY.Name = "trkRotY";
            this.trkRotY.Size = new System.Drawing.Size(200, 45);
            this.trkRotY.TabIndex = 16;
            this.trkRotY.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // lblRotZ
            // 
            this.lblRotZ.AutoSize = true;
            this.lblRotZ.Location = new System.Drawing.Point(10, 217); // Adjusted
            this.lblRotZ.Name = "lblRotZ";
            this.lblRotZ.Size = new System.Drawing.Size(17, 13);
            this.lblRotZ.TabIndex = 17;
            this.lblRotZ.Text = "Z:";
            // 
            // txtRotZ
            // 
            this.txtRotZ.Location = new System.Drawing.Point(30, 214); // Adjusted
            this.txtRotZ.Name = "txtRotZ";
            this.txtRotZ.Size = new System.Drawing.Size(70, 20);
            this.txtRotZ.TabIndex = 18;
            // 
            // trkRotZ
            // 
            this.trkRotZ.Location = new System.Drawing.Point(105, 212); // Added
            this.trkRotZ.Name = "trkRotZ";
            this.trkRotZ.Size = new System.Drawing.Size(200, 45);
            this.trkRotZ.TabIndex = 19;
            this.trkRotZ.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // lblScale
            // 
            this.lblScale.AutoSize = true;
            this.lblScale.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblScale.Location = new System.Drawing.Point(6, 255); // Adjusted Y
            this.lblScale.Name = "lblScale";
            this.lblScale.Size = new System.Drawing.Size(43, 13);
            this.lblScale.TabIndex = 20;
            this.lblScale.Text = "Scale:";
            // 
            // lblScaleX
            // 
            this.lblScaleX.AutoSize = true;
            this.lblScaleX.Location = new System.Drawing.Point(23, 278); // Adjusted Y
            this.lblScaleX.Name = "lblScaleX";
            this.lblScaleX.Size = new System.Drawing.Size(17, 13);
            this.lblScaleX.TabIndex = 21;
            this.lblScaleX.Text = "X:";
            // 
            // txtScaleX
            // 
            this.txtScaleX.Location = new System.Drawing.Point(46, 275); // Adjusted Y
            this.txtScaleX.Name = "txtScaleX";
            this.txtScaleX.ReadOnly = true;
            this.txtScaleX.Size = new System.Drawing.Size(70, 20);
            this.txtScaleX.TabIndex = 22;
            // 
            // lblScaleY
            // 
            this.lblScaleY.AutoSize = true;
            this.lblScaleY.Location = new System.Drawing.Point(130, 278); // Adjusted Y
            this.lblScaleY.Name = "lblScaleY";
            this.lblScaleY.Size = new System.Drawing.Size(17, 13);
            this.lblScaleY.TabIndex = 23;
            this.lblScaleY.Text = "Y:";
            // 
            // txtScaleY
            // 
            this.txtScaleY.Location = new System.Drawing.Point(153, 275); // Adjusted Y
            this.txtScaleY.Name = "txtScaleY";
            this.txtScaleY.ReadOnly = true;
            this.txtScaleY.Size = new System.Drawing.Size(70, 20);
            this.txtScaleY.TabIndex = 24;
            // 
            // lblScaleZ
            // 
            this.lblScaleZ.AutoSize = true;
            this.lblScaleZ.Location = new System.Drawing.Point(23, 304); // Adjusted Y
            this.lblScaleZ.Name = "lblScaleZ";
            this.lblScaleZ.Size = new System.Drawing.Size(17, 13);
            this.lblScaleZ.TabIndex = 25;
            this.lblScaleZ.Text = "Z:";
            // 
            // txtScaleZ
            // 
            this.txtScaleZ.Location = new System.Drawing.Point(46, 301); // Adjusted Y
            this.txtScaleZ.Name = "txtScaleZ";
            this.txtScaleZ.ReadOnly = true;
            this.txtScaleZ.Size = new System.Drawing.Size(70, 20);
            this.txtScaleZ.TabIndex = 26;
            // 
            // btnApplyChanges
            // 
            this.btnApplyChanges.Location = new System.Drawing.Point(250, 388); // Adjusted Y
            this.btnApplyChanges.Name = "btnApplyChanges";
            this.btnApplyChanges.Size = new System.Drawing.Size(150, 23); // Wider
            this.btnApplyChanges.TabIndex = 2; // This tab index is fine after groupbox
            this.btnApplyChanges.Text = "Apply Text Box Changes"; // Clarified
            this.btnApplyChanges.UseVisualStyleBackColor = true;
            this.btnApplyChanges.Click += new System.EventHandler(this.btnApplyChanges_Click);
            // 
            // btnResetSelectedBone
            // 
            this.btnResetSelectedBone.Location = new System.Drawing.Point(410, 388); // Adjusted X, Y
            this.btnResetSelectedBone.Name = "btnResetSelectedBone";
            this.btnResetSelectedBone.Size = new System.Drawing.Size(160, 23); // Wider
            this.btnResetSelectedBone.TabIndex = 3;
            this.btnResetSelectedBone.Text = "Reset Selected Bone";
            this.btnResetSelectedBone.UseVisualStyleBackColor = true;
            this.btnResetSelectedBone.Click += new System.EventHandler(this.btnResetSelectedBone_Click);
            // 
            // btnResetAllPoses
            // 
            this.btnResetAllPoses.Location = new System.Drawing.Point(250, 417); // Adjusted Y
            this.btnResetAllPoses.Name = "btnResetAllPoses";
            this.btnResetAllPoses.Size = new System.Drawing.Size(320, 23); // Wider
            this.btnResetAllPoses.TabIndex = 4;
            this.btnResetAllPoses.Text = "Reset All Poses";
            this.btnResetAllPoses.UseVisualStyleBackColor = true;
            this.btnResetAllPoses.Click += new System.EventHandler(this.btnResetAllPoses_Click);
            // 
            // BonePoseEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 453); // Adjusted client size
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
            ((System.ComponentModel.ISupportInitialize)(this.trkTransX)).EndInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkTransY)).EndInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkTransZ)).EndInit(); // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotX)).EndInit();   // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotY)).EndInit();   // Added
            ((System.ComponentModel.ISupportInitialize)(this.trkRotZ)).EndInit();   // Added
            this.ResumeLayout(false);
        }
    }
}