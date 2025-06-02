using System;
using Assimp;
using System.Windows.Forms;
using MeshIO;
using SoulsFormats;
using CSMath;
using MeshIO.Entities.Geometries.Layers;
using MeshIO.Entities.Skinning;
using MeshIO.FBX.Helpers;
using MeshIO.FBX;
using System.Collections.Generic;
using MeshIO;
using MeshIO.Entities;
using MeshIO.Entities.Geometries;
using MeshIO.Entities.Geometries.Layers;
using MeshIO.Entities.Skinning;
using MeshIO.FBX;
using System.IO;

namespace MySFformat
{

    public static class FlverFbxRotationHelper
    {
        // FLVER typically uses YZX Euler order, radians.
        // FBX typically uses XYZ Euler order, degrees.
        // This helper converts from FLVER's YZX radians to FBX's XYZ degrees
        // AND applies the Z-axis mirror for rotations.
        public static XYZ FlverRotationToFbxEulerDegrees(System.Numerics.Vector3 flverRotationRadians)
        {
            // Convert radians to degrees first
            float xRad = flverRotationRadians.X;
            float yRad = flverRotationRadians.Y;
            float zRad = flverRotationRadians.Z;

            float xDeg = (float)MathUtils.RadToDeg(xRad);
            float yDeg = (float)MathUtils.RadToDeg(yRad);
            float zDeg = (float)MathUtils.RadToDeg(zRad);

            // Input angles are in YZX order (as per FLVER convention)
            MyVector3 inputAnglesDeg = new MyVector3(xDeg, yDeg, zDeg);
            MeshIO.FBX.Helpers.RotationOrder flverOrder = MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard
            MeshIO.FBX.Helpers.RotationOrder fbxOrder = MeshIO.FBX.Helpers.RotationOrder.ZYX;   // Common FBX target

            var convertedAngles = EulerAngleConverter.ConvertRotationOrder(inputAnglesDeg, flverOrder, fbxOrder);

            // Apply mirroring for coordinate system difference (Z-axis flip for positions implies this for rotations)
            // If positions are (X, Y, -Z), rotations around X and Y effectively flip, Z stays.
            return new XYZ(
                -1 * convertedAngles.X,
                -1 * convertedAngles.Y,
                convertedAngles.Z
            );
        }
    }



