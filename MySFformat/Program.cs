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
using System.Data;



namespace MySFformat
{
    public class VertexInfo
    {
     public int meshIndex = 0;
      public  uint vertexIndex = 0;

    }
    static partial class Program 
    {
        public static FLVER targetFlver;
        public static TPF targetTPF = null;
        public static string flverName;
        public static List<DataGridViewTextBoxCell> boneNameList;
        public static List<TextBox> parentList;
        public static List<TextBox> childList;
        public static List<VertexInfo> verticesInfo = new List<VertexInfo>();

        public static Vector3D[] bonePosList = new Vector3D[1000];


        public static Dictionary<String, String> boneParentList;
        public static List<FLVER.Vertex> vertices = new List<FLVER.Vertex>();
        public static Mono3D mono;

        public static string orgFileName = "";


        public static TextBox flexA;
        public static TextBox flexB;
        public static TextBox flexC;

        public static Vector3 checkingPoint;
        public static Vector3 checkingPointNormal;
        public static Boolean useCheckingPoint = false;

        public static int checkingMeshNum = 0;
        public static Boolean useCheckingMesh = false;

        /***settings***/
        public static Boolean basicMode = false;
        public static Boolean loadTexture = true;
        public static Boolean show3D = false;
        public static Boolean legacyDisplay = false;
        public static Boolean smooth = false;
        public static int boneFindParentTimes = 15;//if cannot find bone, find if its parent bone matches flver bone name


        public static Boolean boneDisplay = false;
        public static Boolean dummyDisplay = true;

        public static Boolean setVertexPos = false;
        public static float setVertexX = 0;
        public static float setVertexY = 1.75f;
        public static float setVertexZ = 0;

        public static RotationOrder rotOrder = RotationOrder.YZX;

        public static string version = "1.971";

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
            //ExportFBX();
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
                    ans.Add(new VertexPositionColor(toXnaV3XZY(vi.Positions[0]), Microsoft.Xna.Framework.Color.Black));

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
      


