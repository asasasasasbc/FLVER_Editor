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
    public class VertexInfo
    {
     public int meshIndex = 0;
      public  uint vertexIndex = 0;

    }
    static partial class Program 
    {
        public static FLVER2 targetFlver;
        public static TPF targetTPF = null;
        public static string flverName;
        public static List<VertexInfo> verticesInfo = new List<VertexInfo>();

        public static List<FLVER.Node> poseNodes = new List<FLVER.Node>();

        public static Vector3D[] bonePosList = new Vector3D[2000];


        public static Dictionary<String, String> boneParentList;
        public static List<FLVER.Vertex> vertices = new List<FLVER.Vertex>();
        public static Mono3D mono;
        public static BonePoseEditorForm bonePoseEditorForm;

        public static string orgFileName = "";


        public static TextBox flexA;
        public static TextBox flexB;
        public static TextBox flexC;

        public static Vector3 checkingPoint;
        public static Vector3 checkingPointNormal;
        public static Boolean checkingPointHasTangent = false;
        public static Vector3 checkingPointTangent;
        public static float checkingPointTangentW = 0;
        public static Boolean useCheckingPoint = false;

        public static int checkingMeshNum = -1;
        public static Boolean useCheckingMesh = false;

        /***settings***/
        public static Boolean basicMode = false;
        public static Boolean loadTexture = true;
        public static Boolean show3D = false;
        public static Boolean legacyDisplay = false;
        public static Boolean smooth = false;
        public static int boneFindParentTimes = 15;//if cannot find bone, find if its parent bone matches flver bone name


        public static bool poseDisplay = false;
        public static Boolean boneDisplay = true;
        public static Boolean boneDirDisplay = false;
        public static int checkingBoneIndex = -1;// For bone checking function
        public static float boneLength = 0.01f;
        public static float boneDirLength = 0.1f;
        public static Boolean dummyDisplay = true;
        public static Boolean normalDisplay = false; 
        public static Boolean tangentDisplay = false;

        public static Boolean setVertexPos = false;
        public static float setVertexX = 0;
        public static float setVertexY = 1.75f;
        public static float setVertexZ = 0;

        public static RotationOrder rotOrder = RotationOrder.YZX;

        public static string version = "X2.6";

        //v1.68 Update: fix switch YZ axis's UV coordinate problems when importing models
        //v1.71:Added xml edit & auto set texture path method.
        //v1.72:Fixed scaling doesn't change tangent value error.
        //v1.73:Fixed xml auto edit bug and tangent flip bug. 
        //Also arevised Rev.Normal functionality. Now it also reverse the tangents.
        //Also added bone shift functionality! Can choose to shift bone weights if load another bone.json file

        //v1.8:Added skeleton display & toggle functionalty!
        //Press B to toggle skeleton display and press M to toggle dummmy display!

        //v1.81:Added automatic material rename functionality.
        //Added vertex rigth click edit functionality.
        //Fixed auto set texture path bug.

        //v1.82: Added mesh->M. Reset functionality to help you port DS2 .flv file and make it compatible with new P[ARSN] material. 
        //Added Mesh->TBF, so that you can choose to render the back face or not.
        //Added back face rendering functionality

        //1.83: Added experimental "Export DAE" functionality
        //More general bone display functionality
        //Window maxmimum bug fixed

        //1.85: Added LOD setting when import models

        //1.86: Added Mesh->TBF ALL button
        //Fixed minor LOD importing bug 

        //1.87: In "Check vertex" window, added vertex mesh index info and vertex index info.
        //Added : "delete vertex" and "delete vertex above/below" functionality
        //Added: Silence vertex deletion functionality: ctrl + right click in 3d model viewing software to enter such mode, then press alt + right click to quick delete vertex.

        //1.9: Added texture loading functionality: the tpf file's name must be the same as flver file name.
        //Added MySFormat.ini to help tweaking some special effects.
        //Added mroe shading mod and F: flat shading mod to better suit the need.

        //1.91: Added loading dcx file functionality (need the extension to be .dcx)

        //1.92+1.93 Fixed some minor fbx import bug.

        //1.95: find bones' parent 15 times.

        //1.96: fix "affect bones" function. Now bones can be scaled properly

        //1.97： added experimental Sekiro and Elden Ring .dcx Support
        //1.971: repair minor flver crash problem

        // X2 : Swaped to SoulsFormatsNEXT library
        // Added dummy export json button
        // Tangent manipulation: W inverse
        // Make [Import and override bone only] button more clear.
        // NR->ER flver files porting- advanced cloth physics retain feature
        // Probably requires some shader to work properly? or just use M.reset

        // [Done, Experimental] Multi-UV FBX Exporting
        // [Done, Experimental] Multi-UV Importing
        // When showing up, make sure no window overlap
        // Fix no-texture flver cannot be loaded issue
        // One click VBS (mesh's vertex buffer information) Editing

        //X2.6 TODO List:
        // Strange Mesh Error problem
        //  importing new skeletons without changing exisitng bones hierarchy
        // Core function: make sure tangent calculation is correct and useable
        // Pipeline check: FLVER editor fbx export -> blender editing -> reimport back to FLVER
        //   - need to make sure tangent is correct, and bone is also correct.
        // When export fbx, rename to aquatools format XXX|MaterialXXX|...
        // add new nodes without affecting existing options
        // Pipeline check: whole new animation pipeline walktrough (flver skeleton + further custom animation)
        // exporting dummypolys
        // exporting with axis convertsion
        // 3dsmax support
        // o assign the mesh to use “base buffer layout” and “cloth buffer layouts”
        // sorta like two templates to use one for every other mesh (buffer 0) or cloth mesh (3, 8, 5)


        // Fixing bone importing function's ipreviousSibling not found issue
        // Bone's BoundingBox calculation issues
        // Bone manipulation, etc. mirroring rotation/ +180degree
        // Tangent manipulation, etc.
        // ImportingModel -> Supports cloth physics bufferlayout

        public static string[] argments = { };
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            argments = args;
            Console.WriteLine("Hello!");
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            IniParser settingFile = new IniParser(assemblyPath + "\\MySFformat.ini");
            loadTexture = (settingFile.GetSetting("FLVER", "loadTexture").Trim() != "0") ? true : false;
          //  MessageBox.Show(settingFile.GetSetting("FLVER", "loadTexture"));
            show3D = (settingFile.GetSetting("FLVER", "show3D").Trim() != "0") ? true : false;
            legacyDisplay = (settingFile.GetSetting("FLVER", "legacyDisplay").Trim() != "0") ? true : false;
            ModelAdjModule();
        }

        public static void updateVerticesLegacy() 
        {
            useCheckingMesh = false;
            List<VertexPositionColor> ans = new List<VertexPositionColor>();
            List<VertexPositionColor> triangles = new List<VertexPositionColor>();
            List<VertexPositionColorTexture> textureTriangles = new List<VertexPositionColorTexture>();
            vertices.Clear();
            verticesInfo.Clear();
            List<MeshInfos> mis = new List<MeshInfos>();

            if (useCheckingPoint)
            {
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X, checkingPoint.Z, checkingPoint.Y), Microsoft.Xna.Framework.Color.Blue));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.2f * checkingPointNormal.X, checkingPoint.Z + 0.2f * checkingPointNormal.Z, checkingPoint.Y + 0.2f * checkingPointNormal.Y), Microsoft.Xna.Framework.Color.Blue));


                useCheckingPoint = false;
            }

            for (int i = 0; i < targetFlver.Meshes.Count; i++)
            {
                // int currentV = 0;
                //Microsoft.Xna.Framework.Vector3[] vl = new Microsoft.Xna.Framework.Vector3[3];
                if (targetFlver.Meshes[i] == null) { continue; }
                foreach (var vi in targetFlver.Meshes[i].Vertices) 
                {
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vi.Position), Microsoft.Xna.Framework.Color.Black));

                 }


                for (uint j = 0; j < targetFlver.Meshes[i].Vertices.Count; j++)
                {
                    FLVER.Vertex v = targetFlver.Meshes[i].Vertices[(int)j];
                    vertices.Add(v);
                    VertexInfo vi = new VertexInfo();
                    vi.meshIndex = i;
                    vi.vertexIndex = j;
                    verticesInfo.Add(vi);
                }
            }



            mono.vertices = ans.ToArray();
            // mono.triTextureVertices = textureTriangles.ToArray();
            mono.meshInfos = mis.ToArray();
            mono.triVertices = triangles.ToArray();

        }

        //Reset poses data to default
        public static void resetPoses() {
            poseNodes.Clear();
            foreach (var node in targetFlver.Nodes) {
                FLVER.Node new_node = new FLVER.Node(node);
                poseNodes.Add(new_node);
            }
        }

        public static void LoadPosesJson()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            using (var openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string res = File.ReadAllText(openFileDialog.FileName);
                    var newNodes = serializer.Deserialize<List<FLVER.Node>>(res);
                    if (newNodes.Count != targetFlver.Nodes.Count) {
                        MessageBox.Show($"Error loading or parsing JSON file.\n\n Nodes does not match!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    poseNodes = newNodes;
                    MessageBox.Show("New pose loaded! ", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading or parsing JSON file.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            updateVertices();
        }

        public static void ExportPosesJson()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            exportJson(serializer.Serialize(poseNodes), "Pose.json", "Pose JSON exported successfully!");
        }

        public static void updateVertices()
        {

            if (legacyDisplay) { updateVerticesLegacy();return; }
            List<VertexPositionColor> ans = new List<VertexPositionColor>();
            void DrawLine(Vector3D v1, Vector3D v2, Microsoft.Xna.Framework.Color c, float offsize = 0.005f)
            {
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v1.X - offsize, v1.Z, v1.Y), c));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v2.X, v2.Z, v2.Y), c));
                if (Math.Abs(offsize) < float.Epsilon) { return; }
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v1.X + offsize, v1.Z, v1.Y), c));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v2.X, v2.Z, v2.Y), c));
            }
            void DrawBone(Transform3D parent, Transform3D child, Microsoft.Xna.Framework.Color c)
            {
                // y z
                // Parent -> Child
                Vector3D P_origin = parent.getGlobalOrigin();
                Vector3D C_origin = child.getGlobalOrigin();
                Vector3D bone_vec = C_origin - P_origin;
                float bone_length = bone_vec.length();
                // If the bone is extremely short, don't draw it to avoid visual artifacts or division by zero.
                if (bone_length < 0.0001f)
                {
                    return;
                }
                bone_vec = bone_vec.normalize();
                var bone_vec_y = parent.getGlobalOrigin(0, 1, 0) - P_origin;
                var bone_vec_z = parent.getGlobalOrigin(0, 0, 1) - P_origin;
                //Draw P -> 4 points
                var p1 = P_origin + (boneLength * bone_vec) + (boneLength * bone_vec_y) + (boneLength * bone_vec_z);
                var p2 = P_origin + (boneLength * bone_vec) + (boneLength * bone_vec_y) - (boneLength * bone_vec_z);
                var p3 = P_origin + (boneLength * bone_vec) - (boneLength * bone_vec_y) - (boneLength * bone_vec_z);
                var p4 = P_origin + (boneLength * bone_vec) - (boneLength * bone_vec_y) + (boneLength * bone_vec_z);
                DrawLine(P_origin, p1, c, 0);
                DrawLine(P_origin, p2, c, 0);
                DrawLine(P_origin, p3, c, 0);
                DrawLine(P_origin, p4, c, 0);

                DrawLine(p4, p1, c, 0);
                DrawLine(p1, p2, c, 0);
                DrawLine(p2, p3, c, 0);
                DrawLine(p3, p4, c, 0);

                DrawLine(C_origin, p1, c, 0);
                DrawLine(C_origin, p2, c, 0);
                DrawLine(C_origin, p3, c, 0);
                DrawLine(C_origin, p4, c, 0);
            }
            void DrawRing(Vector3D[] vectors, Microsoft.Xna.Framework.Color c)
            {
                for (var j = 0; j < vectors.Length; j++)
                {
                    var start = vectors[j];
                    var end = vectors[0];
                    if (j + 1 < vectors.Length)
                    {
                        end = vectors[j + 1];
                    }
                    DrawLine(start, end, c, 0);
                }

            }
            List<VertexPositionColor> triangles = new List<VertexPositionColor>();
            List<VertexPositionColorTexture> textureTriangles = new List<VertexPositionColorTexture>();
            vertices.Clear();
            verticesInfo.Clear();
            List<MeshInfos> mis = new List<MeshInfos>();
            // Bone transformation matrix used for skinning and pose
            List<Matrix3D> boneTransMats = new List<Matrix3D>(); // Calculated by pose node
            List<Matrix3D> boneITransMats = new List<Matrix3D>();// Calculated by bone node
            List<Matrix3D> poseTransMats = new List<Matrix3D>(); // Calculated by pose node
            bool hasPose = poseDisplay && poseNodes.Count == targetFlver.Nodes.Count;
            // transform matrix calculation
            var targetNodes = targetFlver.Nodes;
            Transform3D[] boneTrans = new Transform3D[targetNodes.Count];
            var poseTrans = new Transform3D[targetNodes.Count];
            //Reconstruct transform hierarchy
            for (int i = 0; i < targetNodes.Count; i++)
            {
                boneTrans[i] = new Transform3D();
                boneTrans[i].rotOrder = rotOrder;
                boneTrans[i].position = new Vector3D(targetNodes[i].Translation);
                boneTrans[i].setRotationInRad(new Vector3D(targetNodes[i].Rotation));
                boneTrans[i].scale = new Vector3D(targetNodes[i].Scale);
                if (targetNodes[i].ParentIndex >= 0)
                {
                    boneTrans[i].parent = boneTrans[targetNodes[i].ParentIndex];
                    boneTrans[i].parent.children.Add(boneTrans[i]);
                }
            }
            for (int i = 0; i < targetNodes.Count; i++)
            {
                var tranMat = boneTrans[i].getTransMatrix();
                var itranMat = tranMat.inverse();
                boneTransMats.Add(tranMat);
                boneITransMats.Add(itranMat);
            }

            // Pose Calc
            if (hasPose) { 
                targetNodes = poseNodes;
                //Reconstruct transform hierarchy
                for (int i = 0; i < targetNodes.Count; i++)
                {
                    poseTrans[i] = new Transform3D();
                    poseTrans[i].rotOrder = rotOrder;
                    poseTrans[i].position = new Vector3D(targetNodes[i].Translation);
                    poseTrans[i].setRotationInRad(new Vector3D(targetNodes[i].Rotation));
                    poseTrans[i].scale = new Vector3D(targetNodes[i].Scale);
                    if (targetNodes[i].ParentIndex >= 0)
                    {
                        poseTrans[i].parent = poseTrans[targetNodes[i].ParentIndex];
                        poseTrans[i].parent.children.Add(poseTrans[i]);
                    }
                }
                for (int i = 0; i < targetNodes.Count; i++)
                {
                    var tranMat = poseTrans[i].getTransMatrix();
                    poseTransMats.Add(tranMat);
                }
            }

            ////////////////////////////
            // Display bones
            if (boneDisplay)
            {
                
                Microsoft.Xna.Framework.Color boneColor = Microsoft.Xna.Framework.Color.Purple;
                if (hasPose)
                {
                    // Slighty lighter for pose mode
                    boneColor = new Microsoft.Xna.Framework.Color(155, 0, 155, 255);
                }
                
                
                // Draw bones
                for (int i = 0; i < targetNodes.Count; i++)
                {

                    
                    var tranMat = boneTransMats[i];
                    var targetTranform = boneTrans[i];
                    if (hasPose) { tranMat = poseTransMats[i]; targetTranform = poseTrans[i]; }
                    if (targetNodes[i].ParentIndex >= 0)
                    {
                        if (targetNodes[i].FirstChildIndex >= 0)
                        {
                            Microsoft.Xna.Framework.Color c = boneColor;
                            if (checkingBoneIndex == i)
                            {
                                c = Microsoft.Xna.Framework.Color.Yellow;
                            }
                            var targetBone = targetTranform;
                            for (int j = 0; j < targetBone.children.Count; j++)
                            {
                                if (j < targetNodes.Count)
                                {
                                    var childTrans = targetBone.children[j];
                                    //DrawLine(actPos, parentPos, c, 0.005f);
                                    DrawBone(targetTranform, childTrans, c);
                                }
                            }


                        }

                    }
                    //ActualPos
                    Vector3D actPos = targetTranform.getGlobalOrigin();
                    if (boneDirDisplay || i == checkingBoneIndex)
                    {
                        Vector3D offsetX = Matrix3D.matrixTimesVector3D(tranMat, new Vector3D(boneDirLength, 0, 0));
                        Vector3D offsetY = Matrix3D.matrixTimesVector3D(tranMat, new Vector3D(0, boneDirLength, 0));
                        Vector3D offsetZ = Matrix3D.matrixTimesVector3D(tranMat, new Vector3D(0, 0, boneDirLength));
                        DrawLine(actPos, offsetX, Microsoft.Xna.Framework.Color.OrangeRed, 0.01f);
                        DrawLine(actPos, offsetY, Microsoft.Xna.Framework.Color.Yellow, 0.01f);
                        DrawLine(actPos, offsetZ, Microsoft.Xna.Framework.Color.Blue, 0.01f);
                    }
                    //Rotation circle
                    if (i == checkingBoneIndex)
                    {
                        var cirlceZ = targetTranform.getRotCircleZ(boneDirLength);
                        DrawRing(cirlceZ, Microsoft.Xna.Framework.Color.Blue);
                        var cirlceY = targetTranform.getRotCircleY(boneDirLength);
                        DrawRing(cirlceY, Microsoft.Xna.Framework.Color.Yellow);
                        var cirlceX = targetTranform.getRotCircleX(boneDirLength);
                        DrawRing(cirlceX, Microsoft.Xna.Framework.Color.Red);
                    }


                }
            }
            ////////////////////////////

            for (int i = 0; i < targetFlver.Meshes.Count; i++)
            {
                // int currentV = 0;
                //Microsoft.Xna.Framework.Vector3[] vl = new Microsoft.Xna.Framework.Vector3[3];
                if (targetFlver.Meshes[i] == null) { continue; }


                bool renderBackFace = false;
                Microsoft.Xna.Framework.Vector3 light = new Microsoft.Xna.Framework.Vector3(mono.lightX, mono.lightY, mono.lightZ);
                light.Normalize();
                if (targetFlver.Meshes[i].FaceSets.Count > 0)
                {
                    if (targetFlver.Meshes[i].FaceSets[0].CullBackfaces == false) { renderBackFace = true; }
                }
                var faces = targetFlver.Meshes[i].GetFaces();
                for (var fi = 0; fi < faces.Count;fi++)
                {
                    var vl = faces[fi];
                    var tvl = vl;
                    Vector3[] ps = new Vector3[3];
                    ps[0] = vl[0].Position;
                    ps[1] = vl[1].Position;
                    ps[2] = vl[2].Position;
                    //为了优化下PoseTransform
                    if (boneITransMats.Count == targetFlver.Nodes.Count && poseDisplay) {
                        for (var j =0; j < 3;j++)
                        {
                            var v = vl[j]; 
                            var orgPos = new Vector3D(v.Position.X, v.Position.Y, v.Position.Z);
                            var finalPos = new Vector3D(); 
                            float restBoneWeight = 1.0f;
                            // 内层骨骼权重循环
                            for (var k = 0; k < 4; k++) 
                            {
                                Int32 boneIndex = v.BoneIndices[k];
                                float boneWeight = v.BoneWeights[k];
                                // 安全检查 boneIndex
                                if (boneIndex < 0 || boneIndex >= boneITransMats.Count)
                                {
                                    if (boneWeight == 0f) continue;
                                    if (boneWeight != 0)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Warning: Invalid boneIndex {boneIndex} for vertex. Skipping.");
                                        continue;
                                    }
                                }
                                if (boneWeight == 0f) continue; // Optimzie Skip zero bone weight

                                if (boneWeight < 0) { boneWeight += 1; }
                                restBoneWeight -= boneWeight;

                                var boneITransMat = boneITransMats[boneIndex]; // Inverse bind pose matrix
                                var transMat = hasPose ? poseTransMats[boneIndex] : boneTransMats[boneIndex]; // Current pose matrix

                                var vertInBoneSpace = Matrix3D.matrixTimesVector3D(boneITransMat, orgPos);
                                var posedVertContribution = Matrix3D.matrixTimesVector3D(transMat, vertInBoneSpace);

                                finalPos.X += boneWeight * posedVertContribution.X;
                                finalPos.Y += boneWeight * posedVertContribution.Y;
                                finalPos.Z += boneWeight * posedVertContribution.Z;
                            }

                            if (restBoneWeight > 0.0001f)
                            {
                                finalPos.X += restBoneWeight * orgPos.X;
                                finalPos.Y += restBoneWeight * orgPos.Y;
                                finalPos.Z += restBoneWeight * orgPos.Z;
                            }
                            ps[j] = finalPos.toNumV3();
                        }
                    }
                    
                    Microsoft.Xna.Framework.Color cline = Microsoft.Xna.Framework.Color.Black;
                    if (useCheckingMesh && checkingMeshNum == i)
                    {
                        cline.G = 255;
                        cline.R = 255;
                    }
                    cline.A = 125;
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[1]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[0]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[2]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[1]), cline));
                    ans.Add(new VertexPositionColor(toXnaV3XZY(ps[2]), cline));

                    Microsoft.Xna.Framework.Color c = new Microsoft.Xna.Framework.Color();

                    Microsoft.Xna.Framework.Vector3 va = toXnaV3(ps[1]) - toXnaV3(ps[0]);
                    Microsoft.Xna.Framework.Vector3 vb = toXnaV3(ps[2]) - toXnaV3(ps[0]);
                    Microsoft.Xna.Framework.Vector3 vnromal = crossPorduct(va, vb);
                    vnromal.Normalize();
                    
                    float theta = dotProduct(vnromal, light);
                    int value = 125 + (int)(125 * theta);
                    if (value > 255) { value = 255; }
                    if (value < 0) { value = 0; }
                    if (mono.flatShading) { value = 255; }
                    c.R = (byte)value;
                    c.G = (byte)value;
                    c.B = (byte)value;
                    c.A = 255;
                    if (useCheckingMesh && checkingMeshNum == i)
                    {
                        c.B = 0;
                    }
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[0]), c));
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[2]), c));
                    triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[1]), c));

                    if (loadTexture)
                    {
                        if (tvl[0].UVs.Count > 0) { // Avoid UV display error
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[0]), c, new Microsoft.Xna.Framework.Vector2(tvl[0].UVs[0].X, tvl[0].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[2]), c, new Microsoft.Xna.Framework.Vector2(tvl[2].UVs[0].X, tvl[2].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[1]), c, new Microsoft.Xna.Framework.Vector2(tvl[1].UVs[0].X, tvl[1].UVs[0].Y)));
                        }
                    }



                    if (renderBackFace)
                    {
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[0]), c));
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[1]), c));
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(ps[2]), c));


                        if (loadTexture)
                        {
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[0]), c, new Microsoft.Xna.Framework.Vector2(tvl[0].UVs[0].X, tvl[0].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[1]), c, new Microsoft.Xna.Framework.Vector2(tvl[1].UVs[0].X, tvl[1].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(ps[2]), c, new Microsoft.Xna.Framework.Vector2(tvl[2].UVs[0].X, tvl[2].UVs[0].Y)));

                        }

                    }


                }

                for (uint j = 0; j < targetFlver.Meshes[i].Vertices.Count;j++) 
                {
                    FLVER.Vertex v = targetFlver.Meshes[i].Vertices[(int)j];
                      vertices.Add(v);
                    VertexInfo vi = new VertexInfo();
                    vi.meshIndex = i;
                    vi.vertexIndex = j;
                    verticesInfo.Add(vi);

                    //Add Normal Info and Tangent Info
                    if (normalDisplay) {
                        ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Position.X, v.Position.Z, v.Position.Y), Microsoft.Xna.Framework.Color.Blue));
                        ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Position.X + 0.2f * v.Normal.X, v.Position.Z + 0.2f * v.Normal.Z, v.Position.Y + 0.2f * v.Normal.Y), Microsoft.Xna.Framework.Color.Blue));
                    }
                    if (tangentDisplay && v.Tangents.Count >= 1)
                    {
                        var tanget = v.Tangents[0];
                        var color = Microsoft.Xna.Framework.Color.OrangeRed;
                        if (tanget.W < 0) { color = Microsoft.Xna.Framework.Color.DarkRed;}
                        ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Position.X, v.Position.Z, v.Position.Y), color));
                        ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.Position.X + 0.2f * tanget.X, v.Position.Z + 0.2f * tanget.Z, v.Position.Y + 0.2f * tanget.Y), color));
                    }
                }

                MeshInfos mi = new MeshInfos();
                var tName = "ERROR(No texture channel found in material!)";
                // Fix no-texture flver cannot be loaded issue
                if (targetFlver.Materials[targetFlver.Meshes[i].MaterialIndex].Textures.Count > 0) {
                    tName = targetFlver.Materials[targetFlver.Meshes[i].MaterialIndex].Textures[0].Path;
                }
              tName = FindFileName(tName);
                mi.textureName = tName;
                //MessageBox.Show("Found texture name:" + mi.textureName);
               mi.triTextureVertices = textureTriangles.ToArray();
                textureTriangles.Clear();
                mis.Add(mi);
            }
            if (ans.Count % 2 != 0)
            {
                ans.Add(ans[ans.Count - 1]);
            }

            for (int i = 0;i < bonePosList.Length;i++)
            {
                bonePosList[i] = new Vector3D();
            }
            


            for (int i = 0; i < targetFlver.Dummies.Count && dummyDisplay; i++)
            {
                FLVER.Dummy d = targetFlver.Dummies[i];
                var c = Microsoft.Xna.Framework.Color.Purple;
                DrawLine(new Vector3D(d.Position.X - 0.025f, d.Position.Y, d.Position.Z),
                    new Vector3D(d.Position.X + 0.025f, d.Position.Y, d.Position.Z), c);

                DrawLine(new Vector3D(d.Position.X, d.Position.Y, d.Position.Z - 0.025f),
                    new Vector3D(d.Position.X, d.Position.Y, d.Position.Z + 0.025f), c);

                c = Microsoft.Xna.Framework.Color.Green;
                DrawLine(new Vector3D(d.Position.X, d.Position.Y, d.Position.Z),
                    new Vector3D(d.Position.X + d.Forward.X, d.Position.Y + d.Forward.Y, d.Position.Z + d.Forward.Z), c);

            }

            if (useCheckingPoint)
            {
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X , checkingPoint.Z , checkingPoint.Y), Microsoft.Xna.Framework.Color.Blue));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X +  0.2f * checkingPointNormal.X, checkingPoint.Z + 0.2f * checkingPointNormal.Z, checkingPoint.Y + 0.2f * checkingPointNormal.Y), Microsoft.Xna.Framework.Color.Blue));

                if (checkingPointHasTangent) {
                    var v = checkingPoint;
                    var tangent = checkingPointTangent;
                    var color = Microsoft.Xna.Framework.Color.OrangeRed;
                    if (checkingPointTangentW < 0) { color = Microsoft.Xna.Framework.Color.DarkRed; }
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.X, v.Z, v.Y), color));
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.X + 0.2f *tangent.X, v.Z + 0.2f * tangent.Z, v.Y + 0.2f * tangent.Y), color));
                }
               

                useCheckingPoint = false;
            }
            useCheckingMesh = false;
            mono.vertices = ans.ToArray();
            // mono.triTextureVertices = textureTriangles.ToArray();
            mono.meshInfos = mis.ToArray();
            mono.triVertices = triangles.ToArray();
        }




        static void autoBackUp()
        {
            if (!File.Exists(orgFileName + ".bak"))
            {
                System.IO.File.Copy(orgFileName, orgFileName + ".bak", false);
            }
        }

        private static void Select_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        

        static int findFLVER_Bone(FLVER2 f, string name)
        {
            for (int flveri = 0; flveri < f.Nodes.Count; flveri++)
            {
                if (f.Nodes[flveri].Name == name)
                {

                    return flveri;

                }

            }
            return -1;
        }

        static void bufferLayout()
        {
            Form f = new Form();
            f.Text = "Buffer Layout Viewer";
            f.Size = new System.Drawing.Size(800, 600); // 稍微增大窗口以便容纳DGV

            Panel mainPanel = new Panel();
            mainPanel.Dock = DockStyle.Fill; // 让 Panel 填满窗体
            mainPanel.AutoScroll = true;     // 关键：当内容超出 Panel 大小时出现滚动条
            f.Controls.Add(mainPanel);

            int currentY = 10; // Y坐标用于在Panel中垂直排列控件

            Label titleLabel = new Label();
            titleLabel.Text = "Buffer Layouts:";
            titleLabel.AutoSize = true;
            titleLabel.Location = new System.Drawing.Point(10, currentY);
            mainPanel.Controls.Add(titleLabel);
            currentY += titleLabel.Height + 10;

            // var serializer = new JavaScriptSerializer();
            // string serializedResult = serializer.Serialize(targetFlver.BufferLayouts);
            // 上面两行现在不需要了，因为我们要直接用对象

            if (targetFlver.BufferLayouts.Count == 0)
            {
                Label emptyLabel = new Label();
                emptyLabel.Text = "No buffer layouts found.";
                emptyLabel.AutoSize = true;
                emptyLabel.Location = new System.Drawing.Point(10, currentY);
                mainPanel.Controls.Add(emptyLabel);
            }
            else
            {
                for (int i = 0; i < targetFlver.BufferLayouts.Count; i++)
                {
                    var layoutList = targetFlver.BufferLayouts[i];

                    // 为每个 Buffer Layout 添加一个标签
                    Label l = new Label();
                    l.Text = $"Buffer Layout {i}:";
                    l.AutoSize = true;
                    l.Location = new System.Drawing.Point(10, currentY);
                    mainPanel.Controls.Add(l);
                    currentY += l.Height + 5;

                    // 创建 DataGridView
                    DataGridView dgv = new DataGridView();
                    dgv.Location = new System.Drawing.Point(10, currentY);
                    dgv.AllowUserToAddRows = false;
                    dgv.AllowUserToDeleteRows = false;
                    dgv.ReadOnly = true;
                    dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells; // 列宽自动适应内容
                    dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

                    // 设置数据源为当前 Buffer Layout 列表
                    // DataGridView 会自动使用 BufferLayoutItem 的公共属性作为列
                    // 为了显示枚举字符串，我们依赖 BufferLayoutItem 中的 TypeString 和 SemanticString 属性
                    dgv.DataSource = layoutList;

                    // 你可以手动控制列的显示和顺序，以及标题
                    // 如果不设置 DataSource 直接添加列和行，则需要手动填充数据
                    // 如果设置了 DataSource，可以调整自动生成的列
                    // 例如，隐藏原始的 Type 和 Semantic int 列，只显示字符串版本
                    // 但为了简单，如果 BufferLayoutItem 包含 TypeString/SemanticString，它们会被自动显示。
                    // 如果列名和属性名不完全一致，或者想自定义列头，可以这样做：
                    // dgv.AutoGenerateColumns = false; // 关闭自动生成
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Stream", HeaderText = "Stream" });
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SpecialModifier", HeaderText = "Modifier" });
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TypeString", HeaderText = "Type" }); // 使用字符串版本
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SemanticString", HeaderText = "Semantic" }); // 使用字符串版本
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Index", HeaderText = "Index" });
                    // dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Size", HeaderText = "Size" });
                    // dgv.DataSource = layoutList; // 再设置数据源

                    // 计算DGV的高度：表头高度 + 每行高度 * 行数 + 一点边距
                    int dgvHeight = dgv.ColumnHeadersHeight;
                    if (layoutList.Count > 0)
                    {
                        dgvHeight += layoutList.Count * dgv.RowTemplate.Height;
                    }
                    else
                    {
                        dgvHeight += dgv.RowTemplate.Height; // 至少一行的高度，即使是空的
                    }
                    dgvHeight = Math.Min(dgvHeight, 300); // 限制最大高度，防止单个DGV过长

                    dgv.Size = new System.Drawing.Size(mainPanel.ClientSize.Width - 30, dgvHeight); // 宽度适应Panel，高度基于内容
                    dgv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // 随Panel宽度调整

                    mainPanel.Controls.Add(dgv);
                    currentY += dgv.Height + 10; // 为下一个控件留出空间
                }
            }

            var serializer = new JavaScriptSerializer();
            //targetFlver.BufferLayouts[0][0].Type = FLVER.LayoutType.Float1; // 0
            Label l2 = new Label();
            l2.Text = "Buffer Layout json:";
            l2.Size = new System.Drawing.Size(150, 15);
            l2.Location = new System.Drawing.Point(10, currentY);
            mainPanel.Controls.Add(l2);

            currentY += l2.Height + 5;

            string serializedResult = serializer.Serialize(targetFlver.BufferLayouts);
            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY);
            tbones.Text = serializedResult;
            mainPanel.Controls.Add(tbones);
            currentY += tbones.Height + 5;
            
            Button modifyJson = new Button();
            modifyJson.Text = "[DANGEROUS] Modify JSON";
            ButtonTips("DANGEROUS! Apply bufferlayout json modification, may break whole file!\n" +
                "【非常危险！】修改Json代码以修改bufferlayout。", modifyJson);
            modifyJson.Size = new System.Drawing.Size(200, 20);
            modifyJson.Location = new System.Drawing.Point(10, currentY);
            mainPanel.Controls.Add(modifyJson);
            modifyJson.Click += (s, e) => {
                var confirmResult = MessageBox.Show("This is a DANGEROUS action, may completely break your FLVER file, continue?"+
                    "\n这是一个极度危险的操作，可能会完全损坏你的FLVER文件，是否继续？",
                                   "WARNING",
                                   MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.No)
                {
                    return;
                }
                try
                {
                    var layoutMembers = serializer.Deserialize<List<List<LayoutMemberDto>>>(tbones.Text);
                    List<FLVER2.BufferLayout> targetBufferLayouts = new List<FLVER2.BufferLayout>();

                    foreach (var dtoList in layoutMembers)
                    {
                        var bufferLayout = new FLVER2.BufferLayout(); // FLVER2.BufferLayout
                        foreach (var dto in dtoList)
                        {
                            // Use the constructor of LayoutMember that takes parameters
                            var layoutMember = new LayoutMember(
                                dto.Type,
                                dto.Semantic,
                                dto.Index,
                                (short)dto.Stream, // Cast if necessary, though int to short is explicit
                                dto.SpecialModifier
                            );
                            bufferLayout.Add(layoutMember);
                        }
                        targetBufferLayouts.Add(bufferLayout);
                    }

                    targetFlver.BufferLayouts = targetBufferLayouts;
                    autoBackUp();
                    targetFlver.Write(flverName);
                    MessageBox.Show("JSON modifications saved! Please restart the window to see all changes.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    MessageBox.Show($"Error parsing/writing JSON or applying bufferlayouts.\n\n{ex.Message}\nYour FLVER file may be broken.", "JSON/Bufferlayout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            f.Resize += (s, e) =>
            {
                // 如果DGV宽度是固定的，并且希望它们随窗体变化，可以在这里调整
                // 但由于DGV的Anchor设置和Panel的Dock=Fill，它们应该能较好地自适应
                // mainPanel.PerformLayout(); // 可能需要强制重新布局
            };

            f.ShowDialog();
        }


        // 1. Define a DTO that matches your JSON structure and is deserializer-friendly
        public class LayoutMemberDto
        {
            public int Stream { get; set; }
            public short SpecialModifier { get; set; }
            public LayoutType Type { get; set; } // Assuming LayoutType can be deserialized from int
            public LayoutSemantic Semantic { get; set; } // Assuming LayoutSemantic can be deserialized from int
            public int Index { get; set; }
            // The "Size" from JSON will be deserialized here but we'll ignore it
            // when creating the actual LayoutMember, as its Size is calculated.
            public int Size { get; set; }

            public LayoutMemberDto() { }
            public LayoutMemberDto(FLVER.LayoutMember target) {
                Stream = target.Stream;
                SpecialModifier = target.SpecialModifier;
                Type = target.Type;
                Semantic = target.Semantic;
                Index = target.Index;
                Size = target.Size;
            }
        }

        #region Material_Window
        static void ModelMaterial() {

            Form f = new Form();
            f.Text = "Material";
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
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
                l.Text = "mtd";
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

            List<TextBox> material_names_text = new List<TextBox>();
            List<TextBox> mtd_text = new List<TextBox>();
            for (int i = 0; i < targetFlver.Materials.Count; i++)
            {
                // foreach (FLVER.Bone bn in b.Nodes)
                FLVER2.Material bn = targetFlver.Materials[i];
                //Console.WriteLine(bn.Name);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200, 15);
                t.Location = new System.Drawing.Point(70, currentY);
                t.Text = bn.Name;
                p.Controls.Add(t);
                material_names_text.Add(t);

                Label l = new Label();
                l.Text = "[" + i + "]";
                l.Size = new System.Drawing.Size(50, 15);
                l.Location = new System.Drawing.Point(10, currentY + 5);
                p.Controls.Add(l);
                

                TextBox t2 = new TextBox();
                t2.Size = new System.Drawing.Size(300, 15);
                t2.Location = new System.Drawing.Point(270, currentY);
                t2.Text = bn.MTD;//Original is : bn.Flags + ",GX" + bn.GXBytes + ",Unk" + bn.Unk18;
                p.Controls.Add(t2);
                mtd_text.Add(t2);

                Button buttonCheck = new Button();
                int btnI = i;
                buttonCheck.Text = "Edit";
                ButtonTips("Quick edit the texture path and basic information of this material." +
                    "\r\n 快速编辑此材质的贴图路径以及基础信息。",buttonCheck);
                buttonCheck.Size = new System.Drawing.Size(70, 20);
                buttonCheck.Location = new System.Drawing.Point(580, currentY);

                buttonCheck.Click += (s, e) => {
                    materialQuickEdit(targetFlver.Materials[btnI],btnI);
                };

                p.Controls.Add(buttonCheck);

                currentY += 20;
                sizeY += 20;
            }


            var serializer = new JavaScriptSerializer();
            string serializedResult = serializer.Serialize(targetFlver.Materials);


            TextBox tbones = new TextBox();
            tbones.Multiline = true;
            tbones.Size = new System.Drawing.Size(670, 600);
            tbones.Location = new System.Drawing.Point(10, currentY + 20);
            tbones.Text = serializedResult;

            p.Controls.Add(tbones);

            int btnY = 50;

            Button button = new Button();
            button.Text = "Modify";
            ButtonTips("Save materials' names and mtd modification to the flver file.\n" +
               "保存对材质名称和mtd的修改至Flver文件中。", button);
            button.Location = new System.Drawing.Point(700, btnY);
            button.Click += (s, e) => {
                for (int i = 0; i < targetFlver.Materials.Count; i++)
                { 
                    var material = targetFlver.Materials[i];
                    material.Name = material_names_text[i].Text;
                    material.MTD = mtd_text[i].Text;
                }
                autoBackUp(); targetFlver.Write(flverName);
            };

            btnY += 50;

            Button button2 = new Button();
            ButtonTips("Save json text's modification to the flver file.\n" +
           "保存对json文本的修改至Flver文件中。", button2);
            button2.Text = "ModifyJson";
            button2.Location = new System.Drawing.Point(700, btnY);
            button2.Click += (s, e) => {
                targetFlver.Materials = serializer.Deserialize<List<FLVER2.Material>>(tbones.Text);
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");
            };
            btnY += 50;

            Button button3 = new Button();
            ButtonTips("Import external Json text file and save to the flver file.\n" +
          "导入外部的Json文本并保存至Flver文件中。", button3);
            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(700, btnY);
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
                        targetFlver.Materials = serializer.Deserialize<List<FLVER2.Material>>(res);
                        autoBackUp(); targetFlver.Write(flverName);
                        MessageBox.Show("Material change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }


            };
            Button button3ex = new Button();
            btnY += 50;


            button3ex.Text = "ExportJson";
            ButtonTips("Export material json text to a file.\n" +
"导出当前材质信息到一个json文件内。", button3ex);
            button3ex.Location = new System.Drawing.Point(700, btnY);
            button3ex.Click += (s, e) => {
                exportJson(FormatOutput(serializer.Serialize(targetFlver.Materials)), "Material.json", "Material json text exported!");
            };
            btnY += 50;


            Button buttonARSN = new Button();
            ButtonTips("Convert materials (mtd path) to Sekiro/DS3 standard M[ARSN].mtd\n" +
          "替换所有的材质(mtd)为标准的M[ARSN]材质。", buttonARSN);
            buttonARSN.Text = "M[ARSN]";
            buttonARSN.Location = new System.Drawing.Point(700, btnY);
            buttonARSN.Click += (s, e) => {

                foreach (FLVER2.Material m in targetFlver.Materials)
                {
                    var confirmResult = MessageBox.Show("Convert <" + m.Name + ">'s material to M[ARSN].mtd?",
                                   "Convertion",
                                   MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.No)
                    {
                        continue;
                    }
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
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");
            };
            btnY += 50;


            Button buttonDMY = new Button();
            ButtonTips("[Sekiro only]Convert materials (mtd path) to Sekiro standard c9990_dummy.mtd\n" +
          "【仅限只狼】替换材质(mtd)为只狼的c9990_dummy材质。", buttonDMY);
            buttonDMY.Text = "M[DUMMY]";
            buttonDMY.Location = new System.Drawing.Point(700, btnY);
            buttonDMY.Click += (s, e) => {

                foreach (FLVER2.Material m in targetFlver.Materials)
                {
                    var confirmResult = MessageBox.Show("Convert <" + m.Name + ">'s material to c9990_dummy.mtd?",
                                   "Convertion",
                                   MessageBoxButtons.YesNo);
                    if (confirmResult == DialogResult.No)
                    {
                        continue;
                    }
                    if (m.MTD.IndexOf("_e") >= 0)
                    {
                        m.MTD = "N:\\NTC\\data\\Material\\mtd\\character\\c9990_dummy.mtd";
                    }
                    else
                    {
                        m.MTD = "N:\\NTC\\data\\Material\\mtd\\character\\c9990_dummy.mtd";
                    }

                    foreach (FLVER2.Texture t in m.Textures)
                    {
                        if (t.Path.IndexOf("_a.tif") >= 0)
                        {
                            t.Type = "Character_AMSN_snp_Texture2D_2_AlbedoMap_0";
                        }
                        else if (t.Path.IndexOf("_n.tif") >= 0)
                        {
                            t.Type = "Character_AMSN_snp_Texture2D_7_NormalMap_4";
                        }
                        else if (t.Path.IndexOf("_r.tif") >= 0)
                        {
                            t.Type = "g_SpecularTexture";
                        }

                    }
                }
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");



            };
            btnY += 50;

            Button tpfXmlEdit = new Button();
            ButtonTips("Auto-edit the xml file depacked from the /tpf texture file. So that you don't need to manually modify it to add new textures.\n" +
          "自动编辑.tpf贴图文件用yabber解包出来的xml文件。", tpfXmlEdit);
            tpfXmlEdit.Text = "Xml Edit";
            tpfXmlEdit.Location = new System.Drawing.Point(700, btnY);
            tpfXmlEdit.Click += (s, e) => {

                XmlEdit();

            };
            btnY += 50;


            Button mtdConvert = new Button();
            ButtonTips("Rename all the materials (mtd path) to the name you want.\n" +
          "自动转换所有材质路径为你输入的值。", mtdConvert);
            mtdConvert.Text = "M. Rename";
            mtdConvert.Location = new System.Drawing.Point(700, btnY);
            mtdConvert.Click += (s, e) => {
                string res = "M[ARSN].mtd";
                DialogResult dr = BasicTools.ShowInputDialog(ref res);
                if (dr == DialogResult.Cancel) { return; }
                foreach (var v in targetFlver.Materials)
                {
                    v.MTD = res;

                }
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");

            };
            btnY += 50;


            f.Size = new System.Drawing.Size(800, 600);
            p.Size = new System.Drawing.Size(650, 530);
            f.Resize += (s, e) =>
                {
                    p.Size = new System.Drawing.Size(f.Size.Width - 150, f.Size.Height - 70);
                    button.Location = new System.Drawing.Point(f.Size.Width - 100, 50);
                    button2.Location = new System.Drawing.Point(f.Size.Width - 100, 100);
                    button3.Location = new System.Drawing.Point(f.Size.Width - 100, 150);
                    button3ex.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
                    buttonARSN.Location = new System.Drawing.Point(f.Size.Width - 100, 250);
                    buttonDMY.Location = new System.Drawing.Point(f.Size.Width - 100, 300);
                    tpfXmlEdit.Location = new System.Drawing.Point(f.Size.Width - 100, 350);
                    mtdConvert.Location = new System.Drawing.Point(f.Size.Width - 100, 400);
                };

            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(button3ex);
            f.Controls.Add(buttonARSN);
            f.Controls.Add(buttonDMY);
            f.Controls.Add(tpfXmlEdit);
            f.Controls.Add(mtdConvert);
            f.ShowDialog();
            //Application.Run(f);



        }
        private static void XmlEdit()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog1;
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog() { Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*" };
            openFileDialog1.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            openFileDialog1.Title = "Choose .xml file depacked from .tpf file by Yabber";
            //openFileDialog1.ShowDialog();
            //MessageBox.Show("Import something?");
            String targetXml = "";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //Console.WriteLine(openFileDialog1.FileName);
                //openFileDialog1.
                targetXml = openFileDialog1.FileName;
                
            }
            else
            {
                /* Mono3D mono = new Mono3D();
                 mono.Run();*/
                return;
            }
            String parentDir = Path.GetDirectoryName(targetXml);
            String[] fileArray = Directory.GetFiles(parentDir, "*.dds");
            System.Console.Write(fileArray);
            String[] orgContent = File.ReadLines(targetXml).ToArray<String>();

            String newContent = "";
            for (int i =0; i < 7;i++)
            {
                newContent += orgContent[i] + "\r\n";
            }

            for (int i =0;i < fileArray.Length;i++)
            {
                
                newContent += "    <texture>" + "\r\n";
                newContent += "      <name>"+ Path.GetFileName(fileArray[i]) +"</name>" + "\r\n";

                String xmlFormat = "00";
                {
                  
                    if (MessageBox.Show("Is " + Path.GetFileName(fileArray[i]) + " a albedo(diffuse) texture?",
                                     "Set",
                                     MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        xmlFormat = "00";
                    }
                    else if (MessageBox.Show("Is " + Path.GetFileName(fileArray[i]) + " a normal texture?",
                                  "Set",
                                  MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        xmlFormat = "6A";
                    }
                   else if (MessageBox.Show("Is " + Path.GetFileName(fileArray[i]) + " a reflection/specular texture?",
                                   "Set",
                                   MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        xmlFormat = "00";
                    }
                }

                newContent += "      <format>0x" + xmlFormat +"</format>" + "\r\n";




                newContent += "      <flags1>00</flags1>" + "\r\n";
                newContent += "      <flags2>0x00000000</flags2>" + "\r\n";
                newContent += "    </texture>" + "\r\n";
            }


            newContent += "  </textures> \r\n   </tpf> ";
            File.WriteAllText(targetXml,newContent);


            MessageBox.Show("Xml auto edited!");
        }
        #endregion Material_Window

        


        static void materialQuickEdit(FLVER2.Material m , int mIndex = 0)
        {
            //MessageBox.Show("Now editing material:" + m.Name);
            Form f = new Form();
            f.Text = "Material quick editor : <" + m.Name + ">";
            Panel p = new Panel();
            List<TextBox> typeList = new List<TextBox>();
            List<TextBox> pathList = new List<TextBox>();
            int currentY = 10;

            Button btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Location = new System.Drawing.Point(500, 50);
         
            f.Controls.Add(btnOk);


            Button btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new System.Drawing.Point(500, 100);
            btnCancel.Click += (s, e) =>
            {
                f.Close();
            };
            f.Controls.Add(btnCancel);

            Button btnOkJs = new Button();
            btnOkJs.Text = "Json Mod";
            btnOkJs.Location = new System.Drawing.Point(500, 150);

            f.Controls.Add(btnOkJs);


            Label tName = new Label();
            tName.Size = new System.Drawing.Size(90, 15);
            tName.Location = new System.Drawing.Point(10, currentY);
            tName.Text = "Material Name";
            p.Controls.Add(tName);

            TextBox tName2 = new TextBox();
            tName2.Size = new System.Drawing.Size(200, 15);
            tName2.Location = new System.Drawing.Point(100, currentY);
            tName2.Text = m.Name;
            p.Controls.Add(tName2);

            currentY += 20;


            Label lMTD = new Label();
            lMTD.Size = new System.Drawing.Size(80, 15);
            lMTD.Location = new System.Drawing.Point(10, currentY);
            lMTD.Text = "Mtd path:";
            p.Controls.Add(lMTD);

            TextBox tMTD = new TextBox();
            tMTD.Size = new System.Drawing.Size(200, 15);
            tMTD.Location = new System.Drawing.Point(100, currentY);
            tMTD.Text = m.MTD;
            p.Controls.Add(tMTD);

            currentY += 20;



            btnOk.Click += (s, e) =>
            {
                m.Name = tName2.Text;
                m.MTD = tMTD.Text;

                for (int i2 = 0; i2 < m.Textures.Count; i2++)
                {

                    m.Textures[i2].Path = pathList[i2].Text;
                    m.Textures[i2].Type = typeList[i2].Text;
                }


                    autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Modification saved! Please exit the material window!");
                f.Close();
            };


         


            for (int i =0;i < m.Textures.Count;i++)
            {
                currentY += 20;

                Label lTYPE = new Label();
                lTYPE.Size = new System.Drawing.Size(40, 15);
                lTYPE.Location = new System.Drawing.Point(10, currentY);
                lTYPE.Text = "Type:";
                p.Controls.Add(lTYPE);

                TextBox tTYPE = new TextBox();
                tTYPE.Size = new System.Drawing.Size(340, 15);
                tTYPE.Location = new System.Drawing.Point(60, currentY);
                tTYPE.Text = m.Textures[i].Type;
                p.Controls.Add(tTYPE);
                typeList.Add(tTYPE);

                currentY += 20;


                Label lPATH = new Label();
                lPATH.Size = new System.Drawing.Size(40, 15);
                lPATH.Location = new System.Drawing.Point(10, currentY);
                lPATH.Text = "Path:";
                p.Controls.Add(lPATH);

                TextBox tPATH = new TextBox();
                tPATH.Size = new System.Drawing.Size(340, 15);
                tPATH.Location = new System.Drawing.Point(60, currentY);
                tPATH.Text = m.Textures[i].Path;
                p.Controls.Add(tPATH);
                pathList.Add(tPATH);

                Button btnBrowse = new Button();
                btnBrowse.Text = "Browse";
                btnBrowse.Size = new System.Drawing.Size(60, 20);
                btnBrowse.Location = new System.Drawing.Point(410, currentY);
                p.Controls.Add(btnBrowse);

                btnBrowse.Click += (s, e) =>
                {
                    var openFileDialog2 = new OpenFileDialog();
                    openFileDialog2.Filter = "DDS Texture Files (DDS)|*.DDS";
                    
                    if (openFileDialog2.ShowDialog() == DialogResult.OK)
                    {
                        string fn = openFileDialog2.FileName;
                        string fnn = Path.GetFileNameWithoutExtension(fn);
                        //MessageBox.Show("Opened:" + fnn);
                        tPATH.Text = fnn + ".tif";

                    }
                 };


                currentY += 20;
            }



            TextBox tJs = new TextBox();
            tJs.Size = new System.Drawing.Size(400, 300);
            tJs.Multiline = true;
            tJs.Location = new System.Drawing.Point(10, currentY);
            tJs.Text = new JavaScriptSerializer().Serialize(m);
            p.Controls.Add(tJs);

            currentY += 20;

            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);

            btnOkJs.Click += (s, e) =>
            {
                // for () { }
                targetFlver.Materials[mIndex] = new JavaScriptSerializer().Deserialize<FLVER2.Material>(tJs.Text);
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Modification saved! Please exit the material window!");
                f.Close();
            };

            f.Size = new System.Drawing.Size(600, 600);
            p.Size = new System.Drawing.Size(500, 580);
            f.Resize += (s, e) =>
            {
                p.Size = new System.Drawing.Size(500, f.Size.Height - 70);

            };
            f.ShowDialog();
        }

        
        //1.73 New
        /// <summary>
        /// Shift bone weights
        /// </summary>
        /// <param name="newNodes">The new bones list</param>
        public static void BoneWeightShift(List<FLVER.Node> newNodes)
        {
    
            //Step 1 build a int table to map old bone index -> new bone index
            int[] boneMapTable = new int[targetFlver.Nodes.Count];
            for (int i =0;i<targetFlver.Nodes.Count;i++)
            {
                boneMapTable[i] = findNewIndex(newNodes,i);


            }


            //Step 2 according to the table, change all the vertices' bone weights
            foreach (var v in vertices)
            {
                for (int i =0;i < v.BoneIndices.Length;i++)
                {
                    v.BoneIndices[i] = boneMapTable[v.BoneIndices[i]];

                }

            }
        }


        //Find Bone index, if no such bone find its parent's index
        public static int findNewIndex(List<FLVER.Node> newNodes, int oldBoneIndex)
        {
            int ans = 0;
            string oldBoneName = targetFlver.Nodes[oldBoneIndex].Name;
            for (int i =0;i < 5;i++)
            {
                ans = findNewIndexByName(newNodes,oldBoneName);
                if (ans >= 0 ) { return ans; }
                oldBoneIndex = targetFlver.Nodes[oldBoneIndex].ParentIndex;
                if (oldBoneIndex < 0) { return 0; }
                oldBoneName = targetFlver.Nodes[oldBoneIndex].Name;
            }


            return 0;
        }

        public static int findNewIndexByName(List<FLVER.Node> newNodes, string oldBoneName) {

            for (int i =0; i < newNodes.Count;i++)
            {
                if (oldBoneName == newNodes[i].Name)
                {
                    return i;
                }
            }

            return -1;
        }


        public static void ButtonTips(string tips, Button btn)
        {
            System.Windows.Forms.ToolTip ToolTip1 = new System.Windows.Forms.ToolTip();
            ToolTip1.SetToolTip(btn, tips);

        }



       /// <summary>
       /// Find the file name  without its path name and extension name.
       /// </summary>
       /// <param name="arg">Input.</param>
       /// <returns></returns>
        public static string FindFileName(string arg)
        {
            int startIndex = arg.LastIndexOf('/' );

            int altStartIndex = arg.LastIndexOf('\\');

            if (altStartIndex > startIndex)
            {
                startIndex = altStartIndex;
            }

            int endIndex = arg.LastIndexOf('.');
            if (startIndex <0) { startIndex = 0; }
            if (endIndex >=0) {
                //maye "..\\aquatools" endindex = 1 startIndex = 2
                if (startIndex >= endIndex) { endIndex = arg.Length; }

                string res = arg.Substring(startIndex , endIndex - startIndex );
                if ((res.ToCharArray())[0] == '\\'  || (res.ToCharArray())[0] == '/')
                {
                    res = res.Substring(1);
                }
                return res; }

            return arg;
        }


        public static void SetMeshInfoToDefault()
        {


            int layoutCount = targetFlver.BufferLayouts.Count;
            FLVER2.BufferLayout newBL = new FLVER2.BufferLayout();
            
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.Float3, FLVER.LayoutSemantic.Position, 0));
            newBL.Add(new FLVER.LayoutMember( FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Normal, 0));
            newBL.Add(new FLVER.LayoutMember( FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Tangent, 0));
            newBL.Add(new FLVER.LayoutMember( FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Tangent, 1));
            
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.BoneIndices, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.BoneWeights, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.VertexColor, 1));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.Short4, FLVER.LayoutSemantic.UV, 0));
            
            targetFlver.BufferLayouts.Add(newBL);
            
            foreach (FLVER2.Mesh mn in targetFlver.Meshes)
            {

                //FLVER2.Mesh mn = new FLVER2.Mesh();
                // mn.MaterialIndex = 0;
                // mn.BoneIndices = new List<int>();
                // mn.BoneIndices.Add(0);
                // mn.BoneIndices.Add(1);
                mn.BoundingBox = new FLVER2.Mesh.BoundingBoxes();
                mn.BoundingBox.Min = new Vector3(-1, -1, -1);
                mn.BoundingBox.Max = new Vector3(1, 1, 1);
                mn.BoundingBox.Unk = new Vector3();
                //mn.Unk1 = 0;
                
                mn.NodeIndex = 0;
                mn.Dynamic = 1;
                 mn.VertexBuffers = new List<FLVER2.VertexBuffer>();
                 mn.VertexBuffers.Add(new FLVER2.VertexBuffer(layoutCount));
                //  mn.Vertices = new List<FLVER.Vertex>();
                var varray = mn.FaceSets[0].Indices;
                
                mn.FaceSets = new List<FLVER2.FaceSet>();
                //FLVER.Vertex myv = new FLVER.Vertex();
                //myv.Colors = new List<FLVER.VertexColor>();
                //FLVER.Vertex v = generateVertex(new Vector3(vit.X, vit.Y, vit.Z), uv1.toNumV3(), uv2.toNumV3(), normal.toNumV3(), tangent.toNumV3(), 1);
            
                for (int i = 0; i < mn.Vertices.Count;i++)
                {
                    FLVER.Vertex vit = mn.Vertices[i];
            
                    mn.Vertices[i] = generateVertex(new Vector3(vit.Position.X, vit.Position.Y, vit.Position.Z), 
                        vit.UVs[0], vit.UVs[0], vit.Normal, 
                        vit.Tangents[0], 
                        1);
                    mn.Vertices[i].BoneIndices = vit.BoneIndices;
                    mn.Vertices[i].BoneWeights = vit.BoneWeights;
            
                }
            
                mn.FaceSets.Add(generateBasicFaceSet());
                mn.FaceSets[0].Indices = varray;
                mn.FaceSets[0].CullBackfaces = false;
                //mn.FaceSets[0].Unk06 = 17;
                if (mn.FaceSets[0].Indices.Count > 65534)
                {
              
                    //MessageBox.Show("There are more than 65535 vertices in a mesh , switch to 32 bits index size mode.");
                    //Now SoulsFormatsNEXT automatically calculates indexSize!
                    //OLD mn.FaceSets[0].IndexSize = 32;
                }
            }



        }

        public static void SetFlverMatPath(FLVER2.Material m, string typeName, string newPath)
        {
            for (int i=0;i < m.Textures.Count;i++)
            {
                if (m.Textures[i].Type == typeName)
                {
                    m.Textures[i].Path = newPath;
                    return;
                }


            }

            FLVER2.Texture tn = new FLVER2.Texture();
            tn.Type = typeName;
            tn.Path = newPath;
            tn.Scale = new Vector2 (1, 1);
            //tn.ScaleX = 1;
            //tn.ScaleY = 1;
            tn.Unk10 = 1;
            tn.Unk11 = true;
            m.Textures.Add(tn);
        }

        public static DataTable ToDataTable<T>(IList<T> data)
        {
            System.ComponentModel.PropertyDescriptorCollection props =
            System.ComponentModel.TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            for (int i = 0; i < props.Count; i++)
            {
                System.ComponentModel.PropertyDescriptor prop = props[i];
                table.Columns.Add(prop.Name, prop.PropertyType);
            }
            object[] values = new object[props.Count];
            foreach (T item in data)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = props[i].GetValue(item);
                }
                table.Rows.Add(values);
            }
            return table;
        }
    }


}
