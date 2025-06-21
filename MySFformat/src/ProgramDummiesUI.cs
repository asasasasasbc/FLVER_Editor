using Assimp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing; // For System.Drawing.Point, Size, Color (UI)
using System.IO;
using System.Linq;
using System.Numerics; // For System.Numerics.Vector3 
using SoulsVector3 = System.Numerics.Vector3; 
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SoulsFormats;

namespace MySFformat
{
    public class ColorJavaScriptConverter : JavaScriptConverter
    {
        public override IEnumerable<Type> SupportedTypes
        {
            get { yield return typeof(System.Drawing.Color); }
        }

        public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
        {
            if (type == typeof(System.Drawing.Color))
            {
                byte r = 0, g = 0, b = 0, a = 255; // Default to opaque black

                if (dictionary.TryGetValue("R", out object rObj) && rObj != null)
                    r = Convert.ToByte(rObj);
                else if (dictionary.TryGetValue("r", out rObj) && rObj != null) // Case-insensitivity for R
                    r = Convert.ToByte(rObj);

                if (dictionary.TryGetValue("G", out object gObj) && gObj != null)
                    g = Convert.ToByte(gObj);
                else if (dictionary.TryGetValue("g", out gObj) && gObj != null) // Case-insensitivity for G
                    g = Convert.ToByte(gObj);

                if (dictionary.TryGetValue("B", out object bObj) && bObj != null)
                    b = Convert.ToByte(bObj);
                else if (dictionary.TryGetValue("b", out bObj) && bObj != null) // Case-insensitivity for B
                    b = Convert.ToByte(bObj);

                if (dictionary.TryGetValue("A", out object aObj) && aObj != null)
                    a = Convert.ToByte(aObj);
                else if (dictionary.TryGetValue("a", out aObj) && aObj != null) // Case-insensitivity for A
                    a = Convert.ToByte(aObj);

                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
            return null;
        }

        public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
        {
            if (obj is System.Drawing.Color color)
            {
                var result = new Dictionary<string, object>();
                // We'll output only R, G, B, A for simplicity and consistency.
                // The default JavaScriptSerializer output for Color includes other properties
                // like IsKnownColor, IsEmpty, IsNamedColor, IsSystemColor, Name.
                // Our Deserialize method only needs R, G, B, A.
                result["R"] = color.R;
                result["G"] = color.G;
                result["B"] = color.B;
                result["A"] = color.A;
                return result;
            }
            // Should not be reached if SupportedTypes is correctly implemented
            return new Dictionary<string, object>();
        }
    }
    static partial class Program
    {

        static FLVER.Dummy _selectedDummy = null;

        static ListBox lbDummies;
        static Panel editorPanel; // Right panel for editing properties
        static Panel leftPanelContainer; // To hold the listbox and its buttons

        // Editor Controls
        static NumericUpDown nudPosX, nudPosY, nudPosZ;
        static NumericUpDown nudForwardX, nudForwardY, nudForwardZ;
        static NumericUpDown nudUpwardX, nudUpwardY, nudUpwardZ;
        static NumericUpDown nudRefID;
        static NumericUpDown nudParentBone, nudAttachBone;
        static NumericUpDown nudColorR, nudColorG, nudColorB, nudColorA;
        static CheckBox chkFlag1, chkUseUpward;
        static NumericUpDown nudUnk30, nudUnk34;

        // New Controls for JSON editing
        static TextBox txtDummyJson;
        static Button btnApplyChanges;
        static Button btnApplyJsonChanges;