    // Helper for global matrix (from your skinning example)
    public static class TransformExtensions
    {
        /// <summary>
        /// Calculates the local transformation matrix for a MeshIO.Transform
        /// using MySFformat.Matrix3D and a specified RotationOrder.
        /// Assumes MeshIO.Transform.EulerRotation is in DEGREES.
        /// </summary>
        public static Matrix3D GetLocalMatrix(MeshIO.Transform meshIoTransform, RotationOrder order)
        {
            if (meshIoTransform == null) return Matrix3D.Identity();

            // Ensure MeshIO.Transform.EulerRotation provides angles in degrees.
            // If it's in radians, you'd convert here:
            // float rotX_deg = meshIoTransform.EulerRotation.X * (180f / (float)Math.PI);
            // float rotY_deg = meshIoTransform.EulerRotation.Y * (180f / (float)Math.PI);
            // float rotZ_deg = meshIoTransform.EulerRotation.Z * (180f / (float)Math.PI);
            // For now, assuming they are already in degrees:
            float rotX_deg = (float)meshIoTransform.EulerRotation.X;
            float rotY_deg = (float)meshIoTransform.EulerRotation.Y;
            float rotZ_deg = (float)meshIoTransform.EulerRotation.Z;

            Matrix3D mS = Matrix3D.generateScaleMatrix((float)meshIoTransform.Scale.X, (float)meshIoTransform.Scale.Y, (float)meshIoTransform.Scale.Z);
            Matrix3D mRx = Matrix3D.generateRotXMatrix(rotX_deg);
            Matrix3D mRy = Matrix3D.generateRotYMatrix(rotY_deg);
            Matrix3D mRz = Matrix3D.generateRotZMatrix(rotZ_deg);
            Matrix3D mT = Matrix3D.generateTranslationMatrix((float)meshIoTransform.Translation.X, (float)meshIoTransform.Translation.Y, (float)meshIoTransform.Translation.Z);

            Matrix3D rotationProduct;
            switch (order)
            {
                case RotationOrder.XYZ: rotationProduct = mRx * mRy * mRz; break;
                case RotationOrder.XZY: rotationProduct = mRx * mRz * mRy; break;
                case RotationOrder.YXZ: rotationProduct = mRy * mRx * mRz; break;
                case RotationOrder.YZX: rotationProduct = mRy * mRz * mRx; break;
                case RotationOrder.ZXY: rotationProduct = mRz * mRx * mRy; break;
                case RotationOrder.ZYX: rotationProduct = mRz * mRy * mRx; break;
                default:
                    // Fallback or throw exception
                    Console.WriteLine($"Warning: Unknown rotation order {order}, using XYZ.");
                    rotationProduct = mRx * mRy * mRz;
                    break;
            }

            // Standard order: T * R * S
            // This means scale is applied first, then rotation, then translation.
            // M = T * R_compound * S
            return mT * rotationProduct * mS;
        }
        public static Matrix4 GetUnityMatrix(this MeshIO.Node node, MeshIO.Node stopAtParent = null)
        {
            return new Matrix4(
                1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f
            );

        }
        //MeshIO的Matrix我感觉算的有问题，很有可能是EulerAngle-》Matrix的RotationOrder有问题，我这边自己算一遍
        public static Matrix4 GetMatrix4(Transform t)
        {
            var pos = t.Translation; // pos.X pos.Y pos.Z
            var rot = t.EulerRotation; // rot.X rot.Y rot.Z
            var scale = t.Scale;  // scale.X scale.Y scale.Z
            Matrix3D ans = GetLocalMatrix(t, RotationOrder.ZYX);

            return new Matrix4(
                ans.value[0, 0], ans.value[0, 1], ans.value[0, 2], ans.value[0, 3],
                ans.value[1, 0], ans.value[1, 1], ans.value[1, 2], ans.value[1, 3],
                ans.value[2, 0], ans.value[2, 1], ans.value[2, 2], ans.value[2, 3],
                ans.value[3, 0], ans.value[3, 1], ans.value[3, 2], ans.value[3, 3]
            );
        }

        public static Matrix4 GetGlobalMatrix(this MeshIO.Node node, MeshIO.Node stopAtParent = null)
        {
            if (node == null) return Matrix4.Identity;

            //Matrix4 globalMatrix = Matrix4.Identity; // start with parent matrix
            Matrix4 globalMatrix = GetMatrix4(node.Transform); // Start with the node's local matrix
            MeshIO.Node currentParent = (MeshIO.Node)node.Parent;

            while (currentParent != null && currentParent != stopAtParent)
            {
                globalMatrix = GetMatrix4(currentParent.Transform) * globalMatrix; // Pre-multiply by parent's local matrix
                currentParent = (MeshIO.Node)currentParent.Parent;
            }
            return globalMatrix;
        }
    }

    static partial class Program
    {

  






