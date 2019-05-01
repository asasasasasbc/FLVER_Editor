using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.UI;
using System.Web.Script.Serialization;


using SoulsFormats;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using ObjLoader.Loader.Loaders;

using Assimp;

namespace MySFformat
{
    static class Program
    {
        public static FLVER targetFlver;
        public static string flverName;
        public static List<TextBox> tList;
        public static List<TextBox> parentList;
        public static List<TextBox> childList;
        public static List<FLVER.Vertex> vertices = new List<FLVER.Vertex>();
        public static Mono3D mono;

        public static string orgFileName = "";

        public static Vector3 checkingPoint;
        public static Boolean useCheckingPoint = false;

        public static int checkingMeshNum = 0;
        public static Boolean useCheckingMesh = false;

        public static Boolean basicMode = false;

        public static string version = "1.42";

        public static string[] argments = { };
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            /* Application.EnableVisualStyles();
             Application.SetCompatibleTextRenderingDefault(false);
             Application.Run(new Form1());*/
            argments = args;

            Console.WriteLine("Hello!");
            ModelAdjModule();
        }
       public static  Microsoft.Xna.Framework.Vector3 toXnaV3(System.Numerics.Vector3 v)
        {

            return new Microsoft.Xna.Framework.Vector3(v.X,v.Y,v.Z);
        }

        public static Microsoft.Xna.Framework.Vector3 toXnaV3XZY(System.Numerics.Vector3 v)
        {

            return new Microsoft.Xna.Framework.Vector3(v.X, v.Z, v.Y);
        }

