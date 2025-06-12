// FbxBoneImporter.cs (Updated Version)
using Assimp;
using MeshIO.FBX.Helpers;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // 确保引用 System.Numerics
using Matrix4x4 = Assimp.Matrix4x4;
using Quaternion = Assimp.Quaternion; // For ArgumentOutOfRangeException

namespace MySFformat
{
    public static class FbxBoneImporter
    {

        /// <summary>
        /// Imports a bone hierarchy from an Assimp scene and overrides the bones in a FLVER2 model,
        /// respecting the user-defined coordinate system settings.
        /// </summary>
        /// <param name="sourceScene">The source Assimp scene.</param>
        /// <param name="targetFlver">The target FLVER2 model to modify.</param>
        /// <param name="settings">The user's FBX import settings, containing axis mapping.</param>
        public static void ImportAndOverrideBones(Scene sourceScene, FLVER2 targetFlver, FbxImportSettings settings)
        {
            if (sourceScene.RootNode == null)
            {
                System.Windows.Forms.MessageBox.Show("Source model has no root node.", "Bone Import Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // --- CORE TRANSFORMATION LOGIC ---
            // 1. Create the dynamic coordinate system transformation matrix based on user settings.
            Matrix4x4 remapMatrix = CreateRemapMatrix(settings);
            Matrix4x4 inverseRemapMatrix = new Matrix4x4(remapMatrix);
            inverseRemapMatrix.Inverse();

            // Clear existing bones and prepare for new ones
            targetFlver.Nodes.Clear();
            var assimpNodes = new List<Assimp.Node>();
            var flverNodes = new List<FLVER.Node>();
            var nodeMapping = new Dictionary<Assimp.Node, int>();

            // --- Pass 1: Traverse, Convert, and Create Nodes ---
            var startNode = sourceScene.RootNode;
            if (startNode.Name == "RootNode" && startNode.HasChildren) {
                startNode = startNode.Children[0];
            }
            //if (startNode.Name == "Armature" && startNode.HasChildren)
            //{
            //    startNode = startNode.Children[0];
            //}

            foreach (var child in startNode.Children)
            {
                remapMatrix = CreateRemapMatrix(settings);
                Console.WriteLine($"Remap matrix: {remapMatrix.ToString()}");
                inverseRemapMatrix = new Matrix4x4(remapMatrix);
                inverseRemapMatrix.Inverse();
                RecursiveProcessNodes(child, -1, assimpNodes, flverNodes, nodeMapping, remapMatrix, inverseRemapMatrix);
            }

            

            // Add all created FLVER nodes to the target model
            targetFlver.Nodes.AddRange(flverNodes);

            // --- Pass 2: Rebuild Hierarchy Indices ---
            RebuildFlverHierarchy(assimpNodes, targetFlver, nodeMapping);

            System.Windows.Forms.MessageBox.Show($"Successfully imported and replaced {targetFlver.Nodes.Count} bones.", "Bone Import Complete", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }

        /// <summary>
        /// Recursively traverses the Assimp node tree, converts transforms, and creates FLVER.Nodes.
        /// </summary>
        private static void RecursiveProcessNodes(Assimp.Node assimpNode, int parentIndex, List<Assimp.Node> assimpNodeList, List<FLVER.Node> flverNodeList, Dictionary<Assimp.Node, int> nodeMapping, Matrix4x4 remapMatrix, Matrix4x4 inverseRemapMatrix)
        {
            var flverNode = new FLVER.Node
            {
                Name = assimpNode.Name,
                ParentIndex = (short)parentIndex,
            };

            // 2. Convert Assimp's local transform matrix to System.Numerics.Matrix4x4
            var fbxMatrix = assimpNode.Transform;

            // 3. Apply the change-of-basis formula: M_flver = P * M_fbx * P_inverse
            // This correctly converts the transformation from the FBX coordinate system to the FLVER one.
            var flverMatrix = remapMatrix * fbxMatrix * inverseRemapMatrix;
            var numericsFlverMatrix = ToNumerics(flverMatrix);
            // 4. Decompose the final matrix to get FLVER-compatible TRS values.
            flverMatrix.Decompose(out var scale, out var rotationQuat, out var translation);
            if (scale != null)
            {
                flverNode.Translation = new Vector3(translation.X, translation.Y, translation.Z);
                flverNode.Scale = new Vector3(scale.X, scale.Y, scale.Z);
                //MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard
                // MeshIO.FBX.Helpers.RotationOrder.ZYX;   // Common FBX target
                // 5. Convert the quaternion to FLVER's Euler angles (in radians).
                var debug=false;
                if (assimpNode.Name == "[cloth]BD_M_5030_hair_06") { 
                    debug = true;
                    Console.WriteLine($"-Remap matrix: {remapMatrix.ToString()}");
                    Console.WriteLine($"-fbxMatrix matrix: {fbxMatrix.ToString()}");
                    Console.WriteLine($"-inverseRemapMatrix matrix: {inverseRemapMatrix.ToString()}");
                    Console.WriteLine($"-flverMatrix matrix: {flverMatrix.ToString()}");
                }
                flverNode.Rotation = MatrixToEulerYZX(numericsFlverMatrix);
            }
            else
            {
                // Fallback for non-decomposable matrices (e.g., shearing, though rare for bones)
                
                flverNode.Translation = Vector3.Transform(new Vector3(assimpNode.Transform.A4, assimpNode.Transform.B4, assimpNode.Transform.C4), ToNumerics(remapMatrix));
                flverNode.Scale = new Vector3(1, 1, 1);
                flverNode.Rotation = new Vector3(0, 0, 0);
            }

            int currentIndex = flverNodeList.Count;
            assimpNodeList.Add(assimpNode);
            flverNodeList.Add(flverNode);
            nodeMapping[assimpNode] = currentIndex;

            // Recurse for children
            foreach (var child in assimpNode.Children)
            {
                RecursiveProcessNodes(child, currentIndex, assimpNodeList, flverNodeList, nodeMapping, remapMatrix, inverseRemapMatrix);
            }
        }

        /// <summary>
        /// Rebuilds the parent-child-sibling indices for the flat list of FLVER nodes.
        /// </summary>
        private static void RebuildFlverHierarchy(List<Assimp.Node> assimpNodes, FLVER2 flver, Dictionary<Assimp.Node, int> nodeMapping)
        {
            for (int i = 0; i < assimpNodes.Count; i++)
            {
                var assimpNode = assimpNodes[i];
                var flverNode = flver.Nodes[i];

                if (assimpNode.ChildCount > 0)
                    flverNode.FirstChildIndex = (short)nodeMapping[assimpNode.Children[0]];
                else
                    flverNode.FirstChildIndex = -1;

                var parent = assimpNode.Parent;
                if (parent != null)
                {
                    var siblings = parent.Children;
                    int siblingIndex = siblings.IndexOf(assimpNode);
                    if (siblingIndex < siblings.Count - 1 && siblingIndex != -1)
                    {
                        var targetSibling = siblings[siblingIndex + 1];
                        if (nodeMapping.ContainsKey(targetSibling))
                        {
                            flverNode.NextSiblingIndex = (short)nodeMapping[targetSibling];
                        }
                        else {
                            flverNode.NextSiblingIndex = -1;
                        }
                        
                    }
                    else { flverNode.NextSiblingIndex = -1; }
                        
                }
                else
                {
                    flverNode.NextSiblingIndex = -1;
                }
            }
        }

        #region Helper Methods

        /// <summary>
        /// Creates a transformation matrix based on the user-defined axis remapping settings.
        /// The columns of this matrix are the new basis vectors.
        /// </summary>
        private static Matrix4x4 CreateRemapMatrix(FbxImportSettings settings)
        {
            Vector3 GetAxisAsVector(FbxImportSettings.Axis axis)
            {
                switch (axis)
                {
                    case FbxImportSettings.Axis.X: return Vector3.UnitX;
                    case FbxImportSettings.Axis.Y: return Vector3.UnitY;
                    case FbxImportSettings.Axis.Z: return Vector3.UnitZ;
                    case FbxImportSettings.Axis.NegX: return -Vector3.UnitX;
                    case FbxImportSettings.Axis.NegY: return -Vector3.UnitY;
                    case FbxImportSettings.Axis.NegZ: return -Vector3.UnitZ;
                    default: throw new ArgumentOutOfRangeException(nameof(axis));
                }
            }

            Vector3 newX_target = GetAxisAsVector(settings.PrimaryAxis);
            Vector3 newY_target = GetAxisAsVector(settings.SecondaryAxis);
            Vector3 newZ_target = Vector3.Cross(newX_target, newY_target);

            if (settings.MirrorTertiaryAxis)
            {
                //TODO：
                newZ_target *= -1;
            }

            // The transformation matrix has the target basis vectors as its columns.
            // This matrix transforms a vector from the source (FBX) basis to the target (FLVER) basis.
            return new Matrix4x4(
                newX_target.X, newY_target.X, newZ_target.X, 0,
                newX_target.Y, newY_target.Y, newZ_target.Y, 0,
                newX_target.Z, newY_target.Z, newZ_target.Z, 0,
                0, 0, 0, 1
            );
        }

        /// <summary>
        /// 正确地将 Assimp 的列主序矩阵转换为 System.Numerics 的列主序矩阵。
        /// System.Numerics.Matrix4x4 的构造函数需要行主序的参数，所以必须正确地重新排列。
        /// </summary>
        private static System.Numerics.Matrix4x4 ToNumerics(Assimp.Matrix4x4 m)
        {
            return new System.Numerics.Matrix4x4(
                m.A1, m.A2, m.A3, m.A4, // 第一行
                m.B1, m.B2, m.B3, m.B4, // 第二行
                m.C1, m.C2, m.C3, m.C4, // 第三行
                m.D1, m.D2, m.D3, m.D4  // 第四行
            );
        }


        //
        public static CSMath.XYZ RefFlverToFBXRot(System.Numerics.Vector3 flverRotationRadians)
        {
            // Convert radians to degrees first
            float xRad = flverRotationRadians.X;
            float yRad = flverRotationRadians.Y;
            float zRad = flverRotationRadians.Z;

            float xDeg = (float)CSMath.MathUtils.RadToDeg(xRad);
            float yDeg = (float)CSMath.MathUtils.RadToDeg(yRad);
            float zDeg = (float)CSMath.MathUtils.RadToDeg(zRad);

            // Input angles are in YZX order (as per FLVER convention)
            MeshIO.FBX.Helpers.MyVector3 inputAnglesDeg = new MeshIO.FBX.Helpers.MyVector3(xDeg, yDeg, zDeg);
            MeshIO.FBX.Helpers.RotationOrder flverOrder = MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard ()
            MeshIO.FBX.Helpers.RotationOrder fbxOrder = MeshIO.FBX.Helpers.RotationOrder.ZYX;   // Common FBX target

            var convertedAngles = MeshIO.FBX.Helpers.EulerAngleConverter.ConvertRotationOrder(inputAnglesDeg, flverOrder, fbxOrder);

            // Apply mirroring for coordinate system difference (Z-axis flip for positions implies this for rotations)
            // If positions are (X, Y, -Z), rotations around X and Y effectively flip, Z stays.
            return new CSMath.XYZ(
                -1 * convertedAngles.X,
                -1 * convertedAngles.Y,
                convertedAngles.Z
            );
        }

        public static float Clamp(float a, float min, float max) {
            if (a < min) { return min; }
            if (a > max) { return max; }
            return a;
        }
        /// <summary>
        /// Converts a Quaternion to Euler angles (in radians) based on the specified rotation order.
        /// </summary>
        public static Vector3 FromQuaternion(System.Numerics.Quaternion q, RotationOrder order)
        {
            System.Numerics.Quaternion nq = new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);
            var M = System.Numerics.Matrix4x4.CreateFromQuaternion(nq);
            float x, y, z;

            switch (order)
            {
                case RotationOrder.XYZ:
                    // Pitch from M31
                    float sinPitch = Clamp(-M.M31, -1.0f, 1.0f);
                    y = (float)Math.Asin(sinPitch);
                    float cosPitch = (float)Math.Cos(y);
                    if (Math.Abs(cosPitch) > 0.0001f)
                    {
                        // Roll from M32, M33
                        x = (float)Math.Atan2(M.M32, M.M33);
                        // Yaw from M21, M11
                        z = (float)Math.Atan2(M.M21, M.M11);
                    }
                    else // Gimbal lock
                    {
                        x = (float)Math.Atan2(-M.M23, M.M22);
                        z = 0;
                    }
                    break;

                case RotationOrder.XZY:
                    // Yaw from M21
                    float sinYaw = Clamp(M.M21, -1.0f, 1.0f);
                    z = (float)Math.Asin(sinYaw);
                    float cosYaw = (float)Math.Cos(z);
                    if (Math.Abs(cosYaw) > 0.0001f)
                    {
                        // Roll from M23, M22
                        x = (float)Math.Atan2(-M.M23, M.M22);
                        // Pitch from M31, M11
                        y = (float)Math.Atan2(-M.M31, M.M11);
                    }
                    else // Gimbal lock
                    {
                        x = (float)Math.Atan2(M.M32, M.M33);
                        y = 0;
                    }
                    break;

                case RotationOrder.YXZ:
                    // Roll from M32
                    float sinRoll = Clamp(M.M32, -1.0f, 1.0f);
                    x = (float)Math.Asin(sinRoll);
                    float cosRoll = (float)Math.Cos(x);
                    if (Math.Abs(cosRoll) > 0.0001f)
                    {
                        // Pitch from M31, M33
                        y = (float)Math.Atan2(-M.M31, M.M33);
                        // Yaw from M12, M22
                        z = (float)Math.Atan2(-M.M12, M.M22);
                    }
                    else // Gimbal lock
                    {
                        y = (float)Math.Atan2(M.M13, M.M11);
                        z = 0;
                    }
                    break;

                case RotationOrder.YZX:
                    // Roll from M12
                    float sinRoll2 = Clamp(-M.M12, -1.0f, 1.0f);
                    x = (float)Math.Asin(sinRoll2);
                    float cosRoll2 = (float)Math.Cos(x);
                    if (Math.Abs(cosRoll2) > 0.0001f)
                    {
                        // Yaw from M13, M11
                        z = (float)Math.Atan2(M.M13, M.M11);
                        // Pitch from M32, M22
                        y = (float)Math.Atan2(M.M32, M.M22);
                    }
                    else // Gimbal lock
                    {
                        z = (float)Math.Atan2(-M.M31, M.M33);
                        y = 0;
                    }
                    break;

                case RotationOrder.ZXY:
                    // Pitch from M23
                    float sinPitch2 = Clamp(M.M23, -1.0f, 1.0f);
                    x = (float)Math.Asin(sinPitch2);
                    float cosPitch2 = (float)Math.Cos(x);
                    if (Math.Abs(cosPitch2) > 0.0001f)
                    {
                        // Yaw from M21, M22
                        z = (float)Math.Atan2(-M.M21, M.M22);
                        // Roll from M13, M33
                        y = (float)Math.Atan2(-M.M13, M.M33);
                    }
                    else // Gimbal lock
                    {
                        z = (float)Math.Atan2(M.M12, M.M11);
                        y = 0;
                    }
                    break;

                case RotationOrder.ZYX:
                    // Pitch from M13
                    float sinPitch3 = Clamp(-M.M13, -1.0f, 1.0f);
                    y = (float)Math.Asin(sinPitch3);
                    float cosPitch3 = (float)Math.Cos(y);
                    if (Math.Abs(cosPitch3) > 0.01f)
                    {
                        // Roll from M23, M33
                        x = (float)Math.Atan2(M.M23, M.M33);
                        // Yaw from M12, M11
                        z = (float)Math.Atan2(M.M12, M.M11);
                    }
                    else // Gimbal lock
                    {
                        //x = 0;
                        //z = (float)Math.Atan2(-M.M21, M.M22);
                        y = 0; // Set yaw to zero because of gimbal lock
                        z = (float)Math.Atan2(-M.M21, M.M22); // Adjust roll instead
                        x = (float)Math.Atan2(-M.M23, M.M33); // This line is necessary if you want to calculate the roll in gimbal lock situation
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(order), "Invalid rotation order specified.");
            }

            return new Vector3(x, y, z);
        }

        private static Vector3 MatrixToEulerYZX(System.Numerics.Matrix4x4 M)
        {
            float x, y, z;

            // We extract the Z rotation from M.M21. For a YZX rotation matrix:
            // R = Ry * Rz * Rx
            // M.M21 = sin(z)
            float sinZ = M.M21;

            // Clamp the value to handle potential floating point inaccuracies
            sinZ = Math.Max(-1.0f, Math.Min(1.0f, sinZ));

            // Check for gimbal lock (when Z rotation is +/- 90 degrees)
            if (Math.Abs(sinZ) > 0.99999f)
            {
                // Gimbal lock has occurred. We can't distinguish between Y and X rotations.
                // By convention, we set the Y rotation to 0 and attribute all remaining rotation to X.
                y = 0.0f;
                z = (float)Math.PI / 2.0f * sinZ; // z will be +90 or -90 degrees

                // x = atan2(-M.M32, M.M22) would be the formula but since we have YZX order, 
                // the correct formula is derived from the expanded matrix at sin(z)=+/-1
                // x = atan2(M.M32, M.M33)
                x = (float)Math.Atan2(M.M32, M.M33);
            }
            else
            {
                // No gimbal lock
                z = (float)Math.Asin(sinZ);

                // y = atan2(-M.M31, M.M11)
                y = (float)Math.Atan2(-M.M31, M.M11);

                // x = atan2(-M.M23, M.M22)
                x = (float)Math.Atan2(-M.M23, M.M22);
            }

            return new Vector3(x, y, z);
        }


        private const float Epsilon = 1e-6f; // For gimbal lock checks

        // Helper to construct the rotation matrix from Euler angles and a given order
        private static Matrix3D BuildRotationMatrix(MyVector3 eulerAnglesDegrees, RotationOrder order)
        {
            Matrix3D rx = Matrix3D.generateRotXMatrix(eulerAnglesDegrees.X);
            Matrix3D ry = Matrix3D.generateRotYMatrix(eulerAnglesDegrees.Y);
            Matrix3D rz = Matrix3D.generateRotZMatrix(eulerAnglesDegrees.Z);
            switch (order)
            {
                case RotationOrder.XYZ: return rx * ry * rz;
                case RotationOrder.XZY: return rx * rz * ry;
                case RotationOrder.YXZ: return ry * rx * rz;
                case RotationOrder.YZX: return ry * rz * rx;
                case RotationOrder.ZXY: return rz * rx * ry;
                case RotationOrder.ZYX: return rz * ry * rx;
                default: throw new ArgumentException("Invalid rotation order", nameof(order));
            }
        }

        // Helper to extract Euler angles from a rotation matrix for a given order
        // This is the complex part with many formulas.
        // Formulas adapted from: http://www.gregslabaugh.com/publications/euler.pdf
        // And also common knowledge from various graphics programming resources.
        // Note: Matrix elements are m[row, col] (0-indexed)
        private static MyVector3 ExtractEulerAngles(Matrix3D m, RotationOrder order)
        {
            float r11 = m.value[0, 0], r12 = m.value[0, 1], r13 = m.value[0, 2];
            float r21 = m.value[1, 0], r22 = m.value[1, 1], r23 = m.value[1, 2];
            float r31 = m.value[2, 0], r32 = m.value[2, 1], r33 = m.value[2, 2];

            float xRad = 0, yRad = 0, zRad = 0;

            switch (order)
            {
                case RotationOrder.XYZ: // R = Rx Ry Rz
                                        // y = asin(r13)
                                        // x = atan2(-r23/cos(y), r33/cos(y))
                                        // z = atan2(-r12/cos(y), r11/cos(y))
                    yRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r13)));
                    if (Math.Abs(r13) < 1.0f - Epsilon) // Not in gimbal lock
                    {
                        xRad = (float)Math.Atan2(-r23, r33);
                        zRad = (float)Math.Atan2(-r12, r11);
                    }
                    else // Gimbal lock
                    {
                        zRad = 0; // Conventionally set z to 0
                        if (r13 > 0) // y = +PI/2
                            xRad = (float)Math.Atan2(r21, r22);
                        else // y = -PI/2
                            xRad = (float)Math.Atan2(-r21, -r22); // or Math.Atan2(r21, -r22) depending on convention
                    }
                    break;

                case RotationOrder.XZY: // R = Rx Rz Ry
                                        // z = asin(-r12)
                                        // x = atan2(r32/cos(z), r22/cos(z))
                                        // y = atan2(r13/cos(z), r11/cos(z))
                    zRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r12)));
                    if (Math.Abs(r12) < 1.0f - Epsilon)
                    {
                        xRad = (float)Math.Atan2(r32, r22);
                        yRad = (float)Math.Atan2(r13, r11);
                    }
                    else
                    {
                        yRad = 0;
                        if (-r12 > 0) // z = +PI/2
                            xRad = (float)Math.Atan2(-r31, r33); // Check r31, r33 based on matrix structure
                        else // z = -PI/2
                            xRad = (float)Math.Atan2(r31, r33);
                    }
                    break;

                case RotationOrder.YXZ: // R = Ry Rx Rz
                                        // x = asin(-r23)
                                        // y = atan2(r13/cos(x), r33/cos(x))
                                        // z = atan2(r21/cos(x), r22/cos(x))
                    xRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r23)));
                    if (Math.Abs(r23) < 1.0f - Epsilon)
                    {
                        yRad = (float)Math.Atan2(r13, r33);
                        zRad = (float)Math.Atan2(r21, r22);
                    }
                    else
                    {
                        zRad = 0;
                        if (-r23 > 0) // x = +PI/2
                            yRad = (float)Math.Atan2(-r12, r11); // or -r12, r11 or similar, depending on exact matrix
                        else // x = -PI/2
                            yRad = (float)Math.Atan2(r12, r11);
                    }
                    break;

                case RotationOrder.YZX: // R = Ry Rz Rx
                                        // z = asin(r21)
                                        // y = atan2(-r31/cos(z), r11/cos(z))
                                        // x = atan2(-r23/cos(z), r22/cos(z))
                    zRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r21)));
                    if (Math.Abs(r21) < 1.0f - Epsilon)
                    {
                        yRad = (float)Math.Atan2(-r31, r11);
                        xRad = (float)Math.Atan2(-r23, r22);
                    }
                    else
                    {
                        yRad = 0; // Set yaw to zero because of gimbal lock
                        if (r21 > 0) // z = +PI/2
                        {
                            // We maintain the sum of zRad and yRad (which is now zero)
                            xRad = (float)Math.Atan2(r32, r33);
                        }
                        else // z = -PI/2
                        {
                            // We maintain the sum of zRad and yRad (which is now zero)
                            xRad = (float)Math.Atan2(-r32, -r33);
                        }
                    }
                    break;

                case RotationOrder.ZXY: // R = Rz Rx Ry
                                        // x = asin(r32)
                                        // z = atan2(-r12/cos(x), r22/cos(x))
                                        // y = atan2(-r31/cos(x), r33/cos(x))
                    xRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, r32)));
                    if (Math.Abs(r32) < 1.0f - Epsilon)
                    {
                        zRad = (float)Math.Atan2(-r12, r22);
                        yRad = (float)Math.Atan2(-r31, r33);
                    }
                    else
                    {
                        yRad = 0;
                        if (r32 > 0) // x = +PI/2
                            zRad = (float)Math.Atan2(r13, r11);
                        else // x = -PI/2
                            zRad = (float)Math.Atan2(-r13, -r11);
                    }
                    break;

                case RotationOrder.ZYX: // R = Rz Ry Rx (Common "yaw, pitch, roll")
                                        // y = asin(-r31)
                                        // z = atan2(r21/cos(y), r11/cos(y))
                                        // x = atan2(r32/cos(y), r33/cos(y))
                    yRad = (float)Math.Asin(Math.Max(-1.0f, Math.Min(1.0f, -r31)));
                    if (Math.Abs(r31) < 1.0f - Epsilon)
                    {
                        zRad = (float)Math.Atan2(r21, r11);
                        xRad = (float)Math.Atan2(r32, r33);
                    }
                    else
                    {
                        xRad = 0;
                        if (-r31 > 0) // y = +PI/2 (pitch up)
                            zRad = (float)Math.Atan2(-r12, -r13); // or r12, r22 etc.
                        else // y = -PI/2 (pitch down)
                            zRad = (float)Math.Atan2(r12, r13); // or -r12, r22 etc. Gimbal lock formulas need careful derivation.
                                                                // For ZYX, if y = +/-90: z = atan2(m[0,1], m[0,2]) is common if x=0.
                                                                // Or (from Diebel "Representing Attitude"):
                                                                // if y = +PI/2, z = 0, x = atan2(r12, r22)
                                                                // if y = -PI/2, z = 0, x = -atan2(r12, r22)
                                                                // Let's use one of these common conventions.
                                                                // Setting x=0:
                                                                // if (-r31 > 0.99999) // y = 90
                                                                //     zRad = (float)Math.Atan2(r12, r22); (No, this is for r12 from matrix Ry(90)RxRz)
                                                                // else // y = -90
                                                                //     zRad = (float)Math.Atan2(-r12, r22);
                                                                // The Slabaugh paper (and many others) for ZYX (his order 12):
                                                                // if y = PI/2: x=0, z=atan2(r12,r22)
                                                                // if y = -PI/2: x=0, z=atan2(-r12,-r22)
                        if (-r31 > 0)  // y = +PI/2
                        {
                            xRad = 0; // Convention
                            zRad = (float)Math.Atan2(r12, r22); // This combination appears in ZYX from matrix multiplication for gimbal lock
                        }
                        else // y = -PI/2
                        {
                            xRad = 0; // Convention
                            zRad = (float)Math.Atan2(-r12, -r22);
                        }
                    }
                    break;

                default:
                    throw new ArgumentException("Invalid rotation order for extraction.", nameof(order));
            }
            
            return new MyVector3((float)CSMath.MathUtils.RadToDeg(xRad), 
                (float)CSMath.MathUtils.RadToDeg(yRad), 
                (float)CSMath.MathUtils.RadToDeg(zRad)
                );
        }

        #endregion
    }
}