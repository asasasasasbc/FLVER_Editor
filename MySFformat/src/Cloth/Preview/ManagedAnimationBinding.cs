using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace MySFformat.Cloth.Preview
{
    // Format adapter: owned world-space poses -> authored Skin transforms and
    // collidableTransformMap. Core only consumes mapped numeric frame inputs.
    internal sealed class ManagedAnimationBinding
    {
        readonly ClothDocument document;
        readonly XElement[] skeletons;
        readonly string[] names;
        readonly string[][] bones;
        ColliderBinding[][] colliders;
        sealed class ColliderBinding
        {
            internal XElement Skeleton;
            internal int Bone;
            internal Matrix4x4 Offset,InverseRest;
            internal Vector3 Start,End;
            internal double RadiusA,RadiusB;
        }
        internal sealed class Frame
        {
            internal Dictionary<XElement,Matrix4x4[]> Poses;
            internal double[][][] Capsules;
        }
        internal string[] Names {get{return (string[])names.Clone();}}
        internal string[][] Bones {get{return bones.Select(b=>(string[])b.Clone()).ToArray();}}
        internal bool Supported {get{return skeletons.Length>0;}}
        static string V(XElement o,string k){return ClothDocument.Value(o,k);}
        static double[] N(XElement o,string k){return ClothDocument.Numbers(V(o,k));}
        static int I(XElement o,string k){return int.Parse(V(o,k),CultureInfo.InvariantCulture);}
        static string[] R(XElement o,string k){return V(o,k).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);}
        static Vector3 Vector(double[] values){return new Vector3((float)values[0],(float)values[1],(float)values[2]);}
        static Matrix4x4 Matrix(double[] a,int p){return new Matrix4x4((float)a[p],(float)a[p+1],(float)a[p+2],(float)a[p+3],(float)a[p+4],(float)a[p+5],(float)a[p+6],(float)a[p+7],(float)a[p+8],(float)a[p+9],(float)a[p+10],(float)a[p+11],(float)a[p+12],(float)a[p+13],(float)a[p+14],(float)a[p+15]);}
        internal ManagedAnimationBinding(ClothDocument document)
        {
            this.document=document;
            skeletons=document.Objects.Values.Where(o=>(string)o.Attribute("class")=="hkaSkeleton").ToArray();
            names=skeletons.Select(s=>V(s,"name")).ToArray();
            bones=skeletons.Select(s=>ClothDocument.Field(s,"bones").Elements().Select(b=>V(b,"name")).ToArray()).ToArray();
        }
        ColliderBinding[] PrepareColliders(ClothDomain domain)
        {
            var refs=R(domain.Sim,"perInstanceCollidables");if(refs.Length==0)return new ColliderBinding[0];
            var map=ClothDocument.Field(domain.Sim,"collidableTransformMap").Elements().Single();
            int set=I(map,"transformSetIndex");var indices=N(map,"transformIndices");var offsets=N(map,"offsets");
            bool unbound=set==-1&&indices.Length==0&&offsets.Length==0;
            XElement skeleton=null;Matrix4x4[] rest=null;
            if(!unbound)
            {
                if(indices.Length!=refs.Length||offsets.Length!=refs.Length*16)throw new InvalidDataException("Animated collider map dimensions mismatch.");
                skeleton=ManagedReferenceBinding.FindSkeleton(document,domain.Owner,set);rest=ManagedReferenceBinding.ReferenceWorld(skeleton);
                var simRefs=R(domain.Owner,"simClothDatas");int sim=Array.IndexOf(simRefs,(string)domain.Sim.Attribute("name"));
                var ops=R(domain.Owner,"operators").Select(r=>document.Objects[r]).ToArray();
                var order=N(document.Objects[R(domain.Owner,"clothStateDatas")[0]],"operators");
                foreach(int index in order.Select(v=>(int)v))
                {
                    var op=ops[index];if((string)op.Attribute("class")=="hclSimulateOperator"&&I(op,"simClothIndex")==sim)break;
                    var output=op.Elements("hkparam").SingleOrDefault(p=>(string)p.Attribute("name")=="outputTransformSetIdx"||(string)p.Attribute("name")=="outputTransformSetIndex");
                    if(output!=null&&int.Parse(output.Value,CultureInfo.InvariantCulture)==set)throw new NotSupportedException("Animated collider uses an unsupported dynamic transform-set producer.");
                }
            }
            var result=new ColliderBinding[refs.Length];
            for(int i=0;i<result.Length;i++)
            {
                var collider=document.Objects[refs[i]];var shape=document.Objects[V(collider,"shape")];string cls=(string)shape.Attribute("class");
                var binding=new ColliderBinding {Skeleton=skeleton};
                if(unbound){var values=N(collider,"transform");if(values.Length!=16)throw new InvalidDataException("Invalid unbound collider matrix.");binding.Offset=Matrix(values,0);}
                else
                {
                    if(indices[i]!=Math.Truncate(indices[i])||indices[i]<0||indices[i]>=rest.Length)throw new InvalidDataException("Animated collider bone index outside skeleton.");
                    binding.Bone=(int)indices[i];binding.Offset=Matrix(offsets,i*16);
                    if(!Matrix4x4.Invert(rest[binding.Bone],out binding.InverseRest))throw new InvalidDataException("Singular collider reference bone.");
                }
                if(cls=="hclSphereShape"){var sphere=N(shape,"sphere");binding.Start=binding.End=Vector(sphere);binding.RadiusA=binding.RadiusB=sphere[3];}
                else if(cls=="hclCapsuleShape"||cls=="hclTaperedCapsuleShape")
                {
                    bool tapered=cls=="hclTaperedCapsuleShape";binding.Start=Vector(N(shape,tapered?"small":"start"));binding.End=Vector(N(shape,tapered?"big":"end"));
                    binding.RadiusA=double.Parse(V(shape,tapered?"smallRadius":"radius"),CultureInfo.InvariantCulture);
                    binding.RadiusB=tapered?double.Parse(V(shape,"bigRadius"),CultureInfo.InvariantCulture):binding.RadiusA;
                }
                else throw new NotSupportedException("Animated collider shape is not supported: "+cls);
                result[i]=binding;
            }
            return result;
        }
        internal Frame[] Snapshot(float[][][] poses,int steps)
        {
            if(poses==null)return null;
            if(!Supported||poses.Length!=steps)throw new ArgumentException("Animated pose batch must match step and skeleton counts.");
            var result=new Frame[steps];
            // Copy and validate the ENTIRE batch before any graph/solver advances.
            for(int f=0;f<steps;f++)
            {
                if(poses[f]==null||poses[f].Length!=skeletons.Length)throw new ArgumentException("Animated skeleton count mismatch.");
                var frame=new Frame {Poses=new Dictionary<XElement,Matrix4x4[]>()};result[f]=frame;
                for(int s=0;s<skeletons.Length;s++)
                {
                    var values=poses[f][s];if(values==null||values.Length!=bones[s].Length*16||values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new ArgumentException("Invalid animated bone matrix dimensions or values.");
                    var copied=values.Select(v=>(double)v).ToArray();
                    var matrices=new Matrix4x4[bones[s].Length];
                    for(int b=0;b<matrices.Length;b++)
                    {
                        var m=Matrix(copied,b*16);Matrix4x4 inverse;
                        if(Math.Abs(m.M14)>1e-5||Math.Abs(m.M24)>1e-5||Math.Abs(m.M34)>1e-5||Math.Abs(m.M44-1)>1e-5||!Matrix4x4.Invert(m,out inverse))throw new ArgumentException("Animated bone matrix must be finite, affine and invertible.");
                        matrices[b]=m;
                    }
                    frame.Poses.Add(skeletons[s],matrices);
                }
            }
            if(colliders==null)colliders=document.Domains.Select(PrepareColliders).ToArray();
            foreach(var frame in result)frame.Capsules=colliders.Select(domain=>domain.Select(binding=>
            {
                var matrix=binding.Offset;
                if(binding.Skeleton!=null)
                {
                    var bone=frame.Poses[binding.Skeleton][binding.Bone];var delta=binding.InverseRest*bone;
                    var x=Vector3.TransformNormal(Vector3.UnitX,delta);var y=Vector3.TransformNormal(Vector3.UnitY,delta);var z=Vector3.TransformNormal(Vector3.UnitZ,delta);
                    if(Math.Abs(x.LengthSquared()-1)>1e-3||Math.Abs(y.LengthSquared()-1)>1e-3||Math.Abs(z.LengthSquared()-1)>1e-3||Math.Abs(Vector3.Dot(x,y))>1e-3||Math.Abs(Vector3.Dot(x,z))>1e-3||Math.Abs(Vector3.Dot(y,z))>1e-3||delta.GetDeterminant()<0)throw new NotSupportedException("Animated collider scaling/shear/reflection is not supported.");
                    matrix=matrix*bone;
                }
                var a=Vector3.Transform(binding.Start,matrix);var b=Vector3.Transform(binding.End,matrix);
                if(new[]{a.X,a.Y,a.Z,b.X,b.Y,b.Z}.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new ArgumentException("Animated collider exceeds numeric range.");
                return new[]{(double)a.X,a.Y,a.Z,b.X,b.Y,b.Z,binding.RadiusA,binding.RadiusB};
            }).ToArray()).ToArray();
            return result;
        }
    }
}
