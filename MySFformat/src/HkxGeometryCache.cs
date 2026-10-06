using System;
using System.Collections.Generic;
using System.Numerics;
using SoulsFormats;
using Microsoft.Xna.Framework.Graphics;

namespace MySFformat
{
    // Render-thread-only cache. Public/manual updateVertices invalidates it so edits
    // to weights, bind bones, topology, visibility and material settings remain live.
    sealed class HkxGeometryCache
    {
        public readonly FLVER2 Model;
        public readonly Transform3D[] Bones;
        public readonly List<Matrix3D> Bind = new List<Matrix3D>();
        public readonly List<Matrix3D> InverseBind = new List<Matrix3D>();
        public readonly List<VertexPositionColor> Lines = new List<VertexPositionColor>();
        public readonly List<VertexPositionColor> Triangles = new List<VertexPositionColor>();
        public readonly List<VertexPositionColorTexture> Textured = new List<VertexPositionColorTexture>();
        public static T[] CopyBuffer<T>(List<T> source, T[] buffer)
        {
            if (buffer == null || buffer.Length != source.Count) buffer = new T[source.Count];
            source.CopyTo(buffer); return buffer;
        }
        readonly Dictionary<FLVER2.Mesh, MeshData> meshes = new Dictionary<FLVER2.Mesh, MeshData>();
        public HkxGeometryCache(FLVER2 model, Transform3D[] bones, List<Matrix3D> bind, List<Matrix3D> inverse)
        { Model = model; Bones = bones; Bind = bind; InverseBind = inverse; }
        public MeshData GetMesh(FLVER2.Mesh mesh)
        {
            MeshData data;
            if (!meshes.TryGetValue(mesh, out data)) { data = new MeshData(mesh, InverseBind); meshes.Add(mesh, data); }
            return data;
        }
        internal sealed class MeshData
        {
            public readonly List<FLVER.Vertex[]> Faces;
            public readonly int[][] Indices;
            public readonly Vector3[] Positions;
            readonly Influence[][] influences;
            readonly Vector3[] rest;
            struct Influence { public int Bone; public float Weight; public Vector3 Position; }
            public MeshData(FLVER2.Mesh mesh, List<Matrix3D> inverse)
            {
                Faces = mesh.GetFaces(); Indices = new int[Faces.Count][];
                var indices = new Dictionary<FLVER.Vertex, int>(); var vertices = new List<FLVER.Vertex>();
                for (int f = 0; f < Faces.Count; f++)
                {
                    var ids = new int[3]; Indices[f] = ids;
                    for (int c = 0; c < 3; c++)
                    {
                        var v = Faces[f][c]; int index;
                        if (!indices.TryGetValue(v, out index)) { index = vertices.Count; indices.Add(v, index); vertices.Add(v); }
                        ids[c] = index;
                    }
                }
                Positions = new Vector3[vertices.Count]; rest = new Vector3[vertices.Count]; influences = new Influence[vertices.Count][];
                for (int i = 0; i < vertices.Count; i++)
                {
                    var v = vertices[i]; var items = new List<Influence>(4); float remaining = 1;
                    for (int k = 0; k < 4; k++)
                    {
                        int bone = v.BoneIndices[k]; float weight = v.BoneWeights[k];
                        if (bone < 0 || bone >= inverse.Count || weight == 0) continue;
                        if (weight < 0) weight += 1;
                        remaining -= weight;
                        items.Add(new Influence { Bone = bone, Weight = weight, Position = Transform(inverse[bone], v.Position) });
                    }
                    influences[i] = items.ToArray();
                    if (remaining > 0.0001f) rest[i] = remaining * v.Position;
                }
            }
            public void Skin(List<Matrix3D> pose)
            {
                for (int i = 0; i < Positions.Length; i++)
                {
                    var p = Vector3.Zero;
                    foreach (var item in influences[i]) p += item.Weight * Transform(pose[item.Bone], item.Position);
                    Positions[i] = p + rest[i];
                }
            }
            static Vector3 Transform(Matrix3D matrix, Vector3 p)
            {
                var m = matrix.value;
                return new Vector3(m[0,0]*p.X+m[0,1]*p.Y+m[0,2]*p.Z+m[0,3],
                    m[1,0]*p.X+m[1,1]*p.Y+m[1,2]*p.Z+m[1,3],
                    m[2,0]*p.X+m[2,1]*p.Y+m[2,2]*p.Z+m[2,3]);
            }
        }
    }
}
