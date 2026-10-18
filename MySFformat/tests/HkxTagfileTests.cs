// Asset-free TAG0 tests. Compile with System.Xml.Linq and pass the Editor EXE.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

class HkxTagfileTests
{
    static byte[] Bytes(Action<BinaryWriter> write) { using (var s = new MemoryStream()) { using (var w = new BinaryWriter(s)) { write(w); return s.ToArray(); } } }
    static byte[] Join(params byte[][] parts) { return parts.SelectMany(b => b).ToArray(); }
    static byte[] Section(string name, byte[] data)
    {
        int n = data.Length + 8; return Join(new[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n }, Encoding.ASCII.GetBytes(name), data);
    }
    static void Var(BinaryWriter w, int value)
    {
        if (value < 128) w.Write((byte)value);
        else if (value < 16384) { w.Write((byte)(128 | (value >> 8))); w.Write((byte)value); }
        else { w.Write((byte)(192 | (value >> 16))); w.Write((byte)(value >> 8)); w.Write((byte)value); }
    }
    static byte[] Fixture(bool missingPatch = false, bool invalidPointer = false, bool cycle = false, string sdk = "20180100")
    {
        var names = new[] { "hkRootLevelContainer", "unsigned int", "hkStringPtr", "char", "hkVector4f", "float", "hkArray" };
        var tna = Bytes(w => { Var(w, 8); for (int i = 0; i < 7; i++) { Var(w, i); Var(w, 0); } });
        var bodies = Bytes(w => {
            for (int i = 1; i <= 7; i++)
            {
                int[] formats = { 0, 7, 32772, 131, 8196, 1064, 5, 8 };
                int[] sizes = { 0, 48, 4, 8, 1, 16, 4, 16 };
                int[] aligns = { 0, 16, 4, 8, 1, 16, 4, 8 };
                int sub = i == 5 ? 6 : i == 7 ? 2 : 0;
                Var(w, i); Var(w, cycle && i == 1 ? 1 : 0); Var(w, 9 | (sub != 0 ? 2 : 0) | (i == 1 ? 32 : 0));
                Var(w, formats[i]); if (sub != 0) Var(w, sub); Var(w, sizes[i]); Var(w, aligns[i]);
                if (i == 1)
                {
                    Var(w, 4); int[] offsets = { 0, 8, 16, 32 }; int[] types = { 2, 3, 5, 7 };
                    for (int f = 0; f < 4; f++) { Var(w, f); Var(w, 32); Var(w, offsets[f]); Var(w, types[f]); }
                }
            }
        });
        var data = new byte[68]; BitConverter.GetBytes(42u).CopyTo(data, 0); BitConverter.GetBytes(invalidPointer ? 999UL : 2UL).CopyTo(data, 8);
        BitConverter.GetBytes(-1f).CopyTo(data, 20); BitConverter.GetBytes(3UL).CopyTo(data, 32); Encoding.ASCII.GetBytes("cloth\0").CopyTo(data, 48);
        for (int i = 0; i < 3; i++) BitConverter.GetBytes((uint)(i + 1)).CopyTo(data, 56 + i * 4);
        var items = Bytes(w => { w.Write(new byte[12]); w.Write(0x10000001u); w.Write(0); w.Write(1); w.Write(0x20000004u); w.Write(48); w.Write(6); w.Write(0x20000002u); w.Write(56); w.Write(3); });
        var patches = Bytes(w => { w.Write(3); w.Write(1); w.Write(8); if (!missingPatch) { w.Write(7); w.Write(1); w.Write(32); } });
        var type = Section("TYPE", Join(Section("TSTR", Encoding.ASCII.GetBytes(string.Join("\0", names) + "\0")), Section("TNA1", tna),
            Section("FSTR", Encoding.ASCII.GetBytes("answer\0title\0direction\0values\0")), Section("TBDY", bodies)));
        return Section("TAG0", Join(Section("SDKV", Encoding.ASCII.GetBytes(sdk)), Section("DATA", data), type,
            Section("INDX", Join(Section("ITEM", items), Section("PTCH", patches)))));
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static int Main(string[] args)
    {
        try
        {
            var asm = Assembly.LoadFrom(args[0]); var reader = asm.GetType("MySFformat.HkxTagfileReader");
            Func<byte[], XDocument> read = bytes => (XDocument)reader.GetMethod("Read").Invoke(Activator.CreateInstance(reader, new object[] { bytes }), null);
            byte[] source = Fixture(), copy = (byte[])source.Clone(); var xml = read(source); var obj = xml.Descendants("hkobject").Single();
            Func<string, XElement> field = name => obj.Elements("hkparam").Single(p => (string)p.Attribute("name") == name);
            Check((string)xml.Root.Attribute("sourceFormat") == "TAG0-20180100", "TAG0 format retained rather than relabeled as 2014");
            Check(field("answer").Value == "42" && field("title").Value == "cloth", "typed integer and indexed UTF8 string decoded");
            Check(field("direction").Value == "(0 -1 0 0)", "fixed vector lanes decoded");
            Check((int)field("values").Attribute("numelements") == 3 && field("values").Value == "1 2 3", "indexed dynamic array decoded");
            Check(source.SequenceEqual(copy), "reader does not mutate input bytes");
            foreach (var bad in new[] { source.Take(source.Length - 1).ToArray(), Fixture(true), Fixture(false, true), Fixture(false, false, true) })
            {
                bool refused = false; try { read(bad); } catch (TargetInvocationException e) { refused = e.InnerException is InvalidDataException; }
                Check(refused, "malformed lengths, pointers, missing patches or inheritance cycles rejected");
            }
            bool exportRefused = false;
            try { asm.GetType("MySFformat.HkxPackfileCodec").GetMethod("Pack").Invoke(null, new object[] { xml }); }
            catch (TargetInvocationException e) { exportRefused = e.InnerException is NotSupportedException; }
            Check(exportRefused, "2014 Pack cannot export a TAG0 source document");
            var sekiro = read(Fixture(sdk: "20160200"));
            Check((string)sekiro.Root.Attribute("sourceFormat") == "TAG0-20160200" && (string)sekiro.Root.Attribute("contentsversion") == "hk_2016.2.0", "Sekiro retains distinct 2016 SDK identity");
            var backend = reader.GetMethod("CompatibilityBackend");
            Check((string)backend.Invoke(null, new object[] { sekiro }) == "Native HCT (Sekiro compatibility)" && (string)backend.Invoke(null, new object[] { xml }) == "Native HCT (ER compatibility)", "SDK profiles select different compatibility backends");
            foreach (var document in new[] { sekiro, new XDocument(new XElement("hkpackfile", new XAttribute("sourceFormat", "TAG0-20990100"))) })
            {
                bool refused = false;
                try { asm.GetType("MySFformat.HkxPackfileCodec").GetMethod("Pack").Invoke(null, new object[] { document }); }
                catch (TargetInvocationException e) { refused = e.InnerException is NotSupportedException; }
                Check(refused, "Sekiro and unknown TAG0 sources cannot enter the 2014 writer");
            }
            bool unknownRefused = false;
            try { read(Fixture(sdk: "20170100")); }
            catch (TargetInvocationException e) { unknownRefused = e.InnerException is NotSupportedException; }
            Check(unknownRefused, "unknown SDK is still refused");
            bool bad2016Refused = false;
            try { read(Fixture(missingPatch: true, sdk: "20160200")); }
            catch (TargetInvocationException e) { bad2016Refused = e.InnerException is InvalidDataException; }
            Check(bad2016Refused, "2016 pointer patch validation is not bypassed");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
