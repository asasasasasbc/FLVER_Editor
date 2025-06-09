// Program.UI.cs
using SoulsFormats; // Assuming this is the namespace for FLVER2, BND4, etc.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text;
using System.Web.Script.Serialization; // For JavaScriptSerializer
using System.Windows.Forms;

namespace MySFformat
{
    /// <summary>
    /// This partial class contains all the UI generation and handling logic for the model editor.
    /// It's combined with Program.cs at compile time.
    /// </summary>
    static partial class Program
    {
        public static List<DataGridViewTextBoxCell> boneNameCellList;
        public static List<DataGridViewTextBoxCell> boneParentCellList;
        public static List<DataGridViewTextBoxCell> boneChildCellList;

        public static TextBox tbones;
        public static TextBox tbones2;

        // Initial positioning
        static int WINDOW_WIDTH = 550;
        static int WINDOW_HEIGHT = 750;
        static int SIDE_PANEL_W = 150;
        // This is the main method that handles file loading and launches the editor form.
        static void ModelAdjModule()
        {
            string[] argments = Environment.GetCommandLineArgs(); // Get command line args correctly

            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                Title = "Choose fromsoftware .flver model file. by Forsaknsilver"
            };

            if (argments.Length > 1) // argments[0] is the executable path
            {
                openFileDialog1.FileName = argments[1];
            }
            else if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return; // User cancelled
            }

            orgFileName = openFileDialog1.FileName;
            string fname = openFileDialog1.FileName;
            FLVER2 b = null;