    public static void ExportFBX()
        {

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = "ExportedFlver.fbx",
                Filter = "FBX files (*.fbx)|*.fbx|All files (*.*)|*.*",
                Title = "Export FLVER to FBX"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    MeshIO.Scene mioScene = new MeshIO.Scene { Name = Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + "_Scene" };
                    mioScene.RootNode.GetIdOrDefault(); // Ensure scene root has an ID

                    // 1. Create Armature Root
                    MeshIO.Entities.Bone armatureRootNode = new MeshIO.Entities.Bone("Armature") { IsSkeletonRoot = true };
                    armatureRootNode.GetIdOrDefault();
                    mioScene.RootNode.AddChildNode(armatureRootNode);

                    // 2. Process FLVER Nodes (Bones)
                    Dictionary<int, MeshIO.Entities.Bone> processedBones = new Dictionary<int, MeshIO.Entities.Bone>();
                    for (int i = 0; i < targetFlver.Nodes.Count; i++)
                    {
                        var flverNode = targetFlver.Nodes[i];
                        MeshIO.Entities.Bone mioBone = new MeshIO.Entities.Bone(flverNode.Name ?? $"Node_{i}");
                        mioBone.Length = 0.2; // Make it slightly smaller for readabililty
                        mioBone.GetIdOrDefault();

                        // Apply transforms (with Z-flip for position and rotation components)
                        mioBone.Transform.Translation = new XYZ(flverNode.Translation.X, flverNode.Translation.Y, -flverNode.Translation.Z);

                        // FLVER Rotation: System.Numerics.Vector3, Euler angles in radians, YZX order
                        // MeshIO.Entities.Bone.Transform.EulerRotation expects degrees.
                        mioBone.Transform.EulerRotation = FlverFbxRotationHelper.FlverRotationToFbxEulerDegrees(flverNode.Rotation);

                        mioBone.Transform.Scale = new XYZ(flverNode.Scale.X, flverNode.Scale.Y, flverNode.Scale.Z);
                        mioBone.Properties.Add(new Property<int>("RotationOrder", 0));
                        // Set FBX SDK specific rotation order property if needed by importer (MeshIO might handle this via EulerRotation setter)
                        // FbxWriter for Bone might automatically use the order from EulerRotation if it's smart,
                        // or you might need to set a custom property if MeshIO supports reading it for FBX export.
                        // For now, we assume EulerRotation in XYZ order (degrees) is sufficient.
                        // mioBone.Properties.Add(new Property<int>("RotationOrder", 0)); // 0 for XYZ

                        processedBones.Add(i, mioBone);
                    }

                    // Build bone hierarchy
                    for (int i = 0; i < targetFlver.Nodes.Count; i++)
                    {
                        var flverNode = targetFlver.Nodes[i];
                        MeshIO.Entities.Bone currentMioBone = processedBones[i];

                        if (flverNode.ParentIndex == -1)
                        {
                            armatureRootNode.AddChildNode(currentMioBone);
                        }
                        else
                        {
                            if (processedBones.TryGetValue(flverNode.ParentIndex, out MeshIO.Entities.Bone parentMioBone))
                            {
                                parentMioBone.AddChildNode(currentMioBone);
                            }
                            else
                            {
                                // This case should ideally not happen in a valid FLVER
                                Console.WriteLine($"Warning: Parent node with index {flverNode.ParentIndex} not found for node '{flverNode.Name}'. Attaching to armature root.");
                                armatureRootNode.AddChildNode(currentMioBone);
                            }
                        }
                    }
                    
                    // 3. Process FLVER Materials
                    List<MeshIO.Shaders.Material> mioMaterials = new List<MeshIO.Shaders.Material>();
                    foreach (var flverMaterial in targetFlver.Materials)
                    {
                        var mioMat = new MeshIO.Shaders.Material { Name = flverMaterial.Name };
                        mioMat.GetIdOrDefault();
                        // You can try to set diffuse color if available, e.g.:
                        mioMat.DiffuseColor = new Color(200,200,200,255); // Assuming Color takes 0-1 floats
                        mioMat.TransparencyFactor = 0;
                        mioMaterials.Add(mioMat);
                    }


                    // 4. Process FLVER Meshes
                    for (int meshIdx = 0; meshIdx < targetFlver.Meshes.Count; meshIdx++)
                    {
                        var flverMesh = targetFlver.Meshes[meshIdx];
                        MeshIO.Entities.Geometries.Mesh mioMesh = new MeshIO.Entities.Geometries.Mesh { Name = $"Mesh_{meshIdx}" };
                        mioMesh.GetIdOrDefault();

                        // Vertices
                        foreach (var v in flverMesh.Vertices)
                        {
                            mioMesh.Vertices.Add(new XYZ(v.Position.X, v.Position.Y, -v.Position.Z));
                        }

                        // Normals
                        if (flverMesh.Vertices.Count > 0 && flverMesh.Vertices[0].Normal != null) // Check if normals exist
                        {
                            var normalLayer = new LayerElementNormal { Name = "Normals" };
                                normalLayer.MappingMode = MappingMode.ByVertex;//MappingMode.ByControlPoint; // One normal per vertex
                            normalLayer.ReferenceMode = ReferenceMode.Direct;
                            foreach (var v in flverMesh.Vertices)
                            {
                                normalLayer.Normals.Add(new XYZ(v.Normal.X, v.Normal.Y, -v.Normal.Z));
                            }
                            mioMesh.Layers.Add(normalLayer);
                        }

                        // UVs (assuming at least one UV channel, handle multiple if necessary)
                        if (flverMesh.Vertices.Count > 0 && flverMesh.Vertices[0].UVs.Count > 0)
                        {
                            var uvLayer = new LayerElementUV { Name = "UVChannel_1" }; // FBX standard name for first UV
                            uvLayer.MappingMode = MappingMode.ByVertex;//MappingMode.ByControlPoint; /
                                uvLayer.ReferenceMode = ReferenceMode.Direct;
                            foreach (var v in flverMesh.Vertices)
                            {
                                // Flip V coordinate
                                uvLayer.UV.Add(new XY(v.UVs[0].X, 1.0f - v.UVs[0].Y));
                            }
                            mioMesh.Layers.Add(uvLayer);
                        }

                        // Tangents (optional, but good for normal mapping)
                        if (flverMesh.Vertices.Count > 0 && flverMesh.Vertices[0].Tangents.Count > 0)
                        {
                            var tangentLayer = new LayerElementTangent { Name = "Tangents" };
                            tangentLayer.MappingMode = MappingMode.ByVertex;
                            tangentLayer.ReferenceMode = ReferenceMode.Direct;
                            foreach (var v in flverMesh.Vertices)
                            {
                                tangentLayer.Tangents.Add(new XYZ(v.Tangents[0].X, v.Tangents[0].Y, -v.Tangents[0].Z));
                                // Note: Binormals/Bitangents might also be needed depending on engine/renderer.
                                // FBX can store them or they can be derived.
                            }
                            mioMesh.Layers.Add(tangentLayer);
                        }

                        // Faces
                        foreach (var fs in flverMesh.FaceSets)
                        {
                            if (fs.Flags != SoulsFormats.FLVER2.FaceSet.FSFlags.None) continue; // Skip LODs etc.

                            // Check if flverMesh.Vertices.Count is available. If flverMesh is dynamic, might need to get it from the Vertices list.
                            bool use32BitIndices = flverMesh.Vertices.Count >= 65535;
                            var triangles = fs.Triangulate(use32BitIndices); // SoulsFormats method
                            for (int j = 0; j < triangles.Count; j += 3)
                            {
                                mioMesh.Polygons.Add(new Triangle(triangles[j], triangles[j + 1], triangles[j + 2]));
                            }
                        }

                        // Create Node for the mesh
                        MeshIO.Node meshNode = new MeshIO.Node { Name = $"Node_Mesh_{meshIdx}_{mioMaterials[flverMesh.MaterialIndex].Name}" };
                        meshNode.GetIdOrDefault();
                        meshNode.Entities.Add(mioMesh);

                        // Parent mesh node to armature root (common practice for skinned meshes)
                        mioScene.RootNode.AddChildNode(meshNode);
                        // Alternatively, parent to scene root: mioScene.RootNode.AddChildNode(meshNode);

                        // Assign Material to Mesh Node
                        if (flverMesh.MaterialIndex >= 0 && flverMesh.MaterialIndex < mioMaterials.Count)
                        {
                            meshNode.Materials.Add(mioMaterials[flverMesh.MaterialIndex]);
                            var materialLayer = new LayerElementMaterial
                            {
                                Name = "MaterialAssignment",
                                MappingMode = MappingMode.AllSame,
                                ReferenceMode = ReferenceMode.IndexToDirect
                            };
                            materialLayer.Indexes.Add(0); // All polygons use the first material in meshNode.Materials
                            mioMesh.Layers.Add(materialLayer);
                        }
                        
                        // Skinning
                        // FLVER2 Vertex.BoneIndices are indices into targetFlver.Nodes
                        if (flverMesh.UseBoneWeights) // A simple check for skinning
                        {
                            MeshIO.Entities.Skinning.Skin skin = new MeshIO.Entities.Skinning.Skin { Name = $"{mioMesh.Name}_Skin" };
                            skin.GetIdOrDefault();
                            skin.DeformedGeometry = mioMesh; // Link skin to the geometry
                            meshNode.Entities.Add(skin); // Attach skin deformer to the mesh node

                            // Group vertex indices and weights by bone
                            // Key: bone's index in targetFlver.Nodes
                            var boneInfluenceData = new Dictionary<int, List<(int vertexGlobalIndex, double weight)>>();

                            for (int vIdx = 0; vIdx < flverMesh.Vertices.Count; vIdx++)
                            {
                                var flverVertex = flverMesh.Vertices[vIdx];
                                for (int influenceIdx = 0; influenceIdx < flverVertex.BoneIndices.Length; influenceIdx++)
                                {
                                    int boneNodeIndex = flverVertex.BoneIndices[influenceIdx];
                                    float weight = flverVertex.BoneWeights[influenceIdx];

                                    if (weight > 0.0001f) // Consider non-zero weights
                                    {
                                        if (!boneInfluenceData.ContainsKey(boneNodeIndex))
                                        {
                                            boneInfluenceData[boneNodeIndex] = new List<(int, double)>();
                                        }
                                        boneInfluenceData[boneNodeIndex].Add((vIdx, weight));
                                    }
                                }
                            }

                            foreach (var kvp in boneInfluenceData)
                            {
                                int boneNodeIndex = kvp.Key;
                                List<(int vertexGlobalIndex, double weight)> influences = kvp.Value;

                                if (!processedBones.TryGetValue(boneNodeIndex, out MeshIO.Entities.Bone linkBone))
                                {
                                    Console.WriteLine($"Warning: Bone with NodeIndex {boneNodeIndex} not found in processedBones. Skipping cluster for this bone on mesh {meshIdx}.");
                                    continue;
                                }

                                Cluster cluster = new Cluster { Name = $"Cluster_{linkBone.Name}_{mioMesh.Name}", Link = linkBone };
                                cluster.GetIdOrDefault();

                                // TransformMatrix is the world transformation of the bone at bind time.
                                // 事实证明，TransformMatrix用Calc，TransformLinkMatrix用Unity是不行的，会导致Bind出问题
                                // 事实证明，TransformMatrix用Unity，TransformLinkMatrix用Calc也是不行的，会导致Mesh出问题
                                //cluster.TransformMatrix = linkBone.GetGlobalMatrix(mioScene.RootNode);
                                


                                // TransformLinkMatrix is the world transformation of the mesh at bind time.
                                //cluster.TransformLinkMatrix = meshNodeBindGlobalMatrix;
                                cluster.TransformLinkMatrix = linkBone.GetGlobalMatrix(null);//mioScene.RootNode
                                Matrix4 tmp;
                                CSMath.Matrix4.Inverse(cluster.TransformLinkMatrix, out tmp);
                                cluster.TransformMatrix = tmp;


                                foreach (var influence in influences)
                                {
                                    cluster.Indexes.Add(influence.vertexGlobalIndex);
                                    cluster.Weights.Add(influence.weight);
                                }
                                skin.Clusters.Add(cluster);
                            }
                        } 
                    }

                    // 5. FBX Export
                    var writerGlobalSettings = new FbxGlobalSettings(FbxVersion.v7400);
                    FbxWriterOptions options = new FbxWriterOptions
                    {
                        IsBinaryFormat = true, // Start with ASCII for easier debugging
                        Version = FbxVersion.v7400,
                        GlobalSettings = writerGlobalSettings
                    };
                    
                    FbxWriter.Write(saveFileDialog.FileName, mioScene, options);
                    MessageBox.Show("FBX Export successful!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"FBX Export Error.\n\nError message: {ex.Message}\n\nDetails:\n\n{ex.StackTrace}",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


    // You'll need the NotificationHelper from MeshIO.Examples.Common for logging, or remove/replace the logger call.
    // namespace MeshIO.Examples.Common { public static class NotificationHelper { public static void LogConsoleNotification(object sender, NotificationEventArgs e) { Console.WriteLine($"[{e.Type}] {e.Message}"); } } }

    // Example Usage (assuming you have a 'targetFlver' instance of SoulsFormats.FLVER.FLVER2):
    // FlverToFbxExporter.SetTargetFlver(myFlverInstance);
    // FlverToFbxExporter.ExportFlverToFbx();
    //1.83 New
    //Experimental
    public static void ExportDAE()
        {
            var openFileDialog2 = new SaveFileDialog();
            openFileDialog2.FileName = "Exported.dae";
            string res = "";
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Assimp.Scene s = new Assimp.Scene();
                    s.RootNode = new Assimp.Node();

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
                        Assimp.Mesh meshNew = new Assimp.Mesh("Mesh_M" + i, Assimp.PrimitiveType.Triangle);
                        foreach (var v in m.Vertices)
                        {
                            // To make the exported model looks correct, need to flip the 3d model's Z axis
                            //meshNew.Vertices.Add(new Assimp.Vector3D(v.Position.X, v.Position.Y, v.Position.Z));
                            //meshNew.Normals.Add(new Assimp.Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                            //meshNew.Tangents.Add(new Assimp.Vector3D(v.Tangents[0].X, v.Tangents[0].Y, v.Tangents[0].Z));
                            meshNew.Vertices.Add(new Assimp.Vector3D(v.Position.X, v.Position.Y, -v.Position.Z));
                            meshNew.Normals.Add(new Assimp.Vector3D(v.Normal.X, v.Normal.Y, -v.Normal.Z));
                            meshNew.Tangents.Add(new Assimp.Vector3D(v.Tangents[0].X, v.Tangents[0].Y, -v.Tangents[0].Z));

                            meshNew.TextureCoordinateChannels[0].Add(new Assimp.Vector3D(v.UVs[0].X, 1 - v.UVs[0].Y, 0));

                        }

                        var vs = m.GetFaces();
                        foreach (var fs in m.FaceSets)
                        {
                            // Ignore LOD facesets
                            if (fs.Flags != FLVER2.FaceSet.FSFlags.None) { continue; }
                            var arr = fs.Triangulate(m.Vertices.Count < 65535);
                            for (int j = 0; j < arr.Count - 2; j += 3)
                            {
                                meshNew.Faces.Add(new Face(new int[] { (int)arr[j], (int)arr[j + 1], (int)arr[j + 2] }));
                            }


                            //OLD:foreach (var arr in fs.GetFaces())
                            //OLD:{
                            //OLD:    meshNew.Faces.Add(new Face(new int[] { (int)arr[0], (int)arr[1],(int)arr[2] }));
                            //OLD:}
                        }

                        meshNew.MaterialIndex = m.MaterialIndex;
                        s.Meshes.Add(meshNew);


                        Assimp.Node nbase = new Assimp.Node();
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
            else
            {
                return;
            }


        }



    }
}