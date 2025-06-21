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
using System.Numerics;

namespace MySFformat
{
    public static class FSEulerAngleConverter
    {
        private const float Deg2Rad = (float)(Math.PI / 180.0);
        private const float Rad2Deg = (float)(180.0 / Math.PI);
        // Threshold for gimbal lock detection (when sin of middle angle is close to +/-1)
        private const float GimbalLockThreshold = 0.99999f; // Slightly less than 1.0

        public static RotationOrder inverse(RotationOrder r) {
            if (r == RotationOrder.XYZ) { return RotationOrder.ZYX; }
            if (r == RotationOrder.XZY) { return RotationOrder.YZX; }
            if (r == RotationOrder.YXZ) { return RotationOrder.ZXY; }
            if (r == RotationOrder.YZX) { return RotationOrder.XZY; }
            if (r == RotationOrder.ZXY) { return RotationOrder.YXZ; }
            if (r == RotationOrder.ZYX) { return RotationOrder.XYZ; }
            return RotationOrder.XYZ;
        }

        /// <summary>
        /// Converts Euler angles from one rotation order to another, robustly handling gimbal lock.
        /// </summary>
        /// <param name="eulerAnglesDegrees">Input Euler angles in degrees.</param>
        /// <param name="inputOrder">Rotation order of the input Euler angles.</param>
        /// <param name="outputOrder">Desired rotation order for the output Euler angles.</param>
        /// <returns>Euler angles in degrees in the specified output order.</returns>
        public static MyVector3 ConvertRotationOrder(MyVector3 eulerAnglesDegrees, RotationOrder inputOrder, RotationOrder outputOrder)
        {
            //Have to inverse RotationOrder because row-major and col-major difference, outside of this function use col-major
            //Inside uses row-major(Which is FBX's rotation order definition)
            inputOrder = inverse(inputOrder);
            outputOrder = inverse(outputOrder);
            // Convert MyVector3 to System.Numerics.Vector3 for calculations
            Vector3 eulerDegreesVecNum = new Vector3(eulerAnglesDegrees.X, eulerAnglesDegrees.Y, eulerAnglesDegrees.Z);

            // 1. Convert input Euler angles (specific order) to Quaternion
            System.Numerics.Quaternion totalRotation = EulerToQuaternion(eulerDegreesVecNum, inputOrder);

            // 2. Convert Quaternion to output Euler angles (specific order)
            Vector3 outputEulerDegreesVecNum = QuaternionToEuler(totalRotation, outputOrder);

            // Convert System.Numerics.Vector3 back to MyVector3
            return new MyVector3(outputEulerDegreesVecNum.X, outputEulerDegreesVecNum.Y, outputEulerDegreesVecNum.Z);
        }

        /// <summary>
        /// Converts Euler angles (in degrees) of a specific order to a Quaternion.
        /// Assumes intrinsic rotations (rotations around the object's own axes).
        /// </summary>
        private static System.Numerics.Quaternion EulerToQuaternion(Vector3 eulerDegrees, RotationOrder order)
        {
            Vector3 eulerRadians = eulerDegrees * Deg2Rad;
            float x = eulerRadians.X;
            float y = eulerRadians.Y;
            float z = eulerRadians.Z;

            System.Numerics.Quaternion qX = System.Numerics.Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
            System.Numerics.Quaternion qY = System.Numerics.Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
            System.Numerics.Quaternion qZ = System.Numerics.Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);

            // Intrinsic rotations: apply in order, so quaternion multiplication is in reverse order.
            // E.g., for XYZ order, it's R = Rz * Ry * Rx (rotate by X, then new Y, then new Z)
            // System.Numerics.Quaternion multiplication is applied left-to-right (q2 * q1 means q1 then q2)
            // So for intrinsic R = Rz * Ry * Rx, we need Qfinal = Qz * Qy * Qx
            switch (order)
            {
                case RotationOrder.XYZ: // Rz(R_y(R_x))
                    return qZ * qY * qX;
                case RotationOrder.XZY: // Ry(R_z(R_x))
                    return qY * qZ * qX;
                case RotationOrder.YXZ: // Rz(R_x(R_y))
                    return qZ * qX * qY;
                case RotationOrder.YZX: // Rx(R_z(R_y))  <- Your flverOrder
                    return qX * qZ * qY;
                case RotationOrder.ZXY: // Ry(R_x(R_z))
                    return qY * qX * qZ;
                case RotationOrder.ZYX: // Rx(R_y(R_z))  <- Your fbxOrder
                    return qX * qY * qZ;
                default:
                    throw new ArgumentException("Unsupported input rotation order.", nameof(order));
            }
        }

