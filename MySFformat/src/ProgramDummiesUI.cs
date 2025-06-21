using Assimp;
using Microsoft.Xna.Framework.Graphics;
using ObjLoader.Loader.Loaders;
using SoulsFormats;
using SoulsFormats.Other.MWC;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Windows;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using MessageBox = System.Windows.Forms.MessageBox;

namespace MySFformat
{
    static partial class Program
    {
        static void dummies()
        {
            Form f = new Form();
            f.Text = "Dummies";
            Panel p = new Panel();
            int currentY2 = 10;
            p.AutoScroll = true;
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dummyStr = File.ReadAllText(assemblyPath + "\\dummyInfo.dll");
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
            t.Location = new System.Drawing.Point(10, currentY2 + 5);
            t.Text = "-1";
            p.Controls.Add(t);


            TextBox tref = new TextBox();
            ;
            tref.Size = new System.Drawing.Size(100, 15);
            tref.Location = new System.Drawing.Point(150, currentY2 + 5);
            tref.Text = "";
            tref.ReadOnly = true;
            p.Controls.Add(tref);


            Button buttonCheck = new Button();
            ButtonTips("Check the dummy point by the index you typed, the chosen point will be displayed with a white X.\n" +
                 "按照你输入的序列数找到对应的辅助点，辅助点会以白色的X显示。", buttonCheck);
            buttonCheck.Text = "Check";
            buttonCheck.Location = new System.Drawing.Point(70, currentY2 + 5);
            buttonCheck.Click += (s, e) => {
                int i = int.Parse(t.Text);
                if (i >= 0 && i < targetFlver.Dummies.Count)
                {

                    useCheckingPoint = true;
                    checkingPointHasTangent = false;
                    checkingPoint = new Vector3(targetFlver.Dummies[i].Position.X, targetFlver.Dummies[i].Position.Y, targetFlver.Dummies[i].Position.Z);
                    checkingPointNormal = new Vector3(targetFlver.Dummies[i].Forward.X * 0.2f, targetFlver.Dummies[i].Forward.Y * 0.2f, targetFlver.Dummies[i].Forward.Z * 0.2f);

                    tref.Text = "RefID:" + targetFlver.Dummies[i].ReferenceID;
                    updateVertices();
                }
                else
                {

                    MessageBox.Show("Invalid modification value!");
                }

            };
            p.Controls.Add(buttonCheck);


            currentY2 += 25;

            Label ltip = new Label();

            ltip.Location = new System.Drawing.Point(10, currentY2 + 5);
            ltip.Size = new System.Drawing.Size(200, 15);
            ltip.Text = "Translate value (x,y,z):";
            p.Controls.Add(ltip);

            currentY2 += 20;

            TextBox tX = new TextBox();
            tX.Size = new System.Drawing.Size(60, 15);
            tX.Location = new System.Drawing.Point(10, currentY2 + 5);
            tX.Text = "0";
            p.Controls.Add(tX);


            TextBox tY = new TextBox();
            tY.Size = new System.Drawing.Size(60, 15);
            tY.Location = new System.Drawing.Point(70, currentY2 + 5);
            tY.Text = "0";
            p.Controls.Add(tY);

            TextBox tZ = new TextBox();
            tZ.Size = new System.Drawing.Size(60, 15);
            tZ.Location = new System.Drawing.Point(130, currentY2 + 5);
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
            ButtonTips("Translate the point you chosen and save to flver file.\n" +
                 "移动你所选择的辅助点，然后保存移动后的信息至Flver文件内。", button);
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {
                int i = int.Parse(t.Text);
                if (i >= 0 && i < targetFlver.Dummies.Count)
                {
                    targetFlver.Dummies[i].Position += new Vector3(float.Parse(tX.Text), float.Parse(tY.Text), float.Parse(tZ.Text));
                    autoBackUp(); targetFlver.Write(flverName);
                    updateVertices();
                }
                else
                {

                    MessageBox.Show("Invalid modification value!");
                }

            };


            Button button2 = new Button();
            ButtonTips("Save the json text you modified to the flver file.\n" +
                "存储你修改的json文本至Flver文件中。", button2);
            button2.Text = "JsonMod";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {
                targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(tbones.Text);
                autoBackUp(); targetFlver.Write(flverName);
                updateVertices();
                MessageBox.Show("Dummy change completed! Please exit the program!", "Info");
            };

            Button button3 = new Button();
            ButtonTips("Import external json file's dummy information and save to the flver file.\n" +
                 "读取外部json文本并存储至Flver文件中。", button3);
            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                var openFileDialog1 = new OpenFileDialog() { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
                string res = "";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sr = new StreamReader(openFileDialog1.FileName);
                        res = sr.ReadToEnd();
                        sr.Close();
                        targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(res);
                        autoBackUp(); targetFlver.Write(flverName);
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

            // 

            Button button4 = new Button();

            button4.Text = "ExportJson";
            button4.Location = new System.Drawing.Point(650, 200);
            button4.Click += (s, e) => {
                exportJson(serializer.Serialize(targetFlver.Dummies), "Dummies.json", "Nodes JSON exported successfully!");
            };

            Button buttonFix = new Button();
            ButtonTips("Fix external weapon's weapon trail/lighting reversal problem in Sekiro by adding kusabimaru's dummy information.\n" +
               "写入契丸的辅助点信息以解决武器在只狼内没有剑风以及无法雷闪的问题。", buttonFix);
            buttonFix.Text = "SekiroFix";
            buttonFix.Location = new System.Drawing.Point(650, 250);
            buttonFix.Click += (s, e) => {


                // targetFlver.Dummies = serializer.Deserialize<List<FLVER.Dummy>>(res);
                //autoBackUp();targetFlver.Write(flverName);
                for (int i = 0; i < refDummy.Count; i++)
                {
                    for (int j = 0; j < targetFlver.Dummies.Count; j++)
                    {
                        if (targetFlver.Dummies[j].ReferenceID == refDummy[i].ReferenceID)
                        {
                            break;
                        }
                        else if (j == targetFlver.Dummies.Count - 1)
                        {

                            targetFlver.Dummies.Add(refDummy[i]);
                            break;
                        }
                    }

                }
                autoBackUp(); targetFlver.Write(flverName);

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
                button4.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
                buttonFix.Location = new System.Drawing.Point(f.Size.Width - 100, 250);
            };

            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(button4);
            f.Controls.Add(buttonFix);
            f.ShowDialog();
        }
    }
}
