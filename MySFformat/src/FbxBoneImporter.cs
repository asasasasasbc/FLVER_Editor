// FbxBoneImporter.cs (Updated Version)
using Assimp;
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

            // 4. Decompose the final matrix to get FLVER-compatible TRS values.
            flverMatrix.Decompose(out var scale, out var rotationQuat, out var translation);
            if (scale != null)
            {
                flverNode.Translation = new Vector3(translation.X, translation.Y, translation.Z);
                flverNode.Scale = new Vector3(scale.X, scale.Y, scale.Z);
                //MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard
                // MeshIO.FBX.Helpers.RotationOrder.ZYX;   // Common FBX target
                // 5. Convert the quaternion to FLVER's YZX Euler angles (in radians).
                flverNode.Rotation = QuaternionToEulerYZX(rotationQuat);
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

        private static System.Numerics.Matrix4x4 ToNumerics(Assimp.Matrix4x4 m)
        {
            return new System.Numerics.Matrix4x4(m.A1, m.B1, m.C1, m.D1, m.A2, m.B2, m.C2, m.D2, m.A3, m.B3, m.C3, m.D3, m.A4, m.B4, m.C4, m.D4);
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
            MeshIO.FBX.Helpers.RotationOrder flverOrder = MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard
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

        private static Vector3 QuaternionToEulerYZX(Quaternion q)
        {
            System.Numerics.Quaternion nq = new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);
            var M = System.Numerics.Matrix4x4.CreateFromQuaternion(nq);
            float x, y, z;
            //XYZ seems a liitle bit correct...
            //XZY: wrong
            // YXZ: wrong
            //YZX: wrong
            // ZXY: wrong
            // ZYX: Correct--- I have no idea why this is correct... but it just works
            var order = RotationOrder.ZYX; // or XZY?
            switch (order)
            {
                case RotationOrder.XYZ:
                    // Gimbal lock check
                    if (M.M31 > 0.99999f)
                    {
                        y = (float)Math.Asin(M.M31);
                        x = (float)Math.Atan2(-M.M23, M.M22);
                        z = 0.0f;
                    }
                    else if (M.M31 < -0.99999f)
                    {
                        y = (float)Math.Asin(M.M31);
                        x = (float)Math.Atan2(M.M23, M.M22);
                        z = 0.0f;
                    }
                    else
                    {
                        y = (float)Math.Asin(M.M31);
                        x = (float)Math.Atan2(-M.M32, M.M33);
                        z = (float)Math.Atan2(-M.M21, M.M11);
                    }
                    break;

                case RotationOrder.XZY:
                    if (M.M21 > 0.99999f)
                    {
                        z = (float)Math.Asin(M.M21);
                        x = (float)Math.Atan2(M.M32, M.M33);
                        y = 0;
                    }
                    else if (M.M21 < -0.99999f)
                    {
                        z = (float)Math.Asin(M.M21);
                        x = (float)Math.Atan2(-M.M32, -M.M33);
                        y = 0;
                    }
                    else
                    {
                        z = (float)Math.Asin(M.M21);
                        x = (float)Math.Atan2(-M.M23, M.M22);
                        y = (float)Math.Atan2(-M.M31, M.M11);
                    }
                    break;

                case RotationOrder.YXZ:
                    if (M.M32 > 0.99999f)
                    {
                        x = (float)Math.Asin(M.M32);
                        y = (float)Math.Atan2(M.M13, M.M11);
                        z = 0;
                    }
                    else if (M.M32 < -0.99999f)
                    {
                        x = (float)Math.Asin(M.M32);
                        y = (float)Math.Atan2(-M.M13, -M.M11);
                        z = 0;
                    }
                    else
                    {
                        x = (float)Math.Asin(M.M32);
                        y = (float)Math.Atan2(-M.M31, M.M33);
                        z = (float)Math.Atan2(-M.M12, M.M22);
                    }
                    break;

                case RotationOrder.YZX: // This is the one used for FLVER
                    if (M.M12 > 0.99999f)
                    {
                        z = (float)Math.Asin(M.M12);
                        y = (float)Math.Atan2(M.M23, M.M33);
                        x = 0;
                    }
                    else if (M.M12 < -0.99999f)
                    {
                        z = (float)Math.Asin(M.M12);
                        y = (float)Math.Atan2(-M.M23, -M.M33);
                        x = 0;
                    }
                    else
                    {
                        z = (float)Math.Asin(M.M12);
                        y = (float)Math.Atan2(-M.M13, M.M11);
                        x = (float)Math.Atan2(-M.M32, M.M22);
                    }
                    break;

                case RotationOrder.ZXY:
                    if (M.M23 > 0.99999f)
                    {
                        x = (float)Math.Asin(M.M23);
                        z = (float)Math.Atan2(M.M12, M.M11);
                        y = 0;
                    }
                    else if (M.M23 < -0.99999f)
                    {
                        x = (float)Math.Asin(M.M23);
                        z = (float)Math.Atan2(-M.M12, -M.M11);
                        y = 0;
                    }
                    else
                    {
                        x = (float)Math.Asin(M.M23);
                        z = (float)Math.Atan2(-M.M21, M.M22);
                        y = (float)Math.Atan2(-M.M13, M.M33);
                    }
                    break;

                case RotationOrder.ZYX: // Common "Euler" or "Tait-Bryan"
                    if (M.M13 > 0.99999f)
                    {
                        y = -(float)Math.Asin(M.M13);
                        z = (float)Math.Atan2(-M.M21, M.M22);
                        x = 0;
                    }
                    else if (M.M13 < -0.99999f)
                    {
                        y = -(float)Math.Asin(M.M13);
                        z = (float)Math.Atan2(M.M21, M.M22);
                        x = 0;
                    }
                    else
                    {
                        y = -(float)Math.Asin(M.M13);
                        z = (float)Math.Atan2(M.M12, M.M11);
                        x = (float)Math.Atan2(M.M23, M.M33);
                    }
                    break;

                default:
                    throw new ArgumentException("Invalid RotationOrder specified.");
            }

            // The results are the angles around the X, Y, and Z axes.
            //
            //float xDeg = (float)CSMath.MathUtils.RadToDeg(x);
            //float yDeg = (float)CSMath.MathUtils.RadToDeg(y);
            //float zDeg = (float)CSMath.MathUtils.RadToDeg(z);
            //
            //// Input angles are in YZX order (as per FLVER convention)
            //MeshIO.FBX.Helpers.MyVector3 inputAnglesDeg = new MeshIO.FBX.Helpers.MyVector3(xDeg, yDeg, zDeg);
            //MeshIO.FBX.Helpers.RotationOrder flverOrder = MeshIO.FBX.Helpers.RotationOrder.YZX; // FLVER standard
            //MeshIO.FBX.Helpers.RotationOrder fbxOrder = MeshIO.FBX.Helpers.RotationOrder.ZYX;   // Common FBX target
            //
            //var convertedAngles = MeshIO.FBX.Helpers.EulerAngleConverter.ConvertRotationOrder(inputAnglesDeg, fbxOrder, flverOrder);
            //x = (float)CSMath.MathUtils.DegToRad(convertedAngles.X);
            //y = (float)CSMath.MathUtils.DegToRad(convertedAngles.Y);
            //z = (float)CSMath.MathUtils.DegToRad(convertedAngles.Z);

            return new Vector3(x, y, z);
        }

        #endregion
    }
}