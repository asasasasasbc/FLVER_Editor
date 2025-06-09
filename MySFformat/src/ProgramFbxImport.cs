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
    static partial class Program
    {
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


            //Prepare bone name convertion table:
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string convertStr = File.ReadAllText(assemblyPath + "\\boneConvertion.ini");
            //Console.WriteLine("Test reading" + convertStr);
            string[] convertStrlines = convertStr.Split(
    new[] { "\r\n", "\r", "\n" },
    StringSplitOptions.None
);
            Dictionary<string, string> convertionTable = new Dictionary<string, string>();
            for (int i2 = 0; i2 + 1 < convertStrlines.Length; i2++)
            {
                string target = convertStrlines[i2];
                if (target == null) { continue; }
                if (target.IndexOf('#') == 0) { continue; }
                Console.WriteLine(target + "->" + convertStrlines[i2 + 1]);
                convertionTable.Add(target, convertStrlines[i2 + 1]);
                i2++;
            }

            //Table prepartion finished

            Scene md = importer.ImportFile(res, PostProcessSteps.CalculateTangentSpace);// PostProcessPreset.TargetRealTimeMaximumQuality

            MessageBox.Show("Meshes count:" + md.Meshes.Count + "Material count:" + md.MaterialCount + "");

            boneParentList = new Dictionary<String, String>();
            //Build the parent list of bones.

            printNodeStruct(md.RootNode);

            //First, added a custom default layout.
            int layoutCount = targetFlver.BufferLayouts.Count;
            FLVER2.BufferLayout newBL = new FLVER2.BufferLayout();

            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.Float3, FLVER.LayoutSemantic.Position, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Normal, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Tangent, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.Tangent, 1));
            
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4, FLVER.LayoutSemantic.BoneIndices, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.BoneWeights, 0));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.UByte4Norm, FLVER.LayoutSemantic.VertexColor, 1));
            newBL.Add(new FLVER.LayoutMember(FLVER.LayoutType.Short4, FLVER.LayoutSemantic.UV, 0));

            targetFlver.BufferLayouts.Add(newBL);

            int materialCount = targetFlver.Materials.Count;
            Boolean flipYZ = false;
            Boolean setTexture = false;
            Boolean setAmbientAsDiffuse = false;
            Boolean setLOD = false;

            var confirmResult = MessageBox.Show("Do you want to switch YZ axis values? \n It may help importing some fbx files.",
                                  "Set",
                                  MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                flipYZ = true;
            }

            var confirmResult2 = MessageBox.Show("Auto set texture pathes?",
                                  "Set",
                                  MessageBoxButtons.YesNo);
            if (confirmResult2 == DialogResult.Yes)
            {
                setTexture = true;


                /* var res3 = MessageBox.Show("Auto ambient texture as diffuse texture?",
                                    "Set",
                                    MessageBoxButtons.YesNo);

                 if (res3 == DialogResult.Yes)
                 {
                     setAmbientAsDiffuse = true;
                 }*/
            }

            var confirmResult3 = MessageBox.Show("Set LOD level? (Only neccssary if this model need to be viewed far away)",
                          "Set",
                          MessageBoxButtons.YesNo);
            if (confirmResult3 == DialogResult.Yes)
            {
                setLOD = true;
            }



            foreach (var mat in md.Materials)
            {
                FLVER2.Material matnew = new JavaScriptSerializer().Deserialize<FLVER2.Material>(new JavaScriptSerializer().Serialize(targetFlver.Materials[0]));
                matnew.Name = res.Substring(res.LastIndexOf('\\') + 1) + "_" + mat.Name;
                // mat.HasTextureDiffuse

                if (setTexture)
                {

                    if (setAmbientAsDiffuse)
                    {

                        if (mat.HasTextureEmissive)
                        {

                            SetFlverMatPath(matnew, "g_DiffuseTexture", FindFileName(mat.TextureEmissive.FilePath) + ".tif");

                        }


                    }
                    else
                    if (mat.HasTextureDiffuse) //g_DiffuseTexture
                    {
                        //  MessageBox.Show("Diffuse mat is" + FindFileName( mat.TextureDiffuse.FilePath));
                        SetFlverMatPath(matnew, "g_DiffuseTexture", FindFileName(mat.TextureDiffuse.FilePath) + ".tif");


                    }
                    if (mat.HasTextureNormal)//g_BumpmapTexture
                    {
                        //MessageBox.Show("Diffuse mat is" + FindFileName(mat.TextureNormal.FilePath));
                        SetFlverMatPath(matnew, "g_BumpmapTexture", FindFileName(mat.TextureNormal.FilePath) + ".tif");

                    }
                    if (mat.HasTextureSpecular)//g_SpecularTexture
                    {
                        /// MessageBox.Show("Specualr mat is" + FindFileName(mat.TextureSpecular.FilePath));
                        SetFlverMatPath(matnew, "g_SpecularTexture", FindFileName(mat.TextureSpecular.FilePath) + ".tif");
                    }
                }
                targetFlver.Materials.Add(matnew);
            }


            //mn.MaterialIndex = materialCount;

            foreach (var m in md.Meshes)
            {
                /* MessageBox.Show("Name:" + m.Name + "\nHas bones:" + m.HasBones + "\nHas normal:" + m.HasNormals + "\nHas tangent" + m.HasTangentBasis +
                     "\nVrtices count: " + m.VertexCount
                     );*/


                FLVER2.Mesh mn = new FLVER2.Mesh();
                mn.MaterialIndex = 0;
                mn.BoneIndices = new List<int>();
                mn.BoneIndices.Add(0);
                mn.BoneIndices.Add(1);
                mn.BoundingBox = new FLVER2.Mesh.BoundingBoxes();
                mn.BoundingBox.Max = new Vector3(1, 1, 1);
                mn.BoundingBox.Min = new Vector3(-1, -1, -1);
                mn.BoundingBox.Unk = new Vector3();
                //mn.Unk1 = 0;
                mn.NodeIndex = 0;
                mn.Dynamic = 1;
                mn.VertexBuffers = new List<FLVER2.VertexBuffer>();
                mn.VertexBuffers.Add(new FLVER2.VertexBuffer(layoutCount));
                mn.Vertices = new List<FLVER.Vertex>();

                List<List<int>> verticesBoneIndices = new List<List<int>>();
                List<List<float>> verticesBoneWeights = new List<List<float>>();


                //If it has bones, then record the bone weight info
                if (m.HasBones)
                {
                    for (int i2 = 0; i2 < m.VertexCount; i2++)
                    {
                        verticesBoneIndices.Add(new List<int>());
                        verticesBoneWeights.Add(new List<float>());
                    }

                    for (int i2 = 0; i2 < m.BoneCount; i2++)
                    {
                        string boneName = m.Bones[i2].Name;
                        int boneIndex = 0;

                        if (convertionTable.ContainsKey(m.Bones[i2].Name))
                        {

                            boneName = convertionTable[boneName];
                            // m.Bones[i2].Name = convertionTable[m.Bones[i2].Name];
                            boneIndex = findFLVER_Bone(targetFlver, boneName);
                        }
                        else
                        {
                            Console.WriteLine("Cannot find ->" + boneName);
                            //If cannot find a corresponding boneName in convertion.ini then
                            //Try to find org bone's parent, check if it 
                            boneIndex = findFLVER_Bone(targetFlver, boneName);


                            //if such bone can not be found in flver, then check its parent to see if it can be convert to its parent bone.
                            //check up to 5th grand parent.
                            for (int bp = 0; bp < boneFindParentTimes; bp++)
                            {

                                if (boneIndex == -1)
                                {
                                    if (boneParentList.ContainsValue(boneName))
                                    {
                                        if (boneParentList[boneName] != null)
                                        {
                                            boneName = boneParentList[boneName];
                                            if (convertionTable.ContainsKey(boneName))
                                            {
                                                boneName = convertionTable[boneName];
                                            }
                                            boneIndex = findFLVER_Bone(targetFlver, boneName);

                                        }
                                    }
                                }

                            }
                        }


                        if (boneIndex == -1) { boneIndex = 0; }
                        for (int i3 = 0; i3 < m.Bones[i2].VertexWeightCount; i3++)
                        {
                            var vw = m.Bones[i2].VertexWeights[i3];

                            verticesBoneIndices[vw.VertexID].Add(boneIndex);
                            verticesBoneWeights[vw.VertexID].Add(vw.Weight);
                        }


                    }

                }

                // m.Bones[0].VertexWeights[0].
                for (int i = 0; i < m.Vertices.Count; i++)
                {
                    var vit = m.Vertices[i];
                    //m.TextureCoordinateChannels[0]
                    var channels = m.TextureCoordinateChannels[0];

                    var uv1 = new Vector3D();
                    var uv2 = new Vector3D();

                    if (channels != null && m.TextureCoordinateChannelCount > 0)
                    {

                        uv1 = getMyV3D(channels[i]);
                        uv1.Y = 1 - uv1.Y;
                        uv2 = getMyV3D(channels[i]);
                        uv2.Y = 1 - uv2.Y;
                        if (m.TextureCoordinateChannelCount > 1)
                        {
                            // uv2 = getMyV3D((m.TextureCoordinateChannels[1])[i]);
                        }
                    }

                    var normal = new Vector3D(0,1,0);
                    if (m.HasNormals && m.Normals.Count > i) 
                    {
                        normal = getMyV3D(m.Normals[i]).normalize();
                    }




                    //Vector3D tangent = new Vector3D( crossPorduct( getMyV3D(m.Tangents[i]).normalize().toXnaV3() , normal.toXnaV3())).normalize();
                    //var tangent = RotatePoint(normal.toNumV3(), 0, (float)Math.PI / 2, 0);
                    var tangent = new Vector3D(1,0,0);
                    if (m.Tangents.Count > i)
                    {
                        tangent = getMyV3D(m.Tangents[i]).normalize();
                    }
                    else {
                        //Calculate tanget instead
                        if (m.HasNormals && m.Normals.Count > i)
                            tangent = new Vector3D(crossPorduct(getMyV3D(m.Normals[i]).normalize().toXnaV3(), normal.toXnaV3())).normalize();
                    }

                    FLVER.Vertex v = generateVertex(new Vector3(vit.X, vit.Y, vit.Z), uv1.toNumV3(), uv2.toNumV3(), normal.toNumV3(), tangent.toNumV3(), 1);

                    if (flipYZ)
                    {
                        v = generateVertex(new Vector3(vit.X, vit.Z, vit.Y), uv1.toNumV3(), uv2.toNumV3(),
                           new Vector3(normal.X, normal.Z, normal.Y), new Vector3(tangent.X, tangent.Z, tangent.Y), 1);

                    }


                    if (m.HasBones)
                    {
                        for (int j = 0; j < verticesBoneIndices[i].Count && j < 4; j++)
                        {
                            v.BoneIndices[j] = (verticesBoneIndices[i])[j];
                            v.BoneWeights[j] = (verticesBoneWeights[i])[j];
                        }
                    }
                    mn.Vertices.Add(v);
                }



                List<int> faceIndexs = new List<int>();
                for (int i = 0; i < m.FaceCount; i++)
                {

                    if (flipYZ)
                    {
                        if (m.Faces[i].Indices.Count == 3)
                        {
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                            faceIndexs.Add((int)m.Faces[i].Indices[1]);
                            faceIndexs.Add((int)m.Faces[i].Indices[2]);
                        }
                        else if (m.Faces[i].Indices.Count == 4)
                        {
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                            faceIndexs.Add((int)m.Faces[i].Indices[1]);
                            faceIndexs.Add((int)m.Faces[i].Indices[2]);

                            faceIndexs.Add((int)m.Faces[i].Indices[2]);
                            faceIndexs.Add((int)m.Faces[i].Indices[3]);
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                        }

                    }
                    else
                    {
                        if (m.Faces[i].Indices.Count == 3)
                        {
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                            faceIndexs.Add((int)m.Faces[i].Indices[2]);
                            faceIndexs.Add((int)m.Faces[i].Indices[1]);
                        }
                        else if (m.Faces[i].Indices.Count == 4)
                        {
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                            faceIndexs.Add((int)m.Faces[i].Indices[2]);
                            faceIndexs.Add((int)m.Faces[i].Indices[1]);

                            faceIndexs.Add((int)m.Faces[i].Indices[2]);
                            faceIndexs.Add((int)m.Faces[i].Indices[0]);
                            faceIndexs.Add((int)m.Faces[i].Indices[3]);
                        }


                    }


                }
                //
                mn.FaceSets = new List<FLVER2.FaceSet>();
                //FLVER.Vertex myv = new FLVER.Vertex();
                //myv.Colors = new List<FLVER.Vertex.Color>();

                mn.FaceSets.Add(generateBasicFaceSet());
                mn.FaceSets[0].Indices = faceIndexs;
                if (mn.FaceSets[0].Indices.Count > 65534)
                {
                    MessageBox.Show("There are more than 65535 vertices in a mesh , switch to 32 bits index size mode.");
                    //SoulsFormatNEXT auto calculated:mn.FaceSets[0].IndexSize = 32;
                }


                if (setLOD == true)
                {
                    //Special thanks to Meowmaritus
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel1;
                        //SoulsFormatNEXT auto calculated:fs.IndexSize = mn.FaceSets[0].IndexSize;
                        fs.Indices =mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }

                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel2;
                        //SoulsFormatNEXT auto calculated:fs.IndexSize = mn.FaceSets[0].IndexSize;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                    //unk8000000000 is the motion blur
                    {
                        //fs.Flags = SoulsFormats.FLVER.FaceSet.FSFlags.Unk80000000;
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        //SoulsFormatNEXT auto calculated:fs.IndexSize = mn.FaceSets[0].IndexSize;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }

                    {
                        //fs.Flags = SoulsFormats.FLVER.FaceSet.FSFlags.Unk80000000;
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel1 | SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        //SoulsFormatNEXT auto calculated:fs.IndexSize = mn.FaceSets[0].IndexSize;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }

                    {
                        //fs.Flags = SoulsFormats.FLVER.FaceSet.FSFlags.Unk80000000;
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel2 | SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        //SoulsFormatNEXT auto calculated:fs.IndexSize = mn.FaceSets[0].IndexSize;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                }


                mn.MaterialIndex = materialCount + m.MaterialIndex;


                targetFlver.Meshes.Add(mn);
            }

            MessageBox.Show("Added a custom mesh! PLease click modify to save it!");
            updateVertices();
        }





    }
}
