using System;
using System.IO;
using SoulsFormats;
namespace MySFformat
{
    static partial class Program
    {
        internal static void LoadBinderTextures(BND4 binder)
        {
            targetTPF = null;
            if (!loadTexture) return;
            foreach (var file in binder.Files)
            {
                if (!file.Name.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase) && !file.Name.EndsWith(".tpf.dcx", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var tpf = TPF.Read(file.Bytes);
                    if (targetTPF == null) targetTPF = tpf;
                    else targetTPF.Textures.AddRange(tpf.Textures);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("TPF " + file.Name + ": " + ex);
                    Console.Error.WriteLine("TPF " + file.Name + ": " + ex.Message);
                }
            }
        }
    }
    public static class TpfTextureDecoder
    {
        public static byte[] GetDdsBytes(TPF.Texture texture)
        {
            if (texture == null || texture.Bytes == null) throw new ArgumentNullException("texture");
            var bytes = texture.Bytes;
            if (bytes.Length >= 4 && bytes[0] == 68 && bytes[1] == 68 && bytes[2] == 83 && bytes[3] == 32) return bytes;
            // SoulsFormats handles PS4 tiled block layout and creates the DDS header.
            // It returns a new buffer; the original binder/texture is never rewritten.
            var dds = texture.Headerize();
            if (dds.Length < 128 || dds[0] != 68 || dds[1] != 68 || dds[2] != 83 || dds[3] != 32)
                throw new InvalidDataException("TPF conversion did not produce DDS: " + texture.Name);
            return dds;
        }
    }
}
