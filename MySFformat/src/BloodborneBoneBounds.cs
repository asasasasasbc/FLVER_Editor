using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SoulsFormats;

namespace MySFformat
{
    /// <summary>BB (0x20014) node-local bind-pose bounds. No disk writes or geometry edits.</summary>
    public static class BloodborneBoneBounds
    {
        public sealed class Result
        {
            internal FLVER2 Source;
            internal Vector3[] Min, Max;
            public int ChangedNodes { get; internal set; }
            public int GeometryNodes { get; internal set; }
            public int RepairedUnusedNodes { get; internal set; }
            public int ReferencedVertices { get; internal set; }
            public int SkippedEmptyMeshes { get; internal set; }
            public void Apply(FLVER2 model)
            {
                if (!ReferenceEquals(model, Source) || model.Nodes.Count != Min.Length)
                    throw new InvalidOperationException("Bounds belong to another model. Calculate again.");
                for (int i = 0; i < Min.Length; i++)
                {
                    model.Nodes[i].BoundingBoxMin = Min[i];
                    model.Nodes[i].BoundingBoxMax = Max[i];
                }
            }
        }

        static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        static bool Finite(Vector3 v) { return Finite(v.X) && Finite(v.Y) && Finite(v.Z); }
        static bool Valid(Vector3 min, Vector3 max)
        {
            return Finite(min) && Finite(max) && min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z &&
                Math.Abs(min.X) < 1e20f && Math.Abs(min.Y) < 1e20f && Math.Abs(min.Z) < 1e20f &&
                Math.Abs(max.X) < 1e20f && Math.Abs(max.Y) < 1e20f && Math.Abs(max.Z) < 1e20f;
        }
        static bool Finite(Matrix4x4 m)
        {
            return Finite(m.M11) && Finite(m.M12) && Finite(m.M13) && Finite(m.M14) &&
                Finite(m.M21) && Finite(m.M22) && Finite(m.M23) && Finite(m.M24) &&
                Finite(m.M31) && Finite(m.M32) && Finite(m.M33) && Finite(m.M34) &&
                Finite(m.M41) && Finite(m.M42) && Finite(m.M43) && Finite(m.M44);
        }

