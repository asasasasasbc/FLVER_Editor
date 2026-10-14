// GPL-3.0. Local preview protocol; vendor binaries are not distributed.
namespace MySFformat.Cloth.Preview
{
    public sealed class PreviewSkeleton
    {
        public string Name;
        public string[] Bones;
        public float[] Matrices;
    }
    public sealed class PreviewMesh
    {
        public int BufferIndex, MeshIndex, ReadBufferIndex, VertexCount, SceneSection;
        public int[] AliasSources, AliasTargets;
        public int[] Conversions, Slots, Starts, Strides;
        public float[][] Channels;
    }
    public sealed class PreviewCloth
    {
        public string Name;
        public int PreviousState, CurrentState;
        public int[] TransformSkeletons;
        public PreviewMesh[] Meshes;
        public int[] ParticleCounts;
        public string[] SimulationNames;
        public PreviewInitialization[] Initializations;
        public float[][] ReferenceParticles;
    }
    public sealed class PreviewInitialization
    {
        public int Operator, Simulation, Input, Output, Count;
        public bool UseReferencePose;
    }
    public sealed class PreviewDescription
    {
        public PreviewSkeleton[] Skeletons;
        public PreviewCloth[] Cloths;
        public bool Clm2Loaded;
        public int Clm2Meshes, Clm2Entries;
        public bool RuntimeRepacked;
        public string OriginalLayout, RuntimeLayout;
        public PreviewEnvironment Environment;
    }
}
