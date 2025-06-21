using Assimp;
// Assuming Microsoft.Xna.Framework.Color is the intended type available in your project.
// The original Dummy class definition uses Microsoft.Xna.Framework.Graphics.Color.
// Ensure this XnaColor alias matches the actual type in FLVER.Dummy.
using XnaColor = Microsoft.Xna.Framework.Color; // User's alias
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing; // For System.Drawing.Point, Size, Color (UI)
using System.IO;
using System.Linq;
using System.Numerics; // For System.Numerics.Vector3 (used for checkingPoint)
using SoulsVector3 = System.Numerics.Vector3;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SoulsFormats; // For FLVER.Dummy

namespace MySFformat
{
    static partial class Program
    {
        // Assume these are accessible class members or passed in:
        // static FLVER targetFlver;
        // static string flverName;
        // static bool useCheckingPoint;
        // static bool checkingPointHasTangent;
        // static System.Numerics.Vector3 checkingPoint;
        // static System.Numerics.Vector3 checkingPointNormal;
        // static void updateVertices() { /* ... */ }
        // static void autoBackUp() { /* ... */ }
        // static void ButtonTips(string tip, Control control) { /* ... */ }
        // static void exportJson(string content, string defaultFileName, string successMessage) { /* ... */ }

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
        static Button btnApplyChanges;

        static void dummies()
        {
            _selectedDummy = null;

            Form f = new Form();
            f.Text = "Dummy Editor";
            f.Size = new System.Drawing.Size(650, 700); // User-defined fixed size
            f.FormBorderStyle = FormBorderStyle.FixedSingle; // Prevent resizing if layout is truly fixed
            f.MaximizeBox = false;

            int padding = 10;
            int bottomPanelHeight = 50; // Increased slightly for button spacing

            // --- LEFT PANEL CONTAINER ---
            leftPanelContainer = new Panel();
            leftPanelContainer.Location = new System.Drawing.Point(padding, padding);
            leftPanelContainer.Size = new System.Drawing.Size(250, f.ClientSize.Height - bottomPanelHeight - 2 * padding);
            leftPanelContainer.BorderStyle = BorderStyle.FixedSingle; // Optional: for visual separation
            f.Controls.Add(leftPanelContainer);

            lbDummies = new ListBox();
            lbDummies.FormattingEnabled = true;
            lbDummies.Location = new System.Drawing.Point(padding, padding);
            // Calculate ListBox height to leave space for buttons below it inside leftPanelContainer
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

            // --- RIGHT PANEL (Editor) ---
            editorPanel = new Panel();
            editorPanel.Location = new System.Drawing.Point(leftPanelContainer.Right + padding, padding);
            editorPanel.Size = new System.Drawing.Size(
                f.ClientSize.Width - leftPanelContainer.Right - 2 * padding,
                leftPanelContainer.Height // Same height as left panel
            );
            editorPanel.AutoScroll = true;
            editorPanel.BorderStyle = BorderStyle.FixedSingle; // Optional
            SetupEditorControls(editorPanel); // Controls are positioned relative to editorPanel
            f.Controls.Add(editorPanel);


            // --- GLOBAL BUTTONS PANEL (Bottom) ---
            Panel bottomButtonsPanel = new Panel();
            bottomButtonsPanel.Location = new System.Drawing.Point(padding, leftPanelContainer.Bottom + padding);
            bottomButtonsPanel.Size = new System.Drawing.Size(f.ClientSize.Width - 2 * padding, bottomPanelHeight - padding);
            // bottomButtonsPanel.BorderStyle = BorderStyle.FixedSingle; // Optional
            f.Controls.Add(bottomButtonsPanel);

            int buttonYInBottomPanel = (bottomButtonsPanel.Height - 30) / 2; // Center buttons vertically

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

            // Initial population
            RefreshDummyList();
            EnableEditorControls(false);

            f.FormClosing += (s, e) => { useCheckingPoint = false; };
            f.ShowDialog();
        }

