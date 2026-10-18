// Self-authored, asset-free document binding tests. Pass the built Editor path.
using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

class ClothColliderBindingTests
{
    static XElement Field(string name, object value) { return new XElement("hkparam", new XAttribute("name", name), value); }
    static XElement Object(string id, string cls, params XElement[] fields)
    { return new XElement("hkobject", new XAttribute("name", id), new XAttribute("class", cls), fields); }
    static readonly string Identity = "(1 0 0 0)(0 1 0 0)(0 0 1 0)(0 0 0 1)";
    static XDocument Scene(int transformSet, string indices, string offsets)
    {
        return new XDocument(new XElement("hkpackfile", new XElement("hksection",
            Object("#1", "hkaSkeleton", Field("bones", Object("bone", "bone", Field("name", "Root"))),
                Field("parentIndices", "-1"), Field("referencePose", "(0 0 0 0)(0 0 0 1)(1 1 1 0)")),
            Object("#2", "hclClothData", Field("name", "Synthetic"), Field("simClothDatas", "#3"), Field("operators", "")),
            Object("#3", "hclSimClothData", Field("name", "Sim"), Field("simClothPoses", "#4"),
                Field("particleDatas", Object("particle", "particle", Field("invMass", "1"))),
                Field("triangleIndices", ""), Field("staticCollisionMasks", "0"), Field("staticConstraintSets", ""),
                Field("perInstanceCollidables", "#5"), Field("collidableTransformMap",
                    Object("map", "hclSimClothDataCollidableTransformMap", Field("transformSetIndex", transformSet),
                        Field("transformIndices", indices), Field("offsets", offsets)))),
            Object("#4", "hclSimClothPose", Field("positions", "(0 0 0 0)")),
            Object("#5", "hclCollidable", Field("shape", "#6"),
                Field("transform", "(1 0 0 0)(0 1 0 0)(0 0 1 0)(2 3 4 1)")),
            Object("#6", "hclSphereShape", Field("sphere", "(1 0 0 0.2)")))));
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static int Main(string[] args)
    {
        try
        {
            var asm = Assembly.LoadFrom(args[0]);
            var fromXml = asm.GetType("MySFformat.ClothDocument").GetMethod("FromXml");
            Func<XDocument, dynamic> load = xml => fromXml.Invoke(null, new object[] { xml, "synthetic", false });
            var input = Scene(-1, "", ""); string original = input.ToString(); dynamic unbound = load(input);
            dynamic collider = unbound.Domains[0].Colliders[0];
            Check((string)collider.Bone == "(unbound)", "unbound collider has no invented bone");
            Check(collider.Start.X == 3 && collider.Start.Y == 3 && collider.Start.Z == 4, "unbound collider uses its authored transform");
            Check(input.ToString() == original && unbound.Xml.ToString() == original, "unbound load does not rewrite author XML");
            dynamic bound = load(Scene(0, "0", Identity)); collider = bound.Domains[0].Colliders[0];
            Check((string)collider.Bone == "Root" && collider.Start.X == 1 && collider.Start.Y == 0, "bound collider still uses offset and skeleton");
            foreach (var invalid in new[] { Scene(-1, "0", ""), Scene(-1, "", Identity), Scene(-2, "", ""), Scene(0, "", ""), Scene(0, "0", "") })
            {
                bool rejected = false;
                try { load(invalid); } catch (TargetInvocationException e) { rejected = e.InnerException is System.IO.InvalidDataException; }
                Check(rejected, "malformed collider map rejected without partial binding");
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
