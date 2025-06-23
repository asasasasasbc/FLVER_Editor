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
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using static SoulsFormats.FLVER;
using MessageBox = System.Windows.Forms.MessageBox;
namespace MySFformat
{
    static partial class Program
    {
        public class MyVertexBuffer
        {
            public MyVertexBuffer() { }
            public bool EdgeCompressed { get; set; }
            public int BufferIndex { get; set; }
            public int LayoutIndex { get; set; }
        }
        public class MyMesh
        {
            public MyMesh() { }
            //
            // 摘要:
            //     An optional bounding box for meshes added in DS2.
            public class BoundingBoxes
            {
                public Vector3 Min { get; set; }
                public Vector3 Max { get; set; }
                public Vector3 Unk { get; set; }
                public BoundingBoxes()
                {
                    Min = new Vector3(float.MinValue);
                    Max = new Vector3(float.MaxValue);
                }
            }

            private int[] faceSetIndices;

            private int[] vertexBufferIndices;

            public bool UseBoneWeights { get; set; }

            public int MaterialIndex { get; set; }
            public int NodeIndex { get; set; }
            public List<int> BoneIndices { get; set; }

            public List<MyVertexBuffer> VertexBuffers { get; set; }
            public BoundingBoxes BoundingBox { get; set; }
        }
        static void ModelMesh()
        {

            int[] tests = { 0, 0, 0 };

            Form f = new Form();
            f.Text = "Mesh";
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            checkingMeshNum = -1;
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);

            List<CheckBox> cbList = new List<CheckBox>();//List for deleting
            List<TextBox> tbList = new List<TextBox>();
            List<CheckBox> affectList = new List<CheckBox>();


            TextBox meshInfo = new TextBox();
            meshInfo.ReadOnly = false;
            meshInfo.Multiline = true;

            Button applyJsonMod = new Button();
            applyJsonMod.Text = "[DANGEROUS] Modify JSON";
            ButtonTips("[DANGEROUS]Apply changes in json except Facesets, Vertices part. May break the whole file." +
                "\n【危险】应用Json文本的修改，但是不会应用Facesets和Vertices部分。可能会导致文件损坏。", applyJsonMod);
            applyJsonMod.Click += (s, e) => {
                if (checkingMeshNum < 0 || checkingMeshNum >= targetFlver.Meshes.Count) { return; }
                try
                {
                    //useCheckingMesh = true;
                    //checkingMeshNum = btnI
                    FLVER2.Mesh mes = targetFlver.Meshes[checkingMeshNum];
                    JavaScriptSerializer jse = new JavaScriptSerializer();

                    MyMesh newMesh = jse.Deserialize<MyMesh>(meshInfo.Text);
                    jse.MaxJsonLength = Int32.MaxValue; // Fix too large mesh crash issue
                    mes.VertexBuffers.Clear();
                    foreach (var vb in newMesh.VertexBuffers)
                    {
                        var tmpVb = new FLVER2.VertexBuffer(vb.LayoutIndex);
                        tmpVb.BufferIndex = vb.BufferIndex;
                        tmpVb.EdgeCompressed = vb.EdgeCompressed;
                        mes.VertexBuffers.Add(tmpVb);
                    }
                    //TODO ADAPT:m2.Unk1 = mes.Unk1;
                    mes.MaterialIndex = newMesh.MaterialIndex;
                    //mes.Dynamic = newMesh.Dynamic; //Controlled by use bone weights
                    mes.UseBoneWeights = newMesh.UseBoneWeights;
                    mes.NodeIndex = newMesh.NodeIndex;
                    mes.BoundingBox.Min = newMesh.BoundingBox.Min;
                    mes.BoundingBox.Max = newMesh.BoundingBox.Max;
                    mes.BoundingBox.Unk = newMesh.BoundingBox.Unk;
                    mes.BoneIndices = newMesh.BoneIndices;
                    //mes = jse.Deserialize<FLVER2.Mesh>(jse.Serialize(mes));
                    // mes.Vertices = null;
                    updateVertices();
                    MessageBox.Show($"Json modification for mesh {checkingMeshNum} completed. Click Modify to save changes!");

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Exception occuried:\n{ex.Message}.");

                }
            };
            #region LeftPanel
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
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(270, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Button dA = new Button();
                dA.Text = "A";
                dA.Size = new System.Drawing.Size(15, 15);
                ButtonTips("Select/Deselect All.\n" +
    "全选/全不选", dA);
                dA.Location = new System.Drawing.Point(320, currentY + 5);
                dA.Click += (s, e) => {
                    Boolean allSelected = true;
                    foreach (var item in cbList)
                    {
                        if (item.Checked == false) { allSelected = false; }
                    }
                    foreach (var item in cbList)
                    {
                        item.Checked = !allSelected;
                    }


                };
                p.Controls.Add(dA);



            }



