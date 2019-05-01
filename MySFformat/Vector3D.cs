using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySFformat
{
    class Vector3D
    {
        public float X = 0;
        public float Y = 0;
        public float Z = 0;

        public Vector3D()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }
        public Vector3D(float x,float y,float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public Vector3D(Microsoft.Xna.Framework.Vector3 a)
        {
            X = a.X;
            Y = a.Y;
            Z = a.Z;
        }
        public Vector3D(System.Numerics.Vector3 a)
        {
            X = a.X;
            Y = a.Y;
            Z = a.Z;
        }

        public Microsoft.Xna.Framework.Vector3 toXnaV3() {
            return new Microsoft.Xna.Framework.Vector3(X,Y,Z);
        }

        public System.Numerics.Vector3 toNumV3()
        {
            return new System.Numerics.Vector3(X, Y, Z);
        }


        public static float dotProduct(Vector3D a, Vector3D b)
        {
            float x1 = a.X;
            float y1 = a.Y;
            float z1 = a.Z;
            float x2 = b.X;
            float y2 = b.Y;
            float z2 = b.Z;
            return x1 * x2 + y1 * y2 + z1 * z2;
        }


        public static Vector3D crossPorduct(Vector3D a, Vector3D b)
        {
            float x1 = a.X;
            float y1 = a.Y;
            float z1 = a.Z;
            float x2 = b.X;
            float y2 = b.Y;
            float z2 = b.Z;
            return new Vector3D(y1 * z2 - z1 * y2, z1 * x2 - x1 * z2, x1 * y2 - y1 * x2);
        }

        public float length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        }
        public Vector3D normalize()
        {
            float l = length();
            if (l == 0) { return new Vector3D(); }
            return new Vector3D(X/l,Y/l,Z/l);
        }

        public static Vector3D operator +(Vector3D a,
                                          Vector3D b)
        {

            return new Vector3D(a.X + b.X,a.Y + b.Y,a.Z + b.Z); ;
        }


        public static Vector3D operator -(Vector3D a,
                                  Vector3D b)
        {

            return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z); ;
        }


        public static float calculateDistanceFromLine(Vector3D point, Vector3D x1, Vector3D x2)
        {
            return crossPorduct(point - x1, point - x2).length() /
                (x2 - x1).length();
               
            
        }
    }
}
