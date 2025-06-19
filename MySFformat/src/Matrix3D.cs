using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySFformat
{
    public class Matrix3D
    {
        public float[,] value = new float[,] { {0, 0,0,0 },
            {0, 0,0,0 },
            {0, 0,0,0  },
            { 0, 0,0,0  } };


        public static Matrix3D generateTranslationMatrix(float x, float y, float z)
        {
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {1, 0,0,x },
              {0, 1, 0,y },
              {0, 0 ,1,z  },
              {0, 0, 0,1 }  };
            return m;

        }


        public static Matrix3D generateScaleMatrix(float x, float y, float z)
        {
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {x, 0,0,0 },
              {0, y, 0,0 },
              {0, 0 ,z,0  },
              {0, 0, 0,1 }  };
            return m;

        }
        public static Matrix3D generateScaleMatrix(Vector3D v)
        {
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {v.X, 0,0,0 },
              {0, v.Y, 0,0 },
              {0, 0 ,v.Z,0  },
              {0, 0, 0,1 }  };
            return m;

        }

        /// <summary>
        /// Computes the rotation tranformation matrix, parameter in degrees.
        /// </summary>
        public static Matrix3D generateRotXMatrix(float a)
        {
            float rad = (float)(a / 180f * Math.PI);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {1, 0,0,0 },
              {0, C(rad), -S(rad)       ,          0 },
              {0, S(rad) ,  C(rad)      ,          0  },
              {0, 0                     , 0         ,1 }  };
            return m;

        }
        /// <summary>
        /// Computes the rotation tranformation matrix, parameter in degrees.
        /// </summary>
        public static Matrix3D generateRotYMatrix(float a)
        {
            float rad = (float)(a / 180f * Math.PI);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {C(rad), 0,S(rad),0 },
              {0, 1, 0,0 },
              {-S(rad), 0 ,C(rad),0  },
              {0, 0, 0,1 }  };
            return m;

        }

        /// <summary>
        /// Computes the rotation tranformation matrix, parameter in degrees.
        /// </summary>
        public static Matrix3D generateRotZMatrix(float a)
        {
            float rad = (float)(a / 180f * Math.PI);
            Matrix3D m = new Matrix3D();
            m.value = new float[,]
            { {C(rad), -S(rad),0,0 },
              {S(rad), C(rad), 0,0 },
              {0, 0 ,1,0  },
              {0, 0, 0,1 }  };
            return m;

        }




        public static float C(float rad) { return (float)Math.Cos(rad); }
        public static float S(float rad) { return (float)Math.Sin(rad); }

        public static Vector3D matrixTimesVector3D(Matrix3D m, Vector3D v)
        {
            float x = 0;
            float y = 0;
            float z = 0;
            x = m.value[0, 0] * v.X + m.value[0, 1] * v.Y + m.value[0, 2] * v.Z + m.value[0, 3] * 1;
            y = m.value[1, 0] * v.X + m.value[1, 1] * v.Y + m.value[1, 2] * v.Z + m.value[1, 3] * 1;
            z = m.value[2, 0] * v.X + m.value[2, 1] * v.Y + m.value[2, 2] * v.Z + m.value[2, 3] * 1;

            return new Vector3D(x, y, z);

        }



        public static Matrix3D operator *(
                               Matrix3D m1, Matrix3D m2)
        {

            Matrix3D m = new Matrix3D();
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {


                    m.value[j, i] = m1.value[j, 0] * m2.value[0, i] + m1.value[j, 1] * m2.value[1, i] + m1.value[j, 2] * m2.value[2, i] + m1.value[j, 3] * m2.value[3, i];


                }

            }



            return m;
        }

        /// <summary>
        /// Matrix cross product matrix.
        /// </summary>
        public static Matrix3D matrixTimesMatrix(Matrix3D m1, Matrix3D m2)
        {
            Matrix3D m = new Matrix3D();
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {


                    m.value[j, i] = m1.value[j, 0] * m2.value[0, i] + m1.value[j, 1] * m2.value[1, i] + m1.value[j, 2] * m2.value[2, i] + m1.value[j, 3] * m2.value[3, i];


                }

            }



            return m;

        }
        /// <summary>
        /// Returns identity matrix
        /// </summary>
        public static Matrix3D Identity()
        {
            Matrix3D m = new Matrix3D();
            m.value = new float[,] {
        {1, 0, 0, 0},
        {0, 1, 0, 0},
        {0, 0, 1, 0},
        {0, 0, 0, 1}
            };
            return m;
        }


        /// <summary>
        /// Computes the inverse of this matrix.
        /// </summary>
        /// <returns>The inverted matrix.</returns>
        public Matrix3D inverse()
        {
            float[,] m = this.value;
            Matrix3D result = new Matrix3D();
            float[,] inv = result.value; // Store result here

            // These are the elements of the adjugate matrix (transpose of the cofactor matrix)
            inv[0, 0] = m[1, 1] * m[2, 2] * m[3, 3] - m[1, 1] * m[2, 3] * m[3, 2] - m[2, 1] * m[1, 2] * m[3, 3] + m[2, 1] * m[1, 3] * m[3, 2] + m[3, 1] * m[1, 2] * m[2, 3] - m[3, 1] * m[1, 3] * m[2, 2];
            inv[1, 0] = -m[1, 0] * m[2, 2] * m[3, 3] + m[1, 0] * m[2, 3] * m[3, 2] + m[2, 0] * m[1, 2] * m[3, 3] - m[2, 0] * m[1, 3] * m[3, 2] - m[3, 0] * m[1, 2] * m[2, 3] + m[3, 0] * m[1, 3] * m[2, 2];
            inv[2, 0] = m[1, 0] * m[2, 1] * m[3, 3] - m[1, 0] * m[2, 3] * m[3, 1] - m[2, 0] * m[1, 1] * m[3, 3] + m[2, 0] * m[1, 3] * m[3, 1] + m[3, 0] * m[1, 1] * m[2, 3] - m[3, 0] * m[1, 3] * m[2, 1];
            inv[3, 0] = -m[1, 0] * m[2, 1] * m[3, 2] + m[1, 0] * m[2, 2] * m[3, 1] + m[2, 0] * m[1, 1] * m[3, 2] - m[2, 0] * m[1, 2] * m[3, 1] - m[3, 0] * m[1, 1] * m[2, 2] + m[3, 0] * m[1, 2] * m[2, 1];

            inv[0, 1] = -m[0, 1] * m[2, 2] * m[3, 3] + m[0, 1] * m[2, 3] * m[3, 2] + m[2, 1] * m[0, 2] * m[3, 3] - m[2, 1] * m[0, 3] * m[3, 2] - m[3, 1] * m[0, 2] * m[2, 3] + m[3, 1] * m[0, 3] * m[2, 2];
            inv[1, 1] = m[0, 0] * m[2, 2] * m[3, 3] - m[0, 0] * m[2, 3] * m[3, 2] - m[2, 0] * m[0, 2] * m[3, 3] + m[2, 0] * m[0, 3] * m[3, 2] + m[3, 0] * m[0, 2] * m[2, 3] - m[3, 0] * m[0, 3] * m[2, 2];
            inv[2, 1] = -m[0, 0] * m[2, 1] * m[3, 3] + m[0, 0] * m[2, 3] * m[3, 1] + m[2, 0] * m[0, 1] * m[3, 3] - m[2, 0] * m[0, 3] * m[3, 1] - m[3, 0] * m[0, 1] * m[2, 3] + m[3, 0] * m[0, 3] * m[2, 1];
            inv[3, 1] = m[0, 0] * m[2, 1] * m[3, 2] - m[0, 0] * m[2, 2] * m[3, 1] - m[2, 0] * m[0, 1] * m[3, 2] + m[2, 0] * m[0, 2] * m[3, 1] + m[3, 0] * m[0, 1] * m[2, 2] - m[3, 0] * m[0, 2] * m[2, 1];

            inv[0, 2] = m[0, 1] * m[1, 2] * m[3, 3] - m[0, 1] * m[1, 3] * m[3, 2] - m[1, 1] * m[0, 2] * m[3, 3] + m[1, 1] * m[0, 3] * m[3, 2] + m[3, 1] * m[0, 2] * m[1, 3] - m[3, 1] * m[0, 3] * m[1, 2];
            inv[1, 2] = -m[0, 0] * m[1, 2] * m[3, 3] + m[0, 0] * m[1, 3] * m[3, 2] + m[1, 0] * m[0, 2] * m[3, 3] - m[1, 0] * m[0, 3] * m[3, 2] - m[3, 0] * m[0, 2] * m[1, 3] + m[3, 0] * m[0, 3] * m[1, 2];
            inv[2, 2] = m[0, 0] * m[1, 1] * m[3, 3] - m[0, 0] * m[1, 3] * m[3, 1] - m[1, 0] * m[0, 1] * m[3, 3] + m[1, 0] * m[0, 3] * m[3, 1] + m[3, 0] * m[0, 1] * m[1, 3] - m[3, 0] * m[0, 3] * m[1, 1];
            inv[3, 2] = -m[0, 0] * m[1, 1] * m[3, 2] + m[0, 0] * m[1, 2] * m[3, 1] + m[1, 0] * m[0, 1] * m[3, 2] - m[1, 0] * m[0, 2] * m[3, 1] - m[3, 0] * m[0, 1] * m[1, 2] + m[3, 0] * m[0, 2] * m[1, 1];

            inv[0, 3] = -m[0, 1] * m[1, 2] * m[2, 3] + m[0, 1] * m[1, 3] * m[2, 2] + m[1, 1] * m[0, 2] * m[2, 3] - m[1, 1] * m[0, 3] * m[2, 2] - m[2, 1] * m[0, 2] * m[1, 3] + m[2, 1] * m[0, 3] * m[1, 2];
            inv[1, 3] = m[0, 0] * m[1, 2] * m[2, 3] - m[0, 0] * m[1, 3] * m[2, 2] - m[1, 0] * m[0, 2] * m[2, 3] + m[1, 0] * m[0, 3] * m[2, 2] + m[2, 0] * m[0, 2] * m[1, 3] - m[2, 0] * m[0, 3] * m[1, 2];
            inv[2, 3] = -m[0, 0] * m[1, 1] * m[2, 3] + m[0, 0] * m[1, 3] * m[2, 1] + m[1, 0] * m[0, 1] * m[2, 3] - m[1, 0] * m[0, 3] * m[2, 1] - m[2, 0] * m[0, 1] * m[1, 3] + m[2, 0] * m[0, 3] * m[1, 1];
            inv[3, 3] = m[0, 0] * m[1, 1] * m[2, 2] - m[0, 0] * m[1, 2] * m[2, 1] - m[1, 0] * m[0, 1] * m[2, 2] + m[1, 0] * m[0, 2] * m[2, 1] + m[2, 0] * m[0, 1] * m[1, 2] - m[2, 0] * m[0, 2] * m[1, 1];

            // Calculate determinant using the first row of original matrix and first column of adjugate matrix
            float det = m[0, 0] * inv[0, 0] + m[0, 1] * inv[1, 0] + m[0, 2] * inv[2, 0] + m[0, 3] * inv[3, 0];

            // Check for singularity
            if (Math.Abs(det) < 1e-6f) // Use a small epsilon for floating point comparison
            {
                // Matrix is singular, cannot invert.
                // You can throw an exception, or return identity/null depending on requirements.
                // For robustness, throwing an exception is often preferred.
                return Identity();
            }

            // Divide adjugate matrix by determinant
            float invDet = 1.0f / det;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    inv[i, j] *= invDet;
                }
            }

            return result;
        }
    }
}
