using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using OpenPhysicsHKX;

namespace MySFformat.Cloth.Preview
{
    // Evaluates authored rest-skeleton Skin buffers. Never substitutes sim rest
    // positions for missing reference vertices. Animated/dependent deformers remain refused.
    internal sealed class ManagedReferenceBinding
    {
        internal double[][] Positions,Normals;
        internal Dictionary<int,ReferenceBufferData> Buffers;
        internal readonly Dictionary<int,double[]> FixedTargets=new Dictionary<int,double[]>();
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static XElement F(XElement o,string key){return o.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")==key);}
        static string V(XElement o,string key){var f=F(o,key);if(f==null)throw new InvalidDataException("Missing managed binding field: "+key);return f.Value.Trim();}
        static int I(XElement o,string key){return int.Parse(V(o,key),C);}
        static double[] N(XElement o,string key){return ClothDocument.Numbers(V(o,key));}
        static string[] Refs(XElement o,string key){return V(o,key).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);}
        static Matrix4x4 Matrix(double[] a,int p){return new Matrix4x4((float)a[p],(float)a[p+1],(float)a[p+2],(float)a[p+3],(float)a[p+4],(float)a[p+5],(float)a[p+6],(float)a[p+7],(float)a[p+8],(float)a[p+9],(float)a[p+10],(float)a[p+11],(float)a[p+12],(float)a[p+13],(float)a[p+14],(float)a[p+15]);}
        static double[] Array(Vector3 v){return new[]{(double)v.X,v.Y,v.Z};}
        static Vector3 Vector(double[] a,int p){return new Vector3((float)a[p],(float)a[p+1],(float)a[p+2]);}
        internal static Vector3 DecodePacked(double[] words)
        {
            if(words.Length!=4||words.Any(w=>w!=Math.Truncate(w)||w<short.MinValue||w>ushort.MaxValue))throw new InvalidDataException("Invalid packed reference vector.");
            float scale=BitConverter.ToSingle(BitConverter.GetBytes((uint)(ushort)(int)words[3]<<16),0)*65536f;
            var v=new Vector3(unchecked((short)(int)words[0])*scale,unchecked((short)(int)words[1])*scale,unchecked((short)(int)words[2])*scale);
            if(float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z))throw new InvalidDataException("Nonfinite packed reference vector.");return v;
        }
        static Vector3 Local(XElement row,string prefix,int lane,bool unpacked)
        {
            var direct=F(row,prefix+(lane+1).ToString(C));
            if(unpacked)return Vector(ClothDocument.Numbers(direct.Value),0);
            if(direct!=null&&direct.HasElements){var nested=direct.Elements().Single();return DecodePacked(Enumerable.Range(1,4).Select(i=>double.Parse(V(nested,"values"+i),C)).ToArray());}
            return DecodePacked(Enumerable.Range(1,4).Select(i=>double.Parse(V(row,prefix+(lane*4+i)),C)).ToArray());
        }
        static Matrix4x4[] Skeleton(ClothDocument document,XElement owner,int set)
        {
            var sets=Refs(owner,"transformSetDefinitions");if(set<0||set>=sets.Length)throw new InvalidDataException("Skin transform set outside definitions.");
            var def=document.Objects[sets[set]];int count=I(def,"numTransforms");
            var candidates=document.Objects.Values.Where(o=>(string)o.Attribute("class")=="hkaSkeleton"&&ClothDocument.Field(o,"bones").Elements().Count()==count).ToArray();
            if(candidates.Length>1)candidates=candidates.Where(o=>V(o,"name")==V(def,"name")).ToArray();
            if(candidates.Length!=1)throw new NotSupportedException("No unique rest skeleton for managed Skin transform set.");
            var skel=candidates[0];var pose=N(skel,"referencePose");var parents=N(skel,"parentIndices");if(pose.Length!=count*12||parents.Length!=count)throw new InvalidDataException("Invalid Skin skeleton dimensions.");
            var world=new Matrix4x4[count];var marks=new byte[count];Func<int,Matrix4x4> resolve=null;
            resolve=i=>{if(i<0||i>=count||marks[i]==1)throw new InvalidDataException("Invalid/cyclic Skin skeleton.");if(marks[i]==2)return world[i];marks[i]=1;int p=i*12;var q=new Quaternion((float)pose[p+4],(float)pose[p+5],(float)pose[p+6],(float)pose[p+7]);var local=Matrix4x4.CreateScale(Vector(pose,p+8))*Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(Vector(pose,p));int parent=(int)parents[i];if(parent>=32768)parent-=65536;world[i]=parent<0?local:local*resolve(parent);marks[i]=2;return world[i];};
            for(int i=0;i<count;i++)resolve(i);return world;
        }
        // Static-only validation helper. Runtime playback uses ManagedClothGraph.
        internal static ManagedReferenceBinding Build(ClothDocument document,ClothDomain domain,int simIndex,XElement[] before)
        {
            var constraints=domain.Constraints.Where(c=>F(c,"referenceMeshBufferIdx")!=null).ToArray();
            var moves=before.Where(o=>(string)o.Attribute("class")=="hclMoveParticlesOperator"&&I(o,"simClothIndex")==simIndex).ToArray();
            var indices=constraints.Select(c=>I(c,"referenceMeshBufferIdx")).Concat(moves.Select(o=>I(o,"refBufferIdx"))).Distinct().ToArray();
            if(indices.Length==0)return null;
            if(indices.Length!=1)throw new NotSupportedException("Managed constraints require multiple distinct reference buffers; not yet implemented.");
            int buffer=indices[0];var buffers=Refs(domain.Owner,"bufferDefinitions");if(buffer<0||buffer>=buffers.Length)throw new InvalidDataException("Reference buffer outside owner definitions.");
            int count=I(document.Objects[buffers[buffer]],"numVertices");if(count<1||count>2000000)throw new InvalidDataException("Unsafe managed reference count.");
            var writer=before.LastOrDefault(o=>{var p=F(o,"outputBufferIndex")??F(o,"outputBufferIdx");return p!=null&&int.Parse(p.Value,C)==buffer;});
            if(writer==null)throw new NotSupportedException("Managed reference buffer has no authored producer.");
            return ValidateSingle(EvaluateSkin(document,domain.Owner,writer,count,before),domain,constraints,moves,count);
        }
        internal static ManagedReferenceBinding EvaluateSkin(ClothDocument document,XElement owner,XElement writer,int count,XElement[] before)
        {
            string cls=(string)writer.Attribute("class");bool boneSpace=cls=="hclBoneSpaceSkinPNOperator"||cls=="hclBoneSpaceSkinPNTOperator";
            if(cls!="hclObjectSpaceSkinPNOperator"&&cls!="hclObjectSpaceSkinPNTOperator"&&!boneSpace)throw new NotSupportedException("OpenPhysicsHKX reference dependency is not implemented: "+cls+". No rest-binding substitution.");
            int transformSet=I(writer,"transformSetIndex");
            if(before.TakeWhile(o=>!ReferenceEquals(o,writer)).Any(o=>{var p=F(o,"outputTransformSetIdx")??F(o,"outputTransformSetIndex");return p!=null&&int.Parse(p.Value,C)==transformSet;}))throw new NotSupportedException("Skin depends on a dynamically produced transform set; rest-skeleton substitution is not supported.");
            var subset=N(writer,"transformSubset");var world=Skeleton(document,owner,transformSet);
            var transforms=new Matrix4x4[subset.Length];var normalTransforms=new Matrix4x4[subset.Length];
            var bind=boneSpace?new double[0]:N(writer,"boneFromSkinMeshTransforms");if(!boneSpace&&bind.Length!=subset.Length*16)throw new InvalidDataException("Skin bind-transform count mismatch.");
            for(int i=0;i<subset.Length;i++)
            {
                if(subset[i]!=Math.Truncate(subset[i])||subset[i]<0||subset[i]>=world.Length)throw new InvalidDataException("Skin bone subset index invalid.");
                transforms[i]=boneSpace?world[(int)subset[i]]:Matrix(bind,i*16)*world[(int)subset[i]];
                Matrix4x4 inverse;if(!Matrix4x4.Invert(transforms[i],out inverse))throw new InvalidDataException("Singular Skin normal transform.");normalTransforms[i]=Matrix4x4.Transpose(inverse);
            }
            return EvaluateDeformer(writer,count,transforms,normalTransforms);
        }
        internal static ManagedReferenceBinding EvaluateDeformer(XElement writer,int count,Matrix4x4[] transforms,Matrix4x4[] normalTransforms=null)
        {
            string cls=(string)writer.Attribute("class");bool boneSpace=cls.Contains("BoneSpace"),hasNormals=!cls.EndsWith("POperator",StringComparison.Ordinal);
            var deformer=ClothDocument.Field(writer,boneSpace?"boneSpaceDeformer":"objectSpaceDeformer").Elements().Single();
            string channels=cls.EndsWith("PNTOperator",StringComparison.Ordinal)?"PNTs":hasNormals?"PNs":"Ps";
            var packed=ClothDocument.Field(writer,"local"+channels).Elements().ToArray();var unpacked=ClothDocument.Field(writer,"localUnpacked"+channels).Elements().ToArray();
            if(unpacked.Length!=0&&packed.Length!=0)throw new NotSupportedException("Mixed packed/unpacked managed Skin blocks are not yet reviewed.");
            bool uncompressed=unpacked.Length!=0;var localRows=uncompressed?unpacked:packed;
            var controls=N(deformer,"controlBytes");if(controls.Length!=localRows.Length)throw new InvalidDataException("Skin control/local-block count mismatch.");
            if(normalTransforms==null)normalTransforms=new Matrix4x4[transforms.Length];
            string[] blendNames={"fourBlendEntries","threeBlendEntries","twoBlendEntries","oneBlendEntries"};
            var blocks=blendNames.Select(k=>ClothDocument.Field(deformer,k).Elements().ToArray()).ToArray();var cursor=new int[4];
            var result=new ManagedReferenceBinding{Positions=new double[count][],Normals=new double[count][]};
            for(int block=0;block<controls.Length;block++)
            {
                int code=(int)controls[block];if(code!=controls[block]||code<0||code>3)throw new NotSupportedException("Unsupported managed Skin control byte.");
                int influences=4-code;
                if(cursor[code]>=blocks[code].Length)throw new InvalidDataException("Skin control references missing blend block.");var entries=blocks[code][cursor[code]++];
                for(int lane=0;lane<(boneSpace?16/influences:16);lane++)
                {
                    int index=I(entries,"vertexIndices"+(lane+1));if(index<0||index>=count)throw new InvalidDataException("Skin vertex index outside reference buffer.");
                    var local=boneSpace?Vector3.Zero:Local(localRows[block],"localPosition",lane,uncompressed);var normal=boneSpace||!hasNormals?Vector3.Zero:Local(localRows[block],"localNormal",lane,uncompressed);var p=Vector3.Zero;var n=Vector3.Zero;double weightSum=0;
                    for(int j=0;j<influences;j++)
                    {
                        int column=lane*influences+j+1,bone=I(entries,"boneIndices"+column);if(bone<0||bone>=transforms.Length)throw new InvalidDataException("Skin blend bone outside subset.");
                        if(hasNormals&&normalTransforms[bone].M44==0){Matrix4x4 inverse;if(!Matrix4x4.Invert(transforms[bone],out inverse))throw new NotSupportedException("Degenerate deformer frame cannot transform required normals.");normalTransforms[bone]=Matrix4x4.Transpose(inverse);}
                        if(boneSpace)
                        {
                            // Bone-space vectors are already weight-scaled and carry
                            // their translation weight in W (including zero-weight lanes).
                            var values=N(localRows[block],"localPosition"+column);if(values.Length!=4||values[3]<0||values[3]>1)throw new InvalidDataException("Invalid weighted bone-space position.");
                            var transformed=Vector4.Transform(new Vector4((float)values[0],(float)values[1],(float)values[2],(float)values[3]),transforms[bone]);
                            p+=new Vector3(transformed.X,transformed.Y,transformed.Z);if(hasNormals)n+=Vector3.TransformNormal(Local(localRows[block],"localNormal",column-1,uncompressed),normalTransforms[bone]);weightSum+=values[3];
                        }
                        else
                        {
                            double weight=influences==1?1:I(entries,"boneWeights"+column)/255.0;if(weight<0||weight>1)throw new InvalidDataException("Invalid Skin weight.");weightSum+=weight;
                            p+=Vector3.Transform(local,transforms[bone])*(float)weight;if(hasNormals)n+=Vector3.TransformNormal(normal,normalTransforms[bone])*(float)weight;
                        }
                    }
                    if(Math.Abs(weightSum-1)>1e-5)throw new InvalidDataException("Skin weights do not sum to one; not renormalized.");
                    if(n.LengthSquared()>1e-20f)n=Vector3.Normalize(n);
                    result.Positions[index]=Array(p);if(hasNormals)result.Normals[index]=Array(n);
                }
            }
            for(int i=0;i<4;i++)if(cursor[i]!=blocks[i].Length)throw new InvalidDataException("Unused Skin blend blocks.");
            return result;
        }
        static ManagedReferenceBinding ValidateSingle(ManagedReferenceBinding result,ClothDomain domain,XElement[] constraints,XElement[] moves,int count)
        {
            Action<int> require=i=>{if(i<0||i>=count||result.Positions[i]==null)throw new NotSupportedException("Authored Skin producer does not write required reference vertex "+i+"; no rest fallback.");};
            foreach(var constraint in constraints)foreach(var field in constraint.Descendants("hkparam").Where(p=>(string)p.Attribute("name")=="referenceVertex"))require(int.Parse(field.Value,C));
            foreach(var move in moves)foreach(var pair in ClothDocument.Field(move,"vertexParticlePairs").Elements())
            {
                int vertex=I(pair,"vertexIndex"),particle=I(pair,"particleIndex");require(vertex);
                if(particle<0||particle>=domain.Fixed.Length||!domain.Fixed[particle])throw new NotSupportedException("MoveParticles targeting dynamic particles requires runtime operator scheduling.");
                result.FixedTargets[particle]=(double[])result.Positions[vertex].Clone();
            }
            // Unwritten slots cannot be referenced (checked above). Only unused storage
            // gets zeros to satisfy the dense API; no requested target is synthesized.
            for(int i=0;i<count;i++)if(result.Positions[i]==null){result.Positions[i]=new double[3];result.Normals[i]=new double[3];}
            return result;
        }
    }
}