        /// <summary>
        /// Converts a Quaternion to Euler angles (in degrees) of a specific order.
        /// Handles gimbal lock robustly.
        /// Formulas adapted from various sources, e.g., Ken Shoemake "Euler Angle Conversion",
        /// and http://www.euclideanspace.com/maths/geometry/rotations/conversions/quaternionToEuler/
        /// System.Numerics.Quaternion is (X, Y, Z, W).
        /// </summary>
        private static Vector3 QuaternionToEuler(System.Numerics.Quaternion q, RotationOrder order)
        {
            // Normalize the quaternion to ensure W is positive if possible, for consistency.
            // While not strictly necessary for math, it can avoid some alternative representations
            // that are equivalent but might differ by 2*PI or sign flips.
            // For System.Numerics.Quaternion, this is less of an issue than with manual math.
            // q = Quaternion.Normalize(q); // Normalization is good if q might not be unit.
            // If W is negative, q and -q represent the same rotation.
            // Negating q (if W < 0) can sometimes lead to more "canonical" Euler angles.
            if (q.W < 0)
            {
                q = System.Numerics.Quaternion.Negate(q); // Flips all components
            }


            float xRad = 0, yRad = 0, zRad = 0;

            // Common components from quaternion for direct extraction
            float qx = q.X;
            float qy = q.Y;
            float qz = q.Z;
            float qw = q.W;

            // Pre-calculate squared terms
            float qx2 = qx * qx;
            float qy2 = qy * qy;
            float qz2 = qz * qz;
            // float qw2 = qw * qw; // Not always needed directly

            switch (order)
            {
                case RotationOrder.XYZ: // Roll (X), Pitch (Y), Yaw (Z)
                    {
                        float sinPitch = 2.0f * (qw * qy - qx * qz);
                        if (Math.Abs(sinPitch) > GimbalLockThreshold) // Gimbal lock
                        {
                            yRad = Math.Sign(sinPitch) * (float)(Math.PI / 2.0); // Pitch = +/- 90 deg
                            xRad = 0; // Conventionally, set Roll to 0
                            zRad = Math.Sign(sinPitch) * 2.0f * (float)Math.Atan2(qx, qw); // Yaw absorbs
                        }
                        else
                        {
                            yRad = (float)Math.Asin(sinPitch);
                            xRad = (float)Math.Atan2(2.0f * (qw * qx + qy * qz), 1.0f - 2.0f * (qx2 + qy2));
                            zRad = (float)Math.Atan2(2.0f * (qw * qz + qx * qy), 1.0f - 2.0f * (qy2 + qz2));
                        }
                    }
                    break;

                case RotationOrder.XZY: // X, then Z, then Y
                    {
                        float sinZ = 2.0f * (qw * qz + qx * qy);
                        if (Math.Abs(sinZ) > GimbalLockThreshold)
                        {
                            zRad = Math.Sign(sinZ) * (float)(Math.PI / 2.0);
                            xRad = 0; // Convention
                            yRad = Math.Sign(sinZ) * 2.0f * (float)Math.Atan2(qy, qw); // Y absorbs
                        }
                        else
                        {
                            zRad = (float)Math.Asin(sinZ);
                            xRad = (float)Math.Atan2(2.0f * (qw * qx - qy * qz), 1.0f - 2.0f * (qx2 + qz2));
                            yRad = (float)Math.Atan2(2.0f * (qw * qy - qx * qz), 1.0f - 2.0f * (qy2 + qz2));
                        }
                    }
                    break;

                case RotationOrder.YXZ: // Y, then X, then Z
                    {
                        float sinX = 2.0f * (qw * qx + qy * qz);
                        if (Math.Abs(sinX) > GimbalLockThreshold)
                        {
                            xRad = Math.Sign(sinX) * (float)(Math.PI / 2.0);
                            yRad = 0; // Convention
                            zRad = Math.Sign(sinX) * 2.0f * (float)Math.Atan2(qz, qw); // Z absorbs
                        }
                        else
                        {
                            xRad = (float)Math.Asin(sinX);
                            yRad = (float)Math.Atan2(2.0f * (qw * qy - qx * qz), 1.0f - 2.0f * (qy2 + qx2));
                            zRad = (float)Math.Atan2(2.0f * (qw * qz - qx * qy), 1.0f - 2.0f * (qz2 + qx2));
                        }
                    }
                    break;

                case RotationOrder.YZX: // Y, then Z, then X  <- Your flverOrder
                    {
                        float sinZ = 2.0f * (qw * qz - qx * qy); // Note sign change vs XZY due to order
                        if (Math.Abs(sinZ) > GimbalLockThreshold)
                        {
                            zRad = Math.Sign(sinZ) * (float)(Math.PI / 2.0);
                            yRad = 0; // Convention
                            xRad = Math.Sign(sinZ) * 2.0f * (float)Math.Atan2(qx, qw); // X absorbs
                        }
                        else
                        {
                            zRad = (float)Math.Asin(sinZ);
                            yRad = (float)Math.Atan2(2.0f * (qw * qy + qx * qz), 1.0f - 2.0f * (qy2 + qz2));
                            xRad = (float)Math.Atan2(2.0f * (qw * qx + qy * qz), 1.0f - 2.0f * (qx2 + qz2));
                        }
                    }
                    break;

                case RotationOrder.ZXY: // Z, then X, then Y
                    {
                        float sinX = 2.0f * (qw * qx - qy * qz); // Note sign change
                        if (Math.Abs(sinX) > GimbalLockThreshold)
                        {
                            xRad = Math.Sign(sinX) * (float)(Math.PI / 2.0);
                            zRad = 0; // Convention
                            yRad = Math.Sign(sinX) * 2.0f * (float)Math.Atan2(qy, qw); // Y absorbs
                        }
                        else
                        {
                            xRad = (float)Math.Asin(sinX);
                            zRad = (float)Math.Atan2(2.0f * (qw * qz + qx * qy), 1.0f - 2.0f * (qz2 + qx2));
                            yRad = (float)Math.Atan2(2.0f * (qw * qy + qx * qz), 1.0f - 2.0f * (qy2 + qx2));
                        }
                    }
                    break;

                case RotationOrder.ZYX: // Z, then Y, then X  <- Your fbxOrder
                    {
                        float sinY = 2.0f * (qw * qy + qx * qz); // Note sign change vs XYZ
                        if (Math.Abs(sinY) > GimbalLockThreshold)
                        {
                            yRad = Math.Sign(sinY) * (float)(Math.PI / 2.0);
                            zRad = 0; // Convention
                            xRad = Math.Sign(sinY) * 2.0f * (float)Math.Atan2(qx, qw); // X absorbs
                        }
                        else
                        {
                            yRad = (float)Math.Asin(sinY);
                            zRad = (float)Math.Atan2(2.0f * (qw * qz - qx * qy), 1.0f - 2.0f * (qy2 + qz2));
                            xRad = (float)Math.Atan2(2.0f * (qw * qx - qy * qz), 1.0f - 2.0f * (qx2 + qy2));
                        }
                    }
                    break;

                default:
                    throw new ArgumentException("Unsupported output rotation order.", nameof(order));
            }

            return new Vector3(xRad * Rad2Deg, yRad * Rad2Deg, zRad * Rad2Deg);
        }
    }
    public static class FlverFbxRotationHelper
    {
        // FLVER typically uses YZX Euler order, radians.
        // FBX typically uses XYZ Euler order, degrees.
        // This helper converts from FLVER's YZX radians to FBX's XYZ degrees
        // AND applies the Z-axis mirror for rotations.
        public static XYZ FlverRotationToFbxEulerDegrees(System.Numerics.Vector3 flverRotationRadians, bool debug=false)
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
            RotationOrder flverOrder = RotationOrder.YZX; // FLVER standard [YZX]
            RotationOrder fbxOrder = RotationOrder.ZYX;   // Common FBX target [ZYX]
            if (debug) {
                Console.WriteLine($"Raw degree {xRad} {yRad} {zRad}");
            }
            var convertedAngles = FSEulerAngleConverter.ConvertRotationOrder(inputAnglesDeg, flverOrder, fbxOrder);
            if (debug)
            {
                Console.WriteLine($"Converted Raw degree {convertedAngles}");
            }
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
                    int fbxRotOrder = 1;
                    MeshIO.Scene mioScene = new MeshIO.Scene { Name = Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + "_Scene" };
                    mioScene.RootNode.GetIdOrDefault(); // Ensure scene root has an ID
                    
                    //mioScene.Properties.Add(new Property<int>("RotationOrder", 1)); // Not working for blender
                    // 1. Create Armature Root
                    MeshIO.Entities.Bone armatureRootNode = new MeshIO.Entities.Bone("Armature") { IsSkeletonRoot = true };
                    armatureRootNode.GetIdOrDefault();
                    //armatureRootNode.Properties.Add(new Property<int>("RotationOrder", 1)); // Not working for blender
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
                        
                        mioBone.Transform.EulerRotation = FlverFbxRotationHelper.FlverRotationToFbxEulerDegrees(flverNode.Rotation, false);
                        Console.WriteLine($"{flverNode.Name}:{flverNode.Rotation}>{mioBone.Transform.EulerRotation}");
                        mioBone.Transform.Scale = new XYZ(flverNode.Scale.X, flverNode.Scale.Y, flverNode.Scale.Z);
                        //mioBone.Properties.Add(new Property<int>("RotationOrder", 1)); // Not working for blender

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
                        mioMat.ShadingModel = "Diffuse";
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
                            }
                            mioMesh.Layers.Add(tangentLayer);
                        }

                        // Bitangents (optional, but good for normal mapping)
                        if (flverMesh.Vertices.Count > 0 && flverMesh.Vertices[0].Tangents.Count > 0)
                        {
                            var bitangentLayer = new LayerElementBinormal { Name = "BitangentLayer" };
                            bitangentLayer.MappingMode = MappingMode.ByVertex;
                            bitangentLayer.ReferenceMode = ReferenceMode.Direct;
                             
                            foreach (var v in flverMesh.Vertices)
                            {
                                // 1. 获取已经转换到目标坐标系下的法线(Normal)和切线(Tangent)
                                // 注意：这里我们只取XYZ分量，W分量单独处理
                                XYZ mioNormal = new XYZ(v.Normal.X, v.Normal.Y, -v.Normal.Z);
                                XYZ mioTangent = new XYZ(v.Tangents[0].X, v.Tangents[0].Y, -v.Tangents[0].Z);

                                // 2. 使用CSMath库（MeshIO依赖的库）来进行叉积计算
                                // 假设目标FBX消费端（如Blender/Unity）需要一个右手坐标系TBN
                                // 那么 Bitangent = cross(Normal, Tangent)
                                // CSMath.XYZ.Cross(A, B) 返回 A和B的叉积
                                XYZ calculatedBitangent = XYZ.Cross(mioNormal, mioTangent);

                                // 3. 根据原始切线的W分量来决定是否翻转Bitangent
                                // 这是至关重要的一步，用来修正UV镜像导致的手性翻转问题
                                float tangentW = v.Tangents[0].W;
                                if (tangentW < 0.0f)
                                {
                                    // 如果W为负，翻转计算出的Bitangent
                                    calculatedBitangent = new XYZ(
                                        -calculatedBitangent.X,
                                        -calculatedBitangent.Y,
                                        -calculatedBitangent.Z
                                    );
                                }

                                // 4. 将最终的Bitangent添加到layer中
                                // 注意：此时的 bitangent 已经是目标坐标系下的向量，不需要再翻转Z轴了！
                                bitangentLayer.Binormals.Add(calculatedBitangent);
                            }
                            mioMesh.Layers.Add(bitangentLayer);
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