        static void dummies()
        {
            _selectedDummy = null;

            Form f = new Form();
            f.Text = "Dummy Editor";
            f.Size = new System.Drawing.Size(650, 700); // User-defined fixed size
            f.FormBorderStyle = FormBorderStyle.FixedSingle; // Prevent resizing if layout is truly fixed
            f.MaximizeBox = false;

            int padding = 10;
            int bottomPanelHeight = 50;

            leftPanelContainer = new Panel();
            leftPanelContainer.Location = new System.Drawing.Point(padding, padding);
            leftPanelContainer.Size = new System.Drawing.Size(250, f.ClientSize.Height - bottomPanelHeight - 2 * padding);
            leftPanelContainer.BorderStyle = BorderStyle.FixedSingle;
            f.Controls.Add(leftPanelContainer);

            lbDummies = new ListBox();
            lbDummies.FormattingEnabled = true;
            lbDummies.Location = new System.Drawing.Point(padding, padding);
            int listButtonHeight = 30;
            int listButtonSpacing = 5;
            lbDummies.Size = new System.Drawing.Size(
                leftPanelContainer.Width - 2 * padding,
                leftPanelContainer.Height - 2 * padding - listButtonHeight - listButtonSpacing
            );
            lbDummies.SelectedIndexChanged += LbDummies_SelectedIndexChanged;
            leftPanelContainer.Controls.Add(lbDummies);

            Button btnAddDummy = new Button();
            ButtonTips("Add a new default dummy point.\n增加一个空白Dummy点。", btnAddDummy);
            btnAddDummy.Text = "Add New";
            btnAddDummy.Size = new System.Drawing.Size(80, listButtonHeight);
            btnAddDummy.Location = new System.Drawing.Point(padding, lbDummies.Bottom + listButtonSpacing);
            btnAddDummy.Click += BtnAddDummy_Click;
            leftPanelContainer.Controls.Add(btnAddDummy);

            Button btnRemoveDummy = new Button();
            ButtonTips("Remove the selected dummy point.\n删除当前的Dummy点。", btnRemoveDummy);
            btnRemoveDummy.Text = "Remove";
            btnRemoveDummy.Size = new System.Drawing.Size(80, listButtonHeight);
            btnRemoveDummy.Location = new System.Drawing.Point(btnAddDummy.Right + listButtonSpacing, btnAddDummy.Top);
            btnRemoveDummy.Click += BtnRemoveDummy_Click;
            leftPanelContainer.Controls.Add(btnRemoveDummy);

            editorPanel = new Panel();
            editorPanel.Location = new System.Drawing.Point(leftPanelContainer.Right + padding, padding);
            editorPanel.Size = new System.Drawing.Size(
                f.ClientSize.Width - leftPanelContainer.Right - 3 * padding, // Adjusted width for consistent padding
                leftPanelContainer.Height
            );
            editorPanel.AutoScroll = true;
            editorPanel.BorderStyle = BorderStyle.FixedSingle;
            SetupEditorControls(editorPanel);
            f.Controls.Add(editorPanel);

            Panel bottomButtonsPanel = new Panel();
            bottomButtonsPanel.Location = new System.Drawing.Point(padding, leftPanelContainer.Bottom + padding);
            bottomButtonsPanel.Size = new System.Drawing.Size(f.ClientSize.Width - 2 * padding, bottomPanelHeight - padding);
            f.Controls.Add(bottomButtonsPanel);

            int buttonYInBottomPanel = (bottomButtonsPanel.Height - 30) / 2;

            Button btnImportJson = new Button();
            ButtonTips("Import dummy information from a JSON file.\n导入Json点位配置。", btnImportJson);
            btnImportJson.Text = "Import JSON";
            btnImportJson.Size = new System.Drawing.Size(100, 30);
            btnImportJson.Location = new System.Drawing.Point(padding, buttonYInBottomPanel);
            btnImportJson.Click += BtnImportJson_Click;
            bottomButtonsPanel.Controls.Add(btnImportJson);

            Button btnExportJson = new Button();
            ButtonTips("Export current dummy information to a JSON file.\n导出Json点位配置。", btnExportJson);
            btnExportJson.Text = "Export JSON";
            btnExportJson.Size = new System.Drawing.Size(100, 30);
            btnExportJson.Location = new System.Drawing.Point(btnImportJson.Right + padding, buttonYInBottomPanel);
            btnExportJson.Click += BtnExportJson_Click;
            bottomButtonsPanel.Controls.Add(btnExportJson);

            Button btnSekiroFix = new Button();
            ButtonTips("Fix external weapon's weapon trail/lighting reversal problem in Sekiro by adding kusabimaru's dummy information." +
                "\n写入契丸的辅助点信息以解决武器在只狼内没有剑风以及无法雷闪的问题。", btnSekiroFix);
            btnSekiroFix.Text = "SekiroFix";
            btnSekiroFix.Size = new System.Drawing.Size(100, 30);
            btnSekiroFix.Location = new System.Drawing.Point(btnExportJson.Right + padding, buttonYInBottomPanel);
            btnSekiroFix.Click += BtnSekiroFix_Click;
            bottomButtonsPanel.Controls.Add(btnSekiroFix);

            RefreshDummyList();
            EnableEditorControls(false);

            f.FormClosing += (s, e) => { useCheckingPoint = false; };
            f.ShowDialog();
        }

