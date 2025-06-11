// Program.cs (partial)
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
using CSMath;


namespace MySFformat
{
    static partial class Program
    {
        // --- NEW HELPER FUNCTIONS FOR AXIS REMAPPING ---

        /// <summary>
        /// Gets a single axis value from a source vector based on the Axis enum.
        /// </summary>
        private static float GetAxisValue(Vector3D v, FbxImportSettings.Axis axis)
        {
            switch (axis)
            {
                case FbxImportSettings.Axis.X: return v.X;
                case FbxImportSettings.Axis.Y: return v.Y;
                case FbxImportSettings.Axis.Z: return v.Z;
                case FbxImportSettings.Axis.NegX: return -v.X;
                case FbxImportSettings.Axis.NegY: return -v.Y;
                case FbxImportSettings.Axis.NegZ: return -v.Z;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        /// <summary>
        /// Remaps a vector from Assimp's coordinate system to the game's coordinate system based on user settings.
        /// </summary>
        private static Vector3 RemapVector(Vector3D input, FbxImportSettings.Axis primary, FbxImportSettings.Axis secondary, bool mirrorTertiary)
        {
            // Determine the remapped basis vectors. This defines the transformation.
            Vector3 newX = new Vector3(GetAxisValue(new Vector3D(1, 0, 0), primary), GetAxisValue(new Vector3D(0, 1, 0), primary), GetAxisValue(new Vector3D(0, 0, 1), primary));
            Vector3 newY = new Vector3(GetAxisValue(new Vector3D(1, 0, 0), secondary), GetAxisValue(new Vector3D(0, 1, 0), secondary), GetAxisValue(new Vector3D(0, 0, 1), secondary));
            Vector3 newZ = Vector3.Cross(newX, newY);

            if (mirrorTertiary)
            {
                newZ *= -1;
            }

            // The remapped vector is a linear combination of the new basis vectors,
            // scaled by the original vector's components.
            return (input.X * newX) + (input.Y * newY) + (input.Z * newZ);
        }

        /// <summary>
        /// Placeholder function for importing and overriding bones from the source model.
        /// </summary>
        private static void ImportAndOverrideBones(Scene sourceScene, FLVER2 targetFlver)
        {
            // TODO: Implement the logic to read bones from sourceScene.RootNode,
            // create new FLVER.Bone instances, clear targetFlver.Bones, and add the new ones.
            // This is a complex task involving converting transformation matrices and rebuilding the bone hierarchy.
            MessageBox.Show("Import and Override Bones is not yet implemented.", "Placeholder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //Current, best version can load FBX, OBJ, DAE etc.
        //Use assimp library
        static void importFBX()
        {
            // Load settings and show the import form
            FbxImportSettings settings = FbxImportSettings.Load();
            using (var form = new ProgramFbxImportForm(settings))
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return; // User cancelled
                }
                settings = form.Settings; // Get updated settings
                settings.Save(); // Save for next time
            }

            if (string.IsNullOrEmpty(settings.ImportFilePath) || !File.Exists(settings.ImportFilePath))
            {
                MessageBox.Show("Import file path is not valid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AssimpContext importer = new AssimpContext();
            // importer.SetConfig(new NormalSmoothingAngleConfig(66.0f));

            //Prepare bone name convertion table:
            Dictionary<string, string> convertionTable = new Dictionary<string, string>();
            if (settings.UseBoneConversion)
            {
                if (File.Exists(settings.BoneConversionFilePath))
                {
                    string convertStr = File.ReadAllText(settings.BoneConversionFilePath);
                    string[] convertStrlines = convertStr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int i2 = 0; i2 + 1 < convertStrlines.Length; i2++)
                    {
                        string target = convertStrlines[i2];
                        if (string.IsNullOrWhiteSpace(target) || target.StartsWith("#")) { continue; }
                        Console.WriteLine(target + "->" + convertStrlines[i2 + 1]);
                        convertionTable.Add(target, convertStrlines[i2 + 1]);
                        i2++;
                    }
                }
                else
                {
                    MessageBox.Show($"Bone conversion file not found at: {settings.BoneConversionFilePath}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            //Table prepartion finished

            Scene md = importer.ImportFile(settings.ImportFilePath, PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate); // PostProcessPreset.TargetRealTimeMaximumQuality

            MessageBox.Show("Meshes count:" + md.Meshes.Count + " Material count:" + md.MaterialCount + "");

            if (settings.ImportAndOverrideBones)
            {
                ImportAndOverrideBones(md, targetFlver);
            }

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

            // --- NEW: Robustly determine if winding order needs to be flipped. ---
            // A change in coordinate system handedness requires flipping face winding.
            // This is determined by the determinant of the basis vector transformation matrix.
            // If the determinant is negative, handedness has changed.
            Vector3 basisX = new Vector3(1, 0, 0);
            Vector3 basisY = new Vector3(0, 1, 0);
            Vector3 basisZ = new Vector3(0, 0, 1);

            Vector3 remappedBasisX = RemapVector(new Vector3D(1, 0, 0), settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);
            Vector3 remappedBasisY = RemapVector(new Vector3D(0, 1, 0), settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);
            Vector3 remappedBasisZ = RemapVector(new Vector3D(0, 0, 1), settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);

            Matrix4 basisMatrix = new Matrix4(
                remappedBasisX.X, remappedBasisY.X, remappedBasisZ.X, 0,
                remappedBasisX.Y, remappedBasisY.Y, remappedBasisZ.Y, 0,
                remappedBasisX.Z, remappedBasisY.Z, remappedBasisZ.Z, 0,
                0, 0, 0, 1
            );

            bool flipWinding = basisMatrix.GetDeterminant() > 0; 

            foreach (var mat in md.Materials)
            {
                FLVER2.Material matnew = new JavaScriptSerializer().Deserialize<FLVER2.Material>(new JavaScriptSerializer().Serialize(targetFlver.Materials[0]));
                matnew.Name = Path.GetFileNameWithoutExtension(settings.ImportFilePath) + "_" + mat.Name;

                if (settings.SetTexture)
                {
                    if (mat.HasTextureDiffuse) //g_DiffuseTexture
                    {
                        SetFlverMatPath(matnew, "g_DiffuseTexture", FindFileName(mat.TextureDiffuse.FilePath) + ".tif");
                    }
                    if (mat.HasTextureNormal)//g_BumpmapTexture
                    {
                        SetFlverMatPath(matnew, "g_BumpmapTexture", FindFileName(mat.TextureNormal.FilePath) + ".tif");
                    }
                    if (mat.HasTextureSpecular)//g_SpecularTexture
                    {
                        SetFlverMatPath(matnew, "g_SpecularTexture", FindFileName(mat.TextureSpecular.FilePath) + ".tif");
                    }
                }
                targetFlver.Materials.Add(matnew);
            }


            //mn.MaterialIndex = materialCount;

            foreach (var m in md.Meshes)
            {
                FLVER2.Mesh mn = new FLVER2.Mesh();
                mn.MaterialIndex = 0;
                mn.BoneIndices = new List<int>();
                mn.BoneIndices.Add(0);
                mn.BoneIndices.Add(1);
                mn.BoundingBox = new FLVER2.Mesh.BoundingBoxes();
                mn.BoundingBox.Max = new Vector3(1, 1, 1);
                mn.BoundingBox.Min = new Vector3(-1, -1, -1);
                mn.BoundingBox.Unk = new Vector3();
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
                            boneIndex = findFLVER_Bone(targetFlver, boneName);
                        }
                        else
                        {
                            Console.WriteLine("Cannot find ->" + boneName);
                            boneIndex = findFLVER_Bone(targetFlver, boneName);

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
                    var channels = m.TextureCoordinateChannels[0];

                    var uv1 = new Vector3D();
                    var uv2 = new Vector3D();

                    if (channels != null && m.TextureCoordinateChannelCount > 0)
                    {
                        uv1 = getMyV3D(channels[i]);
                        uv1.Y = 1 - uv1.Y;
                        uv2 = getMyV3D(channels[i]);
                        uv2.Y = 1 - uv2.Y;
                    }

                    var normal = new Vector3D(0, 1, 0);
                    if (m.HasNormals && m.Normals.Count > i)
                    {
                        normal = getMyV3D(m.Normals[i]).normalize();
                    }

                    var tangent = new Vector3D(1, 0, 0);

                    //TODO: Two experimental function, tangent calculation suited for blender
                    var swapTanBitan = false; // Blender export only
                    var inverseTan = false; // Blender export only
                    if (settings.blenderTan) {
                        swapTanBitan = true;
                        inverseTan = true;
                    }
                    if (m.HasTangentBasis && m.Tangents.Count > i)
                    {
                        tangent = getMyV3D(m.Tangents[i]).normalize();
                        if (swapTanBitan) { tangent = getMyV3D(m.BiTangents[i]).normalize(); }

                    }
                    else if (m.HasNormals && m.Normals.Count > i)
                    {
                        // Actually speaking totally wrong 
                        tangent = new Vector3D(crossPorduct(getMyV3D(m.Normals[i]).normalize().toXnaV3(), normal.toXnaV3())).normalize();
                    }

                    bool hasBitangent = false;
                    Vector3D bitangent = new Vector3D();
                    if (m.HasTangentBasis && m.BiTangents.Count > i)
                    {
                        hasBitangent = true;
                        bitangent = getMyV3D(m.BiTangents[i]).normalize();
                        if (swapTanBitan) { bitangent = getMyV3D(m.Tangents[i]).normalize(); }
                    }
                    else {
                        bitangent = tangent;
                    }

                        // --- NEW: Remap vectors based on settings, including the mirror option ---
                    Vector3 remappedPosition = RemapVector(getMyV3D(vit), settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);
                    Vector3 remappedNormal = RemapVector(normal, settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);
                    Vector3 remappedTangent = RemapVector(tangent, settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);
                    Vector3 remappedBitangent = RemapVector(bitangent, settings.PrimaryAxis, settings.SecondaryAxis, settings.MirrorTertiaryAxis);

                    if (inverseTan) { remappedTangent = new Vector3(-remappedTangent.X, -remappedTangent.Y, -remappedTangent.Z); }

                    float tangentW = 1;
                    if (hasBitangent) { 
                        //判断CrossProduct(normal, tangent) 是不是和 Bitangent的夹角小于90°，如果小于则表示w不用翻面，否则W要翻面
                        // 不能在remapped中比较，要用fbx导出的时候的坐标系
                        Vector3D n = normal;
                        Vector3D t = tangent;
                        Vector3D positiveBitangent = new Vector3D(crossPorduct(n.toXnaV3(), t.toXnaV3()));
                        if (Vector3D.dotProduct(positiveBitangent, bitangent) < 0) {
                            tangentW *= -1;
                        }
                    }

                    FLVER.Vertex v = generateVertex(remappedPosition, uv1.toNumV3(), uv2.toNumV3(), remappedNormal, remappedTangent, tangentW);

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
                foreach (var face in m.Faces)
                {
                    if (face.IndexCount == 3)
                    {
                        // --- NEW: Use robust flipWinding calculated earlier ---
                        if (flipWinding)
                        {
                            faceIndexs.Add(face.Indices[0]);
                            faceIndexs.Add(face.Indices[2]);
                            faceIndexs.Add(face.Indices[1]);
                        }
                        else
                        {
                            faceIndexs.Add(face.Indices[0]);
                            faceIndexs.Add(face.Indices[1]);
                            faceIndexs.Add(face.Indices[2]);
                        }

                    }
                    else if (face.IndexCount == 4)
                    { // NOT Yet Tested on quads yet...
                        if (flipWinding)
                        {
                            faceIndexs.Add(face.Indices[0]);
                            faceIndexs.Add(face.Indices[2]);
                            faceIndexs.Add(face.Indices[1]);

                            faceIndexs.Add(face.Indices[2]);
                            faceIndexs.Add(face.Indices[0]);
                            faceIndexs.Add(face.Indices[3]);
                        }
                        else
                        {
                            faceIndexs.Add(face.Indices[0]);
                            faceIndexs.Add(face.Indices[1]);
                            faceIndexs.Add(face.Indices[2]);

                            faceIndexs.Add(face.Indices[2]);
                            faceIndexs.Add(face.Indices[3]);
                            faceIndexs.Add(face.Indices[0]);
                        }


                    }
                    else { 
                        //Probrably something WRONG
                    }

                    
                }

                mn.FaceSets = new List<FLVER2.FaceSet>();
                mn.FaceSets.Add(generateBasicFaceSet());
                mn.FaceSets[0].Indices = faceIndexs;
                if (mn.FaceSets[0].Indices.Count > 65534)
                {
                    //MessageBox.Show("There are more than 65535 vertices in a mesh , switch to 32 bits index size mode.");
                }

                if (settings.SetLOD)
                {
                    //Special thanks to Meowmaritus
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel1;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel2;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel1 | SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                    {
                        FLVER2.FaceSet fs = generateBasicFaceSet();
                        fs.Flags = SoulsFormats.FLVER2.FaceSet.FSFlags.LodLevel2 | SoulsFormats.FLVER2.FaceSet.FSFlags.MotionBlur;
                        fs.Indices = mn.FaceSets[0].Indices;
                        mn.FaceSets.Add(fs);
                    }
                }

                mn.MaterialIndex = materialCount + m.MaterialIndex;
                targetFlver.Meshes.Add(mn);
            }

            MessageBox.Show("Added a custom mesh! Please click modify to save it!");
            updateVertices();
        }
    }
}