            {
                Label l = new Label();
                l.Text = "Chosen";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(340, currentY + 5);
                p.Controls.Add(l);
            }
            {
                Button dA = new Button();
                dA.Text = "A";
                dA.Size = new System.Drawing.Size(15, 15);
                ButtonTips("Select/Deselect All.\n" +
    "全选/全不选", dA);
                dA.Location = new System.Drawing.Point(390, currentY + 5);
                dA.Click += (s, e) => {
                    Boolean allSelected = true;
                    foreach (var item in affectList)
                    {
                        if (item.Checked == false) { allSelected = false; }
                    }
                    foreach (var item in affectList)
                    {
                        item.Checked = !allSelected;
                    }


                };
                p.Controls.Add(dA);



            }



            {
                Label l = new Label();
                l.Text = "Force bone weight to";
                l.Size = new System.Drawing.Size(170, 15);
                l.Location = new System.Drawing.Point(410, currentY + 5);
                p.Controls.Add(l);
            }




            {
                Button dA = new Button();
                dA.Text = "TBF All";
                dA.Size = new System.Drawing.Size(70, 20);
                ButtonTips("Toggle all chosen back face (double side) rendering functionality.\n" +
    "开关选择的双面渲染", dA);
                dA.Location = new System.Drawing.Point(580, currentY);
                dA.Click += (s, e) => {
                    for (int i = 0; i < affectList.Count; i++)
                    {
                        if (affectList[i].Checked == false) { continue; }
                        foreach (var fs in targetFlver.Meshes[i].FaceSets)
                        {
                            fs.CullBackfaces = !fs.CullBackfaces;
                        }
                    }
                    autoBackUp(); targetFlver.Write(flverName);
                    MessageBox.Show("Finished toggling all back face rendering!", "Info");

                };
                p.Controls.Add(dA);



            }




            currentY += 20;



            for (int i = 0; i < targetFlver.Meshes.Count; i++)
            {
                // foreach (FLVER.Bone bn in b.Nodes)
                FLVER2.Mesh bn = targetFlver.Meshes[i];
                //Console.WriteLine(bn.MaterialIndex);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200, 15);
                t.Location = new System.Drawing.Point(70, currentY);
                t.ReadOnly = true;
                t.Text = "[M:" + targetFlver.Materials[bn.MaterialIndex].Name + "]" /*+ ,Unk1:???//TODO ADAPT:bn.Unk1*/ + ",Dyna:" + bn.Dynamic;
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
                cb2.Checked = true;
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
                    FLVER2.Mesh mes = targetFlver.Meshes[btnI];
                    JavaScriptSerializer jse = new JavaScriptSerializer();
                    jse.MaxJsonLength = Int32.MaxValue; // Fix too large mesh crash issue
                    FLVER2.Mesh m2 = new FLVER2.Mesh();
                    m2.Vertices = new List<FLVER.Vertex>();
                    m2.VertexBuffers = mes.VertexBuffers;
                    //TODO ADAPT:m2.Unk1 = mes.Unk1;
                    m2.MaterialIndex = mes.MaterialIndex;
                    m2.FaceSets = jse.Deserialize<List<FLVER2.FaceSet>>(jse.Serialize(mes.FaceSets));
                    foreach (FLVER2.FaceSet fs in m2.FaceSets)
                    {
                        fs.Indices = null;
                    }
                    m2.Dynamic = mes.Dynamic;
                    m2.NodeIndex = mes.NodeIndex;
                    m2.BoundingBox = mes.BoundingBox;
                    //m2.BoundingBoxUnk = mes.BoundingBoxUnk;
                    //m2.BoundingBoxMin = mes.BoundingBoxMin;
                    //m2.BoundingBoxMax = mes.BoundingBoxMax;
                    m2.BoneIndices = mes.BoneIndices;


                    //mes = jse.Deserialize<FLVER2.Mesh>(jse.Serialize(mes));
                    // mes.Vertices = null;
                    meshInfo.Text = jse.Serialize(m2);
                    updateVertices();
                };

