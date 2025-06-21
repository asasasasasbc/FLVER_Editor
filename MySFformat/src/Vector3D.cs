using System;
using System.Numerics; // Aliased as Vector3 for RotateLine/RotatePoint
using Microsoft.Xna.Framework; // For XNA Vector3 conversion

namespace MySFformat
{
    public struct Vector3D : IEquatable<Vector3D>
    {
        public float X; // Mutable
        public float Y; // Mutable
        public float Z; // Mutable

        // Constructor for explicit initialization
        public Vector3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // Constructor from Microsoft.Xna.Framework.Vector3
        public Vector3D(Microsoft.Xna.Framework.Vector3 a)
        {
            X = a.X;
            Y = a.Y;
            Z = a.Z;
        }

        // Constructor from System.Numerics.Vector3
        public Vector3D(System.Numerics.Vector3 a)
        {
            X = a.X;
            Y = a.Y;
            Z = a.Z;
        }

        // Methods that modify the instance (if you want them)
        // For example, if you want normalize to modify in-place:
        public void NormalizeInPlace() // Note: void return type, modifies 'this'
        {
            float l = length();
            if (l > float.Epsilon) // Use an epsilon for robust comparison
            {
                X /= l;
                Y /= l;
                Z /= l;
            }
            else
            {
                X = Y = Z = 0; // Or handle as an error, or leave as is
            }
        }

        // Methods that return new instances (as you originally had)
        public Microsoft.Xna.Framework.Vector3 toXnaV3()
        {
            return new Microsoft.Xna.Framework.Vector3(X, Y, Z);
        }

        public System.Numerics.Vector3 toNumV3()
        {
            return new System.Numerics.Vector3(X, Y, Z);
        }

        public static float dotProduct(Vector3D a, Vector3D b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vector3D crossPorduct(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public float length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public float LengthSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        public Vector3D normalize() // Returns a new normalized vector
        {
            float l = length();
            if (l > float.Epsilon)
            {
                return new Vector3D(X / l, Y / l, Z / l);
            }
            return Vector3D.Zero; // Return a zero vector if length is zero
        }

        public Vector3D clone()
        {
            return new Vector3D(X, Y, Z); // or just `return this;`
        }

        // Operators
        public static Vector3D operator +(Vector3D a, Vector3D b)
        {
            return new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3D operator -(Vector3D a, Vector3D b)
        {
            return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector3D operator *(Vector3D a, float b)
        {
            return new Vector3D(a.X * b, a.Y * b, a.Z * b);
        }

        public static Vector3D operator *(float b, Vector3D a)
        {
            return new Vector3D(a.X * b, a.Y * b, a.Z * b);
        }

        public static Vector3D operator /(Vector3D a, float b)
        {
            if (Math.Abs(b) < float.Epsilon) throw new DivideByZeroException("Cannot divide Vector3D by zero or near-zero.");
            return new Vector3D(a.X / b, a.Y / b, a.Z / b);
        }

        public static Vector3D operator -(Vector3D a)
        {
            return new Vector3D(-a.X, -a.Y, -a.Z);
        }

        // Static utility methods
        public static float calculateDistanceFromLine(Vector3D point, Vector3D lineStart, Vector3D lineEnd)
        {
            Vector3D lineDir = lineEnd - lineStart;
            float lineLengthSqr = lineDir.LengthSquared(); // Use squared length to avoid sqrt if possible
            if (lineLengthSqr < float.Epsilon * float.Epsilon) // Check if lineStart and lineEnd are the same
            {
                return (point - lineStart).length();
            }
            // Using the formula: |(point - lineStart) x (lineEnd - lineStart)| / |lineEnd - lineStart|
            // which simplifies to |(point - lineStart) x lineDir| / |lineDir|
            return crossPorduct(point - lineStart, lineDir).length() / (float)Math.Sqrt(lineLengthSqr);
        }

        public static System.Numerics.Vector3 RotateLine(System.Numerics.Vector3 p, System.Numerics.Vector3 org, System.Numerics.Vector3 direction, double theta)
        {
            double x = p.X;
            double y = p.Y;
            double z = p.Z;

            double a = org.X;
            double b = org.Y;
            double c = org.Z;

            double dirLength = direction.Length();
            if (dirLength == 0) return p;

            double nu = direction.X / dirLength;
            double nv = direction.Y / dirLength;
            double nw = direction.Z / dirLength;

            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);
            double oneMinusCosTheta = 1 - cosTheta;

            double rX = (a * (nv * nv + nw * nw) - nu * (b * nv + c * nw - nu * x - nv * y - nw * z)) * oneMinusCosTheta + x * cosTheta + (-c * nv + b * nw - nw * y + nv * z) * sinTheta;
            double rY = (b * (nu * nu + nw * nw) - nv * (a * nu + c * nw - nu * x - nv * y - nw * z)) * oneMinusCosTheta + y * cosTheta + (c * nu - a * nw + nw * x - nu * z) * sinTheta;
            double rZ = (c * (nu * nu + nv * nv) - nw * (a * nu + b * nv - nu * x - nv * y - nw * z)) * oneMinusCosTheta + z * cosTheta + (-b * nu + a * nv - nv * x + nu * y) * sinTheta;

            return new System.Numerics.Vector3((float)rX, (float)rY, (float)rZ);
        }

        public static System.Numerics.Vector3 RotatePoint(System.Numerics.Vector3 p, float pitch, float roll, float yaw)
        {
            // ... (implementation remains the same as previous)
            double cosa = Math.Cos(yaw);
            double sina = Math.Sin(yaw);

            double cosb = Math.Cos(pitch);
            double sinb = Math.Sin(pitch);

            double cosc = Math.Cos(roll);
            double sinc = Math.Sin(roll);

            double Axx = cosa * cosb;
            double Axy = cosa * sinb * sinc - sina * cosc;
            double Axz = cosa * sinb * cosc + sina * sinc;

            double Ayx = sina * cosb;
            double Ayy = sina * sinb * sinc + cosa * cosc;
            double Ayz = sina * sinb * cosc - cosa * sinc;

            double Azx = -sinb;
            double Azy = cosb * sinc;
            double Azz = cosb * cosc;

            float px = p.X;
            float py = p.Y;
            float pz = p.Z;

            float newX = (float)(Axx * px + Axy * py + Axz * pz);
            float newY = (float)(Ayx * px + Ayy * py + Ayz * pz);
            float newZ = (float)(Azx * px + Azy * py + Azz * pz);

            return new System.Numerics.Vector3(newX, newY, newZ);
        }

        // IEquatable implementation
        public bool Equals(Vector3D other)
        {
            // For mutable structs, consider if exact bitwise equality is always desired
            // or if an epsilon comparison is more appropriate for floating point values.
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                hash = hash * 23 + Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Vector3D left, Vector3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3D left, Vector3D right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        // Common static instances (these will be copies when accessed)
        public static Vector3D Zero => new Vector3D(0, 0, 0);
        public static Vector3D One => new Vector3D(1, 1, 1);
        public static Vector3D UnitX => new Vector3D(1, 0, 0);
        public static Vector3D UnitY => new Vector3D(0, 1, 0);
        public static Vector3D UnitZ => new Vector3D(0, 0, 1);
    }
}