        private static void SetupEditorControls(Panel parentPanel)
        {
            int currentY = 10;
            int labelWidth = 100;
            int controlIndent = 10;
            int controlWidthStandard = 80;
            int spacing = 5;
            int tripletControlWidth = 60;
            int rowHeight = 25;
            int nudHeight = 20;

            // Position
            AddLabel(parentPanel, "Position:", controlIndent, currentY);
            nudPosX = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, tripletControlWidth, nudHeight, -10000m, 10000m, 0.01m);
            nudPosY = AddNumericUpDown(parentPanel, nudPosX.Right + spacing, currentY, tripletControlWidth, nudHeight, -10000m, 10000m, 0.01m);
            nudPosZ = AddNumericUpDown(parentPanel, nudPosY.Right + spacing, currentY, tripletControlWidth, nudHeight, -10000m, 10000m, 0.01m);
            currentY += rowHeight + spacing;

            // Forward
            AddLabel(parentPanel, "Forward:", controlIndent, currentY);
            nudForwardX = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            nudForwardY = AddNumericUpDown(parentPanel, nudForwardX.Right + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            nudForwardZ = AddNumericUpDown(parentPanel, nudForwardY.Right + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            currentY += rowHeight + spacing;

            // Upward
            AddLabel(parentPanel, "Upward:", controlIndent, currentY);
            nudUpwardX = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            nudUpwardY = AddNumericUpDown(parentPanel, nudUpwardX.Right + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            nudUpwardZ = AddNumericUpDown(parentPanel, nudUpwardY.Right + spacing, currentY, tripletControlWidth, nudHeight, -10m, 10m, 0.01m);
            currentY += rowHeight + spacing;

            // ReferenceID
            AddLabel(parentPanel, "Reference ID:", controlIndent, currentY);
            nudRefID = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, controlWidthStandard, nudHeight, short.MinValue, short.MaxValue, 1, 0);
            currentY += rowHeight + spacing;

            // ParentBoneIndex
            AddLabel(parentPanel, "Parent Bone:", controlIndent, currentY);
            nudParentBone = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, controlWidthStandard, nudHeight, -1, short.MaxValue, 1, 0);
            currentY += rowHeight + spacing;

            // AttachBoneIndex
            AddLabel(parentPanel, "Attach Bone:", controlIndent, currentY);
            nudAttachBone = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, controlWidthStandard, nudHeight, -1, short.MaxValue, 1, 0);
            currentY += rowHeight + spacing;

            // Color (RGBA)
            AddLabel(parentPanel, "Color (RGBA):", controlIndent, currentY);
            int colorNudWidth = 50;
            nudColorR = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, colorNudWidth, nudHeight, 0, 255, 1, 0);
            nudColorG = AddNumericUpDown(parentPanel, nudColorR.Right + spacing, currentY, colorNudWidth, nudHeight, 0, 255, 1, 0);
            nudColorB = AddNumericUpDown(parentPanel, nudColorG.Right + spacing, currentY, colorNudWidth, nudHeight, 0, 255, 1, 0);
            nudColorA = AddNumericUpDown(parentPanel, nudColorB.Right + spacing, currentY, colorNudWidth, nudHeight, 0, 255, 1, 0);
            currentY += rowHeight + spacing;