        private static void SetupEditorControls(Panel parentPanel)
        {
            int currentY = 10;
            int labelWidth = 100; // X position of control start
            int controlIndent = 10; // X position of labels
            int controlWidthStandard = 80;
            int spacing = 5;
            int tripletControlWidth = 60;
            int rowHeight = 25; // Height of one row (NUD + small gap)
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
            currentY += rowHeight + spacing + 10; // More space for apply button

            // Apply Button
            btnApplyChanges = new Button();
            ButtonTips("Apply changes to the selected dummy point and save to FLVER.\n应用并修改此Dummy点。", btnApplyChanges);
            btnApplyChanges.Text = "Apply Changes";
            btnApplyChanges.Size = new System.Drawing.Size(120, 30);
            // Center it horizontally in the panel
            btnApplyChanges.Location = new System.Drawing.Point(controlIndent, currentY);
            btnApplyChanges.Anchor = AnchorStyles.None; // Ensure it does not resize/move with parent scroll/resize
            btnApplyChanges.Click += BtnApplyChanges_Click;
            parentPanel.Controls.Add(btnApplyChanges);
        }

        private static Label AddLabel(Panel parent, string text, int xPos, int yPos)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new System.Drawing.Point(xPos, yPos + 3); // +3 for vertical alignment with NUD
            lbl.AutoSize = true;
            parent.Controls.Add(lbl);
            return lbl;
        }

        // Changed NumericUpDown parameters to decimal to match NUD properties directly
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

        // Overload for integer-based NUDs if preferred (like your original) for some cases
        private static NumericUpDown AddNumericUpDown(Panel parent, int xPos, int yPos, int width, int height,
                                         long min, long max, long increment, int decimalPlaces = 0)
        {
            return AddNumericUpDown(parent, xPos, yPos, width, height, (decimal)min, (decimal)max, (decimal)increment, decimalPlaces);
        }


