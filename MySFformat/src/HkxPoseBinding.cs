using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace MySFformat
{
    public sealed class HkxPoseBinding
    {
        readonly HkxAnimationClip clip;
        readonly int[] map, parents;
        readonly Matrix4x4[] local;
        public int MatchedCount { get; private set; }
        public string[] UnmatchedNames { get; private set; }
        public HkxPoseBinding(HkxAnimationClip animation, string[] names, int[] parentIndices, Matrix4x4[] bindLocal)
        {
            if (names.Length != parentIndices.Length || names.Length != bindLocal.Length) throw new ArgumentException("Skeleton array lengths differ.");
            clip = animation; parents = (int[])parentIndices.Clone(); local = (Matrix4x4[])bindLocal.Clone(); map = new int[names.Length];
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < clip.NodeCount; i++) if (!String.IsNullOrEmpty(clip.Names[i]))
            { if (byName.ContainsKey(clip.Names[i])) throw new InvalidDataException("Duplicate HKX bone: " + clip.Names[i]); byName.Add(clip.Names[i], i); }
            var missing = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                int index;
                if (byName.TryGetValue(names[i], out index)) { map[i] = index; MatchedCount++; }
                else { map[i] = -1; missing.Add(names[i]); }
                if (parents[i] < -1 || parents[i] >= names.Length) throw new InvalidDataException("Invalid FLVER parent index.");
            }
            if (MatchedCount == 0) throw new InvalidDataException("No matching bone names. Choose a skeleton HKX for this FLVER character.");
            UnmatchedNames = missing.ToArray(); Sample(0);
        }
        public Matrix4x4[] Sample(float seconds)
        {
            var hk = clip.SampleWorld(seconds); var result = new Matrix4x4[map.Length]; var state = new byte[map.Length];
            for (int i = 0; i < map.Length; i++) Resolve(i, hk, result, state);
            return result;
        }
        void Resolve(int i, Matrix4x4[] hk, Matrix4x4[] result, byte[] state)
        {
            if (state[i] == 2) return; if (state[i] == 1) throw new InvalidDataException("Cyclic FLVER skeleton."); state[i] = 1;
            if (parents[i] >= 0) Resolve(parents[i], hk, result, state);
            result[i] = map[i] >= 0 ? hk[map[i]] : parents[i] >= 0 ? local[i] * result[parents[i]] : local[i];
            state[i] = 2;
        }
        public static Matrix3D ToEditorMatrix(Matrix4x4 m)
        {
            // System.Numerics is row-vector; the editor is column-vector.
            return new Matrix3D { value = new[,] { { m.M11,m.M21,m.M31,m.M41 }, { m.M12,m.M22,m.M32,m.M42 }, { m.M13,m.M23,m.M33,m.M43 }, { m.M14,m.M24,m.M34,m.M44 } } };
        }
    }
}