            // Flag1
            chkFlag1 = new CheckBox { Text = "Flag1", Location = new System.Drawing.Point(controlIndent, currentY), AutoSize = true };
            parentPanel.Controls.Add(chkFlag1);
            currentY += rowHeight + spacing;

            // UseUpwardVector
            chkUseUpward = new CheckBox { Text = "Use Upward Vector", Location = new System.Drawing.Point(controlIndent, currentY), AutoSize = true };
            parentPanel.Controls.Add(chkUseUpward);
            currentY += rowHeight + spacing;

            // Unk30
            AddLabel(parentPanel, "Unk30:", controlIndent, currentY);
            nudUnk30 = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, controlWidthStandard, nudHeight, int.MinValue, int.MaxValue, 1, 0);
            currentY += rowHeight + spacing;

            // Unk34
            AddLabel(parentPanel, "Unk34:", controlIndent, currentY);
            nudUnk34 = AddNumericUpDown(parentPanel, labelWidth + spacing, currentY, controlWidthStandard, nudHeight, int.MinValue, int.MaxValue, 1, 0);
            currentY += rowHeight + spacing;

            // JSON Text Box
            AddLabel(parentPanel, "Dummy JSON:", controlIndent, currentY);
            currentY += 20; // Space for label below it

            txtDummyJson = new TextBox();
            txtDummyJson.Location = new System.Drawing.Point(controlIndent, currentY);
            txtDummyJson.Size = new System.Drawing.Size(parentPanel.ClientSize.Width - 2 * controlIndent, 175);
            txtDummyJson.Multiline = true;
            txtDummyJson.ScrollBars = ScrollBars.Vertical;
            txtDummyJson.WordWrap = true; 
            txtDummyJson.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtDummyJson.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parentPanel.Controls.Add(txtDummyJson);
            currentY += txtDummyJson.Height + spacing + 10;

            // Apply Button
            btnApplyChanges = new Button();
            ButtonTips("Apply changes from the fields above to the selected dummy point and save to FLVER.\n应用并修改此Dummy点。", btnApplyChanges);
            btnApplyChanges.Text = "Apply Changes";
            btnApplyChanges.Size = new System.Drawing.Size(120, 30);
            btnApplyChanges.Location = new System.Drawing.Point(controlIndent, currentY);
            btnApplyChanges.Click += BtnApplyChanges_Click;
            parentPanel.Controls.Add(btnApplyChanges);