        public static void updateVertices()
        {
            List<VertexPositionColor> ans = new List<VertexPositionColor>();
            List<VertexPositionColor> triangles = new List<VertexPositionColor>();
            vertices.Clear();

            for (int i = 0; i < targetFlver.Meshes.Count; i++)
            {
                // int currentV = 0;
                //Microsoft.Xna.Framework.Vector3[] vl = new Microsoft.Xna.Framework.Vector3[3];
                if (targetFlver.Meshes[i] == null) { continue; }

                foreach (FLVER.Vertex[] vl in targetFlver.Meshes[i].GetFaces())
                {
                    Microsoft.Xna.Framework.Color cline = Microsoft.Xna.Framework.Color.Black;
                    if (useCheckingMesh && checkingMeshNum == i)
                    {
                        cline.G = 255;
                        cline.R = 255;
                    }
                    cline.A = 125;
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[0].Positions[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[1].Positions[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[0].Positions[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[2].Positions[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[1].Positions[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vl[2].Positions[0]), cline));

                    Microsoft.Xna.Framework.Color c = new Microsoft.Xna.Framework.Color();

                    Microsoft.Xna.Framework.Vector3 va = toXnaV3(vl[1].Positions[0]) - toXnaV3(vl[0].Positions[0]);
                    Microsoft.Xna.Framework.Vector3 vb = toXnaV3(vl[2].Positions[0]) - toXnaV3(vl[0].Positions[0]);
                    Microsoft.Xna.Framework.Vector3 vnromal = crossPorduct(va, vb);
                    vnromal.Normalize();
                    Microsoft.Xna.Framework.Vector3 light = new Microsoft.Xna.Framework.Vector3(mono.lightX, mono.lightY, mono.lightZ);
                    light.Normalize();
                    float theta = dotProduct(vnromal, light);
                    int value = 125 + (int)(125 * theta);
                    if (value > 255) { value = 255; }
                    if (value < 0) { value = 0; }
                    c.R = (byte)value;
                    c.G = (byte)value;
                    c.B = (byte)value;
                    c.A = 255;
                    if (useCheckingMesh && checkingMeshNum == i)
                    {
                        c.B = 0;
                    }
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[0].Positions[0]), c));
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[2].Positions[0]), c));
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[1].Positions[0]), c));
                }
                

                foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                {
                    vertices.Add(v);
                    /* if(v.Positions.Count >= 3)
                     {
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z), Microsoft.Xna.Framework.Color.Black));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[1].X, v.Positions[1].Y, v.Positions[1].Z), Microsoft.Xna.Framework.Color.Black));

                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z), Microsoft.Xna.Framework.Color.Black));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[2].X, v.Positions[2].Y, v.Positions[2].Z), Microsoft.Xna.Framework.Color.Black));

                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[1].X, v.Positions[1].Y, v.Positions[1].Z), Microsoft.Xna.Framework.Color.Black));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[2].X, v.Positions[2].Y, v.Positions[2].Z), Microsoft.Xna.Framework.Color.Black));
                     }*/
                    //for (int j = 0; j < v.Positions.Count; j++)
                    {
                        //ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Positions[j].X, v.Positions[j].Z, v.Positions[j].Y), Microsoft.Xna.Framework.Color.Black));

                       /* vl[currentV] = new Microsoft.Xna.Framework.Vector3(v.Positions[j].X, v.Positions[j].Z, v.Positions[j].Y);
                        currentV++;
                        if (currentV == 3)
                        {
                            ans.Add(new VertexPositionColor(vl[0], Microsoft.Xna.Framework.Color.Black));
                            ans.Add(new VertexPositionColor(vl[1], Microsoft.Xna.Framework.Color.Black));
                            ans.Add(new VertexPositionColor(vl[0], Microsoft.Xna.Framework.Color.Black));
                            ans.Add(new VertexPositionColor(vl[2], Microsoft.Xna.Framework.Color.Black));
                            ans.Add(new VertexPositionColor(vl[1], Microsoft.Xna.Framework.Color.Black));
                            ans.Add(new VertexPositionColor(vl[2], Microsoft.Xna.Framework.Color.Black));
                            currentV = 0;
                        }*/
                    }
                }
            
            }
            if (ans.Count % 2 != 0)
            {
                ans.Add(ans[ans.Count -1]);
            }

            /*for (int i = 0; i < targetFlver.Bones.Count; i++)
            {
                //targetFlver.Bones[i].Translation
                Vector3 actPos = findBoneTrans(targetFlver.Bones,i,new Vector3());
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X - 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z - 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z + 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z, actPos.Y - 0.025f), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z, actPos.Y + 0.025f), Microsoft.Xna.Framework.Color.Purple));
            }*/
            for (int i = 0; i < targetFlver.Dummies.Count; i++)
            {
                FLVER.Dummy d = targetFlver.Dummies[i];

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X -0.025f, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X + 0.025f, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X , d.Position.Z - 0.025f, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X , d.Position.Z + 0.025f, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Green));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X + d.Forward.X, d.Position.Z + d.Forward.Z, d.Position.Y + d.Forward.Y), Microsoft.Xna.Framework.Color.Green));

            }

            if (useCheckingPoint)
            {
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                useCheckingPoint = false;
            }
            useCheckingMesh = false;
            mono.vertices = ans.ToArray();

            mono.triVertices = triangles.ToArray();
        }

       

        public static Vector3 findBoneTrans(List<FLVER.Bone> b,int index,Vector3 v)
        {
            if (b[index].ParentIndex == -1)
            {
                v += b[index].Translation;
                return v;
            }
            return findBoneTrans(b, b[index].ParentIndex,v);
        }


        static void autoBackUp()
        {
           
            if(!File.Exists(orgFileName + ".bak"))
            {
                System.IO.File.Copy(orgFileName, orgFileName + ".bak", false);
            }



        }

        static void ModelAdjModule()
        {

            System.Windows.Forms.OpenFileDialog openFileDialog1;
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            openFileDialog1.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            openFileDialog1.Title = "Choose template seikiro model file. by Forsaknsilver";
            //openFileDialog1.ShowDialog();
            //MessageBox.Show("Import something?");

            if (argments.Length > 0)
            {
                openFileDialog1.FileName = argments[0];
                orgFileName = openFileDialog1.FileName;
            }
            else 
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //Console.WriteLine(openFileDialog1.FileName);
                //openFileDialog1.
                orgFileName = openFileDialog1.FileName;
            }
            else
            {
               /* Mono3D mono = new Mono3D();
                mono.Run();*/
                return;
            }
      


            FLVER b = FLVER.Read(openFileDialog1.FileName);

           

            targetFlver = b;
           
            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.CurrentThread.IsBackground = true;
                mono = new Mono3D();
                updateVertices();
                mono.Run();
                
            }).Start();
           


            flverName = openFileDialog1.FileName;

            Form f = new Form();
            f.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            f.Text = "FLVER Bones - " + openFileDialog1.FileName;
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            tList = new List<TextBox>();
            parentList = new List<TextBox>();
            childList = new List<TextBox>();
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);


            {
                Label l = new Label();
                l.Text = "index";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "name";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(70, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "parent";
                l.Size = new System.Drawing.Size(90, 15);
                l.Location = new System.Drawing.Point(270, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "child";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(360, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "position";
                l.Size = new System.Drawing.Size(90, 15);
                l.Location = new System.Drawing.Point(410, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "scale";
                l.Size = new System.Drawing.Size(70, 15);
                l.Location = new System.Drawing.Point(500, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "rotation";
                l.Size = new System.Drawing.Size(70, 15);
                l.Location = new System.Drawing.Point(590, currentY + 5);
                p.Controls.Add(l);
            }
            currentY += 20;

            if (b.Bones.Count > 80)
            {
                var confirmResult = MessageBox.Show("looks like there are too many bones, do you want the basic mode to save CPU resources?",
                                  "Caution",
                                  MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    basicMode = true;
                }
            }

            if(basicMode == false)
            for (int i = 0; i < b.Bones.Count; i++)
               
            {
                // foreach (FLVER.Bone bn in b.Bones)
                FLVER.Bone bn = b.Bones[i];
                //Console.WriteLine(bn.Name);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200,15);
                t.Location = new System.Drawing.Point(70,currentY);
                t.Text = bn.Name;
                p.Controls.Add(t);

                Label l = new Label();
                l.Text = "["+i + "]";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY+5);
                p.Controls.Add(l);

                TextBox t2 = new TextBox();
                t2.Size = new System.Drawing.Size(70, 15);
                t2.Location = new System.Drawing.Point(270, currentY);
                t2.Text = bn.ParentIndex + "";
                p.Controls.Add(t2);

                TextBox t3 = new TextBox();
                t3.Size = new System.Drawing.Size(70, 15);
                t3.Location = new System.Drawing.Point(340, currentY);
                t3.Text = bn.ChildIndex + "";
                p.Controls.Add(t3);

                //bn.Translation
                TextBox t4 = new TextBox();
                t4.Size = new System.Drawing.Size(90, 15);
                t4.Location = new System.Drawing.Point(410, currentY);
                t4.ReadOnly = true;
                t4.Text = bn.Translation.X + "," + bn.Translation.Y +"," + bn.Translation.Z ;
                p.Controls.Add(t4);

                TextBox t5 = new TextBox();
                t5.Size = new System.Drawing.Size(90, 15);
                t5.Location = new System.Drawing.Point(500, currentY);
                t5.ReadOnly = true;
                t5.Text = bn.Scale.X + "," + bn.Scale.Y + "," + bn.Scale.Z;
                p.Controls.Add(t5);

                TextBox t6 = new TextBox();
                t6.Size = new System.Drawing.Size(90, 15);
                t6.Location = new System.Drawing.Point(590, currentY);
                t6.ReadOnly = true;
                t6.Text = bn.Rotation.X + "," + bn.Rotation.Y + "," + bn.Rotation.Z;
                p.Controls.Add(t6);
                
                currentY += 20;
                sizeY += 20;
                tList.Add(t);
                parentList.Add(t2);
                childList.Add(t3);
            }
            
            f.Size = new System.Drawing.Size(550,650);
            p.Size = new System.Drawing.Size(400, 530);
            //f.Show();

            Button button = new Button();
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(435, 50);
            button.Click += (s, e) => {
                for (int i2 =0;i2 < b.Bones.Count;i2++)
                {
                    if (tList.Count < b.Bones.Count)
                    {
                        MessageBox.Show("Bone does not match, something modified?");
                        break;
                    }
                    b.Bones[i2].Name = tList[i2].Text;
                    b.Bones[i2].ParentIndex = short.Parse(parentList[i2].Text);
                    b.Bones[i2].ChildIndex = short.Parse(childList[i2].Text);
                }
                autoBackUp();targetFlver.Write(flverName);
                MessageBox.Show("Modification finished");
            };

            var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(b.Bones);

            {
                Label l = new Label();
                l.Text = "Bones Json text";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
                currentY += 20;
            }


            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY+5);
            tbones.Text = serializedResult;

            p.Controls.Add(tbones);

            currentY += 600;

            {
                Label l = new Label();
                l.Text = "Header Json text";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
                currentY += 20;
            }

            TextBox tbones2 = new TextBox();
            tbones2.Multiline = true;
            tbones2.Size = new System.Drawing.Size(670, 300);
            tbones2.Location = new System.Drawing.Point(10, currentY + 5);
            serializedResult = serializer.Serialize(b.Header);
            tbones2.Text = serializedResult;

            p.Controls.Add(tbones2);


            Button button2 = new Button();
            button2.Text = "Material";
            button2.Location = new System.Drawing.Point(435, 100);
            button2.Click += (s, e) => {
                ModelMaterial();
            };

            Button button3 = new Button();
            button3.Text = "Mesh";
            button3.Location = new System.Drawing.Point(435, 150);
            button3.Click += (s, e) => {
                ModelMesh();
            };
            Button button4 = new Button();
            button4.Text = "Swap";
            button4.Location = new System.Drawing.Point(435, 200);
            button4.Click += (s, e) => {
                ModelSwapModule();
            };

            Button button_dummy = new Button();
            button_dummy.Text = "Dummy";
            button_dummy.Location = new System.Drawing.Point(435, 250);
            button_dummy.Click += (s, e) => {
                dummies();
            };


            Button button5 = new Button();
           
            button5.Text = "ModifyJson";
            button5.Location = new System.Drawing.Point(435, 300);
            button5.Click += (s, e) => {
               b.Bones = serializer.Deserialize<List<FLVER.Bone>>(tbones.Text);
                b.Header = serializer.Deserialize<FLVER.FLVERHeader>(tbones2.Text);
                autoBackUp();targetFlver.Write(flverName);
                MessageBox.Show("Json bone change completed! Please exit the program!", "Info");
            };


            Button button6 = new Button();

            button6.Text = "LoadJson";
            button6.Location = new System.Drawing.Point(435, 350);
            button6.Click += (s, e) => {

                var openFileDialog2 = new OpenFileDialog();
                string res = "";
                if (openFileDialog2.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sr = new StreamReader(openFileDialog2.FileName);
                        res = sr.ReadToEnd();
                        sr.Close();
                        targetFlver.Bones = serializer.Deserialize<List<FLVER.Bone>>(res);
                        autoBackUp();targetFlver.Write(flverName);
                        MessageBox.Show("Bone change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }

                
            };

            Button button7 = new Button();

            button7.Text = "BB_BoneFix";
            button7.Location = new System.Drawing.Point(435, 400);
            button7.Click += (s, e) => {

                var confirmResult = MessageBox.Show("Do you want to set pelvis bone from BB to Sekiro style?",
                                     "Set",
                                     MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    // If 'Yes', do something here.
                    for (int i = 0; i < targetFlver.Bones.Count; i++)
                    {
                        if (targetFlver.Bones[i].Name == "Pelvis")
                        {
                            targetFlver.Bones[i].Rotation.Z = targetFlver.Bones[i].Rotation.Z + (float)Math.PI;

                            for (int j = 0; j < targetFlver.Bones.Count; j++)
                            {
                                if (targetFlver.Bones[j].ParentIndex == i)
                                {
                                    targetFlver.Bones[j].Rotation.Z = targetFlver.Bones[j].Rotation.Z + (float)Math.PI;
                                    targetFlver.Bones[j].Translation *= -1;
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
                    for (int i = 0; i < targetFlver.Bones.Count; i++)
                    {
                        if (targetFlver.Bones[i].Name == "R_Foot" || targetFlver.Bones[i].Name == "L_Foot")
                        {
                            targetFlver.Bones[i].Translation.X = 0.42884814f;
                            targetFlver.Bones[i].Translation.Z = 0.02f;

                            targetFlver.Bones[i].Rotation.Y -= 0.01f; 
                        } else if (targetFlver.Bones[i].Name == "R_Calf" || targetFlver.Bones[i].Name == "L_Calf")
                        {
                            targetFlver.Bones[i].Scale.X = 1.1f;
                        }
                        else if (targetFlver.Bones[i].Name == "R_Toe0" || targetFlver.Bones[i].Name == "L_Toe0")
                        {
                            targetFlver.Bones[i].Translation.X += 0.02f;
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
                    for (int i = 0; i < targetFlver.Bones.Count; i++)
                    {
                        FLVER.Bone bone = targetFlver.Bones[i];
                        if (bone.Name == "L_Clavicle")
                        {
                            bone.Translation.Y += 0.05f;

                        }
                        else if (targetFlver.Bones[i].Name == "R_Clavicle")
                        {
                            bone.Translation.Y -= 0.05f;

                        }
                        else if (targetFlver.Bones[i].Name == "L_UpperArm")
                        {
                            bone.Translation.Y -= 0.05f;
                            bone.Translation.Z -= 0.02f;
                            bone.Scale.X = 0.9f;
                        }
                        else if (targetFlver.Bones[i].Name == "R_UpperArm")
                        {
                            bone.Translation.Y += 0.05f;
                            bone.Translation.Z -= 0.02f;
                            bone.Scale.X = 0.9f;
                        }
                        else if (targetFlver.Bones[i].Name == "L_Forearm")
                        {
                            bone.Scale.X = 1.1f;
                        }
                        else if (targetFlver.Bones[i].Name == "R_Forearm")
                        {
                            bone.Scale.X = 1.1f;
                        }



                        //R_Calf

                    }
                }
              

                autoBackUp();targetFlver.Write(flverName);

                MessageBox.Show("BB pelvis bone fix completed! Please exit the program!", "Info");


            };

            Button button8 = new Button();
            
            button8.Text = "BufferLayout";
            button8.Font = new System.Drawing.Font(button.Font.FontFamily, 7);
            button8.Location = new System.Drawing.Point(435, 450);
            //button8.AutoSize = true;
            button8.Click += (s, e) => {

                bufferLayout();


            };

            Button button9 = new Button();

            button9.Text = "ImportModel";
            button9.Font = new System.Drawing.Font(button.Font.FontFamily, 8);
            //button9.AutoSize = true;
            button9.Location = new System.Drawing.Point(435, 500);
            button9.Click += (s, e) => {

                //importObj();
                importFBX();
            };

            Button button10 = new Button();

            button10.Text = "FBXimport";
            button10.Location = new System.Drawing.Point(435, 550);
            button10.Click += (s, e) => {

                importFBX();

            };

            Label thanks = new Label();
            thanks.Text = "FLVER Editor "+ version + " Author: Forsakensilver(遗忘的银灵) Special thanks: TKGP & Katalash ";
            thanks.Location = new System.Drawing.Point(10, f.Size.Height - 60);
            thanks.Size = new System.Drawing.Size(700,50);

            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                button.Location = new System.Drawing.Point(f.Size.Width - 115, 50);
                button2.Location = new System.Drawing.Point(f.Size.Width - 115, 100);
                button3.Location = new System.Drawing.Point(f.Size.Width - 115, 150);
                button4.Location = new System.Drawing.Point(f.Size.Width - 115, 200);
                button_dummy.Location = new System.Drawing.Point(f.Size.Width - 115, 250);
                button5.Location = new System.Drawing.Point(f.Size.Width - 115, 300);
                button6.Location = new System.Drawing.Point(f.Size.Width - 115, 350);
                button7.Location = new System.Drawing.Point(f.Size.Width - 115, 400);
                button8.Location = new System.Drawing.Point(f.Size.Width - 115, 450);
                button9.Location = new System.Drawing.Point(f.Size.Width - 115, 500);
                button10.Location = new System.Drawing.Point(f.Size.Width - 115, 550);

                thanks.Location = new System.Drawing.Point(10, f.Size.Height - 60);
            };
            p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);

            if (basicMode == false)
            f.Controls.Add(button);

            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(button4);
            f.Controls.Add(button_dummy);
            f.Controls.Add(button5);
            f.Controls.Add(button6);
            f.Controls.Add(button7);
            f.Controls.Add(button8);
            f.Controls.Add(button9);

            f.Controls.Add(thanks);
            //f.Controls.Add(button10);
            f.BringToFront();
            f.WindowState = FormWindowState.Normal;
            Application.Run(f);
            
           
            //ModelMaterial();
            //Application.Exit();
        }

        class VertexNormalList
        {
            public List<Vector3D> normals = new List<Vector3D>();
            public VertexNormalList()
            {
            }
            public Vector3D calculateAvgNormal()
            {
                Vector3D ans = new Vector3D();
                foreach (var n in normals)
                {
                    ans = ans + n;
                }
                return ans.normalize();
            }
            public void add(Vector3D a)
            {
                normals.Add(a);
            }

        }

        //Deprecated, cannot solve tangent properly.
        static void importObj()
        {
            var openFileDialog2 = new OpenFileDialog();
            string res = "";
            if (openFileDialog2.ShowDialog() == DialogResult.No)
            {
                    return;
            }
            res = openFileDialog2.FileName;
            var objLoaderFactory = new ObjLoaderFactory();
            MaterialStreamProvider msp = new MaterialStreamProvider();
            var openFileDialog3 = new OpenFileDialog();
            openFileDialog3.Title = "Choose MTL file:";
            if (openFileDialog3.ShowDialog() == DialogResult.No)
            {
                return;
            }

            msp.Open(openFileDialog3.FileName);
           var objLoader = objLoaderFactory.Create(msp);
            FileStream fileStream = new FileStream(res,FileMode.Open);
            LoadResult result = objLoader.Load(fileStream);

           

           // ObjLoader.Loader.Data.Elements.Face f = result.Groups[0].Faces[0];
           // ObjLoader.Loader.Data.Elements.FaceVertex[] fv =getVertices(f);

           // string groups = new JavaScriptSerializer().Serialize(fv);
           //string vertices = new JavaScriptSerializer().Serialize(result.Vertices);

           //MessageBox.Show(groups,"Group info");
           // MessageBox.Show(vertices, "V info");
           fileStream.Close();

            //Step 1 add a new buffer layout for my program:
            int layoutCount = targetFlver.BufferLayouts.Count;
            FLVER.BufferLayout newBL = new FLVER.BufferLayout();

            newBL.Add(new FLVER.BufferLayout.Member(0,0,FLVER.BufferLayout.MemberType.Float3,FLVER.BufferLayout.MemberSemantic.Position,0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 12, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Normal, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 16, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Tangent, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 20, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Tangent, 1));

            newBL.Add(new FLVER.BufferLayout.Member(0, 24, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.BoneIndices, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 28, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.BoneWeights, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 32, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.VertexColor, 1));
            newBL.Add(new FLVER.BufferLayout.Member(0, 36, FLVER.BufferLayout.MemberType.UVPair, FLVER.BufferLayout.MemberSemantic.UV, 0));

            targetFlver.BufferLayouts.Add(newBL);

            int materialCount = targetFlver.Materials.Count;




            FLVER.Mesh mn = new FLVER.Mesh();
            mn.MaterialIndex = 0;
            mn.BoneIndices = new List<int>();
            mn.BoneIndices.Add(0);
            mn.BoneIndices.Add(1);
            mn.BoundingBoxMax = new Vector3(1,1,1);
            mn.BoundingBoxMin = new Vector3(-1,-1,-1);
            mn.BoundingBoxUnk = new Vector3();
            mn.Unk1 = 0;
            mn.DefaultBoneIndex = 0;
            mn.Dynamic = false;
            mn.VertexBuffers = new List<FLVER.VertexBuffer>();
            mn.VertexBuffers.Add(new FLVER.VertexBuffer(0,layoutCount,-1));
            mn.Vertices = new List<FLVER.Vertex>();
            // mn.Vertices.Add(generateVertex(new Vector3(1,0,0),new Vector3(0,0,0),new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,0,0)));
            //mn.Vertices.Add(generateVertex(new Vector3(0, 1, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0)));
            //mn.Vertices.Add(generateVertex(new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0)));
            if (result.Groups.Count==0) {
                MessageBox.Show("You imported nothing!");
                return;
            }
            MessageBox.Show("Vertice number:" + result.Vertices.Count + "Texture V number:" + result.Textures.Count + "Normal number:" + result.Normals.Count + "Face groups:" + result.Groups[0].Faces.Count);

            VertexNormalList[] vnlist = new VertexNormalList[result.Vertices.Count + 1];
            for (int i = 0; i < vnlist.Length; i++)
            {
                vnlist[i] = new VertexNormalList();
            }

            List<uint> faceIndexs = new List<uint>();
            uint[] textureIndexs = new uint[result.Vertices.Count + 1];
            foreach (var gr in result.Groups)
            {
                
                foreach (var faces in gr.Faces)
                {
                    
                    var vList = getVertices(faces);
                    /*for (int i3 = 0; i3 < vList.Length - 2; i3++)
                    {
                        faceIndexs.Add((uint)(vList[i3].VertexIndex)-1);
                        faceIndexs.Add((uint)(vList[i3+1].VertexIndex)-1);
                        faceIndexs.Add((uint)(vList[i3+2].VertexIndex)-1);
                    }*/
                    if (vList.Length == 4)
                    {
                        faceIndexs.Add((uint)(vList[0].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[2].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[1].VertexIndex) - 1);
                        
                        //record normal to help calculate vertex normals
                        int helperI = 0;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 2;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 1;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);


                        faceIndexs.Add((uint)(vList[2].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[0].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[3].VertexIndex) - 1);

                       helperI = 2;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 0;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex].Y, result.Normals[vList[helperI].NormalIndex].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 3;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex].X, result.Normals[vList[helperI].NormalIndex].Y, result.Normals[vList[helperI].NormalIndex].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                    }
                     else if (vList.Length == 3)
                    {
                        
                        faceIndexs.Add((uint)(vList[0].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[2].VertexIndex) - 1);
                        faceIndexs.Add((uint)(vList[1].VertexIndex) - 1);


                        int helperI = 2;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 0;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);

                        helperI = 1;
                        vnlist[(uint)(vList[helperI].VertexIndex) - 1].add(new Vector3D(result.Normals[vList[helperI].NormalIndex - 1].X, result.Normals[vList[helperI].NormalIndex - 1].Y, result.Normals[vList[helperI].NormalIndex - 1].Z));
                        textureIndexs[(vList[helperI].VertexIndex) - 1] = ((uint)vList[helperI].TextureIndex - 1);
                    }
                }
               

            }
            //mn.FaceSets[0].Vertices = new uint [3]{0,1,2 };


            mn.FaceSets = new List<FLVER.FaceSet>();
            //FLVER.Vertex myv = new FLVER.Vertex();
            //myv.Colors = new List<FLVER.Vertex.Color>();
            mn.FaceSets.Add(generateBasicFaceSet());
            mn.FaceSets[0].Vertices = faceIndexs.ToArray();



            //Set all the vertices.
            for (int iv = 0; iv < result.Vertices.Count; iv++)
            {
                var v = result.Vertices[iv];

                Vector3 uv1 = new Vector3();
                Vector3 uv2 = new Vector3();
                Vector3 normal = new Vector3(0,1,0);
                Vector3 tangent = new Vector3(1,0,0);
                if (result.Textures != null)
                {
                    if (iv < result.Textures.Count)
                    {
                        
                        var vm = result.Textures[(int)textureIndexs[  iv ] ];
                        uv1 = new Vector3(vm.X, vm.Y, 0);
                        uv2 = new Vector3(vm.X, vm.Y, 0);
                    }
                }
                normal = vnlist[iv].calculateAvgNormal().toNumV3();
                tangent = RotatePoint(normal,0,(float)Math.PI / 2,0);
                mn.Vertices.Add(generateVertex(new Vector3(v.X, v.Y, v.Z), uv1, uv2, normal, tangent));

            }
            FLVER.Material matnew = new JavaScriptSerializer().Deserialize<FLVER.Material>(new JavaScriptSerializer().Serialize(targetFlver.Materials[0]));
            matnew.Name = res.Substring( res.LastIndexOf('\\') + 1); 
            targetFlver.Materials.Add(matnew);
            mn.MaterialIndex = materialCount;


            targetFlver.Meshes.Add(mn);
            MessageBox.Show("Added a custom mesh! PLease click modify to save it!");
            updateVertices();
            //mn.Vertices.Add();
        }


        //Current, best version can load FBX, OBJ, DAE etc.
        //Use assimp library
        static void importFBX()
        {
            AssimpContext importer = new AssimpContext();

           // importer.SetConfig(new NormalSmoothingAngleConfig(66.0f));
          
            //m_model.Meshes[0].Bones[0].VertexWeights[0].
            var openFileDialog2 = new OpenFileDialog();
            string res = "";
            if (openFileDialog2.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            res = openFileDialog2.FileName;
            
            Scene md = importer.ImportFile(res, PostProcessSteps.CalculateTangentSpace);// PostProcessPreset.TargetRealTimeMaximumQuality

            MessageBox.Show("Meshes count:" + md.Meshes.Count + "Material count:" + md.MaterialCount + "");

            //First, added a custom default layout.
            int layoutCount = targetFlver.BufferLayouts.Count;
            FLVER.BufferLayout newBL = new FLVER.BufferLayout();

            newBL.Add(new FLVER.BufferLayout.Member(0, 0, FLVER.BufferLayout.MemberType.Float3, FLVER.BufferLayout.MemberSemantic.Position, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 12, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Normal, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 16, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Tangent, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 20, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.Tangent, 1));

            newBL.Add(new FLVER.BufferLayout.Member(0, 24, FLVER.BufferLayout.MemberType.Byte4B, FLVER.BufferLayout.MemberSemantic.BoneIndices, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 28, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.BoneWeights, 0));
            newBL.Add(new FLVER.BufferLayout.Member(0, 32, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.VertexColor, 1));
            newBL.Add(new FLVER.BufferLayout.Member(0, 36, FLVER.BufferLayout.MemberType.UVPair, FLVER.BufferLayout.MemberSemantic.UV, 0));

            targetFlver.BufferLayouts.Add(newBL);

            int materialCount = targetFlver.Materials.Count;

            foreach (var mat in md.Materials)
            {
                FLVER.Material matnew = new JavaScriptSerializer().Deserialize<FLVER.Material>(new JavaScriptSerializer().Serialize(targetFlver.Materials[0]));
                matnew.Name = res.Substring(res.LastIndexOf('\\') + 1) + "_" + mat.Name;
                targetFlver.Materials.Add(matnew);
            }


            //mn.MaterialIndex = materialCount;

            foreach (var m  in md.Meshes)
            {
                MessageBox.Show("Name:" + m.Name + "\nHas bones:" + m.HasBones + "\nHas normal:" + m.HasNormals + "\nHas tangent" + m.HasTangentBasis +
                    "\nVrtices count: " + m.VertexCount
                    );

                FLVER.Mesh mn = new FLVER.Mesh();
                mn.MaterialIndex = 0;
                mn.BoneIndices = new List<int>();
                mn.BoneIndices.Add(0);
                mn.BoneIndices.Add(1);
                mn.BoundingBoxMax = new Vector3(1, 1, 1);
                mn.BoundingBoxMin = new Vector3(-1, -1, -1);
                mn.BoundingBoxUnk = new Vector3();
                mn.Unk1 = 0;
                mn.DefaultBoneIndex = 0;
                mn.Dynamic = true;
                mn.VertexBuffers = new List<FLVER.VertexBuffer>();
                mn.VertexBuffers.Add(new FLVER.VertexBuffer(0, layoutCount, -1));
                mn.Vertices = new List<FLVER.Vertex>();

                List<List<int>> verticesBoneIndices = new List<List<int>>();
                List<List<float>> verticesBoneWeights = new List<List<float>>();
                

                //If it has bones, then record the bone weight info
                if (m.HasBones)
                {
                    for (int i2 = 0; i2 < m.VertexCount; i2++)
                    {
                        verticesBoneIndices.Add(new List<int>());
                        verticesBoneWeights.Add( new List<float>());
                    }

                    for (int i2 = 0;i2 < m.BoneCount;i2++)
                    {
                        int boneInex = findFLVER_Bone(targetFlver,m.Bones[i2].Name);
                        for (int i3 = 0;i3 < m.Bones[i2].VertexWeightCount;i3++)
                        {
                            var vw =m.Bones[i2].VertexWeights[i3];

                             verticesBoneIndices[vw.VertexID].Add(boneInex);
                             verticesBoneWeights[vw.VertexID].Add(vw.Weight);
                        }


                    }

                }

               // m.Bones[0].VertexWeights[0].
                for (int i =0; i < m.Vertices.Count;i++)
                {
                    var vit = m.Vertices[i];
                    //m.TextureCoordinateChannels[0]
                    var channels = m.TextureCoordinateChannels[0];

                    var uv1 = new Vector3D();
                    var uv2 = new Vector3D();

                    if (channels != null)
                    {
                        
                            uv1 =getMyV3D( channels[i]);
                        uv1.Y = 1 - uv1.Y;
                            uv2 = getMyV3D(channels[i]);
                        uv2.Y = 1 - uv2.Y;
                        if (m.TextureCoordinateChannelCount > 1)
                        {
                           // uv2 = getMyV3D((m.TextureCoordinateChannels[1])[i]);
                        }
                    }
                    
                    var normal = getMyV3D( m.Normals[i] ).normalize();
                    //Vector3D tangent = new Vector3D( crossPorduct( getMyV3D(m.Tangents[i]).normalize().toXnaV3() , normal.toXnaV3())).normalize();
                    //var tangent = RotatePoint(normal.toNumV3(), 0, (float)Math.PI / 2, 0);
                    var tangent = getMyV3D(m.Tangents[i]).normalize();
                    FLVER.Vertex v = generateVertex(new Vector3(vit.X, vit.Y, vit.Z), uv1.toNumV3(), uv2.toNumV3(), normal.toNumV3(), tangent.toNumV3(), 1);

                    if (m.HasBones) { 
                        for (int j = 0; j < verticesBoneIndices[i].Count && j < 4;j++)
                          {
                               v.BoneIndices[j] = (verticesBoneIndices[i])[j];
                               v.BoneWeights[j] = (verticesBoneWeights[i])[j];
                          }
                    }
                    mn.Vertices.Add(v);
                }



                List<uint> faceIndexs = new List<uint>();
                for (int i =0; i< m.FaceCount;i++)
                {
                    if (m.Faces[i].Indices.Count == 3)
                    {
                        faceIndexs.Add((uint)m.Faces[i].Indices[0]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[2]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[1]);
                    } else if (m.Faces[i].Indices.Count == 4)
                    {
                        faceIndexs.Add((uint)m.Faces[i].Indices[0]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[2]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[1]);

                        faceIndexs.Add((uint)m.Faces[i].Indices[2]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[0]);
                        faceIndexs.Add((uint)m.Faces[i].Indices[3]);
                    }

                }
                //
                mn.FaceSets = new List<FLVER.FaceSet>();
                //FLVER.Vertex myv = new FLVER.Vertex();
                //myv.Colors = new List<FLVER.Vertex.Color>();
                mn.FaceSets.Add(generateBasicFaceSet());
                mn.FaceSets[0].Vertices = faceIndexs.ToArray();

                mn.MaterialIndex = materialCount + m.MaterialIndex;


                targetFlver.Meshes.Add(mn);
            }

            MessageBox.Show("Added a custom mesh! PLease click modify to save it!");
            updateVertices();
        }

        static Vector3D getMyV3D(Assimp.Vector3D v)
        {
            return new Vector3D(v.X,v.Y,v.Z);
        }

        static FLVER.FaceSet generateBasicFaceSet()
        {
            FLVER.FaceSet ans = new FLVER.FaceSet();
            ans.CullBackfaces = true;
            ans.TriangleStrip = false;
            ans.Unk06 = 1;
            ans.Unk07 = 0;
            ans.IndexSize = 16;
            
            return ans;

        }

        static FLVER.Vertex generateVertex(Vector3 pos,Vector3 uv1,Vector3 uv2,Vector3 normal,Vector3 tangets, int tangentW = -1)
        {
            FLVER.Vertex ans = new FLVER.Vertex();
            ans.Positions = new List<Vector3>();
            ans.Positions.Add(pos);
            ans.BoneIndices = new int[4] { 0,0,0,0};
            ans.BoneWeights = new float[4] { 1, 0, 0, 0 };
            ans.UVs = new List<Vector3>();
            ans.UVs.Add(uv1);
            ans.UVs.Add(uv2);
            ans.Normals = new List<Vector4>();
            ans.Normals.Add(new Vector4(normal.X,normal.Y,normal.Z,-1f));
            ans.Tangents = new List<Vector4>();
            ans.Tangents.Add(new Vector4(tangets.X, tangets.Y, tangets.Z, tangentW));
            ans.Tangents.Add(new Vector4(tangets.X, tangets.Y, tangets.Z, tangentW));
            ans.Colors = new List<FLVER.Vertex.Color>();
            ans.Colors.Add(new FLVER.Vertex.Color(255,255,255,255));
           
            return ans;
        }


        static FLVER.Vertex generateVertex2tan(Vector3 pos, Vector3 uv1, Vector3 uv2, Vector3 normal, Vector3 tangets, Vector3 tangets2, int tangentW = -1)
        {
            FLVER.Vertex ans = new FLVER.Vertex();
            ans.Positions = new List<Vector3>();
            ans.Positions.Add(pos);
            ans.BoneIndices = new int[4] { 0, 0, 0, 0 };
            ans.BoneWeights = new float[4] { 1, 0, 0, 0 };
            ans.UVs = new List<Vector3>();
            ans.UVs.Add(uv1);
            ans.UVs.Add(uv2);
            ans.Normals = new List<Vector4>();
            ans.Normals.Add(new Vector4(normal.X, normal.Y, normal.Z, -1f));
            ans.Tangents = new List<Vector4>();
            ans.Tangents.Add(new Vector4(tangets.X, tangets.Y, tangets.Z, tangentW));
            ans.Tangents.Add(new Vector4(tangets2.X, tangets2.Y, tangets2.Z, tangentW));
            ans.Colors = new List<FLVER.Vertex.Color>();
            ans.Colors.Add(new FLVER.Vertex.Color(255, 255, 255, 255));

            return ans;
        }


        static ObjLoader.Loader.Data.Elements.FaceVertex[] getVertices(ObjLoader.Loader.Data.Elements.Face f)
        {
            ObjLoader.Loader.Data.Elements.FaceVertex[] ans = new ObjLoader.Loader.Data.Elements.FaceVertex[f.Count];
            for (int i =0; i < f.Count;i++)
            {
                ans[i] = f[i];
            }
            return ans;
        }

        static void dummies()
        {
            Form f = new Form();
            f.Text = "Dummies";
            Panel p = new Panel();
            int currentY2 = 10;
            p.AutoScroll = true;
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dummyStr =  File.ReadAllText(assemblyPath + "\\dummyInfo.dll");
            List<FLVER.Dummy> refDummy = new JavaScriptSerializer().Deserialize<List<FLVER.Dummy>>(dummyStr); 

            //Console.WriteLine(dummyStr);

            f.Controls.Add(p);
            {
                Label l = new Label();
                l.Text = "Choose # to translate:";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(10, currentY2 + 5);
                p.Controls.Add(l);
            }
            currentY2 += 20;

            TextBox t = new TextBox();
            t.Size = new System.Drawing.Size(60, 15);
            t.Location = new System.Drawing.Point(10, currentY2 +5);
            t.Text = "-1";
            p.Controls.Add(t);


            TextBox tref = new TextBox();
            ;
            tref.Size = new System.Drawing.Size(100, 15);
            tref.Location = new System.Drawing.Point(150, currentY2 + 5);
            tref.Text = "";
            p.Controls.Add(tref);


            Button buttonCheck = new Button();
            buttonCheck.Text = "Check";
            buttonCheck.Location = new System.Drawing.Point(70, currentY2 + 5);
            buttonCheck.Click += (s, e) => {
                int i = int.Parse(t.Text);
                if (i >= 0 && i < targetFlver.Dummies.Count)
                {

                    useCheckingPoint = true;
                    checkingPoint = new Vector3(targetFlver.Dummies[i].Position.X, targetFlver.Dummies[i].Position.Y, targetFlver.Dummies[i].Position.Z);

                    tref.Text = "RefID:" + targetFlver.Dummies[i].ReferenceID;
                    updateVertices();
                }
                else
                {

                    MessageBox.Show("Invalid modification value!");
                }

            };
            p.Controls.Add(buttonCheck);


            currentY2 += 20;

            TextBox tX = new TextBox();
            tX.Size = new System.Drawing.Size(60, 15);
            tX.Location = new System.Drawing.Point(10, currentY2+5);
            tX.Text = "0";
            p.Controls.Add(tX);


            TextBox tY = new TextBox();
            tY.Size = new System.Drawing.Size(60, 15);
            tY.Location = new System.Drawing.Point(70, currentY2+5);
            tY.Text = "0";
            p.Controls.Add(tY);

            TextBox tZ = new TextBox();
            tZ.Size = new System.Drawing.Size(60, 15);
            tZ.Location = new System.Drawing.Point(130, currentY2+5);
            tZ.Text = "0";
            p.Controls.Add(tZ);


            currentY2 += 20;


            var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(targetFlver.Dummies);


            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY2 + 20);
            tbones.Text = serializedResult;

            p.Controls.Add(tbones);

            Button button = new Button();
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {
                int i = int.Parse(t.Text);
                if (i >= 0 && i < targetFlver.Dummies.Count)
                {

                    targetFlver.Dummies[i].Position.X += float.Parse(tX.Text);
                    targetFlver.Dummies[i].Position.Y += float.Parse(tY.Text);
                    targetFlver.Dummies[i].Position.Z += float.Parse(tZ.Text);
                    autoBackUp();targetFlver.Write(flverName);
                    updateVertices();
                }
                else {

                    MessageBox.Show("Invalid modification value!");
                }
                
            };


            Button button2 = new Button();

            button2.Text = "JsonMod";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {
                targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(tbones.Text);
                autoBackUp();targetFlver.Write(flverName);
                updateVertices();
                MessageBox.Show("Dummy change completed! Please exit the program!", "Info");
            };

            Button button3 = new Button();

            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                var openFileDialog1 = new OpenFileDialog();
                string res = "";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sr = new StreamReader(openFileDialog1.FileName);
                        res = sr.ReadToEnd();
                        sr.Close();
                        targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(res);
                        autoBackUp();targetFlver.Write(flverName);
                        updateVertices();
                        MessageBox.Show("Dummy change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }


            };

            Button buttonFix = new Button();

            buttonFix.Text = "SekiroFix";
            buttonFix.Location = new System.Drawing.Point(650, 200);
            buttonFix.Click += (s, e) => {


                        // targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(res);
                        //autoBackUp();targetFlver.Write(flverName);
                        for (int i =0;i < refDummy.Count;i++)
                        {
                            for (int j =0; j < targetFlver.Dummies.Count;j++)
                            {
                                if (targetFlver.Dummies[j].ReferenceID == refDummy[i].ReferenceID)
                                {
                                    break;
                                }
                                else if (j == targetFlver.Dummies.Count -1)
                                {

                                    targetFlver.Dummies.Add(refDummy[i]);
                                    break;
                                }
                            }

                        }
                        autoBackUp();targetFlver.Write(flverName);

                        updateVertices();
                        MessageBox.Show("Dummy change fixed! Please exit the program!", "Info");
                    
                   
              


            };

            f.Size = new System.Drawing.Size(750, 600);
            p.Size = new System.Drawing.Size(600, 530);
            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
                buttonFix.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
            };

            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(buttonFix);
            f.ShowDialog();
        }

        static int findFLVER_Bone(FLVER f,string name)
        {
            for (int flveri = 0; flveri < f.Bones.Count;flveri ++)
            {
                if (f.Bones[flveri].Name == name)
                {

                    return flveri;

                }

            }
            return 0;
        }

        static void bufferLayout()
        {
            Form f = new Form();
            f.Text = "Layout";
            Panel p = new Panel();
            int currentY2 = 10;
            p.AutoScroll = true;
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dummyStr = File.ReadAllText(assemblyPath + "\\dummyInfo.dll");
           // List<FLVER.Dummy> refDummy = new JavaScriptSerializer().Deserialize<List<FLVER.Dummy>>(dummyStr);

            //Console.WriteLine(dummyStr);

            f.Controls.Add(p);
            {
                Label l = new Label();
                l.Text = "Buffer Layout text:";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(10, currentY2 + 5);
                p.Controls.Add(l);
            }
            currentY2 += 20;

           



            var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(targetFlver.BufferLayouts);


            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY2 + 20);
            tbones.Text = serializedResult;

            p.Controls.Add(tbones);

            Button button = new Button();
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {
               

            };


            Button button2 = new Button();

            button2.Text = "JsonMod";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {
                targetFlver.BufferLayouts = serializer.Deserialize<List<FLVER.BufferLayout>>(tbones.Text);
                autoBackUp();targetFlver.Write(flverName);
                updateVertices();
                MessageBox.Show("Dummy change completed! Please exit the program!", "Info");
            };

            Button button3 = new Button();

            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                var openFileDialog1 = new OpenFileDialog();
                string res = "";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sr = new StreamReader(openFileDialog1.FileName);
                        res = sr.ReadToEnd();
                        sr.Close();
                        targetFlver.BufferLayouts = serializer.Deserialize<List<FLVER.BufferLayout>>(res);
                        autoBackUp();targetFlver.Write(flverName);
                        updateVertices();
                        MessageBox.Show("Dummy change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }


            };

          

            f.Size = new System.Drawing.Size(750, 600);
            p.Size = new System.Drawing.Size(600, 530);
            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);

            };

            //f.Controls.Add(button);
            //f.Controls.Add(button2);
           // f.Controls.Add(button3);
  
            f.ShowDialog();
        }

        static void ModelMaterial() {

            Form f = new Form();
            f.Text = "Material";
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            tList = new List<TextBox>();
            parentList = new List<TextBox>();
            childList = new List<TextBox>();
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);


            {
                Label l = new Label();
                l.Text = "index";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "name";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(70, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "type";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(270, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "texture path";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(340, currentY + 5);
                p.Controls.Add(l);
            }
            currentY += 20;

            
            for (int i = 0; i < targetFlver.Materials.Count; i++)

            {
                // foreach (FLVER.Bone bn in b.Bones)
                FLVER.Material bn = targetFlver.Materials[i];
                //Console.WriteLine(bn.Name);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200, 15);
                t.Location = new System.Drawing.Point(70, currentY);
                t.Text = bn.Name;
                p.Controls.Add(t);

                Label l = new Label();
                l.Text = "[" + i + "]";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);

                TextBox t2 = new TextBox();
                t2.Size = new System.Drawing.Size(70, 15);
                t2.Location = new System.Drawing.Point(270, currentY);
                t2.Text =bn.Flags + ",GX" + bn.GXBytes + ",Unk" + bn.Unk18;
                p.Controls.Add(t2);

                TextBox t3 = new TextBox();
                t3.Size = new System.Drawing.Size(770, 15);
                t3.Location = new System.Drawing.Point(340, currentY);
                string allMat = "";
                foreach (FLVER.Texture tex in bn.Textures)
                {
                   /* if (tex.Type == "g_DiffuseTexture")
                    {
                        tex.Type = "Character_AMSN_snp_Texture2D_2_AlbedoMap_0";
                    }
                    if (tex.Type == "g_BumpmapTexture")
                    {
                        tex.Type = "Character_AMSN_snp_Texture2D_7_NormalMap_4";
                    }
                    if (tex.Type == "g_SpecularTexture")
                    {
                        tex.Type = "Character_AMSN_snp_Texture2D_0_ReflectanceMap_0";
                    }
                    if (tex.Type == "g_ShininessTexture")
                    {
                        tex.Type = "Character_AMSN_snp_Texture2D_0_ReflectanceMap_0";
                    }*/

                    allMat += "{" + tex.Type + "->" + tex.Path + "," + tex.Unk10 + "," +tex.Unk11 + "," + tex.Unk14 + "," + tex.Unk18 + "," + tex.Unk1C + "}";
                }
                t3.Text = allMat;
                p.Controls.Add(t3);

                currentY += 20;
                sizeY += 20;
                /*tList.Add(t);
                parentList.Add(t2);
                childList.Add(t3);*/
            }
         
          /*  var xmlserializer = new XmlSerializer(typeof( List<FLVER.Material>));
            var stringWriter = new StringWriter();
            string res;
            using (var writer = XmlWriter.Create(stringWriter))
            {
                xmlserializer.Serialize(writer, targetFlver.Materials);
              res = stringWriter.ToString();
            }*/


           var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(targetFlver.Materials);


            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY + 20);
            tbones.Text = serializedResult;

            p.Controls.Add(tbones);

            Button button = new Button();
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {
              /*  for (int i = 0; i < b.Bones.Count; i++)
                {
                    b.Bones[i].Name = tList[i].Text;
                    b.Bones[i].ParentIndex = short.Parse(parentList[i].Text);
                    b.Bones[i].ChildIndex = short.Parse(childList[i].Text);
                }*/
                autoBackUp();targetFlver.Write(flverName);
            };


            Button button2 = new Button();

            button2.Text = "ModifyJson";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {
                targetFlver.Materials = serializer.Deserialize<List<FLVER.Material>>(tbones.Text);
                autoBackUp();targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");
            };

            Button button3 = new Button();

            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                var openFileDialog1 = new OpenFileDialog();
                string res = "";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sr = new StreamReader(openFileDialog1.FileName);
                        res =sr.ReadToEnd();
                        sr.Close();
                        targetFlver.Materials = serializer.Deserialize<List<FLVER.Material>>(res);
                        autoBackUp();targetFlver.Write(flverName);
                        MessageBox.Show("Material change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }

               
            };

            f.Size = new System.Drawing.Size(750, 600);
            p.Size = new System.Drawing.Size(600, 530);
            f.Resize += (s, e) =>
                {
                    p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                    button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                    button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                    button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
                };

            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.ShowDialog();
            //Application.Run(f);

            

        }


        static void ModelMesh()
        {

            Form f = new Form();
            f.Text = "Mesh";
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            tList = new List<TextBox>();
            parentList = new List<TextBox>();
            childList = new List<TextBox>();
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);

            TextBox meshInfo = new TextBox();
            meshInfo.ReadOnly = true;
            meshInfo.Multiline = true;

            {
                Label l = new Label();
                l.Text = "index";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "name";
                l.Size = new System.Drawing.Size(150, 15);
                l.Location = new System.Drawing.Point(70, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "Delete?";
                l.Size = new System.Drawing.Size(70, 15);
                l.Location = new System.Drawing.Point(270, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "Chosen";
                l.Size = new System.Drawing.Size(70, 15);
                l.Location = new System.Drawing.Point(340, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Label l = new Label();
                l.Text = "Force bone weight to";
                l.Size = new System.Drawing.Size(200, 15);
                l.Location = new System.Drawing.Point(410, currentY + 5);
                p.Controls.Add(l);
            }
            currentY += 20;

            List<CheckBox> cbList = new List<CheckBox>();//List for deleting
            List<TextBox> tbList = new List<TextBox>();
            List<CheckBox> affectList = new List<CheckBox>();

            for (int i = 0; i < targetFlver.Meshes.Count; i++)

            {
                // foreach (FLVER.Bone bn in b.Bones)
                FLVER.Mesh bn = targetFlver.Meshes[i];
                //Console.WriteLine(bn.MaterialIndex);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200, 15);
                t.Location = new System.Drawing.Point(70, currentY);
                
                t.Text = "[M:"+targetFlver.Materials[bn.MaterialIndex].Name + "],Unk1:" + bn.Unk1 + ",Dyna:" + bn.Dynamic;
                p.Controls.Add(t);

                Label l = new Label();
                l.Text = "[" + i + "]";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);

                CheckBox cb = new CheckBox();
                cb.Checked = false;
                cb.Size = new System.Drawing.Size(70, 15);
                cb.Location = new System.Drawing.Point(270, currentY);
                p.Controls.Add(cb);
                cbList.Add(cb);


                CheckBox cb2 = new CheckBox();
                cb2.Checked =true;
                cb2.Size = new System.Drawing.Size(70, 15);
                cb2.Location = new System.Drawing.Point(340, currentY);
                p.Controls.Add(cb2);
                affectList.Add(cb2);


                TextBox t2 = new TextBox();
                t2.Size = new System.Drawing.Size(70, 15);
                t2.Location = new System.Drawing.Point(410, currentY);
                t2.Text = "-1";
                p.Controls.Add(t2);
                tbList.Add(t2);

                Button buttonCheck = new Button();
                int btnI = i;
                buttonCheck.Text = "Check";
                buttonCheck.Size = new System.Drawing.Size(70, 20);
                buttonCheck.Location = new System.Drawing.Point(500, currentY);
               
                buttonCheck.Click += (s, e) => {
                 
                    useCheckingMesh = true;
                    checkingMeshNum = btnI;
                    FLVER.Mesh mes = targetFlver.Meshes[btnI];
                    JavaScriptSerializer jse = new JavaScriptSerializer();

                    FLVER.Mesh m2 = new FLVER.Mesh();
                    m2.Vertices = new List<FLVER.Vertex>();
                    m2.VertexBuffers = mes.VertexBuffers;
                    m2.Unk1 = mes.Unk1;
                    m2.MaterialIndex = mes.MaterialIndex;
                    m2.FaceSets = jse.Deserialize<List< FLVER.FaceSet>>(jse.Serialize(mes.FaceSets));
                    foreach (FLVER.FaceSet fs in m2.FaceSets)
                    {
                       fs.Vertices = null;
                    }
                    m2.Dynamic = mes.Dynamic;
                    m2.DefaultBoneIndex = mes.DefaultBoneIndex;
                    m2.BoundingBoxUnk = mes.BoundingBoxUnk;
                    m2.BoundingBoxMin = mes.BoundingBoxMin;
                    m2.BoundingBoxMax = mes.BoundingBoxMax;
                    m2.BoneIndices = mes.BoneIndices;
                    
                    
                     //mes = jse.Deserialize<FLVER.Mesh>(jse.Serialize(mes));
                   // mes.Vertices = null;
                    meshInfo.Text =jse.Serialize(m2);
                    updateVertices();
                };

                p.Controls.Add(buttonCheck);

                currentY += 20;
                sizeY += 20;

                

            }

            Label l2 = new Label();
            l2.Text = "Chosen meshes operation---";
            l2.Size = new System.Drawing.Size(250, 15);
            l2.Location = new System.Drawing.Point(10, currentY + 5);
            p.Controls.Add(l2);

            currentY += 20;

            CheckBox rotCb = new CheckBox();
            rotCb.Size = new System.Drawing.Size(80, 15);
            rotCb.Text = "rotation";
            rotCb.Location = new System.Drawing.Point(10, currentY);
            rotCb.Checked = false;
            p.Controls.Add(rotCb);

            TextBox rotX = new TextBox();
            rotX.Size = new System.Drawing.Size(60, 15);
            rotX.Location = new System.Drawing.Point(90, currentY);
            rotX.Text = "0";
            p.Controls.Add(rotX);

            TextBox rotY = new TextBox();
            rotY.Size = new System.Drawing.Size(60, 15);
            rotY.Location = new System.Drawing.Point(150, currentY);
            rotY.Text = "0";
            p.Controls.Add(rotY);

            TextBox rotZ = new TextBox();
            rotZ.Size = new System.Drawing.Size(70, 15);
            rotZ.Location = new System.Drawing.Point(210, currentY);
            rotZ.Text = "0";
            p.Controls.Add(rotZ);

            currentY += 20;

            CheckBox transCb = new CheckBox();
            transCb.Size = new System.Drawing.Size(80, 15);
            transCb.Text = "translation";
            transCb.Location = new System.Drawing.Point(10, currentY);
            transCb.Checked = false;
            p.Controls.Add(transCb);

            TextBox transX = new TextBox();
            transX.Size = new System.Drawing.Size(60, 15);
            transX.Location = new System.Drawing.Point(90, currentY);
            transX.Text = "0";
            p.Controls.Add(transX);

            TextBox transY = new TextBox();
            transY.Size = new System.Drawing.Size(60, 15);
            transY.Location = new System.Drawing.Point(150, currentY);
            transY.Text = "0";
            p.Controls.Add(transY);

            TextBox transZ = new TextBox();
            transZ.Size = new System.Drawing.Size(70, 15);
            transZ.Location = new System.Drawing.Point(210, currentY);
            transZ.Text = "0";
            p.Controls.Add(transZ);

            currentY += 20;

            CheckBox scaleCb = new CheckBox();
            scaleCb.Size = new System.Drawing.Size(80, 15);
            scaleCb.Text = "scale";
            scaleCb.Location = new System.Drawing.Point(10, currentY);
            scaleCb.Checked = false;
            p.Controls.Add(scaleCb);

            TextBox scaleX = new TextBox();
            scaleX.Size = new System.Drawing.Size(60, 15);
            scaleX.Location = new System.Drawing.Point(90, currentY);
            scaleX.Text = "1";
            p.Controls.Add(scaleX);

            TextBox scaleY = new TextBox();
            scaleY.Size = new System.Drawing.Size(60, 15);
            scaleY.Location = new System.Drawing.Point(150, currentY);
            scaleY.Text = "1";
            p.Controls.Add(scaleY);

            TextBox scaleZ = new TextBox();
            scaleZ.Size = new System.Drawing.Size(70, 15);
            scaleZ.Location = new System.Drawing.Point(210, currentY);
            scaleZ.Text = "1";
            p.Controls.Add(scaleZ);

            currentY += 20;

            CheckBox dummyCb = new CheckBox();
            dummyCb.Size = new System.Drawing.Size(160, 15);
            dummyCb.Text = "Affect dummy";
            dummyCb.Location = new System.Drawing.Point(10, currentY);
            dummyCb.Checked = false;
            p.Controls.Add(dummyCb);

            currentY += 20;

            CheckBox bonesCb = new CheckBox();
            bonesCb.Size = new System.Drawing.Size(160, 15);
            bonesCb.Text = "Affect bones";
            bonesCb.Location = new System.Drawing.Point(10, currentY);
            bonesCb.Checked = false;
            p.Controls.Add(bonesCb);

            currentY += 20;

            CheckBox scaleBoneWeight = new CheckBox();
            scaleBoneWeight.Size = new System.Drawing.Size(200, 15);
            scaleBoneWeight.Text = "Convert bone weight index:";
            scaleBoneWeight.Location = new System.Drawing.Point(10, currentY);
            scaleBoneWeight.Checked = false;
            p.Controls.Add(scaleBoneWeight);

            TextBox boneF = new TextBox();
            boneF.Size = new System.Drawing.Size(60, 15);
            boneF.Location = new System.Drawing.Point(210, currentY);
            boneF.Text = "0";
            p.Controls.Add(boneF);

            TextBox boneT = new TextBox();
            boneT.Size = new System.Drawing.Size(60, 15);
            boneT.Location = new System.Drawing.Point(270, currentY);
            boneT.Text = "0";
            p.Controls.Add(boneT);

            currentY += 20;
            meshInfo.Size = new System.Drawing.Size(360, 300);
            meshInfo.Location = new System.Drawing.Point(10, currentY);
            p.Controls.Add(meshInfo);


            Button button = new Button();
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {

                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }
                    if (cbList[i].Checked == true)
                    {

                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
                                if (v.BoneWeights == null)
                                {

                                    continue;
                                }
                                for (int k = 0; k < v.BoneWeights.Length; k++)
                                {
                                    v.BoneWeights[k] = 0;
                                }
                            }

                        }
                        foreach (var mf in targetFlver.Meshes[i].FaceSets)
                        {
                            mf.Vertices = new uint[0]{ };
                           
                        }
                    }
                    int i2 = int.Parse(tbList[i].Text);
                    if (i2 >= 0)
                    {
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            if (v.Positions == null) { v.Positions = new List<Vector3>(); }
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                if (v.BoneWeights == null) { v.BoneWeights = new float[4]; v.BoneIndices = new int[4]; }
                                //v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
                                for (int k = 0; k < v.BoneWeights.Length; k++)
                                {
                                    v.BoneWeights[k] = 0;
                                }
                                v.BoneIndices[0] = i2;
                                v.BoneWeights[0] = 1;
                            }
                        }
                        if (!targetFlver.Meshes[i].BoneIndices.Contains(i2))
                        {
                            targetFlver.Meshes[i].BoneIndices.Add(i2);
                        }
                        targetFlver.Meshes[i].Dynamic = true;
                    }

                    if (transCb.Checked)
                    {
                        float x = float.Parse(transX.Text);
                        float y = float.Parse(transY.Text);
                        float z = float.Parse(transZ.Text);
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                v.Positions[j] = new Vector3(v.Positions[j].X + x, v.Positions[j].Y + y, v.Positions[j].Z + z);
                            }


                        }

                    }


                    if (rotCb.Checked)
                    {
                        float roll = float.Parse(rotX.Text);
                        float pitch = float.Parse(rotY.Text);

                        float yaw = float.Parse(rotZ.Text);
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                v.Positions[j] = RotatePoint(v.Positions[j], pitch, roll, yaw);
                            }

                            for (int j2 = 0; j2 < v.Normals.Count; j2++)
                            {
                                v.Normals[j2] = RotatePoint(v.Normals[j2], pitch, roll, yaw);

                            }
                            for (int j2 = 0; j2 < v.Tangents.Count; j2++)
                            {

                                v.Tangents[j2] = RotatePoint(v.Tangents[j2], pitch, roll, yaw);

                            }
                        }

                    }


                    if (scaleCb.Checked)
                    {
                        float x = float.Parse(scaleX.Text);
                        float y = float.Parse(scaleY.Text);
                        float z = float.Parse(scaleZ.Text);
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                v.Positions[j] = new Vector3(v.Positions[j].X * x, v.Positions[j].Y * y, v.Positions[j].Z * z);
                            }


                        }
                        if (bonesCb.Checked)
                        {

                            foreach (FLVER.Bone bs in targetFlver.Bones)
                            {
                                bs.Translation.X = x * bs.Translation.X;
                                bs.Translation.Y *= y * bs.Translation.Y;
                                bs.Translation.Z *= z * bs.Translation.Z;
                            }


                        }
                        

                    }

                    if (scaleBoneWeight.Checked == true)
                    {
                        int fromBone = int.Parse(boneF.Text);
                        int toBone = int.Parse(boneT.Text);
                       
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                //v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
                                if (v.BoneIndices != null)
                                {
                                    for (int k = 0; k < v.BoneIndices.Length; k++)
                                    {
                                        if (v.BoneIndices[k] == fromBone)
                                        {
                                            v.BoneIndices[k] = toBone;
                                        }
                                        
                                    }
                                }
                               
                            }
                        }
                        //targetFlver.Meshes[i].Vertices = new List<FLVER.Vertex>();

                    }
                }
                if (dummyCb.Checked)
                {
                    foreach (FLVER.Dummy d in targetFlver.Dummies)
                    {
                        if (transCb.Checked)
                        {
                            float x = float.Parse(transX.Text);
                            float y = float.Parse(transY.Text);
                            float z = float.Parse(transZ.Text);

                            d.Position.X += x;
                            d.Position.Y += y;
                            d.Position.Z += z;
                        }
                        if (rotCb.Checked)
                        {
                            float roll = float.Parse(rotX.Text);
                            float pitch = float.Parse(rotY.Text);
                            float yaw = float.Parse(rotZ.Text);
                            d.Position = RotatePoint(d.Position, pitch, roll, yaw);

                        }
                        if (scaleCb.Checked)
                        {
                            float x = float.Parse(scaleX.Text);
                            float y = float.Parse(scaleY.Text);
                            float z = float.Parse(scaleZ.Text);

                            d.Position.X *= x;
                            d.Position.Y *= y;
                            d.Position.Z *= z;
                        }
                    }
                }

                autoBackUp();targetFlver.Write(flverName);
                updateVertices();
                MessageBox.Show("Modificiation successful!");
            };


            Button button2 = new Button();
            button2.Text = "Attach";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {


                var openFileDialog1 = new OpenFileDialog();
                string res = "";
                openFileDialog1.Title = "Choose the flver file you want to attach to the scene";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        FLVER sekiro = FLVER.Read(openFileDialog1.FileName);
                        int materialOffset = targetFlver.Materials.Count;
                        int layoutOffset = targetFlver.BufferLayouts.Count;

                        Dictionary<int, int> sekiroToTarget = new Dictionary<int, int>();
                        for (int i2 = 0; i2 < sekiro.Bones.Count; i2++)
                        {
                            FLVER.Bone attachBone = sekiro.Bones[i2];
                            for (int i3 = 0; i3 < targetFlver.Bones.Count; i3++)
                            {
                                if (attachBone.Name == targetFlver.Bones[i3].Name)
                                {
                                    sekiroToTarget.Add(i2,i3);
                                    break;
                                }

                            }
                        }



                        foreach (FLVER.Mesh m in sekiro.Meshes)
                        {
                            m.MaterialIndex += materialOffset;
                            foreach (FLVER.VertexBuffer vb in m.VertexBuffers)
                            {
                               // vb.BufferIndex += layoutOffset;
                                vb.LayoutIndex += layoutOffset;
                               
                            }


                            foreach (FLVER.Vertex v in m.Vertices)
                            {
                                if (v.BoneIndices == null) { continue; }
                                for (int i5 = 0;i5 < v.BoneIndices.Length;i5++)
                                {
                                    if (sekiroToTarget.ContainsKey(v.BoneIndices[i5]))
                                    {
                                        
                                        v.BoneIndices[i5] = sekiroToTarget[v.BoneIndices[i5]];
                                    }
                                    else {
                                       // v.BoneIndices[i5] = -1;
                                       
                                    }
                                }
                            }
                           
                            
                        }

                        targetFlver.BufferLayouts = targetFlver.BufferLayouts.Concat(sekiro.BufferLayouts).ToList();

                        targetFlver.Meshes = targetFlver.Meshes.Concat(sekiro.Meshes).ToList();

                        targetFlver.Materials = targetFlver.Materials.Concat(sekiro.Materials).ToList();
                        //sekiro.Meshes[0].MaterialIndex

                        //targetFlver.Materials =  new JavaScriptSerializer().Deserialize<List<FLVER.Material>>(res);
                        autoBackUp();targetFlver.Write(flverName);
                        MessageBox.Show("Attaching new flver file completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }





            };


            Button button3 = new Button();
            button3.Text = "DS3_Fix";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                byte r = 0,g =0,b =0;
                {
                    var confirmResult = MessageBox.Show("Do set vertex R color to 255?",
                                     "Set",
                                     MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.Yes)
                    {
                        r = 255;
                    }
                }
                {
                    var confirmResult = MessageBox.Show("Do set vertex G color to 255?",
                                     "Set",
                                     MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.Yes)
                    {
                        g = 255;
                    }
                }
                {
                    var confirmResult = MessageBox.Show("Do set vertex B color to 255?",
                                     "Set",
                                     MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.Yes)
                    {
                        b = 255;
                    }
                }

                foreach (FLVER.Mesh m in targetFlver.Meshes)
                {
                    
                    foreach (FLVER.Vertex vi in m.Vertices)
                    {
                        
                        if (vi.Colors == null)
                        {
                            vi.Colors = new List<FLVER.Vertex.Color>();
                            vi.Colors.Add(new FLVER.Vertex.Color(255, r, g, b));
                        }
                        else if (vi.Colors.Count == 0)
                        {
                            vi.Colors.Add(new FLVER.Vertex.Color(255, r, g, b));
                        }
                        else {
                            vi.Colors[0] = new FLVER.Vertex.Color(255, r, g, b);
                        }
                    }
                    

                }

                var confirmResult3 = MessageBox.Show("Do you want to change material to Sekiro standard M[ARSN]? ",
                                   "Set",
                                   MessageBoxButtons.YesNo);
                if (confirmResult3 == DialogResult.Yes)
                {
                    foreach (FLVER.Material m in targetFlver.Materials)
                    {
                        if (m.MTD.IndexOf("_e") >= 0)
                        {
                            m.MTD = "M[ARSN]_e.mtd";
                        }
                        else {
                            m.MTD = "M[ARSN].mtd";
                        }

                        foreach (FLVER.Texture t in m.Textures)
                        {
                            if (t.Path.IndexOf("_a.tif") >= 0)
                            {
                                t.Type = "g_DiffuseTexture";
                            } else if (t.Path.IndexOf("_n.tif") >= 0)
                            {
                                t.Type = "g_BumpmapTexture";
                            } else if (t.Path.IndexOf("_r.tif") >= 0)
                            {
                                t.Type = "g_SpecularTexture";
                            }

                        }
                    }
                   
                }


                var confirmResult2 = MessageBox.Show("Add color part to buffer layout? (If this model does not show in sekiro please click yes!)",
                                    "Set",
                                    MessageBoxButtons.YesNo);
                if (confirmResult2 == DialogResult.No)
                {
                    autoBackUp();targetFlver.Write(flverName);

                    MessageBox.Show("Giving every vertex a color completed! Please exit the program!", "Info");
                    return;
                }
                foreach (FLVER.BufferLayout bl in targetFlver.BufferLayouts)
                {
                    //Sematic: 0:Position, 1: bone weight, 2: bone indices, 3:Normal, 5:UV 6: Tangent, 10:Vertex color
                    //{"Unk00":0,"StructOffset":24,"Type":19,"Semantic":10,"Index":1,"Size":4},{"Unk00":0,"StructOffset":28,"Type":22,"Semantic":5,"Index":0,"Size":8}],

                    Boolean hasColorLayout = false;
                    for (int i = 0; i < bl.Count; i++)
                    {
                        if (bl[i].Semantic == FLVER.BufferLayout.MemberSemantic.VertexColor)
                        {
                            hasColorLayout = true;
                            break;
                        }

                    }
                    if (hasColorLayout) { continue; }
                    for (int i =0;i < bl.Count;i++)
                    {
                        if (bl[i].Type == FLVER.BufferLayout.MemberType.Byte4C && bl[i].Semantic == FLVER.BufferLayout.MemberSemantic.UV)
                        {
                            int offset = bl[i].StructOffset;

                            for (int j =i;j < bl.Count;j++)
                            {
                                bl[j].StructOffset += 4;
                            }
                            bl.Insert(i, new FLVER.BufferLayout.Member(0, offset, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.VertexColor, 1));
                            break;
                        }

                    }

                }

               
                autoBackUp();targetFlver.Write(flverName);

                MessageBox.Show("Giving every vertex a color completed! Please exit the program!", "Info");
            };

            Button buttonFlip = new Button();
            buttonFlip.Text = "Switch YZ";
            buttonFlip.Location = new System.Drawing.Point(650, 200);
            buttonFlip.Click += (s, e) => {

                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }
                        float roll = (float)(Math.PI * -0.5f);//X
                        float pitch = (float)(Math.PI);//Y

                        float yaw = 0;
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                                v.Positions[j] = RotatePoint(v.Positions[j], pitch, roll, yaw);
                            }

                            for (int j2 = 0; j2 < v.Normals.Count; j2++)
                            {
                                v.Normals[j2] = RotatePoint(v.Normals[j2], pitch, roll, yaw);

                            }
                            for (int j2 = 0; j2 < v.Tangents.Count; j2++)
                            {

                                v.Tangents[j2] = RotatePoint(v.Tangents[j2], pitch, roll, yaw);

                            }
                        }

                }

                updateVertices();

                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("YZ axis switched!", "Info");
            };


            f.Size = new System.Drawing.Size(750, 600);
            p.Size = new System.Drawing.Size(600, 530);
            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
                buttonFlip.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
            };


            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(buttonFlip);
            
            f.ShowDialog();
            //Application.Run(f);



        }

        public static Vector3 RotatePoint(Vector3 p, float pitch,float roll,float yaw)
        {

            Vector3 ans = new Vector3(0,0,0);


            var cosa = Math.Cos(yaw);
            var sina = Math.Sin(yaw);

            var cosb = Math.Cos(pitch);
            var sinb = Math.Sin(pitch);

            var cosc = Math.Cos(roll);
            var sinc = Math.Sin(roll);

            var Axx = cosa * cosb;
            var Axy = cosa * sinb * sinc - sina * cosc;
            var Axz = cosa * sinb * cosc + sina * sinc;

            var Ayx = sina * cosb;
            var Ayy = sina * sinb * sinc + cosa * cosc;
            var Ayz = sina * sinb * cosc - cosa * sinc;

            var Azx = -sinb;
            var Azy = cosb * sinc;
            var Azz = cosb * cosc;

            var px = p.X;
            var py = p.Y;
            var pz = p.Z;

            ans.X =  (float)( Axx * px + Axy * py + Axz * pz);
            ans.Y = (float)(Ayx * px + Ayy * py + Ayz * pz);
            ans.Z = (float)(Azx * px + Azy * py + Azz * pz);


            return ans;
        }
        public static Vector4 RotatePoint(Vector4 p, float pitch, float roll, float yaw)
        {

            Vector4 ans = new Vector4(0, 0, 0,p.W);


            var cosa = Math.Cos(yaw);
            var sina = Math.Sin(yaw);

            var cosb = Math.Cos(pitch);
            var sinb = Math.Sin(pitch);

            var cosc = Math.Cos(roll);
            var sinc = Math.Sin(roll);

            var Axx = cosa * cosb;
            var Axy = cosa * sinb * sinc - sina * cosc;
            var Axz = cosa * sinb * cosc + sina * sinc;

            var Ayx = sina * cosb;
            var Ayy = sina * sinb * sinc + cosa * cosc;
            var Ayz = sina * sinb * cosc - cosa * sinc;

            var Azx = -sinb;
            var Azy = cosb * sinc;
            var Azz = cosb * cosc;

            var px = p.X;
            var py = p.Y;
            var pz = p.Z;

            ans.X = (float)(Axx * px + Axy * py + Axz * pz);
            ans.Y = (float)(Ayx * px + Ayy * py + Ayz * pz);
            ans.Z = (float)(Azx * px + Azy * py + Azz * pz);


            return ans;
        }

        public static Vector3 RotateLine(Vector3 p, Vector3 org,Vector3 direction, double theta)
        {
            double x = p.X;
            double y = p.Y;
            double z = p.Z;

            double a = org.X;
            double b = org.Y;
            double c = org.Z;



            double nu = direction.X / direction.Length();
            double nv = direction.Y / direction.Length();
            double nw = direction.Z / direction.Length();

            double[] rP = new double[3];

            rP[0] = (a * (nv * nv + nw * nw) - nu * (b * nv + c * nw - nu * x - nv * y - nw * z)) * (1 - Math.Cos(theta)) + x * Math.Cos(theta) + (-c * nv + b * nw - nw * y + nv * z) * Math.Sin(theta);
            rP[1] = (b * (nu * nu + nw * nw) - nv * (a * nu + c * nw - nu * x - nv * y - nw * z)) * (1 - Math.Cos(theta)) + y * Math.Cos(theta) + (c * nu - a * nw + nw * x - nu * z) * Math.Sin(theta);
            rP[2] = (c * (nu * nu + nv * nv) - nw * (a * nu + b * nv - nu * x - nv * y - nw * z)) * (1 - Math.Cos(theta)) + z * Math.Cos(theta) + (-b * nu + a * nv - nv * x + nu * y) * Math.Sin(theta);


            Vector3 ans = new Vector3((float)rP[0], (float)rP[1], (float)rP[2]);
            return ans;


        }

        public static Microsoft.Xna.Framework.Vector3 crossPorduct(Microsoft.Xna.Framework.Vector3 a, Microsoft.Xna.Framework.Vector3 b)
        {
            float x1 = a.X;
            float y1 = a.Y;
            float z1 = a.Z;
            float x2 = b.X;
            float y2 = b.Y;
            float z2 = b.Z;
            return new Microsoft.Xna.Framework.Vector3(y1*z2 - z1*y2,z1*x2-x1*z2,x1*y2-y1*x2);
        }

        public static float dotProduct(Microsoft.Xna.Framework.Vector3 a, Microsoft.Xna.Framework.Vector3 b)
        {
            float x1 = a.X;
            float y1 = a.Y;
            float z1 = a.Z;
            float x2 = b.X;
            float y2 = b.Y;
            float z2 = b.Z;
            return x1*x2 + y1 * y2 + z1 * z2;
        }

        static void ModelSwapModule() {

            System.Windows.Forms.OpenFileDialog openFileDialog1;
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            openFileDialog1.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            openFileDialog1.Title = "Choose template seikiro model file.";
            //openFileDialog1.ShowDialog();

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Console.WriteLine(openFileDialog1.FileName);
                //openFileDialog1.
            }
            else
            {
                return;
            }

            FLVER b = FLVER.Read(openFileDialog1.FileName);



            System.Windows.Forms.OpenFileDialog openFileDialog2 = new System.Windows.Forms.OpenFileDialog();
            openFileDialog2.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            openFileDialog2.Title = "Choose source DS/BB model file.";
            //openFileDialog1.ShowDialog();

            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                Console.WriteLine(openFileDialog2.FileName);
                //openFileDialog1.
            }
            else
            {
                return;
            }
            FLVER src = FLVER.Read(openFileDialog2.FileName);



            Console.WriteLine(b.Header);

            Console.WriteLine("Seikiro unk is:" + b.SekiroUnk);



            Console.WriteLine("Material:");
            foreach (FLVER.Material m in b.Materials)
            {
                Console.WriteLine(m.Name);

            }

            foreach (FLVER.Mesh m in b.Meshes)
            {
                Console.WriteLine("Mesh#" + m.MaterialIndex);

            }

            //* new
            //b.Header.BigEndian = src.Header.BigEndian;
            

            //


            //X: is not the sword axis!!!
            //Y: ++ means closer to the hand!
            //Unit: in meter(?)


            //For Moonlight sword -> threaded cane, Y+0.5f

            Form f = new Form();

            Label l = new Label();
            l.Text = "x,y,z offset? Y= weapon length axis,Y+=Closer to hand";
            l.Size = new System.Drawing.Size(150, 15);
            l.Location = new System.Drawing.Point(10, 20);
            f.Controls.Add(l);


            TextBox t = new TextBox();
            t.Size = new System.Drawing.Size(70, 15);
            t.Location = new System.Drawing.Point(10, 60);
            t.Text = "0";
            f.Controls.Add(t);



            TextBox t2 = new TextBox();
            t2.Size = new System.Drawing.Size(70, 15);
            t2.Location = new System.Drawing.Point(10, 100);
            t2.Text =  "0";
            f.Controls.Add(t2);

            TextBox t3 = new TextBox();
            t3.Size = new System.Drawing.Size(70, 15);
            t3.Location = new System.Drawing.Point(10, 140);
            t3.Text = "0";
            f.Controls.Add(t3);

            CheckBox cb1 = new CheckBox();
            cb1.Size = new System.Drawing.Size(70, 15);
            cb1.Location = new System.Drawing.Point(10, 160);
            cb1.Text = "Copy Material";
            f.Controls.Add(cb1);

            CheckBox cb2 = new CheckBox();
            cb2.Size = new System.Drawing.Size(150, 15);
            cb2.Location = new System.Drawing.Point(10, 180);
            cb2.Text = "Copy Bones";
            f.Controls.Add(cb2);

            CheckBox cb3 = new CheckBox();
            cb3.Size = new System.Drawing.Size(150, 15);
            cb3.Location = new System.Drawing.Point(10, 200);
            cb3.Text = "Copy Dummy";
            f.Controls.Add(cb3);


            CheckBox cb4 = new CheckBox();
            cb4.Size = new System.Drawing.Size(350, 15);
            cb4.Location = new System.Drawing.Point(10, 220);
            cb4.Text = "All vertex weight to first bone";
            f.Controls.Add(cb4);

            f.ShowDialog();

            float x = float.Parse(t.Text);
            float y = float.Parse(t2.Text);
            float z = float.Parse(t3.Text);


            b.Meshes = src.Meshes;
            

            if (cb1.Checked)
                b.Materials = src.Materials;

            if (cb2.Checked)
                b.Bones = src.Bones;

            if (cb3.Checked)
                b.Dummies = src.Dummies;

            if (cb4.Checked)
            {
                for (int i = 0; i < b.Meshes.Count; i++)
                {
                    b.Meshes[i].BoneIndices = new List<int>();
                    b.Meshes[i].BoneIndices.Add(0);
                    b.Meshes[i].BoneIndices.Add(1);
                    b.Meshes[i].DefaultBoneIndex = 1;
                        foreach (FLVER.Vertex v in b.Meshes[i].Vertices)
                        {
                            for (int j = 0; j < v.Positions.Count; j++)
                            {
                            if (v.BoneWeights == null) { continue; }
                                v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
                                for (int k = 0; k < v.BoneWeights.Length; k++)
                                {
                                    v.BoneWeights[k] = 0;
                                v.BoneIndices[k] = 0;
                                
                                }
                            v.BoneIndices[0] = 1;
                            v.BoneWeights[0] = 1;
                        }
                        }
                        //targetFlver.Meshes[i].Vertices = new List<FLVER.Vertex>();

                }
            }

            foreach (FLVER.Mesh m in b.Meshes)
            {
                foreach (FLVER.Vertex v in m.Vertices)
                {
                    for (int i = 0; i < v.Positions.Count; i++)
                    {
                        v.Positions[i] = new System.Numerics.Vector3(v.Positions[i].X + x, v.Positions[i].Y + y, v.Positions[i].Z + z);
                    }
                }

            }



            b.Write(openFileDialog1.FileName + "n");
            MessageBox.Show("Swap completed!", "Info");
            //Console.WriteLine("End reading");
            //Application.Exit();

        }

        public static string FormatOutput(string jsonString)
        {
            var stringBuilder = new StringBuilder();

            bool escaping = false;
            bool inQuotes = false;
            int indentation = 0;

            foreach (char character in jsonString)
            {
                if (escaping)
                {
                    escaping = false;
                    stringBuilder.Append(character);
                }
                else
                {
                    if (character == '\\')
                    {
                        escaping = true;
                        stringBuilder.Append(character);
                    }
                    else if (character == '\"')
                    {
                        inQuotes = !inQuotes;
                        stringBuilder.Append(character);
                    }
                    else if (!inQuotes)
                    {
                        if (character == ',')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append('\t', indentation);
                        }
                        else if (character == '[' || character == '{')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append('\t', ++indentation);
                        }
                        else if (character == ']' || character == '}')
                        {
                            stringBuilder.Append("\r\n");
                            stringBuilder.Append('\t', --indentation);
                            stringBuilder.Append(character);
                        }
                        else if (character == ':')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append('\t');
                        }
                        else
                        {
                            stringBuilder.Append(character);
                        }
                    }
                    else
                    {
                        stringBuilder.Append(character);
                    }
                }
            }

            return stringBuilder.ToString();
        }
    }
}
