using System;
using System.Threading.Tasks;

namespace MySFformat.Cloth.Preview
{
    public interface IClothPreviewSession : IDisposable
    {
        string DisplayName { get; }
        bool SupportsAnimation { get; }
        string Notice { get; }
        int ProcessId { get; }
        string[] DomainNames { get; }
        string[] SkeletonNames { get; }
        string[][] SkeletonBones { get; }
        Task<ClothPreviewFrame> StepAsync(int steps,float[][][] poses=null);
    }
}