        public static Result Calculate(FLVER2 model)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (model.Header.Version != 0x20014)
                throw new InvalidDataException("BB_BoneBoundingBOX_Calc requires a Bloodborne FLVER (version 0x20014). This version check is not automatic game detection.");
            int count = model.Nodes.Count;
            if (count == 0) throw new InvalidDataException("The model has no skeleton nodes.");
            var local = new Matrix4x4[count];
            var world = new Matrix4x4[count];
            var inverse = new Matrix4x4[count];
            var state = new byte[count];
            for (int i = 0; i < count; i++)
            {
                var n = model.Nodes[i];
                if (n.ParentIndex < -1 || n.ParentIndex >= count || !Finite(n.Translation) || !Finite(n.Rotation) || !Finite(n.Scale))
                    throw new InvalidDataException("Invalid node transform or parent at node " + i + ".");
                // Same row-vector XZY rotation as the BB HKX bind-pose path, independent of viewer pose.
                local[i] = Matrix4x4.CreateScale(n.Scale) * Matrix4x4.CreateRotationX(n.Rotation.X) *
                    Matrix4x4.CreateRotationZ(n.Rotation.Z) * Matrix4x4.CreateRotationY(n.Rotation.Y) *
                    Matrix4x4.CreateTranslation(n.Translation);
            }
            for (int start = 0; start < count; start++)
            {
                var chain = new List<int>();
                int cursor = start;
                while (cursor >= 0 && state[cursor] != 2)
                {
                    if (state[cursor] == 1) throw new InvalidDataException("Cyclic skeleton hierarchy.");
                    state[cursor] = 1; chain.Add(cursor); cursor = model.Nodes[cursor].ParentIndex;
                }
                for (int k = chain.Count - 1; k >= 0; k--)
                {
                    int i = chain[k], parent = model.Nodes[i].ParentIndex;
                    world[i] = parent < 0 ? local[i] : local[i] * world[parent];
                    if (!Finite(world[i]) || !Matrix4x4.Invert(world[i], out inverse[i]) || !Finite(inverse[i]))
                        throw new InvalidDataException("Non-invertible or non-finite bind transform at node " + i + ".");
                    state[i] = 2;
                }
            }
            var result = new Result { Source = model, Min = new Vector3[count], Max = new Vector3[count] };
            var touched = new bool[count];
            for (int i = 0; i < count; i++) { result.Min[i] = new Vector3(float.MaxValue); result.Max[i] = new Vector3(-float.MaxValue); }
            Action<int, Vector3, HashSet<int>> addChain = (bone, position, visited) =>
            {
                while (bone >= 0)
                {
                    if (bone >= count) throw new InvalidDataException("Referenced bone is outside the global node table.");
                    if (!visited.Add(bone)) break;
                    var p = Vector3.Transform(position, inverse[bone]);
                    if (!Finite(p)) throw new InvalidDataException("Non-finite bone-local vertex position.");
                    touched[bone] = true;
                    result.Min[bone] = Vector3.Min(result.Min[bone], p);
                    result.Max[bone] = Vector3.Max(result.Max[bone], p);
                    bone = model.Nodes[bone].ParentIndex;
                }
            };
            foreach (var mesh in model.Meshes)
            {
                var used = new HashSet<int>();
                foreach (var faces in mesh.FaceSets)
                    foreach (int index in faces.Triangulate(true))
                    {
                        if (index < 0 || index >= mesh.Vertices.Count) throw new InvalidDataException("Invalid triangle index.");
                        used.Add(index);
                    }
                if (used.Count == 0) { result.SkippedEmptyMeshes++; continue; }
                if (mesh.NodeIndex < -1 || mesh.NodeIndex >= count) throw new InvalidDataException("Invalid mesh owner node.");
                foreach (int index in used)
                {
                    var vertex = mesh.Vertices[index];
                    if (!Finite(vertex.Position)) throw new InvalidDataException("Non-finite vertex position.");
                    var visited = new HashSet<int>();
                    Vector3 modelPosition = vertex.Position;
                    if (mesh.UseBoneWeights)
                    {
                        float total = 0;
                        for (int slot = 0; slot < 4; slot++)
                        {
                            float weight = vertex.BoneWeights[slot];
                            if (!Finite(weight) || weight < 0 || weight > 1.001f) throw new InvalidDataException("Invalid vertex bone weight.");
                            total += weight;
                            if (weight > 0)
                            {
                                int bone = vertex.BoneIndices[slot];
                                if (bone < 0 || bone >= count) throw new InvalidDataException("Invalid global vertex bone index.");
                                addChain(bone, modelPosition, visited);
                            }
                        }
                        if (total < 0.9f || total > 1.1f) throw new InvalidDataException("Referenced vertex has missing or unnormalized weights. Fix skinning first.");
                    }
                    else
                    {
                        int bone = vertex.NormalW;
                        if (bone < 0 || bone >= count) throw new InvalidDataException("Invalid rigid NormalW bone index.");
                        modelPosition = Vector3.Transform(vertex.Position, world[bone]);
                        if (!Finite(modelPosition)) throw new InvalidDataException("Non-finite rigid vertex position.");
                        addChain(bone, modelPosition, visited);
                    }
                    // Mesh owner bounds and all ancestors must also cover the draw geometry.
                    addChain(mesh.NodeIndex, modelPosition, visited);
                    result.ReferencedVertices++;
                }
            }
            if (result.ReferencedVertices == 0) throw new InvalidDataException("No referenced triangle vertices to calculate bounds from.");
            for (int i = 0; i < count; i++)
            {
                var old = model.Nodes[i];
                bool valid = Valid(old.BoundingBoxMin, old.BoundingBoxMax);
                if (touched[i])
                {
                    result.GeometryNodes++;
                    // Small node-local padding avoids zero-thickness planar boxes. Existing
                    // valid authored margins are unioned, never shrunk; padding is not cumulative.
                    var min = result.Min[i] - new Vector3(0.001f);
                    var max = result.Max[i] + new Vector3(0.001f);
                    result.Min[i] = valid ? Vector3.Min(min, old.BoundingBoxMin) : min;
                    result.Max[i] = valid ? Vector3.Max(max, old.BoundingBoxMax) : max;
                }
                else if (valid) { result.Min[i] = old.BoundingBoxMin; result.Max[i] = old.BoundingBoxMax; }
                else { result.Min[i] = result.Max[i] = Vector3.Zero; result.RepairedUnusedNodes++; }
                if (!Valid(result.Min[i], result.Max[i])) throw new InvalidDataException("Calculated bounds are invalid at node " + i + ".");
                if (result.Min[i] != old.BoundingBoxMin || result.Max[i] != old.BoundingBoxMax) result.ChangedNodes++;
            }
            return result;
        }
    }
}