                p.Controls.Add(buttonCheck);




                Button buttonTBF = new Button();
                buttonTBF.Text = "TBF";
                ButtonTips("Toggle back face rendering or not", buttonTBF);
                buttonTBF.Size = new System.Drawing.Size(70, 20);
                buttonTBF.Location = new System.Drawing.Point(580, currentY);

                buttonTBF.Click += (s, e) => {


                    FLVER2.Mesh mes = targetFlver.Meshes[btnI];
                    foreach (var vfs in mes.FaceSets)
                    { vfs.CullBackfaces = !vfs.CullBackfaces; }
                    updateVertices();
                    autoBackUp(); targetFlver.Write(flverName);
                    MessageBox.Show("Finished toggling back face rendering!", "Info");
                };

                p.Controls.Add(buttonTBF);


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


            Button buttonN = new Button();
            buttonN.Text = "N. Flip";
            buttonN.Size = new System.Drawing.Size(70, 20);
            ButtonTips("Scale the normals according to the left textfield's values.\n" +
"按你输入的数值调整法线数值。", buttonN);
            buttonN.Location = new System.Drawing.Point(280, currentY);
            buttonN.Click += (s, e) => {
                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }
                    float x = float.Parse(scaleX.Text);
                    float y = float.Parse(scaleY.Text);
                    float z = float.Parse(scaleZ.Text);
                    foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                    {

                        int xs = 1;
                        int ys = 1;
                        int zs = 1;

                        //1.62: fixed scaling don't change normal error.
                        if (x < 0) { xs = -1; }
                        if (y < 0) { ys = -1; }
                        if (z < 0) { zs = -1; }
                        v.Normal = new Vector3(v.Normal.X * xs, v.Normal.Y * ys, v.Normal.Z * zs);



                    }

                }
                MessageBox.Show("Normal flip completed.");
                autoBackUp(); targetFlver.Write(flverName);



            };
            p.Controls.Add(buttonN);

            currentY += 20;


            CheckBox rotDg = new CheckBox();
            rotDg.Size = new System.Drawing.Size(160, 15);
            rotDg.Text = "Rotate in degrees";
            rotDg.Location = new System.Drawing.Point(10, currentY);
            rotDg.Checked = false;
            p.Controls.Add(rotDg);

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

            CheckBox facesetCb = new CheckBox();
            facesetCb.Size = new System.Drawing.Size(160, 15);
            facesetCb.Text = "Delete faceset only";
            facesetCb.Location = new System.Drawing.Point(10, currentY);
            facesetCb.Checked = false;
            p.Controls.Add(facesetCb);

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

            currentY += 300 + 5;
            applyJsonMod.Size = new System.Drawing.Size(200, 20);
            applyJsonMod.Location = new System.Drawing.Point(10, currentY);
            p.Controls.Add(applyJsonMod);
            #endregion LeftPanel

            Button button = new Button();
            button.Text = "Modify";
            ButtonTips("Modify the meshes and then save to the flver file.\n" +
"修改面片并保存至Flver文件中。", button);
            button.Location = new System.Drawing.Point(650, 50);
            button.Click += (s, e) => {

                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }
                    if (cbList[i].Checked == true)
                    {

                        //if only delete facesets.... but keep vertices.
                        //trick used in some physics case.
                        if (facesetCb.Checked)

                        {
                            foreach (var mf in targetFlver.Meshes[i].FaceSets)
                            {
                                for (int facei = 0; facei < mf.Indices.Count; facei++)
                                {
                                    //  mf.Vertices[facei] = facei%3;
                                    mf.Indices[facei] = 1;
                                }

                            }

                        }
                        else
                        {
                            foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                            {

                                v.Position = new System.Numerics.Vector3(0, 0, 0);
                                for (int k = 0; k < v.BoneWeights.Length; k++)
                                {
                                    v.BoneWeights[k] = 0;
                                }

                            }
                            foreach (var mf in targetFlver.Meshes[i].FaceSets)
                            {
                                mf.Indices.Clear();

                            }


                        }



                    }
                    int i2 = int.Parse(tbList[i].Text);
                    if (i2 >= 0)
                    {
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            if (v.Position == null) { v.Position = new Vector3(); }
                            //v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
                            for (int k = 0; k < v.BoneWeights.Length; k++)
                            {
                                v.BoneWeights[k] = 0;
                            }
                            v.BoneIndices[0] = i2;
                            v.BoneWeights[0] = 1;
                        }
                        if (!targetFlver.Meshes[i].BoneIndices.Contains(i2))
                        {
                            targetFlver.Meshes[i].BoneIndices.Add(i2);
                        }
                        targetFlver.Meshes[i].Dynamic = 1;
                    }

                    if (transCb.Checked)
                    {
                        float x = float.Parse(transX.Text);
                        float y = float.Parse(transY.Text);
                        float z = float.Parse(transZ.Text);
                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {

                            v.Position = new Vector3(v.Position.X + x, v.Position.Y + y, v.Position.Z + z);


                        }

                    }


                    if (rotCb.Checked)
                    {
                        float roll = float.Parse(rotX.Text);
                        float pitch = float.Parse(rotY.Text);

                        float yaw = float.Parse(rotZ.Text);
                        if (rotDg.Checked)
                        {
                            roll = (float)(roll / 180f * Math.PI);
                            pitch = (float)(pitch / 180f * Math.PI);
                            yaw = (float)(yaw / 180f * Math.PI);
                        }


                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {

                            v.Position = RotatePoint(v.Position, pitch, roll, yaw);

                            v.Normal = RotatePoint(v.Normal, pitch, roll, yaw);

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

                            v.Position = new Vector3(v.Position.X * x, v.Position.Y * y, v.Position.Z * z);
                            int xs = 1;
                            int ys = 1;
                            int zs = 1;

                            //1.62: fixed scaling don't change normal error.
                            if (x < 0) { xs = -1; }
                            if (y < 0) { ys = -1; }
                            if (z < 0) { zs = -1; }
                            v.Normal = new Vector3(v.Normal.X * xs, v.Normal.Y * ys, v.Normal.Z * zs);
                            for (int j = 0; j < v.Tangents.Count; j++)
                            {
                                v.Tangents[j] = new Vector4(v.Tangents[j].X * xs, v.Tangents[j].Y * ys, v.Tangents[j].Z * zs, v.Tangents[j].W);
                            }


                        }



                    }

                    if (scaleBoneWeight.Checked == true)
                    {
                        int fromBone = int.Parse(boneF.Text);
                        int toBone = int.Parse(boneT.Text);

                        foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                        {
                            //v.Positions[j] = new System.Numerics.Vector3(0, 0, 0);
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

                            d.Position += new Vector3(x, y, z);
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

                            d.Position *= new Vector3(x, y, z);
                        }
                    }
                }

                //if affect bones were checked
                if (bonesCb.Checked)
                {
                    float x = float.Parse(scaleX.Text);
                    float y = float.Parse(scaleY.Text);
                    float z = float.Parse(scaleZ.Text);
                    //1.67: update affect bone functionality
                    foreach (FLVER.Node bs in targetFlver.Nodes)
                    {
                        if (true)
                        {
                            var tmpVector = new Vector3();
                            tmpVector.X = x * bs.Translation.X;
                            tmpVector.Y = y * bs.Translation.Y;
                            tmpVector.Z = z * bs.Translation.Z;
                            bs.Translation = tmpVector;

                            bs.Scale *= new Vector3(x, y, z);

                        }

                    }


                }
                autoBackUp(); targetFlver.Write(flverName);
                updateVertices();
                MessageBox.Show("Modificiation successful!");
            };


            Button button2 = new Button();
            ButtonTips("Attach another flver file to this flver file.\n" +
"把另一个Flver文件合并到当前的Flver文件内。", button2);
            button2.Text = "Attach";
            button2.Location = new System.Drawing.Point(650, 100);
            button2.Click += (s, e) => {


                var openFileDialog1 = new OpenFileDialog() { Filter = "FLVER files (*.flver)|*.flver|All files (*.*)|*.*" };
                string res = "";
                openFileDialog1.Title = "Choose the flver file you want to attach to the scene";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        FLVER2 sekiro = FLVER2.Read(openFileDialog1.FileName);
                        int materialOffset = targetFlver.Materials.Count;
                        int layoutOffset = targetFlver.BufferLayouts.Count;

                        Dictionary<int, int> sekiroToTarget = new Dictionary<int, int>();
                        for (int i2 = 0; i2 < sekiro.Nodes.Count; i2++)
                        {
                            FLVER.Node attachBone = sekiro.Nodes[i2];
                            for (int i3 = 0; i3 < targetFlver.Nodes.Count; i3++)
                            {
                                if (attachBone.Name == targetFlver.Nodes[i3].Name)
                                {
                                    sekiroToTarget.Add(i2, i3);
                                    break;
                                }

                            }
                        }



                        foreach (FLVER2.Mesh m in sekiro.Meshes)
                        {
                            m.MaterialIndex += materialOffset;
                            foreach (FLVER2.VertexBuffer vb in m.VertexBuffers)
                            {
                                // vb.BufferIndex += layoutOffset;
                                vb.LayoutIndex += layoutOffset;

                            }


                            foreach (FLVER.Vertex v in m.Vertices)
                            {
                                for (int i5 = 0; i5 < v.BoneIndices.Length; i5++)
                                {
                                    if (sekiroToTarget.ContainsKey(v.BoneIndices[i5]))
                                    {

                                        v.BoneIndices[i5] = sekiroToTarget[v.BoneIndices[i5]];
                                    }
                                    else
                                    {
                                        // v.BoneIndices[i5] = -1;

                                    }
                                }
                            }


                        }

                        targetFlver.BufferLayouts = targetFlver.BufferLayouts.Concat(sekiro.BufferLayouts).ToList();

                        targetFlver.Meshes = targetFlver.Meshes.Concat(sekiro.Meshes).ToList();

                        targetFlver.Materials = targetFlver.Materials.Concat(sekiro.Materials).ToList();
                        //sekiro.Meshes[0].MaterialIndex

                        //targetFlver.Materials =  new JavaScriptSerializer().Deserialize<List<FLVER2.Material>>(res);
                        autoBackUp(); targetFlver.Write(flverName);
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
            ButtonTips("【unstable】Fix the problem that DS3 model does not show up in Sekiro.(All click yes)\n" +
"【不稳定】修复黑魂三模型在只狼内无法显示的问题。(全点是即可)", button3);
            button3.Text = "DS3_Fix";
            button3.Location = new System.Drawing.Point(650, 150);
            button3.Click += (s, e) => {

                byte r = 0, g = 0, b = 0;
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

                foreach (FLVER2.Mesh m in targetFlver.Meshes)
                {

                    foreach (FLVER.Vertex vi in m.Vertices)
                    {

                        if (vi.Colors == null)
                        {
                            vi.Colors = new List<FLVER.VertexColor>();
                            vi.Colors.Add(new FLVER.VertexColor(255, r, g, b));
                        }
                        else if (vi.Colors.Count == 0)
                        {
                            vi.Colors.Add(new FLVER.VertexColor(255, r, g, b));
                        }
                        else
                        {
                            vi.Colors[0] = new FLVER.VertexColor(255, r, g, b);
                        }
                    }


                }

                var confirmResult3 = MessageBox.Show("Do you want to change material to Sekiro standard M[ARSN]? ",
                                   "Set",
                                   MessageBoxButtons.YesNo);
                if (confirmResult3 == DialogResult.Yes)
                {
                    foreach (FLVER2.Material m in targetFlver.Materials)
                    {
                        if (m.MTD.IndexOf("_e") >= 0)
                        {
                            m.MTD = "M[ARSN]_e.mtd";
                        }
                        else
                        {
                            m.MTD = "M[ARSN].mtd";
                        }

                        foreach (FLVER2.Texture t in m.Textures)
                        {
                            if (t.Path.IndexOf("_a.tif") >= 0)
                            {
                                t.Type = "g_DiffuseTexture";
                            }
                            else if (t.Path.IndexOf("_n.tif") >= 0)
                            {
                                t.Type = "g_BumpmapTexture";
                            }
                            else if (t.Path.IndexOf("_r.tif") >= 0)
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
                    autoBackUp(); targetFlver.Write(flverName);

                    MessageBox.Show("Giving every vertex a color completed! Please exit the program!", "Info");
                    return;
                }
                foreach (FLVER2.BufferLayout bl in targetFlver.BufferLayouts)
                {
                    //Sematic: 0:Position, 1: bone weight, 2: bone indices, 3:Normal, 5:UV 6: Tangent, 10:Vertex color
                    //{"Unk00":0,"StructOffset":24,"Type":19,"Semantic":10,"Index":1,"Size":4},{"Unk00":0,"StructOffset":28,"Type":22,"Semantic":5,"Index":0,"Size":8}],

                    Boolean hasColorLayout = false;
                    for (int i = 0; i < bl.Count; i++)
                    {

                        if (bl[i].Semantic == FLVER.LayoutSemantic.VertexColor)
                        {
                            hasColorLayout = true;
                            break;
                        }

                    }
                    if (hasColorLayout) { continue; }
                    for (int i = 0; i < bl.Count; i++)
                    {
                        //old SoulsFormat BufferLayout.MemberType.Byte4C shouldbe ... UByte4Norm 19? I guess?
                        if (bl[i].Type == FLVER.LayoutType.UByte4Norm && bl[i].Semantic == FLVER.LayoutSemantic.UV)
                        {
                            //Struct offset seems no longer needed
                            bl.Insert(i, new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.VertexColor, 1));
                            break;
                        }

                        //OLD:if (bl[i].Type == FLVER.LayoutType.UByte4Norm && bl[i].Semantic == FLVER.LayoutSemantic.UV)
                        //OLD:{
                        //OLD:    int offset = bl[i].StructOffset;
                        //OLD:
                        //OLD:    for (int j = i; j < bl.Count; j++)
                        //OLD:    {
                        //OLD:        bl[j].StructOffset += 4;
                        //OLD:    }
                        //OLD:    bl.Insert(i, new FLVER2.BufferLayout.Member(0, offset, FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.VertexColor, 1));
                        //OLD:    break;
                        //OLD:}

                    }

                }


                autoBackUp(); targetFlver.Write(flverName);

                MessageBox.Show("Giving every vertex a color completed! Please exit the program!", "Info");
            };

            Button buttonFlip = new Button();
            ButtonTips("Flip YZ axis.Importing external models may require this step.\n" +
"翻转模型的YZ轴，有些外部模型需要这么做。", buttonFlip);
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
                        v.Position = RotatePoint(v.Position, pitch, roll, yaw);
                        v.Normal = RotatePoint(v.Normal, pitch, roll, yaw);
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

            Button reverseFaceset = new Button();
            ButtonTips("Reverse meshes' faceset.Importing external models may require this step.\n" +
"模型翻面。有些特殊情况需要这么做。", reverseFaceset);
            reverseFaceset.Text = "Rev. Mesh";
            reverseFaceset.Location = new System.Drawing.Point(650, 250);
            reverseFaceset.Click += (s, e) => {

                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }

                    //CHeck is imported mesh or not to find if program only need to reverse faceset once.
                    /* if (targetFlver.Meshes[i].FaceSets.Count >=2)
                     {
                         //Use reference equal to find faceset is the same or not
                         if (targetFlver.Meshes[i].FaceSets[0].Vertices.Equals(targetFlver.Meshes[i].FaceSets[1].Vertices))
                         {

                             Console.WriteLine("Same vertices detected!");
                         }

                     }*/


                    foreach (FLVER2.FaceSet fs in targetFlver.Meshes[i].FaceSets)
                    {

                        for (int ifs = 0; ifs < fs.Indices.Count; ifs += 3)
                        {
                            int temp = fs.Indices[ifs + 1];
                            fs.Indices[ifs + 1] = fs.Indices[ifs + 2];
                            fs.Indices[ifs + 2] = temp;
                        }
                    }

                }

                updateVertices();

                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Faceset switched!", "Info");
            };

            Button reverseNormal = new Button();
            ButtonTips("Reverse chosen meshes' normals & tangents.Importing external models may require this step.\n" +
"反向模型法线&切线。有些特殊情况需要这么做。", reverseNormal);
            reverseNormal.Text = "Rev. Norm.";
            reverseNormal.Location = new System.Drawing.Point(650, 300);
            reverseNormal.Click += (s, e) => {

                for (int i = 0; i < cbList.Count; i++)
                {
                    if (affectList[i].Checked == false) { continue; }

                    foreach (FLVER.Vertex v in targetFlver.Meshes[i].Vertices)
                    {

                        v.Normal = new Vector3(-v.Normal.X, -v.Normal.Y, -v.Normal.Z);
                        for (int j2 = 0; j2 < v.Tangents.Count; j2++)
                        {
                            v.Tangents[j2] = new Vector4(-v.Tangents[j2].X, -v.Tangents[j2].Y, -v.Tangents[j2].Z, v.Tangents[j2].W);

                        }
                    }

                }

                updateVertices();

                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Normals reversed!", "Info");
            };





            Button meshReset = new Button();
            ButtonTips("【Unstable】Reset all mesh's info to DS3/Sekiro default, usually used to port DS2 version flver file.\n" +
"【不稳定】部分重置面片信息，主要用于导入DS2flver文件至DS3之中。", meshReset);
            meshReset.Text = "M. Reset";
            meshReset.Location = new System.Drawing.Point(650, 350);
            meshReset.Click += (s, e) => {

                SetMeshInfoToDefault();

                updateVertices();

                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Meshs resetted!", "Info");
            };


            f.Size = new System.Drawing.Size(800, 650);
            p.Size = new System.Drawing.Size(650, 600);
            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 50);
                button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
                buttonFlip.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
                reverseFaceset.Location = new System.Drawing.Point(f.Size.Width - 100, 250);
                reverseNormal.Location = new System.Drawing.Point(f.Size.Width - 100, 300);
                meshReset.Location = new System.Drawing.Point(f.Size.Width - 100, 350);
            };
            p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 50);
            button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
            button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
            button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
            buttonFlip.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
            reverseFaceset.Location = new System.Drawing.Point(f.Size.Width - 100, 250);
            reverseNormal.Location = new System.Drawing.Point(f.Size.Width - 100, 300);
            meshReset.Location = new System.Drawing.Point(f.Size.Width - 100, 350);


            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(buttonFlip);
            f.Controls.Add(reverseFaceset);
            f.Controls.Add(reverseNormal);
            f.Controls.Add(meshReset);

            f.ShowDialog();
            //Application.Run(f);



        }

    }
}