        private static void RefreshDummyList()
        {
            int selectedIndex = lbDummies.SelectedIndex; // Preserve selection if possible
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
                EnableEditorControls(false); // No selection or selection out of bounds
                if (lbDummies.Items.Count > 0) lbDummies.SelectedIndex = 0; // Select first if exists
                else _selectedDummy = null; // Ensure _selectedDummy is null if list is empty
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
                checkingPoint = new System.Numerics.Vector3(_selectedDummy.Position.X, _selectedDummy.Position.Y, _selectedDummy.Position.Z);
                checkingPointNormal = new System.Numerics.Vector3(_selectedDummy.Forward.X * 0.2f, _selectedDummy.Forward.Y * 0.2f, _selectedDummy.Forward.Z * 0.2f);
                updateVertices();
            }
            else
            {
                _selectedDummy = null;
                EnableEditorControls(false);
                useCheckingPoint = false;
                updateVertices();
            }
        }

        private static void LoadDummyDataToEditor(FLVER.Dummy dummy)
        {
            if (dummy == null) return;

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

            nudColorR.Value = dummy.Color.R;
            nudColorG.Value = dummy.Color.G;
            nudColorB.Value = dummy.Color.B;
            nudColorA.Value = dummy.Color.A;

            chkFlag1.Checked = dummy.Flag1;
            chkUseUpward.Checked = dummy.UseUpwardVector;

            nudUnk30.Value = dummy.Unk30;
            nudUnk34.Value = dummy.Unk34;
        }

        private static void EnableEditorControls(bool enabled)
        {
            // Check if editorPanel itself is null (can happen if form setup fails)
            if (editorPanel == null) return;

            foreach (Control ctrl in editorPanel.Controls)
            {
                if (ctrl is NumericUpDown || ctrl is CheckBox)
                {
                    ctrl.Enabled = enabled;
                }
            }
            if (btnApplyChanges != null) // Check if btnApplyChanges is null
            {
                btnApplyChanges.Enabled = enabled;
            }
        }

        private static void BtnAddDummy_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;

            FLVER.Dummy newDummy = new FLVER.Dummy();
            targetFlver.Dummies.Add(newDummy);

            SaveFlverChanges("Dummy added.");
            RefreshDummyList(); // This will try to preserve selection or select first
            if (targetFlver.Dummies.Count > 0)
            {
                lbDummies.SelectedIndex = targetFlver.Dummies.Count - 1; // Explicitly select the new dummy
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
            // _selectedDummy will be updated by RefreshDummyList -> LbDummies_SelectedIndexChanged

            SaveFlverChanges("Dummy removed.");
            RefreshDummyList(); // This will handle re-selection or disabling controls

            // If list becomes empty, _selectedDummy will be null, controls disabled
            // If items remain, it will select a new item or keep current if valid
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
                // Use SoulsVector3 (SoulsFormats.Vector3) for these properties
                _selectedDummy.Position = new SoulsVector3((float)nudPosX.Value, (float)nudPosY.Value, (float)nudPosZ.Value);
                _selectedDummy.Forward = new SoulsVector3((float)nudForwardX.Value, (float)nudForwardY.Value, (float)nudForwardZ.Value);
                _selectedDummy.Upward = new SoulsVector3((float)nudUpwardX.Value, (float)nudUpwardY.Value, (float)nudUpwardZ.Value);

                _selectedDummy.ReferenceID = (short)nudRefID.Value;
                _selectedDummy.ParentBoneIndex = (short)nudParentBone.Value;
                _selectedDummy.AttachBoneIndex = (short)nudAttachBone.Value;

                // Corrected Color assignment: XnaColor constructor (R, G, B, A)
                _selectedDummy.Color = Color.FromArgb((byte)nudColorA.Value, (byte)nudColorR.Value, (byte)nudColorG.Value, (byte)nudColorB.Value);

                _selectedDummy.Flag1 = chkFlag1.Checked;
                _selectedDummy.UseUpwardVector = chkUseUpward.Checked;

                _selectedDummy.Unk30 = (int)nudUnk30.Value;
                _selectedDummy.Unk34 = (int)nudUnk34.Value;

                SaveFlverChanges("Dummy changes applied.");

                int preservedIndex = lbDummies.SelectedIndex;
                RefreshDummyList(); // This also calls LbDummies_SelectedIndexChanged if an item is selected
                if (preservedIndex >= 0 && preservedIndex < lbDummies.Items.Count)
                {
                    lbDummies.SelectedIndex = preservedIndex; // Re-assert selection to ensure visualizer updates
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // BtnImportJson_Click, BtnExportJson_Click, BtnSekiroFix_Click remain largely the same
        // but ensure RefreshDummyList() is called after modifications to targetFlver.Dummies.

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
                    serializer.MaxJsonLength = int.MaxValue;
                    targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(jsonContent);

                    SaveFlverChanges("Dummies imported from JSON.");
                    RefreshDummyList();
                    // _selectedDummy will be updated by RefreshDummyList
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
            exportJson(serializedResult, "Dummies.json", "Dummies exported to JSON successfully!");
        }

        private static void BtnSekiroFix_Click(object sender, EventArgs e)
        {
            if (targetFlver == null) return;
            try
            {
                string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dummyInfoPath = Path.Combine(assemblyPath, "dummyInfo.dll");

                if (!File.Exists(dummyInfoPath))
                {
                    MessageBox.Show($"Error: dummyInfo.dll not found at {dummyInfoPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string dummyStr = File.ReadAllText(dummyInfoPath);
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                List<FLVER.Dummy> refDummies = serializer.Deserialize<List<FLVER.Dummy>>(dummyStr);

                int dummiesAdded = 0;
                foreach (var refDummy in refDummies)
                {
                    if (!targetFlver.Dummies.Any(d => d.ReferenceID == refDummy.ReferenceID))
                    {
                        targetFlver.Dummies.Add(new FLVER.Dummy(refDummy));
                        dummiesAdded++;
                    }
                }

                if (dummiesAdded > 0)
                {
                    SaveFlverChanges($"{dummiesAdded} dummies added for Sekiro compatibility.");
                    RefreshDummyList();
                    // _selectedDummy will be updated by RefreshDummyList
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

            // updateVertices() is now primarily driven by LbDummies_SelectedIndexChanged
            // or explicitly after operations that clear selection and visualizer needs reset.
            // If _selectedDummy is null after an operation, ensure visualizer is cleared:
            if (_selectedDummy == null)
            {
                useCheckingPoint = false;
                updateVertices();
            }


            if (!string.IsNullOrEmpty(messageForResult))
            {
                // Console.WriteLine(messageForResult); 
            }
        }
    }
}