            // Apply JSON Changes Button
            btnApplyJsonChanges = new Button();
            ButtonTips("Apply changes from the JSON text box to the selected dummy point and save to FLVER.\n从JSON文本框应用并修改此Dummy点。", btnApplyJsonChanges);
            btnApplyJsonChanges.Text = "Apply JSON";
            btnApplyJsonChanges.Size = new System.Drawing.Size(120, 30);
            btnApplyJsonChanges.Location = new System.Drawing.Point(btnApplyChanges.Right + spacing, currentY);
            btnApplyJsonChanges.Click += BtnApplyJsonChanges_Click;
            parentPanel.Controls.Add(btnApplyJsonChanges);
        }

        private static Label AddLabel(Panel parent, string text, int xPos, int yPos)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new System.Drawing.Point(xPos, yPos + 3);
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);
            return lbl;
        }

        private static NumericUpDown AddNumericUpDown(Panel parent, int xPos, int yPos, int width, int height,
                                                     decimal min, decimal max, decimal increment, int decimalPlaces = 2)
        {
            NumericUpDown nud = new NumericUpDown();
            nud.Location = new System.Drawing.Point(xPos, yPos);
            nud.Size = new System.Drawing.Size(width, height);
            nud.Minimum = min;
            nud.Maximum = max;
            nud.Increment = increment;
            nud.DecimalPlaces = decimalPlaces;
            parent.Controls.Add(nud);
            return nud;
        }

        private static NumericUpDown AddNumericUpDown(Panel parent, int xPos, int yPos, int width, int height,
                                         long min, long max, long increment, int decimalPlaces = 0)
        {
            return AddNumericUpDown(parent, xPos, yPos, width, height, (decimal)min, (decimal)max, (decimal)increment, decimalPlaces);
        }


        private static void RefreshDummyList()
        {
            int selectedIndex = lbDummies.SelectedIndex;
            lbDummies.BeginUpdate();
            lbDummies.Items.Clear();
            if (targetFlver != null)
            {
                for (int i = 0; i < targetFlver.Dummies.Count; i++)
                {
                    lbDummies.Items.Add($"[{i}] ID: {targetFlver.Dummies[i].ReferenceID}");
                }
            }
            lbDummies.EndUpdate();
            if (selectedIndex >= 0 && selectedIndex < lbDummies.Items.Count)
            {
                lbDummies.SelectedIndex = selectedIndex;
            }
            else
            {
                EnableEditorControls(false);
                if (lbDummies.Items.Count > 0) lbDummies.SelectedIndex = 0;
                else _selectedDummy = null;
            }
        }

        private static void LbDummies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbDummies.SelectedIndex >= 0 && lbDummies.SelectedIndex < targetFlver.Dummies.Count)
            {
                _selectedDummy = targetFlver.Dummies[lbDummies.SelectedIndex];
                LoadDummyDataToEditor(_selectedDummy);
                EnableEditorControls(true);

                useCheckingPoint = true;
                checkingPointHasTangent = false;
                // FLVER.Dummy.Position is SoulsFormats.Vector3, checkingPoint is System.Numerics.Vector3
                checkingPoint = new System.Numerics.Vector3(_selectedDummy.Position.X, _selectedDummy.Position.Y, _selectedDummy.Position.Z);
                checkingPointNormal = new System.Numerics.Vector3(_selectedDummy.Forward.X * 0.2f, _selectedDummy.Forward.Y * 0.2f, _selectedDummy.Forward.Z * 0.2f);
                updateVertices();
            }
            else
            {
                _selectedDummy = null;
                LoadDummyDataToEditor(null); // Clear editor fields including JSON box
                EnableEditorControls(false);
                useCheckingPoint = false;
                updateVertices();
            }
        }

        private static void LoadDummyDataToEditor(FLVER.Dummy dummy)
        {
            if (dummy == null)
            {
                // Clear all fields if no dummy is selected
                if (nudPosX != null) // Check if controls are initialized
                {
                    nudPosX.Value = nudPosX.Minimum; nudPosY.Value = nudPosY.Minimum; nudPosZ.Value = nudPosZ.Minimum;
                    nudForwardX.Value = nudForwardX.Minimum; nudForwardY.Value = nudForwardY.Minimum; nudForwardZ.Value = nudForwardZ.Minimum;
                    nudUpwardX.Value = nudUpwardX.Minimum; nudUpwardY.Value = nudUpwardY.Minimum; nudUpwardZ.Value = nudUpwardZ.Minimum;
                    nudRefID.Value = nudRefID.Minimum; nudParentBone.Value = nudParentBone.Minimum; nudAttachBone.Value = nudAttachBone.Minimum;
                    nudColorR.Value = 0; nudColorG.Value = 0; nudColorB.Value = 0; nudColorA.Value = 0; // Default color to black transparent
                    chkFlag1.Checked = false; chkUseUpward.Checked = false;
                    nudUnk30.Value = nudUnk30.Minimum; nudUnk34.Value = nudUnk34.Minimum;
                }
                if (txtDummyJson != null) txtDummyJson.Text = "";
                return;
            }

            nudPosX.Value = (decimal)dummy.Position.X;
            nudPosY.Value = (decimal)dummy.Position.Y;
            nudPosZ.Value = (decimal)dummy.Position.Z;

            nudForwardX.Value = (decimal)dummy.Forward.X;
            nudForwardY.Value = (decimal)dummy.Forward.Y;
            nudForwardZ.Value = (decimal)dummy.Forward.Z;

            nudUpwardX.Value = (decimal)dummy.Upward.X;
            nudUpwardY.Value = (decimal)dummy.Upward.Y;
            nudUpwardZ.Value = (decimal)dummy.Upward.Z;

            nudRefID.Value = dummy.ReferenceID;
            nudParentBone.Value = dummy.ParentBoneIndex;
            nudAttachBone.Value = dummy.AttachBoneIndex;

            // FLVER.Dummy.Color is System.Drawing.Color
            nudColorR.Value = dummy.Color.R;
            nudColorG.Value = dummy.Color.G;
            nudColorB.Value = dummy.Color.B;
            nudColorA.Value = dummy.Color.A;

            chkFlag1.Checked = dummy.Flag1;
            chkUseUpward.Checked = dummy.UseUpwardVector;

            nudUnk30.Value = dummy.Unk30;
            nudUnk34.Value = dummy.Unk34;

            // Populate JSON TextBox
            if (txtDummyJson != null)
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    // Consider Newtonsoft.Json for pretty printing if desired:
                    // txtDummyJson.Text = Newtonsoft.Json.JsonConvert.SerializeObject(dummy, Newtonsoft.Json.Formatting.Indented);
                    txtDummyJson.Text = serializer.Serialize(dummy);
                }
                catch (Exception ex)
                {
                    txtDummyJson.Text = $"Error serializing dummy to JSON: {ex.Message}";
                }
            }
        }

        private static void EnableEditorControls(bool enabled)
        {
            if (editorPanel == null) return;

            foreach (Control ctrl in editorPanel.Controls)
            {
                if (ctrl is NumericUpDown || ctrl is CheckBox)
                {
                    ctrl.Enabled = enabled;
                }
                else if (ctrl is TextBox tb) // Specifically txtDummyJson
                {
                    tb.ReadOnly = !enabled; // Editable when 'enabled' is true
                }
            }
            if (btnApplyChanges != null)
            {
                btnApplyChanges.Enabled = enabled;
            }
            if (btnApplyJsonChanges != null)
            {
                btnApplyJsonChanges.Enabled = enabled;
            }

            // If controls are being disabled (e.g. no selection), clear JSON and make ReadOnly.
            if (!enabled && txtDummyJson != null)
            {
                // txtDummyJson.Text = ""; // Already handled by LoadDummyDataToEditor(null)
                txtDummyJson.ReadOnly = true;
            }
        }

        private static void BtnAddDummy_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;

            FLVER.Dummy newDummy = new FLVER.Dummy();
            // Set some defaults perhaps, or leave as SoulsFormats defaults
            newDummy.Color = System.Drawing.Color.FromArgb(255, 255, 255, 255); // Default to white
            newDummy.Forward = new SoulsVector3(0, 0, 1); // Default forward Z
            newDummy.Upward = new SoulsVector3(0, 1, 0);   // Default upward Y

            targetFlver.Dummies.Add(newDummy);

            SaveFlverChanges("Dummy added.");
            RefreshDummyList();
            if (targetFlver.Dummies.Count > 0)
            {
                lbDummies.SelectedIndex = targetFlver.Dummies.Count - 1;
            }
        }

        private static void BtnRemoveDummy_Click(object sender, EventArgs e)
        {
            if (targetFlver == null || _selectedDummy == null || lbDummies.SelectedIndex < 0)
            {
                MessageBox.Show("No dummy selected to remove.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int selectedIdx = lbDummies.SelectedIndex;
            targetFlver.Dummies.RemoveAt(selectedIdx);

            SaveFlverChanges("Dummy removed.");
            RefreshDummyList();
        }

        private static void BtnApplyChanges_Click(object sender, EventArgs e)
        {
            if (targetFlver == null || _selectedDummy == null)
            {
                MessageBox.Show("No dummy selected to apply changes to.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // Use SFVector3 (SoulsFormats.Vector3) for these properties
                _selectedDummy.Position = new SoulsVector3((float)nudPosX.Value, (float)nudPosY.Value, (float)nudPosZ.Value);
                _selectedDummy.Forward = new SoulsVector3((float)nudForwardX.Value, (float)nudForwardY.Value, (float)nudForwardZ.Value);
                _selectedDummy.Upward = new SoulsVector3((float)nudUpwardX.Value, (float)nudUpwardY.Value, (float)nudUpwardZ.Value);

                _selectedDummy.ReferenceID = (short)nudRefID.Value;
                _selectedDummy.ParentBoneIndex = (short)nudParentBone.Value;
                _selectedDummy.AttachBoneIndex = (short)nudAttachBone.Value;

                // FLVER.Dummy.Color is System.Drawing.Color
                _selectedDummy.Color = System.Drawing.Color.FromArgb((byte)nudColorA.Value, (byte)nudColorR.Value, (byte)nudColorG.Value, (byte)nudColorB.Value);

                _selectedDummy.Flag1 = chkFlag1.Checked;
                _selectedDummy.UseUpwardVector = chkUseUpward.Checked;

                _selectedDummy.Unk30 = (int)nudUnk30.Value;
                _selectedDummy.Unk34 = (int)nudUnk34.Value;

                SaveFlverChanges("Dummy changes applied.");

                // Update JSON text box to reflect these changes
                if (txtDummyJson != null)
                {
                    try
                    {
                        var serializer = new JavaScriptSerializer();
                        serializer.MaxJsonLength = int.MaxValue;
                        txtDummyJson.Text = serializer.Serialize(_selectedDummy);
                    }
                    catch (Exception ex)
                    {
                        txtDummyJson.Text = $"Error re-serializing dummy to JSON: {ex.Message}";
                    }
                }

                int preservedIndex = lbDummies.SelectedIndex;
                RefreshDummyList();
                if (preservedIndex >= 0 && preservedIndex < lbDummies.Items.Count)
                {
                    lbDummies.SelectedIndex = preservedIndex;
                }
                // Optional: Notify user
                // MessageBox.Show("Changes applied successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void BtnApplyJsonChanges_Click(object sender, EventArgs e)
        {
            if (targetFlver == null || _selectedDummy == null)
            {
                MessageBox.Show("No dummy selected to apply JSON changes to.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (txtDummyJson == null || string.IsNullOrWhiteSpace(txtDummyJson.Text))
            {
                MessageBox.Show("JSON text box is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                // Register your custom converter
                serializer.RegisterConverters(new JavaScriptConverter[] { new ColorJavaScriptConverter() });
                serializer.MaxJsonLength = int.MaxValue;
                FLVER.Dummy deserializedDummy = serializer.Deserialize<FLVER.Dummy>(txtDummyJson.Text);

                if (deserializedDummy != null)
                {
                    // Apply properties from deserializedDummy to _selectedDummy
                    // This preserves the instance of _selectedDummy in targetFlver.Dummies list
                    _selectedDummy.Position = deserializedDummy.Position;
                    _selectedDummy.Forward = deserializedDummy.Forward;
                    _selectedDummy.Upward = deserializedDummy.Upward;
                    _selectedDummy.ReferenceID = deserializedDummy.ReferenceID;
                    _selectedDummy.ParentBoneIndex = deserializedDummy.ParentBoneIndex;
                    _selectedDummy.AttachBoneIndex = deserializedDummy.AttachBoneIndex;
                    _selectedDummy.Color = deserializedDummy.Color; // System.Drawing.Color
                    _selectedDummy.Flag1 = deserializedDummy.Flag1;
                    _selectedDummy.UseUpwardVector = deserializedDummy.UseUpwardVector;
                    _selectedDummy.Unk30 = deserializedDummy.Unk30;
                    _selectedDummy.Unk34 = deserializedDummy.Unk34;
                    // Copy any other relevant fields if FLVER.Dummy definition changes in future

                    SaveFlverChanges("Dummy changes applied from JSON.");

                    int preservedIndex = lbDummies.SelectedIndex;
                    RefreshDummyList(); // This will reload the editor fields (NUDs, etc.) from the modified _selectedDummy
                    if (preservedIndex >= 0 && preservedIndex < lbDummies.Items.Count)
                    {
                        lbDummies.SelectedIndex = preservedIndex; // Re-select to trigger visualizer update
                    }
                    MessageBox.Show("Dummy changes from JSON applied successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to deserialize JSON into a Dummy object (deserialized object was null).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying JSON changes: {ex.Message}\n\nThis can happen if the JSON structure is invalid or doesn't match the FLVER.Dummy structure (e.g. incorrect types, missing fields that serializer expects, or extra fields it cannot map).\n\nStack Trace:\n{ex.StackTrace}", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private static void BtnImportJson_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;
            var openFileDialog1 = new OpenFileDialog() { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string jsonContent = File.ReadAllText(openFileDialog1.FileName);
                    var serializer = new JavaScriptSerializer();
                    // Register your custom converter
                    serializer.RegisterConverters(new JavaScriptConverter[] { new ColorJavaScriptConverter() });
                    serializer.MaxJsonLength = int.MaxValue;
                    targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(jsonContent);

                    SaveFlverChanges("Dummies imported from JSON.");
                    RefreshDummyList();
                    MessageBox.Show("Dummies imported successfully! Consider restarting viewer if display issues occur.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing JSON: {ex.Message}\n\n{ex.StackTrace}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void BtnExportJson_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string serializedResult = serializer.Serialize(targetFlver.Dummies);
            exportJson(serializedResult, $"{flverName}_Dummies.json", "Dummies exported to JSON successfully!");
        }

        private static void BtnSekiroFix_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;
            try
            {
                string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dummyInfoPath = Path.Combine(assemblyPath, "dummyInfo.dll"); // This is a text file with JSON content, not a DLL.

                if (!File.Exists(dummyInfoPath))
                {
                    MessageBox.Show($"Error: dummyInfo.dll (expected JSON content) not found at {dummyInfoPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string dummyStr = File.ReadAllText(dummyInfoPath);
                var serializer = new JavaScriptSerializer();
                // Register your custom converter
                serializer.RegisterConverters(new JavaScriptConverter[] { new ColorJavaScriptConverter() });
                serializer.MaxJsonLength = int.MaxValue;
                List<FLVER.Dummy> refDummies = serializer.Deserialize<List<FLVER.Dummy>>(dummyStr);

                int dummiesAdded = 0;
                foreach (var refDummy in refDummies)
                {
                    // Check if a dummy with the same ReferenceID already exists.
                    if (!targetFlver.Dummies.Any(d => d.ReferenceID == refDummy.ReferenceID))
                    {
                        // Use the copy constructor for a deep copy if available, or manually copy properties.
                        // FLVER.Dummy has a copy constructor.
                        targetFlver.Dummies.Add(new FLVER.Dummy(refDummy));
                        dummiesAdded++;
                    }
                }

                if (dummiesAdded > 0)
                {
                    SaveFlverChanges($"{dummiesAdded} dummies added for Sekiro compatibility.");
                    RefreshDummyList();
                    MessageBox.Show($"Sekiro dummy fix applied. {dummiesAdded} dummies added. Consider restarting viewer.", "Sekiro Fix", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No new dummies needed for Sekiro fix, or they already exist.", "Sekiro Fix", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying Sekiro fix: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SaveFlverChanges(string messageForResult = null)
        {
            if (targetFlver == null || string.IsNullOrEmpty(flverName)) return;

            autoBackUp();
            targetFlver.Write(flverName);

            if (_selectedDummy == null)
            {
                useCheckingPoint = false;
                updateVertices();
            }
            else // Ensure the visualizer is updated with the current state of _selectedDummy
            {
                // FLVER.Dummy.Position is SoulsFormats.Vector3, checkingPoint is System.Numerics.Vector3
                checkingPoint = new System.Numerics.Vector3(_selectedDummy.Position.X, _selectedDummy.Position.Y, _selectedDummy.Position.Z);
                checkingPointNormal = new System.Numerics.Vector3(_selectedDummy.Forward.X * 0.2f, _selectedDummy.Forward.Y * 0.2f, _selectedDummy.Forward.Z * 0.2f);
                useCheckingPoint = true; // Make sure it's active
                checkingPointHasTangent = false; // As per existing logic
                updateVertices();
            }


            if (!string.IsNullOrEmpty(messageForResult))
            {
                Console.WriteLine(messageForResult); // Or some other logging/status update
            }
        }
    }
}