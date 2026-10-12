using System;
using System.IO;
using System.Xml.Linq;

namespace MySFformat
{
    static partial class Program
    {
        internal static int RunClothCli(string[] args)
        {
            try
            {
                if(args.Length==1&&args[0]=="--cloth-help")
                {
                    Console.WriteLine("Native BB / DS3 Havok64 Cloth HKX (no Java)\n--cloth-pack input.xml --out new.hkx [--layout 8,1,0,1]\n--cloth-unpack input.hkx_or_partsbnd.dcx --out new.xml\nNative XML preserves header layout; legacy XML defaults to 8,1,1,1. Existing/source paths are refused. No game/Binder overwrite.");return 0;
                }
                if((args.Length!=4&&args.Length!=6)||(args[0]!="--cloth-pack"&&args[0]!="--cloth-unpack")||args[2]!="--out"||(args.Length==6&&(args[0]!="--cloth-pack"||args[4]!="--layout")))throw new ArgumentException("Usage: --cloth-pack input.xml --out new.hkx [--layout 8,1,0,1] OR --cloth-unpack input.hkx --out new.xml");
                string input=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[3]);
                if(string.Equals(input,output,StringComparison.OrdinalIgnoreCase)||File.Exists(output))throw new IOException("Choose a new output path. Source/existing files are not overwritten.");
                if(args[0]=="--cloth-pack")
                {
                    if(!Path.GetExtension(input).Equals(".xml",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Cloth pack input must be author XML.");
                    var xml=HkxPackfileCodec.LoadXml(input);if(args.Length==6)xml.Root.SetAttributeValue("nativeLayout",args[5]);ClothDocument.FromXml(xml,input);HkxPackfileCodec.SaveNew(xml,output);
                }
                else
                {
                    if(!Path.GetExtension(output).Equals(".xml",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Cloth unpack output must be XML.");
                    var document=ClothDocument.Load(input);
                    using(var stream=new MemoryStream()){document.Xml.Save(stream);HkxPackfileCodec.WriteNewBytes(stream.ToArray(),output);}
                }
                Console.WriteLine("PASS native C# Cloth "+args[0]+": "+output);return 0;
            }
            catch(Exception ex){Console.Error.WriteLine("Native Cloth conversion failed: "+ex.Message);return 1;}
        }
    }
}