        public static void updateVertices()
        {

            if (legacyDisplay) { updateVerticesLegacy();return; }
            List<VertexPositionColor> ans = new List<VertexPositionColor>();
            List<VertexPositionColor> triangles = new List<VertexPositionColor>();
            List<VertexPositionColorTexture> textureTriangles = new List<VertexPositionColorTexture>();
            vertices.Clear();
            verticesInfo.Clear();
            List<MeshInfos> mis = new List<MeshInfos>();

            for (int i = 0; i < targetFlver.Meshes.Count; i++)
            {
                // int currentV = 0;
                //Microsoft.Xna.Framework.Vector3[] vl = new Microsoft.Xna.Framework.Vector3[3];
                if (targetFlver.Meshes[i] == null) { continue; }


                bool renderBackFace = false;
                if (targetFlver.Meshes[i].FaceSets.Count > 0)
                {
                    if (targetFlver.Meshes[i].FaceSets[0].CullBackfaces == false) { renderBackFace = true; }
                }
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
                    if (mono.flatShading) { value = 255; }
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

                    if (loadTexture)
                    {
                        textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[0].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[0].UVs[0].X, vl[0].UVs[0].Y)));
                        textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[2].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[2].UVs[0].X, vl[2].UVs[0].Y)));
                        textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[1].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[1].UVs[0].X, vl[1].UVs[0].Y)));

                    }



                    if (renderBackFace)
                    {
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[0].Positions[0]), c));
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[1].Positions[0]), c));
                        triangles.Add(new VertexPositionColor(toXnaV3XZY(vl[2].Positions[0]), c));


                        if (loadTexture)
                        {
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[0].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[0].UVs[0].X, vl[0].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[1].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[1].UVs[0].X, vl[1].UVs[0].Y)));
                            textureTriangles.Add(new VertexPositionColorTexture(toXnaV3XZY(vl[2].Positions[0]), c, new Microsoft.Xna.Framework.Vector2(vl[2].UVs[0].X, vl[2].UVs[0].Y)));

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
                }

                MeshInfos mi = new MeshInfos();
               var tName = targetFlver.Materials[ targetFlver.Meshes[i].MaterialIndex].Textures[0].Path;
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
                bonePosList[i] = null;

            }

            //Calcaulte bone global space

            //bone space X,Y,Z axis
            Vector3D[] bsX = new Vector3D[targetFlver.Bones.Count];
            Vector3D[] bsY = new Vector3D[targetFlver.Bones.Count];
            Vector3D[] bsZ = new Vector3D[targetFlver.Bones.Count];

            //bone space origin 
            Vector3D[] bso = new Vector3D[targetFlver.Bones.Count];


            int A = 1;
            int B = 2;
            int C = 3;


          //  deprecated bone generate functionality
                {
                /*
                for (int i = 0; i < targetFlver.Bones.Count && boneDisplay; i++)
                {
                    FLVER.Bone b = targetFlver.Bones[i];

                    bsX[i] = new Vector3D(0,0,1);
                    bsY[i] = new Vector3D(0, 1, 0);
                    bsZ[i] = new Vector3D(-1, 0, 0);
                    bso[i] = new Vector3D();
                    //targetFlver.Bones[i].Translation
                    if (targetFlver.Bones[i].ParentIndex == -1 && targetFlver.Bones[i].ChildIndex == -1) { continue; }
                    if (targetFlver.Bones[i].ParentIndex == -1 ) {

                        bso[i] = new Vector3D(b.Translation.X, b.Translation.Y, b.Translation.Z);

                        Vector3D nx = new Vector3D(bsX[i].toNumV3());
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsX[i].toNumV3(), A * b.Rotation.X));
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsY[i].toNumV3(), B * b.Rotation.Y));
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsZ[i].toNumV3(), C * b.Rotation.Z));



                        Vector3D ny = new Vector3D(bsY[i].toNumV3());
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsX[i].toNumV3(), A * b.Rotation.X));
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsY[i].toNumV3(), B * b.Rotation.Y));
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsZ[i].toNumV3(), C * b.Rotation.Z));


                        Vector3D nz = new Vector3D(bsZ[i].toNumV3());
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsX[i].toNumV3(), A * b.Rotation.X));
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsY[i].toNumV3(),B * b.Rotation.Y));
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsZ[i].toNumV3(), C * b.Rotation.Z));
                        nx = nx * b.Scale.X;
                        ny = ny * b.Scale.Y;
                        ny = ny * b.Scale.Z;

                        bsX[i] = nx;
                        bsY[i] = ny;
                        bsZ[i] = nz;

                        continue;

                    }
                    int pbid = targetFlver.Bones[i].ParentIndex;
                    if (bsX[pbid] != null)
                    {

                         float r1 = b.Rotation.Y;
                         float r2 = b.Rotation.X;
                         float r3 = b.Rotation.Z;
                         if (A == 1) { r1 = b.Rotation.X; }
                         if (A == 2) { r1 = b.Rotation.Y; }
                         if (A == 3) { r1 = b.Rotation.Z; }

                         if (B == 1) { r2 = b.Rotation.X; }
                         if (B == 2) { r2 = b.Rotation.Y; }
                         if (B == 3) { r2 = b.Rotation.Z; }

                         if (C == 1) { r3 = b.Rotation.X; }
                         if (C == 2) { r3 = b.Rotation.Y; }
                         if (C == 3) { r3 = b.Rotation.Z; }
                        bsX[i] = new Vector3D(bsX[pbid].X, bsX[pbid].Y, bsX[pbid].Z);
                        bsY[i] = new Vector3D(bsY[pbid].X, bsY[pbid].Y, bsY[pbid].Z);
                        bsZ[i] = new Vector3D(bsZ[pbid].X, bsZ[pbid].Y, bsZ[pbid].Z);

                        Vector3D nx = new Vector3D(bsX[i].toNumV3());
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsX[i].toNumV3(), r1));
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsY[i].toNumV3(), r2));
                        nx = new Vector3D(RotateLine(nx.toNumV3(), new Vector3(), bsZ[i].toNumV3(), r3));

                        Vector3D ny = new Vector3D(bsY[i].toNumV3());
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsX[i].toNumV3(), r1));
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsY[i].toNumV3(),r2));
                        ny = new Vector3D(RotateLine(ny.toNumV3(), new Vector3(), bsZ[i].toNumV3(), r3));

                        Vector3D nz = new Vector3D(bsZ[i].toNumV3());
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsX[i].toNumV3(),r1));
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsY[i].toNumV3(), r2));
                        nz = new Vector3D(RotateLine(nz.toNumV3(), new Vector3(), bsZ[i].toNumV3(), r3));

                        nx = nx * b.Scale.X;
                        ny = ny * b.Scale.Y;
                        ny = ny * b.Scale.Z;


                        bsX[i] = nx;
                        bsY[i] = ny;
                        bsZ[i] = nz;

                        bso[i] = new Vector3D(bso[pbid].X, bso[pbid].Y, bso[pbid].Z);

                        bso[i] = bso[i] + b.Translation.X * bsX[pbid] + b.Translation.Y * bsY[pbid] + b.Translation.Z * bsZ[pbid];
                    }

                }


                for (int i = 0; i < targetFlver.Bones.Count && boneDisplay ; i++)
                {
                    //targetFlver.Bones[i].Translation
                    if (targetFlver.Bones[i].ParentIndex ==  -1 && targetFlver.Bones[i].ChildIndex == -1) { continue; }

                    Vector3 actPos = findBoneTrans(targetFlver.Bones,i,new Vector3());

                    actPos = bso[i].toNumV3();

                    Vector3D forward = new Vector3D(0f,0.05f,0f);
                    float pitch = targetFlver.Bones[i].Rotation.X;
                    float roll = targetFlver.Bones[i].Rotation.Z;
                    float yaw = targetFlver.Bones[i].Rotation.Y;
                    Vector3 r = targetFlver.Bones[i].Rotation;
                    //forward = new Vector3D( RotatePoint(forward.toNumV3(),pitch,roll,yaw));
                    forward = new Vector3D(RotatePoint(forward.toNumV3(), r.Y,r.X,r.Z));

                    forward = bsX[i] * 0.1f;


                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X - 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                     ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                     ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z - 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                     ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z + 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                     ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z, actPos.Y - 0.025f), Microsoft.Xna.Framework.Color.Purple));
                     ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z, actPos.Y + 0.025f), Microsoft.Xna.Framework.Color.Purple));

                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X - 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + forward.X, actPos.Z + forward.Z, actPos.Y + forward.Y), Microsoft.Xna.Framework.Color.Purple));

                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + forward.X, actPos.Z + forward.Z, actPos.Y + forward.Y), Microsoft.Xna.Framework.Color.Purple));

                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z - 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + forward.X, actPos.Z + forward.Z, actPos.Y + forward.Y), Microsoft.Xna.Framework.Color.Purple));

                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X , actPos.Z + 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                    ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + forward.X, actPos.Z + forward.Z, actPos.Y + forward.Y), Microsoft.Xna.Framework.Color.Purple));


                }
                */
            }
            //

            if (boneDisplay)
            {
                Transform3D[] boneTrans = new Transform3D[targetFlver.Bones.Count];
                for (int i=0;i< targetFlver.Bones.Count;i++)
                {
                    boneTrans[i] = new Transform3D();
                    boneTrans[i].rotOrder = rotOrder;
                    boneTrans[i].position = new Vector3D(targetFlver.Bones[i].Translation);
                    boneTrans[i].setRotationInRad(new Vector3D(targetFlver.Bones[i].Rotation));
                    boneTrans[i].scale = new Vector3D(targetFlver.Bones[i].Scale);

                    if (targetFlver.Bones[i].ParentIndex >= 0)
                    {
                        boneTrans[i].parent = boneTrans[targetFlver.Bones[i].ParentIndex];

                        Vector3D actPos = boneTrans[i].getGlobalOrigin();
                        /* ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X - 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X + 0.025f, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z - 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z + 0.025f, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z, actPos.Y - 0.025f), Microsoft.Xna.Framework.Color.Purple));
                         ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z, actPos.Y + 0.025f), Microsoft.Xna.Framework.Color.Purple));*/


                        if (boneTrans[targetFlver.Bones[i].ParentIndex] != null)
                        {
                            Vector3D parentPos = boneTrans[targetFlver.Bones[i].ParentIndex].getGlobalOrigin();

                            ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(parentPos.X - 0.005f, parentPos.Z - 0.005f, parentPos.Y), Microsoft.Xna.Framework.Color.Purple));
                            ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));

                            ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(parentPos.X + 0.005f, parentPos.Z + 0.005f, parentPos.Y), Microsoft.Xna.Framework.Color.Purple));
                            ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(actPos.X, actPos.Z, actPos.Y), Microsoft.Xna.Framework.Color.Purple));
                        }
                       
                    }

                    

                }

                
            }



            for (int i = 0; i < targetFlver.Dummies.Count && dummyDisplay; i++)
            {
                FLVER.Dummy d = targetFlver.Dummies[i];

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X - 0.025f, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X + 0.025f, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X, d.Position.Z - 0.025f, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X, d.Position.Z + 0.025f, d.Position.Y), Microsoft.Xna.Framework.Color.Purple));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X, d.Position.Z, d.Position.Y), Microsoft.Xna.Framework.Color.Green));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(d.Position.X + d.Forward.X, d.Position.Z + d.Forward.Z, d.Position.Y + d.Forward.Y), Microsoft.Xna.Framework.Color.Green));

            }

            if (useCheckingPoint)
            {
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X - 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X + 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y), Microsoft.Xna.Framework.Color.AntiqueWhite));

                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X , checkingPoint.Z , checkingPoint.Y), Microsoft.Xna.Framework.Color.Blue));
                ans.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(checkingPoint.X +  0.2f * checkingPointNormal.X, checkingPoint.Z + 0.2f * checkingPointNormal.Z, checkingPoint.Y + 0.2f * checkingPointNormal.Y), Microsoft.Xna.Framework.Color.Blue));


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


        /// <summary>
        /// Start window
        /// </summary>
        static void ModelAdjModule()
        {

            System.Windows.Forms.OpenFileDialog openFileDialog1;
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            openFileDialog1.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            openFileDialog1.Title = "Choose fromsoftware .flver model file. by Forsaknsilver";
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
            string fname = openFileDialog1.FileName;
            FLVER b = null;
            if (fname.Length > 4) 
            {
                if (openFileDialog1.FileName.Substring(fname.Length-4) == ".dcx") 
                {
                    //遇到不是DS3,BB的情况会报错，这时候进入DCX状态
                    SoulsFormats.BND4 bnds = null;
                    List<BinderFile> flverFiles = new List<BinderFile>();
                    try
                    {
                       
                        //Support BND4(DS2,DS3,BB) only
                        bnds = SoulsFormats.SoulsFile<SoulsFormats.BND4>.Read(openFileDialog1.FileName);
                    }
                    catch (Exception e) //进入dcx状态
                    {
                        Console.WriteLine("Is not BND4... Try DCX decompress");
                        var fileName = openFileDialog1.FileName;
                        byte[] bytes = DCX.Decompress(fileName, out DCX.Type compression);
                        if (BND4.Is(bytes))
                        {
                            Console.WriteLine($"Unpacking BND4: {fileName}...");
                            bnds = SoulsFormats.SoulsFile<SoulsFormats.BND4>.Read(bytes);
                        }

                        //throw e;

                    }
                    if (bnds == null)
                    {
                        MessageBox.Show("Read error.");
                        Application.Exit();
                    }
                    Form cf = new Form();
                        cf.Size = new System.Drawing.Size(520, 400);
                        cf.Text = "Select the flver file you want to view";
                        cf.FormBorderStyle = FormBorderStyle.FixedDialog;

                        ListBox lv = new ListBox();
                        lv.Size = new System.Drawing.Size(490, 330);
                        lv.Location = new System.Drawing.Point(10, 10);
                        lv.MultiColumn = false;

                        foreach (var bf in bnds.Files)
                        {
                            //  MessageBox.Show("Found:" + bf.Name);

                            if (bf.Name.Contains(".flver"))
                            {
                                flverFiles.Add(bf);
                                lv.Items.Add(bf.Name);
                            }
                            else if (bf.Name.Length >= 4 && loadTexture)
                            {
                                if (bf.Name.Substring(bf.Name.Length - 4) == ".tpf")
                                {
                                    try { targetTPF = TPF.Read(bf.Bytes); } catch (Exception e) { MessageBox.Show("Unsupported tpf file"); }
                                }
                            }

                        }

                        Button select = new Button();
                        select.Text = "Select";
                        select.Size = new System.Drawing.Size(490, 20);
                        select.Location = new System.Drawing.Point(10, 340);
                        select.Click += (s, e) =>
                        {
                            if (lv.SelectedIndices.Count == 0) { return; }
                            b = FLVER.Read(flverFiles[lv.SelectedIndices[0]].Bytes);
                            openFileDialog1.FileName = openFileDialog1.FileName + "." + FindFileName(flverFiles[0].Name) + ".flver";
                            flverName = openFileDialog1.FileName;
                            cf.Close();
                        };
                        cf.Controls.Add(lv);
                        cf.Controls.Add(select);

                        if (flverFiles.Count == 0)
                        {
                            MessageBox.Show("No FLVER files found!");

                            return;
                        }
                        else if (flverFiles.Count == 1)
                        {
                            b = FLVER.Read(flverFiles[0].Bytes);
                            openFileDialog1.FileName = openFileDialog1.FileName + "." + FindFileName(flverFiles[0].Name) + ".flver";
                            flverName = openFileDialog1.FileName;
                        }
                        else
                        {
                            cf.ShowDialog();
                        }

                  
                   

                  
                   // MessageBox.Show("Entering dcx mode");
                    //Application.Exit();
                }
            }


            if (b == null) 
            {
                b = FLVER.Read(openFileDialog1.FileName);
                flverName = openFileDialog1.FileName;
            }



            targetFlver = b;

            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.CurrentThread.IsBackground = true;
                mono = new Mono3D();
                if (show3D) 
                {
                    updateVertices();
                    mono.Run();
                }


            }).Start();



       

            Form f = new Form();
            f.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            f.Text = "FLVER Bones - " + openFileDialog1.FileName;
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            boneNameList = new List<DataGridViewTextBoxCell>();
            parentList = new List<TextBox>();
            childList = new List<TextBox>();

            var boneParentList = new List<DataGridViewTextBoxCell>();
            var boneChildList = new List<DataGridViewTextBoxCell>();
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);

            //Basic mod disabled
            /* if (b.Bones.Count > 80)
             {
                 var confirmResult = MessageBox.Show("looks like there are too many bones, do you want the basic mode to save CPU resources?",
                                   "Caution",
                                   MessageBoxButtons.YesNo);
                 if (confirmResult == DialogResult.Yes)
                 {
                     basicMode = true;
                 }
             }*/
            DataGridView dg = new DataGridView();

            /* int index = dg.Rows.Add();
             dg.Rows[index].Cells[0].Value = "1";
             dg.Rows[index].Cells[1].Value = "2";
             dg.Rows[index].Cells[2].Value = "监听";*/
            var bindingList = new System.ComponentModel.BindingList<FLVER.Bone>(b.Bones);
            //System.Data.DataTable dt = ToDataTable(b.Bones);
            //DataTable dt = new DataTable();
            //dt.Columns.Add(new DataColumn("index", typeof(string)));

            dg.Columns.Add("Index", "Index");
            dg.Columns[0].Width = 50;
            dg.Columns.Add("Name", "Name");
            dg.Columns.Add("ParentID", "ParentID");
            dg.Columns[2].Width = 70;
            dg.Columns.Add("ChildID", "ChildID");
            dg.Columns[3].Width = 70;
            dg.Columns.Add("Position", "Position");
            dg.Columns.Add("Scale", "Scale");
            dg.Columns.Add("Rotation", "Rotation");



            // dt.Columns["colStatus"].Expression = String.Format("IIF(colBestBefore < #{0}#, 'Ok','Not ok')", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            //dt.Rows.Add(DateTime.Now.AddDays(-1));
            //  dt.Rows.Add(DateTime.Now.AddDays(1));
            //  dt.Rows.Add(DateTime.Now.AddDays(2));
            //  dt.Rows.Add(DateTime.Now.AddDays(-2));

            foreach (DataGridViewColumn column in dg.Columns)
            {

                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            dg.Location = new System.Drawing.Point(10, 10);
            dg.Size = new System.Drawing.Size(380, 450); ;
            dg.RowHeadersVisible = false;
            // dg.DataSource = dt;


            if (basicMode == false)
            {

                for (int i = 0; i < b.Bones.Count; i++)

                {
                    // foreach (FLVER.Bone bn in b.Bones)
                    FLVER.Bone bn = b.Bones[i];
                    //Console.WriteLine(bn.Name);

                    /* TextBox t = new TextBox();
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
                     t4.Text = bn.Translation.X + "," + bn.Translation.Y + "," + bn.Translation.Z;
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
                     boneNameList.Add(t);
                     parentList.Add(t2);
                     childList.Add(t3);*/

                    DataGridViewRow row = new DataGridViewRow();
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = "[" + i + "]";

                        row.Cells.Add(textboxcell);
                        textboxcell.ReadOnly = true;
                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.Name;

                        row.Cells.Add(textboxcell);
                        boneNameList.Add(textboxcell);
                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.ParentIndex + "";

                        row.Cells.Add(textboxcell);
                        boneParentList.Add(textboxcell);
                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.ChildIndex + "";

                        row.Cells.Add(textboxcell);
                        boneChildList.Add(textboxcell);
                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.Translation.X + "," + bn.Translation.Y + "," + bn.Translation.Z;

                        row.Cells.Add(textboxcell);

                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.Scale.X + "," + bn.Scale.Y + "," + bn.Scale.Z;

                        row.Cells.Add(textboxcell);

                    }
                    {
                        DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                        textboxcell.Value = bn.Rotation.X + "," + bn.Rotation.Y + "," + bn.Rotation.Z;

                        row.Cells.Add(textboxcell);

                    }
                    //DataGridViewComboBoxCell comboxcell = new DataGridViewComboBoxCell();
                    //row.Cells.Add(comboxcell);
                    dg.Rows.Add(row);

                }
            }

            f.Size = new System.Drawing.Size(550, 700);
            p.Size = new System.Drawing.Size(400, 530);

            p.Controls.Add(dg);
            currentY += 450;




            //Swift value usage
            /*
            {
                flexA = new TextBox();
                flexA.Text = "1";
                flexA.Location = new System.Drawing.Point(20,currentY);
                p.Controls.Add(flexA);

                currentY += 20;

                flexB = new TextBox();
                flexB.Text = "2";
                flexB.Location = new System.Drawing.Point(20, currentY);
                p.Controls.Add(flexB);

                currentY += 20;

                flexC = new TextBox();
                flexC.Text = "3";
                flexC.Location = new System.Drawing.Point(20, currentY);
                p.Controls.Add(flexC);

                currentY += 20;

            }*/
           

            //f.Show();

            Button button = new Button();
            ButtonTips("Save the changes you made in the bones part.（Such as changing parents ID, bone names...）\n" +
                "保存你在Bones部分做出的修改。(改骨骼名称以及父骨骼ID)", button);
            button.Text = "Modify";
            button.Location = new System.Drawing.Point(435, 50);
            button.Click += (s, e) => {
                for (int i2 = 0; i2 < b.Bones.Count; i2++)
                {
                    if (boneNameList.Count < b.Bones.Count)
                    {
                        MessageBox.Show("Bone does not match, something modified?\nWill not save bone info but will save other things.");
                        break;
                    }
                    b.Bones[i2].Name = boneNameList[i2].Value.ToString();
                    b.Bones[i2].ParentIndex = short.Parse(boneParentList[i2].Value.ToString());//parentList[i2].Text
                    b.Bones[i2].ChildIndex = short.Parse(boneChildList[i2].Value.ToString());
                }
                autoBackUp(); targetFlver.Write(flverName);
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
            tbones.Location = new System.Drawing.Point(10, currentY + 5);
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
            ButtonTips("Open the material window.\n" +
                "打开材质编辑窗口。", button2);
            button2.Text = "Material";
            button2.Location = new System.Drawing.Point(435, 100);
            button2.Click += (s, e) => {
                ModelMaterial();
            };

            Button button3 = new Button();
            ButtonTips("Open the mesh window.\n" +
    "打开面片编辑(Mesh)窗口。", button3);
            button3.Text = "Mesh";
            button3.Location = new System.Drawing.Point(435, 150);
            button3.Click += (s, e) => {
                ModelMesh();
            };
            Button button4 = new Button();
            ButtonTips("[Deprecated]Swap mesh & other info between one flver file with another. A new .flvern file will be generated.\n" +
                "It is a deprecated method, I recommend you using Mesh->Attach method instead.\n" +
"【过时】替换第一个Flver文件的模型信息为第二个，会生成一个.flvern文件。\n" +
"现在这个方法已经过时了请用Mesh->Attach方法！", button4);
            button4.Text = "Swap";
            button4.Location = new System.Drawing.Point(435, 200);
            button4.Click += (s, e) => {
                ModelSwapModule();
            };

            Button button_dummy = new Button();
            ButtonTips("Open the dummy window. Dummy contains the info about weapon art trail, weapon trail, damage point etc.\n" +
"打开辅助点(Dummy)窗口。辅助点包含了武器的一些剑风位置，伤害位置之类的信息。", button_dummy);
            button_dummy.Text = "Dummy";
            button_dummy.Location = new System.Drawing.Point(435, 250);
            button_dummy.Click += (s, e) => {
                dummies();
            };


            Button button5 = new Button();

            button5.Text = "ModifyJson";
            ButtonTips("Save the json text you modified in bones and header json part to the flver file.\n" +
"存储你修改的Json文本信息至你的Flver文件内。", button5);
            button5.Location = new System.Drawing.Point(435, 300);
            button5.Click += (s, e) => {
                b.Bones = serializer.Deserialize<List<FLVER.Bone>>(tbones.Text);
                b.Header = serializer.Deserialize<FLVER.FLVERHeader>(tbones2.Text);
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Json bone change completed! Please exit the program!", "Info");
            };


            Button button6 = new Button();

            button6.Text = "LoadJson";
            ButtonTips("Read external bone json file.\n" +
"读取外部包含骨骼信息的json文件到你的flver文件内。", button6);
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
                        
                        var confirmResult = MessageBox.Show("Do you want to shift bone weights to new bone?",
                                 "Set",
                                 MessageBoxButtons.YesNo);
                        if (confirmResult == DialogResult.Yes)
                        {
                            List <FLVER.Bone> newBones = serializer.Deserialize<List<FLVER.Bone>>(res);

                            BoneWeightShift(newBones);

                            targetFlver.Bones = newBones;
                        }
                        else {

                            targetFlver.Bones = serializer.Deserialize<List<FLVER.Bone>>(res);
                        }



                        autoBackUp(); targetFlver.Write(flverName);
                        MessageBox.Show("Bone change completed! Please exit the program!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }


            };

            Button button6ex = new Button();

            button6ex.Text = "ExportJson";
            ButtonTips("Export bones json text to a file.\n" +
"导出当前骨骼信息到一个json文件内。", button6ex);
            button6ex.Location = new System.Drawing.Point(435, 400);
            button6ex.Click += (s, e) => {

                /*var openFileDialog2 = new SaveFileDialog();
                openFileDialog2.FileName = "Bones.json";
                string res = "";
                if (openFileDialog2.ShowDialog() == DialogResult.OK)
                {
                    try
                    {

                        var sw = new StreamWriter(openFileDialog2.FileName);
                        sw.Write(FormatOutput(serializer.Serialize(b.Bones)));
                        sw.Close();
                        MessageBox.Show("Bones json text exported!", "Info");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                        $"Details:\n\n{ex.StackTrace}");
                    }
                }*/
                exportJson(FormatOutput(serializer.Serialize(b.Bones)),"Bones.json", "Bones json text exported!");

            };

            Button button7 = new Button();
            ButtonTips("Fix some bone problems when importing bloodborne models to Sekiro.\n" +
"修复一些血源诅咒转只狼后模型骨骼不对的问题。", button7);
            button7.Text = "BB_BoneFix";
            button7.Location = new System.Drawing.Point(435, 450);
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

                                    //It seems that X controls foot:left right rotate dir : -- means left,
                                    //I recommend X -= 0.3
                                    targetFlver.Bones[j].Rotation.Z = targetFlver.Bones[j].Rotation.Z + (float)Math.PI;
                                    targetFlver.Bones[j].Translation *= -1; //it seems that y contorls forward/backward, 
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


                autoBackUp(); targetFlver.Write(flverName);

                MessageBox.Show("BB pelvis bone fix completed! Please exit the program!", "Info");


            };

            Button button8 = new Button();
            ButtonTips("Check the flver file's buffer layout, which contains the rules of how to write flver file.\n" +
"检查Flver文件的buffer layout（一种存储如何写入顶点，骨骼之类方法的数据结构）。", button8);
            button8.Text = "BufferLayout";
            button8.Font = new System.Drawing.Font(button.Font.FontFamily, 7);
            button8.Location = new System.Drawing.Point(435, 500);
            //button8.AutoSize = true;
            button8.Click += (s, e) => {

                bufferLayout();


            };

            Button button9 = new Button();

            button9.Text = "ImportModel";
            ButtonTips("Import external model file, such as FBX, DAE, OBJ. Caution, only FBX file can keep the bone weight.\n" +
                "UV, normal, tangent can be kept, but you still need to manually modify material information in Material window.\n" +
                "Also,experimentally support meshes that has more than 65535 vertices.\n" +
"导入外部模型文件，比如Fbx,Dae,Obj。但注意只有Fbx文件可以支持导入骨骼权重。\n" +
"可以保留UV贴图坐标，切线法线的信息，但你还是得手动修改贴图信息的。\n" +
"另外，实验性质的加入了导入超过65535个顶点的面片集的功能。", button9);
            button9.Font = new System.Drawing.Font(button.Font.FontFamily, 8);
            //button9.AutoSize = true;
            button9.Location = new System.Drawing.Point(435, 550);
            button9.Click += (s, e) => {

                //importObj();
                importFBX();
            };

            Button button10 = new Button();

            button10.Text = "Export DAE";
            ButtonTips("<Experimental> Export current scene to DAE (Collada) 3d model file.\n" +
"<实验性质>到出场景至DAE模型文件。", button10);
            button10.Location = new System.Drawing.Point(435, 600);
            button10.Click += (s, e) => {

                ExportFBX();

            };

            Label thanks = new Label();
            thanks.Text = "FLVER Editor " + version + " Author: Forsakensilver(遗忘的银灵) Special thanks: TKGP & Katalash ";
            thanks.Location = new System.Drawing.Point(10, f.Size.Height - 60);
            thanks.Size = new System.Drawing.Size(700, 50);

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
                button6ex.Location = new System.Drawing.Point(f.Size.Width - 115, 400);

                button7.Location = new System.Drawing.Point(f.Size.Width - 115, 450);
                button8.Location = new System.Drawing.Point(f.Size.Width - 115, 500);
                button9.Location = new System.Drawing.Point(f.Size.Width - 115, 550);
                button10.Location = new System.Drawing.Point(f.Size.Width - 115, 600);

                thanks.Location = new System.Drawing.Point(10, f.Size.Height - 60);
                dg.Size = new System.Drawing.Size(f.Size.Width - 200, 450);
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
            f.Controls.Add(button6ex);
            f.Controls.Add(button7);
            f.Controls.Add(button8);
            f.Controls.Add(button9);

            f.Controls.Add(thanks);
            f.Controls.Add(button10);
            f.BringToFront();
            f.WindowState = FormWindowState.Normal;
            Application.Run(f);


            //ModelMaterial();
            //Application.Exit();
        }

        private static void Select_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

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
                    checkingPoint = new Vector3(targetFlver.Dummies[i].Position.X, targetFlver.Dummies[i].Position.Y, targetFlver.Dummies[i].Position.Z);
                    checkingPointNormal = new Vector3(targetFlver.Dummies[i].Forward.X * 0.2f, targetFlver.Dummies[i].Forward.Y*0.2f, targetFlver.Dummies[i].Forward.Z*0.2f);

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

                    targetFlver.Dummies[i].Position.X += float.Parse(tX.Text);
                    targetFlver.Dummies[i].Position.Y += float.Parse(tY.Text);
                    targetFlver.Dummies[i].Position.Z += float.Parse(tZ.Text);
                    autoBackUp(); targetFlver.Write(flverName);
                    updateVertices();
                }
                else {

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

            Button buttonFix = new Button();
            ButtonTips("Fix external weapon's weapon trail/lighting reversal problem in Sekiro by adding kusabimaru's dummy information.\n" +
               "写入契丸的辅助点信息以解决武器在只狼内没有剑风以及无法雷闪的问题。", buttonFix);
            buttonFix.Text = "SekiroFix";
            buttonFix.Location = new System.Drawing.Point(650, 200);
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
                buttonFix.Location = new System.Drawing.Point(f.Size.Width - 100, 200);
            };

            f.Controls.Add(button);
            f.Controls.Add(button2);
            f.Controls.Add(button3);
            f.Controls.Add(buttonFix);
            f.ShowDialog();
        }

        static int findFLVER_Bone(FLVER f, string name)
        {
            for (int flveri = 0; flveri < f.Bones.Count; flveri++)
            {
                if (f.Bones[flveri].Name == name)
                {

                    return flveri;

                }

            }
            return -1;
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
                autoBackUp(); targetFlver.Write(flverName);
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
            var boneNameList = new List<TextBox>();
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
                t2.Text = bn.Flags + ",GX" + bn.GXBytes + ",Unk" + bn.Unk18;
                p.Controls.Add(t2);

                /* TextBox t3 = new TextBox();
                 t3.Size = new System.Drawing.Size(770, 15);
                 t3.Location = new System.Drawing.Point(340, currentY);
                 string allMat = "";
                 foreach (FLVER.Texture tex in bn.Textures)
                 {
                     if (tex.Type == "g_DiffuseTexture")
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
                      }

                     allMat += "{" + tex.Type + "->" + tex.Path + "," + tex.Unk10 + "," + tex.Unk11 + "," + tex.Unk14 + "," + tex.Unk18 + "," + tex.Unk1C + "}";
                 }
                 t3.Text = allMat;
                 p.Controls.Add(t3);*/

                Button buttonCheck = new Button();
                int btnI = i;
                buttonCheck.Text = "Edit";
                ButtonTips("Quick edit the texture path and basic information of this material." +
                    "\r\n 快速编辑此材质的贴图路径以及基础信息。",buttonCheck);
                buttonCheck.Size = new System.Drawing.Size(70, 20);
                buttonCheck.Location = new System.Drawing.Point(350, currentY);

                buttonCheck.Click += (s, e) => {

                    //useCheckingMesh = true;
                    // checkingMeshNum = btnI;

                    materialQuickEdit(targetFlver.Materials[btnI],btnI);

                    //mes = jse.Deserialize<FLVER.Mesh>(jse.Serialize(mes));
                    // mes.Vertices = null;
                    
                    //updateVertices();
                };

                p.Controls.Add(buttonCheck);


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

            int btnY = 50;

            Button button = new Button();
            button.Text = "Modify";
            ButtonTips("Save materials' modification to the flver file.\n" +
               "保存对材质的修改至Flver文件中。", button);
            button.Location = new System.Drawing.Point(650, btnY);
            button.Click += (s, e) => {
                /*  for (int i = 0; i < b.Bones.Count; i++)
                  {
                      b.Bones[i].Name = tList[i].Text;
                      b.Bones[i].ParentIndex = short.Parse(parentList[i].Text);
                      b.Bones[i].ChildIndex = short.Parse(childList[i].Text);
                  }*/
                autoBackUp(); targetFlver.Write(flverName);
            };

            btnY += 50;

            Button button2 = new Button();
            ButtonTips("Save json text's modification to the flver file.\n" +
           "保存对json文本的修改至Flver文件中。", button2);
            button2.Text = "ModifyJson";
            button2.Location = new System.Drawing.Point(650, btnY);
            button2.Click += (s, e) => {
                targetFlver.Materials = serializer.Deserialize<List<FLVER.Material>>(tbones.Text);
                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Material change completed! Please exit the program!", "Info");
            };
            btnY += 50;

            Button button3 = new Button();
            ButtonTips("Import external Json text file and save to the flver file.\n" +
          "导入外部的Json文本并保存至Flver文件中。", button3);
            button3.Text = "LoadJson";
            button3.Location = new System.Drawing.Point(650, btnY);
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
                        targetFlver.Materials = serializer.Deserialize<List<FLVER.Material>>(res);
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
            button3ex.Location = new System.Drawing.Point(650, btnY);
            button3ex.Click += (s, e) => {

             
                exportJson(FormatOutput(serializer.Serialize(targetFlver.Materials)), "Material.json", "Material json text exported!");

            };
            btnY += 50;


            Button buttonARSN = new Button();
            ButtonTips("Convert materials (mtd path) to Sekiro/DS3 standard M[ARSN].mtd\n" +
          "替换所有的材质(mtd)为标准的M[ARSN]材质。", buttonARSN);
            buttonARSN.Text = "M[ARSN]";
            buttonARSN.Location = new System.Drawing.Point(650, btnY);
            buttonARSN.Click += (s, e) => {

                foreach (FLVER.Material m in targetFlver.Materials)
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

                    foreach (FLVER.Texture t in m.Textures)
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
            buttonDMY.Location = new System.Drawing.Point(650, btnY);
            buttonDMY.Click += (s, e) => {

                foreach (FLVER.Material m in targetFlver.Materials)
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

                    foreach (FLVER.Texture t in m.Textures)
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
            tpfXmlEdit.Location = new System.Drawing.Point(650, btnY);
            tpfXmlEdit.Click += (s, e) => {

                XmlEdit();

            };
            btnY += 50;


            Button mtdConvert = new Button();
            ButtonTips("Rename all the materials (mtd path) to the name you want.\n" +
          "自动转换所有材质路径为你输入的值。", mtdConvert);
            mtdConvert.Text = "M. Rename";
            mtdConvert.Location = new System.Drawing.Point(650, btnY);
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


            f.Size = new System.Drawing.Size(750, 600);
            p.Size = new System.Drawing.Size(600, 530);
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
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
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

        static void ModelMesh()
        {

            int[] tests = { 0,0,0};
          
            Form f = new Form();
            f.Text = "Mesh";
            Panel p = new Panel();
            int sizeY = 50;
            int currentY = 10;
            var boneNameList = new List<TextBox>();
            parentList = new List<TextBox>();
            childList = new List<TextBox>();
            //p.AutoSize = true;
            p.AutoScroll = true;
            f.Controls.Add(p);

            List<CheckBox> cbList = new List<CheckBox>();//List for deleting
            List<TextBox> tbList = new List<TextBox>();
            List<CheckBox> affectList = new List<CheckBox>();


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
                dA.Location = new System.Drawing.Point(580, currentY );
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
                // foreach (FLVER.Bone bn in b.Bones)
                FLVER.Mesh bn = targetFlver.Meshes[i];
                //Console.WriteLine(bn.MaterialIndex);

                TextBox t = new TextBox();
                t.Size = new System.Drawing.Size(200, 15);
                t.Location = new System.Drawing.Point(70, currentY);
                t.ReadOnly = true;
                t.Text = "[M:" + targetFlver.Materials[bn.MaterialIndex].Name + "],Unk1:" + bn.Unk1 + ",Dyna:" + bn.Dynamic;
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
                    FLVER.Mesh mes = targetFlver.Meshes[btnI];
                    JavaScriptSerializer jse = new JavaScriptSerializer();

                    FLVER.Mesh m2 = new FLVER.Mesh();
                    m2.Vertices = new List<FLVER.Vertex>();
                    m2.VertexBuffers = mes.VertexBuffers;
                    m2.Unk1 = mes.Unk1;
                    m2.MaterialIndex = mes.MaterialIndex;
                    m2.FaceSets = jse.Deserialize<List<FLVER.FaceSet>>(jse.Serialize(mes.FaceSets));
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

                    
                    FLVER.Mesh mes = targetFlver.Meshes[btnI];
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
                        for (int j = 0; j < v.Positions.Count; j++)
                        {
                            int xs = 1;
                            int ys = 1;
                            int zs = 1;

                            //1.62: fixed scaling don't change normal error.
                            if (x < 0) { xs = -1; }
                            if (y < 0) { ys = -1; }
                            if (z < 0) { zs = -1; }
                            v.Normals[j] = new Vector4(v.Normals[j].X * xs, v.Normals[j].Y * ys, v.Normals[j].Z * zs, v.Normals[j].W);


                        }


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
                                for (uint facei = 0; facei < mf.Vertices.Length; facei++)
                                {
                                    //  mf.Vertices[facei] = facei%3;
                                    mf.Vertices[facei] = 1;
                                }

                            }

                        }
                        else {
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
                                mf.Vertices = new uint[0] { };

                            }


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
                        if (rotDg.Checked)
                        {
                            roll = (float) (roll / 180f * Math.PI);
                            pitch = (float)(pitch / 180f * Math.PI);
                            yaw = (float)(yaw / 180f * Math.PI);
                        }


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
                                int xs = 1;
                                int ys = 1;
                                int zs = 1;

                                //1.62: fixed scaling don't change normal error.
                                if (x < 0) { xs = -1; }
                                if (y < 0) { ys = -1; }
                                if (z < 0) { zs = -1; }
                                v.Normals[j] = new Vector4(v.Normals[j].X * xs, v.Normals[j].Y * ys, v.Normals[j].Z * zs, v.Normals[j].W);
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

                //if affect bones were checked
                if (bonesCb.Checked)
                {
                    float x = float.Parse(scaleX.Text);
                    float y = float.Parse(scaleY.Text);
                    float z = float.Parse(scaleZ.Text);
                    //1.67: update affect bone functionality
                    foreach (FLVER.Bone bs in targetFlver.Bones)
                    {
                        if (true)
                        {
                            bs.Translation.X = x * bs.Translation.X;
                            bs.Translation.Y = y * bs.Translation.Y;
                            bs.Translation.Z = z * bs.Translation.Z;

                            //   bs.Scale.X *= x;
                            // bs.Scale.Y *= y;
                            // bs.Scale.Z *= z;

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
                                    sekiroToTarget.Add(i2, i3);
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
                                for (int i5 = 0; i5 < v.BoneIndices.Length; i5++)
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
            ButtonTips("Fix the problem that DS3 model does not show up in Sekiro.(All click yes)\n" +
"修复黑魂三模型在只狼内无法显示的问题。(全点是即可)", button3);
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
                    autoBackUp(); targetFlver.Write(flverName);

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
                    for (int i = 0; i < bl.Count; i++)
                    {
                        if (bl[i].Type == FLVER.BufferLayout.MemberType.Byte4C && bl[i].Semantic == FLVER.BufferLayout.MemberSemantic.UV)
                        {
                            int offset = bl[i].StructOffset;

                            for (int j = i; j < bl.Count; j++)
                            {
                                bl[j].StructOffset += 4;
                            }
                            bl.Insert(i, new FLVER.BufferLayout.Member(0, offset, FLVER.BufferLayout.MemberType.Byte4C, FLVER.BufferLayout.MemberSemantic.VertexColor, 1));
                            break;
                        }

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


                    foreach (FLVER.FaceSet fs in targetFlver.Meshes[i].FaceSets)
                    {

                        for (int ifs = 0; ifs < fs.Vertices.Length; ifs += 3)
                        {
                            uint temp = fs.Vertices[ifs + 1];
                            fs.Vertices[ifs + 1] = fs.Vertices[ifs + 2];
                            fs.Vertices[ifs + 2] = temp;
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
                        for (int j2 = 0; j2 < v.Normals.Count; j2++)
                        {
                            v.Normals[j2] = new Vector4(-v.Normals[j2].X, -v.Normals[j2].Y, -v.Normals[j2].Z, v.Normals[j2].W);

                        }
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
            ButtonTips("Reset all mesh's info to DS3/Sekiro default, usually used to port DS2 version flver file.\n" +
"部分重置面片信息，主要用于导入DS2flver文件至DS3之中。", meshReset);
            meshReset.Text = "M. Reset";
            meshReset.Location = new System.Drawing.Point(650, 350);
            meshReset.Click += (s, e) => {

                SetMeshInfoToDefault();

                updateVertices();

                autoBackUp(); targetFlver.Write(flverName);
                MessageBox.Show("Meshs resetted!", "Info");
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
                reverseFaceset.Location = new System.Drawing.Point(f.Size.Width - 100, 250);
                reverseNormal.Location = new System.Drawing.Point(f.Size.Width - 100, 300);
                meshReset.Location = new System.Drawing.Point(f.Size.Width - 100, 350);
            };


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



        static void materialQuickEdit(FLVER.Material m , int mIndex = 0)
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
                targetFlver.Materials[mIndex] = new JavaScriptSerializer().Deserialize<FLVER.Material>(tJs.Text);
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


        //1.83 New
        //Experimental
        public static void ExportFBX()
        {
            /*var importer = new AssimpContext();
             Scene md = importer.ImportFile("SampleFile.dae", PostProcessSteps.CalculateTangentSpace);// PostProcessPreset.TargetRealTimeMaximumQuality



             MessageBox.Show("Meshes count:" + md.Meshes.Count + "Material count:" + md.MaterialCount + "\nRootNode:" + md.RootNode.MeshIndices.Count);

             string status = "RNode children count " + md.RootNode.ChildCount;

             foreach (var vc in md.RootNode.Children)
             {
                 status += "\n" + vc.Name + "- mesh count" + vc.MeshCount + "- has children" + vc.HasChildren;
                 if (vc.MeshCount > 0) { status += "- index:" + vc.MeshIndices[0]; }
             }

             MessageBox.Show(status);*/
             
            var openFileDialog2 = new SaveFileDialog();
            openFileDialog2.FileName = "Exported.dae";
            string res = "";
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                try
                {



                    Assimp.Scene s = new Scene();
                    s.RootNode = new Node();


                    //s.Meshes = new List<Mesh>();


                    /*
                    Assimp.Mesh m = new Mesh("Test triangles",Assimp.PrimitiveType.Triangle);
                    m.Vertices.Add(new Assimp.Vector3D(1,0,0));
                    m.Vertices.Add(new Assimp.Vector3D(0, 1, 0));
                    m.Vertices.Add(new Assimp.Vector3D(0, 0, 1));
                   
                    
                    m.Normals.Add(new Assimp.Vector3D(0,1,0));
                    m.Normals.Add(new Assimp.Vector3D(0, 1, 0));
                    m.Normals.Add(new Assimp.Vector3D(0, 1, 0));
                    m.Faces.Add(new Face(new int[] { 0,1,2}));
                    m.MaterialIndex = 0;
                   
                   
                    s.Meshes.Add(m);

                    s.Materials.Add(new Material());

                    s.RootNode = new Node();

                    Node nbase = new Node();
                    nbase.Name = "MeshName";
                    nbase.MeshIndices.Add(0);

                    s.RootNode.Children.Add(nbase);*/
                  
                    for (int i = 0; i < targetFlver.Materials.Count; i++)
                    {
                        var m = targetFlver.Materials[i];

                        var assimpMaterial = new Assimp.Material();
                        assimpMaterial.Name = m.Name;
                        s.Materials.Add(assimpMaterial);
                    }

                    for (int i = 0; i < targetFlver.Meshes.Count; i++)
                    {
                        var m = targetFlver.Meshes[i];
                        Assimp.Mesh meshNew = new Mesh("Mesh_M" + i, Assimp.PrimitiveType.Triangle);
                        foreach (var v in m.Vertices)
                        {
                            meshNew.Vertices.Add(new Assimp.Vector3D(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z));
                            meshNew.Normals.Add(new Assimp.Vector3D(v.Normals[0].X, v.Normals[0].Y, v.Normals[0].Z));
                            meshNew.Tangents.Add(new Assimp.Vector3D(v.Tangents[0].X, v.Tangents[0].Y, v.Tangents[0].Z));
                            meshNew.TextureCoordinateChannels[0].Add(new Assimp.Vector3D(v.UVs[0].X,1 - v.UVs[0].Y,0));
                            
                        }
                        
                        
                        foreach (var fs in m.FaceSets)
                        {
                            foreach (var arr in fs.GetFaces())
                            {
                                meshNew.Faces.Add(new Face(new int[] { (int)arr[0], (int)arr[1],(int)arr[2] }));
                            }
                        }

                        meshNew.MaterialIndex = m.MaterialIndex;
                        s.Meshes.Add(meshNew);


                        Node nbase = new Node();
                        nbase.Name = "M_" + i + "_" + targetFlver.Materials[m.MaterialIndex].Name;
                        nbase.MeshIndices.Add(i);

                        s.RootNode.Children.Add(nbase);

                    }


                    AssimpContext exportor = new AssimpContext();
                    exportor.ExportFile(s, openFileDialog2.FileName, "collada");

                    MessageBox.Show("Export successful!", "Info");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
            else {
                return;
            }


        }


        //1.73 New
        /// <summary>
        /// Dummy Text
        /// </summary>
        /// <param name="newBones">The new bones list</param>
        public static void BoneWeightShift(List<FLVER.Bone> newBones)
        {
            //Step 1 build a int table to map old bone index -> new bone index
            int[] boneMapTable = new int[targetFlver.Bones.Count];
            for (int i =0;i<targetFlver.Bones.Count;i++)
            {
                boneMapTable[i] = findNewIndex(newBones,i);


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
        public static int findNewIndex(List<FLVER.Bone> newBones, int oldBoneIndex)
        {
            int ans = 0;
            string oldBoneName = targetFlver.Bones[oldBoneIndex].Name;
            for (int i =0;i < 5;i++)
            {
                ans = findNewIndexByName(newBones,oldBoneName);
                if (ans >= 0 ) { return ans; }
                oldBoneIndex = targetFlver.Bones[oldBoneIndex].ParentIndex;
                if (oldBoneIndex < 0) { return 0; }
                oldBoneName = targetFlver.Bones[oldBoneIndex].Name;
            }


            return 0;
        }

        public static int findNewIndexByName(List<FLVER.Bone> newBones, string oldBoneName) {

            for (int i =0; i < newBones.Count;i++)
            {
                if (oldBoneName == newBones[i].Name)
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

            foreach (FLVER.Mesh mn in targetFlver.Meshes)
            {

                //FLVER.Mesh mn = new FLVER.Mesh();
               // mn.MaterialIndex = 0;
               // mn.BoneIndices = new List<int>();
               // mn.BoneIndices.Add(0);
               // mn.BoneIndices.Add(1);
                mn.BoundingBoxMax = new Vector3(1, 1, 1);
                mn.BoundingBoxMin = new Vector3(-1, -1, -1);
                mn.BoundingBoxUnk = new Vector3();
                mn.Unk1 = 0;
                
                mn.DefaultBoneIndex = 0;
                mn.Dynamic = true;
                 mn.VertexBuffers = new List<FLVER.VertexBuffer>();
                 mn.VertexBuffers.Add(new FLVER.VertexBuffer(0, layoutCount, -1));
                //  mn.Vertices = new List<FLVER.Vertex>();
                var varray = mn.FaceSets[0].Vertices;
                
                mn.FaceSets = new List<FLVER.FaceSet>();
                //FLVER.Vertex myv = new FLVER.Vertex();
                //myv.Colors = new List<FLVER.Vertex.Color>();


                //FLVER.Vertex v = generateVertex(new Vector3(vit.X, vit.Y, vit.Z), uv1.toNumV3(), uv2.toNumV3(), normal.toNumV3(), tangent.toNumV3(), 1);

                for (int i = 0; i < mn.Vertices.Count;i++)
                {
                    FLVER.Vertex vit = mn.Vertices[i];

                    mn.Vertices[i] = generateVertex(new Vector3(vit.Positions[0].X, vit.Positions[0].Y, vit.Positions[0].Z), vit.UVs[0], vit.UVs[0], vit.Normals[0], vit.Tangents[0], 1);
                    mn.Vertices[i].BoneIndices = vit.BoneIndices;
                    mn.Vertices[i].BoneWeights = vit.BoneWeights;

                }

                mn.FaceSets.Add(generateBasicFaceSet());
                mn.FaceSets[0].Vertices = varray;
                mn.FaceSets[0].CullBackfaces = false;
                //mn.FaceSets[0].Unk06 = 17;
                if (mn.FaceSets[0].Vertices.Length > 65534)
                {
                    MessageBox.Show("There are more than 65535 vertices in a mesh , switch to 32 bits index size mode.");
                    mn.FaceSets[0].IndexSize = 32;
                }
            }



        }

        public static void SetFlverMatPath(FLVER.Material m, string typeName, string newPath)
        {
            for (int i=0;i < m.Textures.Count;i++)
            {
                if (m.Textures[i].Type == typeName)
                {
                    m.Textures[i].Path = newPath;
                    return;
                }


            }

            FLVER.Texture tn = new FLVER.Texture();
            tn.Type = typeName;
            tn.Path = newPath;
            tn.ScaleX = 1;
            tn.ScaleY = 1;
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