            // --- File Loading Logic ---
            if (fname.EndsWith(".dcx", StringComparison.OrdinalIgnoreCase))
            {
                b = LoadFlverFromBinder(openFileDialog1);
            }
            else
            {
                try
                {
                    b = FLVER2.Read(fname);
                    flverName = fname;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to read FLVER file directly.\n\nError: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (b == null)
            {
                // This can happen if the user cancels the binder selection dialog
                return;
            }

            targetFlver = b;

            // --- Launch 3D Viewer Thread ---
            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.CurrentThread.IsBackground = true;
                if (show3D)
                {
                    mono = new Mono3D();
                    updateVertices();
                    mono.Run();
                }
            }).Start();
            ShowEditorForm();
        }

        /// <summary>
        /// Handles the logic for reading a FLVER from a BND4 or DCX-compressed BND4.
        /// </summary>
        private static FLVER2 LoadFlverFromBinder(OpenFileDialog openFileDialog1)
        {
            BND4 bnds = null;
            try
            {
                bnds = SoulsFile<BND4>.Read(openFileDialog1.FileName);
            }
            catch (Exception)
            {
                Console.WriteLine("Not a direct BND4... Trying DCX decompression.");
                try
                {
                    byte[] bytes = DCX.Decompress(openFileDialog1.FileName);
                    if (BND4.Is(bytes))
                    {
                        bnds = SoulsFile<BND4>.Read(bytes);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to decompress or read binder file.\n\nError: {ex.Message}", "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }

            if (bnds == null)
            {
                MessageBox.Show("Could not read binder file.", "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            List<BinderFile> flverFiles = new List<BinderFile>();
            foreach (var bf in bnds.Files)
            {
                if (bf.Name.ToLower().Contains(".flver"))
                {
                    flverFiles.Add(bf);
                }
                else if (bf.Name.ToLower().EndsWith(".tpf") && loadTexture)
                {
                    try { targetTPF = TPF.Read(bf.Bytes); } catch { /* Ignore TPF read errors */ }
                }
            }

            if (flverFiles.Count == 0)
            {
                MessageBox.Show("No FLVER files found in the binder!", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            BinderFile selectedFile;
            if (flverFiles.Count == 1)
            {
                selectedFile = flverFiles[0];
            }
            else
            {
                // Let user choose from multiple flvers
                using (var cf = new Form { Size = new Size(520, 400), Text = "Select FLVER to View", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent })
                using (var lv = new ListBox { Size = new Size(490, 330), Location = new Point(10, 10) })
                using (var select = new Button { Text = "Select", Size = new Size(490, 20), Location = new Point(10, 340), DialogResult = DialogResult.OK })
                {
                    foreach (var bf in flverFiles) lv.Items.Add(bf.Name);
                    lv.SelectedIndex = 0;
                    cf.Controls.Add(lv);
                    cf.Controls.Add(select);
                    cf.AcceptButton = select;
                    if (cf.ShowDialog() != DialogResult.OK) return null;
                    selectedFile = flverFiles[lv.SelectedIndex];
                }
            }

            flverName = openFileDialog1.FileName + "." + Path.GetFileNameWithoutExtension(selectedFile.Name) + ".flver";
            return FLVER2.Read(selectedFile.Bytes);
        }

        /// <summary>
        /// Creates and displays the main editor window.
        /// </summary>
        static void ShowEditorForm()
        {
            var f = new Form
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = "FLVER Editor - " + Path.GetFileName(orgFileName),
                Size = new Size(800, 750),
                MinimumSize = new Size(640, 480),
                StartPosition = FormStartPosition.CenterScreen
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };

            // Create and add controls
            var menuStrip = CreateMenuStrip(serializer);
            var mainPanel = CreateMainPanel(serializer);
            var sidePanel = CreateSideButtonPanel(serializer);
            var thanksLabel = CreateThanksLabel();

            f.Controls.Add(mainPanel);
            f.Controls.Add(sidePanel);
            f.Controls.Add(thanksLabel);
            f.Controls.Add(menuStrip);
            f.MainMenuStrip = menuStrip;

            // Handle resizing
            f.Resize += (s, e) =>
            {
                mainPanel.Size = new Size(f.ClientSize.Width - sidePanel.Width - 15, f.ClientSize.Height - thanksLabel.Height - menuStrip.Height - 10);
                sidePanel.Location = new Point(f.ClientSize.Width - sidePanel.Width - 5, menuStrip.Height + 5);
                sidePanel.Height = f.ClientSize.Height - thanksLabel.Height - menuStrip.Height - 15;
                thanksLabel.Location = new Point(10, f.ClientSize.Height - thanksLabel.Height);
            };

            
            f.PerformLayout(); // Ensure controls are created
            f.Size = new Size(WINDOW_WIDTH+1, WINDOW_HEIGHT); // Trigger resize event once to position controls correctly
            f.Size = new Size(WINDOW_WIDTH, WINDOW_HEIGHT);

            // Bring to front and run
            f.Load += (s, e) => { f.Activate(); };
            Application.Run(f);
        }

        #region UI Control Creation

        private static MenuStrip CreateMenuStrip(JavaScriptSerializer serializer)
        {
            var menuStrip = new MenuStrip();

            // --- Legacy Menu ---
            var legacyMenu = new ToolStripMenuItem("【Legacy】");
            var swapItem = new ToolStripMenuItem("Swap", null, (s, e) => ModelSwapModule());
            swapItem.ToolTipText = "[Deprecated]Swap mesh & other info between one flver file with another. A new .flvern file will be generated.\n" +
                "It is a deprecated method, I recommend you using Mesh->Attach method instead.\n" +
"【过时】替换第一个Flver文件的模型信息为第二个，会生成一个.flvern文件。\n" +
"现在这个方法已经过时了请用Mesh->Attach方法！";
            var bbFixItem = new ToolStripMenuItem("BB_BoneFix", null, (s, e) => BB_BoneFix_Click());
            bbFixItem.ToolTipText = "Fix some bone problems when importing Bloodborne models to Sekiro.\n" +
"修复一些血源诅咒转只狼后模型骨骼不对的问题。";

            legacyMenu.DropDownItems.Add(swapItem);
            legacyMenu.DropDownItems.Add(bbFixItem);

            // --- About Menu ---
            var aboutMenu = new ToolStripMenuItem("【About】");
            var aboutItem = new ToolStripMenuItem("About this program...", null, (s, e) => {
                MessageBox.Show($"FLVER Editor {version}\nhttps://github.com/asasasasasbc/FLVER_Editor/releases\nAuthor: Forsakensilver (遗忘的银灵)\n\nSpecial thanks to:\nTKGP\nKatalash\n莫\nSoulsformatsNEXT", "About FLVER Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            aboutMenu.DropDownItems.Add(aboutItem);

            menuStrip.Items.Add(legacyMenu);
            menuStrip.Items.Add(aboutMenu);
            return menuStrip;
        }

        private static Panel CreateMainPanel(JavaScriptSerializer serializer)
        {
            var p = new Panel { Location = new Point(5, 30), AutoScroll = true };
            int currentY = 10;

            // DataGridView for Nodes
            var dg = CreateDataGridView(targetFlver);
            p.Controls.Add(dg);
            currentY += dg.Height + 10;

            // JSON TextBoxes
            p.Controls.Add(new Label { Text = "Nodes (JSON)", Location = new Point(10, currentY), AutoSize = true });
            currentY += 20;
            tbones = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(WINDOW_WIDTH - SIDE_PANEL_W + 30, 300),
                Location = new Point(10, currentY),
                Text = serializer.Serialize(targetFlver.Nodes)
            };
            p.Controls.Add(tbones);
            currentY += tbones.Height + 10;

            p.Controls.Add(new Label { Text = "Header (JSON)", Location = new Point(10, currentY), AutoSize = true });
            currentY += 20;
            tbones2 = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(WINDOW_WIDTH - SIDE_PANEL_W + 30, 150),
                Location = new Point(10, currentY),
                Text = serializer.Serialize(targetFlver.Header)
            };
            p.Controls.Add(tbones2);

            // Link textboxes to the ModifyJson button event
            // We find the button later, so we have to do it this way.
            tbones.Tag = tbones2; // Store reference to the second textbox

            // Resize handling for main panel content
            p.Resize += (s, e) => {
                dg.Width = p.ClientSize.Width - 20;
                //tbones.Width = p.ClientSize.Width - 20;
                //tbones2.Width = p.ClientSize.Width - 20;
            };

            return p;
        }

        private static DataGridView CreateDataGridView(FLVER2 b)
        {
            var dg = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Location = new Point(10, 10),
                Size = new Size(670, 350),
                RowHeadersVisible = false,
                MultiSelect = false
            };

            dg.Columns.Add("Index", "Index");
            dg.Columns[0].Width = 30;
            dg.Columns[0].ReadOnly = true;
            dg.Columns.Add("Name", "Node Name");
            dg.Columns[1].Width = 70;
            //dg.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dg.Columns.Add("ParentID", "ParentID");
            dg.Columns[2].Width = 30;
            dg.Columns.Add("ChildID", "ChildID");
            dg.Columns[3].Width = 30;
            dg.Columns.Add("Position", "Position");
            dg.Columns[4].Width = 200;
            dg.Columns.Add("Rotation", "Rotation");
            dg.Columns[5].Width = 200;
            dg.Columns.Add("Scale", "Scale");
            dg.Columns[6].Width = 200;
            foreach (DataGridViewColumn column in dg.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;

            if (!basicMode)
            {
                boneNameCellList = new List<DataGridViewTextBoxCell>();
                boneParentCellList = new List<DataGridViewTextBoxCell>();
                boneChildCellList = new List<DataGridViewTextBoxCell>();

                for (int i = 0; i < b.Nodes.Count; i++)
                {
                    FLVER.Node bn = b.Nodes[i];
                    int rowIndex = dg.Rows.Add();
                    DataGridViewRow row = dg.Rows[rowIndex];

                    row.Cells[0].Value = $"[{i}]";
                    row.Cells[1].Value = bn.Name;
                    row.Cells[2].Value = bn.ParentIndex.ToString();
                    row.Cells[3].Value = bn.FirstChildIndex.ToString();
                    // Translation: 格式化为小数点后3位
                    row.Cells[4].Value = $"{bn.Translation.X:F6}, {bn.Translation.Y:F6}, {bn.Translation.Z:F6}";

                    // Rotation: 先将弧度转为角度，再格式化为小数点后3位
                    const double radToDeg = 180.0 / Math.PI;
                    row.Cells[5].Value = $"{(bn.Rotation.X * radToDeg):F6}, {(bn.Rotation.Y * radToDeg):F6}, {(bn.Rotation.Z * radToDeg):F6}";

                    // Scale: 格式化为小数点后3位
                    row.Cells[6].Value = $"{bn.Scale.X:F6}, {bn.Scale.Y:F6}, {bn.Scale.Z:F6}";

                    boneNameCellList.Add((DataGridViewTextBoxCell)row.Cells[1]);
                    boneParentCellList.Add((DataGridViewTextBoxCell)row.Cells[2]);
                    boneChildCellList.Add((DataGridViewTextBoxCell)row.Cells[3]);
                }
            }
            return dg;
        }

        private static Panel CreateSideButtonPanel(JavaScriptSerializer serializer)
        {
            var sidePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = SIDE_PANEL_W,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10, 5, 10, 5) // Padding for the panel itself
            };
            var buttons = new List<Button>();
            var toolTip = new ToolTip();

            // Helper to create and add a button
            Button AddButton(string text, string tip, EventHandler onClick)
            {
                var btn = new Button
                {
                    Text = text,
                    Size = new Size(120, 30), // Fixed size for all buttons
                    Margin = new Padding(0, 10, 0, 10) // Vertical spacing! Height = 35};
                };
                if (!string.IsNullOrEmpty(tip)) toolTip.SetToolTip(btn, tip);
                btn.Click += onClick;
                buttons.Add(btn);
                return btn;
            }

            // --- Define Buttons ---
            if (!basicMode) AddButton("Modify", "Save the changes you made in the bones part.（Such as changing parents ID, bone names...）\n" +
                "保存你在Nodes部分做出的修改。(改骨骼名称以及父骨骼ID)", ModifyNodes_Click);



            AddButton("Material", "Open the material window.\n" +
                "打开材质编辑窗口。", (s, e) => ModelMaterial());
            AddButton("Mesh", "Open the mesh window.\n" +
    "打开面片编辑(Mesh)窗口。", (s, e) => ModelMesh());
            AddButton("Dummy", "Open the dummy window. Dummy contains the info about weapon art trail, weapon trail, damage point etc.\n" +
"打开辅助点(Dummy)窗口。辅助点包含了武器的一些剑风位置，伤害位置之类的信息。", (s, e) => dummies());
            AddButton("Modify JSON", "Save the json text you modified in bones and header json part to the flver file.\n" +
"存储你修改的Json文本信息至你的Flver文件内。", (s, e) => ModifyJson_Click(s, e, serializer));
            AddButton("Load JSON", "Read external bone json file.\n" +
"读取外部包含骨骼信息的json文件到你的flver文件内。", (s, e) => LoadJson_Click(s, e, serializer));
            AddButton("Export JSON", "Export bones json text to a file.\n" +
"导出当前骨骼信息到一个json文件内。", (s, e) => ExportJson_Click(s, e, serializer));
            AddButton("Buffer Layout", "Check the flver file's buffer layout, which contains the rules of how to write flver file.\n" +
"检查Flver文件的buffer layout（一种存储如何写入顶点，骨骼之类方法的数据结构）。", (s, e) => bufferLayout());
            AddButton("Import Model", "[May unstable in X2]Import external model file, such as FBX, DAE, OBJ. Caution, only FBX file can keep the bone weight.\n" +
                "UV, normal, tangent can be kept, but you still need to manually modify material information in Material window.\n" +
"【X2版可能不稳定】导入外部模型文件，比如Fbx,Dae,Obj。但注意只有Fbx文件可以支持导入骨骼权重。\n" +
"可以保留UV贴图坐标，切线法线的信息，但你还是得手动修改贴图信息的。\n", (s, e) => importFBX());
            AddButton("Export DAE", "Export current scene to DAE (Collada) 3d model file.\n" +
"导出场景至DAE模型文件。", (s, e) => ExportDAE());
            AddButton("Export FBX", "Export current bones/bone weights/scene to FBX 3d model file.\n" +
"导出场景（包含骨骼、权重等信息）至FBX模型文件。", (s, e) => ExportFBX());

            // Add buttons to panel in reverse order for correct docking
            //buttons.Reverse();
            sidePanel.Controls.AddRange(buttons.ToArray());

            return sidePanel;
        }

        private static Label CreateThanksLabel()
        {
            return new Label
            {
                Text = $"FLVER Editor {version} by Forsakensilver(遗忘的银灵) Special thanks: TKGP & Katalash & 莫 & SoulsformatsNEXT",
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25
            };
        }

        #endregion

        #region Event Handler Logic

        // --- Button Click Implementations ---
        private static void ModifyNodes_Click(object sender, EventArgs e)
        {
            if (boneNameCellList.Count != targetFlver.Nodes.Count)
            {
                MessageBox.Show("Node count mismatch. Cannot save grid changes.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            for (int i = 0; i < targetFlver.Nodes.Count; i++)
            {
                targetFlver.Nodes[i].Name = boneNameCellList[i].Value.ToString();
                targetFlver.Nodes[i].ParentIndex = short.Parse(boneParentCellList[i].Value.ToString());
                targetFlver.Nodes[i].FirstChildIndex = short.Parse(boneChildCellList[i].Value.ToString());
            }
            autoBackUp();
            targetFlver.Write(flverName);
            MessageBox.Show("Node modification finished.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ModifyJson_Click(object sender, EventArgs e, JavaScriptSerializer serializer)
        {
            //var btn = (Button)sender;

            try
            {
                targetFlver.Nodes = serializer.Deserialize<List<FLVER.Node>>(tbones.Text);
                targetFlver.Header = serializer.Deserialize<FLVER2.FLVERHeader>(tbones2.Text);
                autoBackUp();
                targetFlver.Write(flverName);
                MessageBox.Show("JSON modifications saved! Please restart the program to see all changes.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing JSON.\n\n{ex.Message}", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LoadJson_Click(object sender, EventArgs e, JavaScriptSerializer serializer)
        {
            using (var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string res = File.ReadAllText(openFileDialog.FileName);
                    var newNodes = serializer.Deserialize<List<FLVER.Node>>(res);

                    var confirmResult = MessageBox.Show("Shift bone weights to new bone indices?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirmResult == DialogResult.Yes)
                    {
                        BoneWeightShift(newNodes);
                    }

                    targetFlver.Nodes = newNodes;
                    autoBackUp();
                    targetFlver.Write(flverName);
                    MessageBox.Show("New bones loaded! Please restart the program to see all changes.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading or parsing JSON file.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void ExportJson_Click(object sender, EventArgs e, JavaScriptSerializer serializer)
        {
            exportJson(serializer.Serialize(targetFlver.Nodes), "Nodes.json", "Nodes JSON exported successfully!");
        }

        private static void BB_BoneFix_Click()
        {

            var confirmResult = MessageBox.Show("Do you want to set pelvis bone from BB to Sekiro style?",
                                 "Set",
                                 MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                // If 'Yes', do something here.
                for (int i = 0; i < targetFlver.Nodes.Count; i++)
                {
                    if (targetFlver.Nodes[i].Name == "Pelvis")
                    {
                        targetFlver.Nodes[i].Rotation = targetFlver.Nodes[i].Rotation + new Vector3(0, 0, (float)Math.PI);

                        for (int j = 0; j < targetFlver.Nodes.Count; j++)
                        {
                            if (targetFlver.Nodes[j].ParentIndex == i)
                            {

                                //It seems that X controls foot:left right rotate dir : -- means left,
                                //I recommend X -= 0.3
                                targetFlver.Nodes[j].Rotation += new Vector3(0, 0, (float)Math.PI);
                                targetFlver.Nodes[j].Translation *= -1; //it seems that y contorls forward/backward, 
                            }

                        }
                        break;
                    }


                }
            }
            else
            {
                // If 'No', do something here.
            }

            var confirmResult2 = MessageBox.Show("Do you want to modify foot bone to avoid mesh error at sekiro?",
                                "Set",
                                MessageBoxButtons.YesNo);
            if (confirmResult2 == DialogResult.Yes)
            {
                // If 'Yes', do something here.
                for (int i = 0; i < targetFlver.Nodes.Count; i++)
                {
                    if (targetFlver.Nodes[i].Name == "R_Foot" || targetFlver.Nodes[i].Name == "L_Foot")
                    {
                        targetFlver.Nodes[i].Translation += new Vector3(0.42884814f, 0, 0.02f);
                        targetFlver.Nodes[i].Rotation += new Vector3(0, -0.01f, 0);
                    }
                    else if (targetFlver.Nodes[i].Name == "R_Calf" || targetFlver.Nodes[i].Name == "L_Calf")
                    {
                        targetFlver.Nodes[i].Scale = new Vector3(1.1f, targetFlver.Nodes[i].Scale.Y, targetFlver.Nodes[i].Scale.Z);
                    }
                    else if (targetFlver.Nodes[i].Name == "R_Toe0" || targetFlver.Nodes[i].Name == "L_Toe0")
                    {
                        targetFlver.Nodes[i].Translation += new Vector3(0.02f, 0, 0);
                    }


                    //R_Calf

                }
            }
            else
            {
                // If 'No', do something here.
            }

            var confirmResult3 = MessageBox.Show("[OPTIONAL]Do you want to modify clavicle bones to change shoulder at sekiro?\r\nThis is eperiemental and may cause problems.",
                                "Set",
                                MessageBoxButtons.YesNo);
            if (confirmResult3 == DialogResult.Yes)
            {
                // If 'Yes', do something here.
                for (int i = 0; i < targetFlver.Nodes.Count; i++)
                {
                    FLVER.Node bone = targetFlver.Nodes[i];
                    if (bone.Name == "L_Clavicle")
                    {
                        bone.Translation += new Vector3(0, 0.05f, 0);

                    }
                    else if (targetFlver.Nodes[i].Name == "R_Clavicle")
                    {
                        bone.Translation -= new Vector3(0, 0.05f, 0);

                    }
                    else if (targetFlver.Nodes[i].Name == "L_UpperArm")
                    {
                        bone.Translation -= new Vector3(0, 0.05f, 0.02f);
                        bone.Scale = new Vector3(0.9f, bone.Scale.Y, bone.Scale.Z);
                    }
                    else if (targetFlver.Nodes[i].Name == "R_UpperArm")
                    {
                        bone.Translation += new Vector3(0, 0.05f, 0);
                        bone.Translation -= new Vector3(0, 0.05f, 0.02f);
                        bone.Scale = new Vector3(0.9f, bone.Scale.Y, bone.Scale.Z);
                    }
                    else if (targetFlver.Nodes[i].Name == "L_Forearm")
                    {
                        bone.Scale = new Vector3(1.1f, bone.Scale.Y, bone.Scale.Z);
                    }
                    else if (targetFlver.Nodes[i].Name == "R_Forearm")
                    {
                        bone.Scale = new Vector3(1.1f, bone.Scale.Y, bone.Scale.Z);
                    }
                    //R_Calf

                }
            }


            autoBackUp(); targetFlver.Write(flverName);

            MessageBox.Show("BB pelvis bone fix completed! Please exit the program!", "Info");
        }

        #endregion


    }
}
