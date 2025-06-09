using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySFformat

{
    //we use -> order, the reverse order of MAYA expression, the front one tis the parent
    public enum RotationOrder { XYZ, XZY, YXZ, YZX, ZXY, ZYX }

    class Transform3D
    {
        public string name = "";
        public Vector3D position = new Vector3D();


        //rotation unit: degree
        public Vector3D rotation = new Vector3D();

        public Vector3D scale = new Vector3D(1,1,1);

        public Transform3D parent = null;

        public RotationOrder rotOrder = RotationOrder.YZX;

        public Vector3D[] vlist;
        public Vector3D[] getGlobalVlist()
        {

            Vector3D[] ans = new Vector3D[vlist.Length];
            Matrix3D transMatrix = new Matrix3D();


            {

                Matrix3D rs = Matrix3D.generateScaleMatrix(scale);
                Matrix3D rx = Matrix3D.generateRotXMatrix(rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(position.X, position.Y, position.Z);

                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * (rz * rs) )); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * (rz * (ry * rs))); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * (rx * (rz * rs))); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz * (rx * rs))); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * (rx * (ry * rs))); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * (ry * (rx * rs))); }
            }


            Transform3D parentT = null;

            parentT = this.parent;
            while (parentT != null)
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(parentT.rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(parentT.rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(parentT.rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(parentT.position.X, parentT.position.Y, parentT.position.Z);

                transMatrix = Matrix3D.generateScaleMatrix(scale.X, scale.Y, scale.Z) * transMatrix;
                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * (rz * transMatrix))); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * (rz * (ry * transMatrix))); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * (rx * (rz * transMatrix))); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz * (rx * transMatrix))); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * (rx * (ry * transMatrix))); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * (ry * (rx * transMatrix))); }

                if (parent.parent == null) { break; }
                // transMatrix = pos * (rx * (ry * (rz * transMatrix)));
                parentT = parentT.parent;
            }


            for (int i = 0; i < vlist.Length; i++)
            {
                ans[i] = vlist[i].clone();

                // Console.WriteLine("Old X " + ans[i].X + " Y " + ans[i].Y + " Z " + ans[i].Z);
                ans[i] = Matrix3D.matrixTimesVector3D(transMatrix, ans[i]);
                // Console.WriteLine("New X " + ans[i].X + " Y " + ans[i].Y + " Z " + ans[i].Z);
            }


            return ans;
        }
        public Vector3D getGlobalOrigin()
        {

            Vector3D ans = new Vector3D();
            Vector3D org = new Vector3D(); 
            Matrix3D transMatrix = new Matrix3D();


            {

                Matrix3D rs = Matrix3D.generateScaleMatrix(scale.X,scale.Y,scale.Z);
                Matrix3D rx = Matrix3D.generateRotXMatrix(rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(position.X, position.Y, position.Z);


                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * (rz * rs))); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * (rz * (ry * rs))); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * (rx * (rz * rs))); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz * (rx * rs))); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * (rx * (ry * rs))); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * (ry * (rx * rs))); }
            }


            Transform3D parentT = null;

            parentT = this.parent;
            while (parentT != null)
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(parentT.rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(parentT.rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(parentT.rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(parentT.position.X, parentT.position.Y, parentT.position.Z);


                transMatrix = Matrix3D.generateScaleMatrix(parentT.scale.X, parentT.scale.Y, parentT.scale.Z)* transMatrix ;

                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * (rz * transMatrix))); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * (rz * (ry * transMatrix))); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * (rx * (rz * transMatrix))); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz * (rx * transMatrix))); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * (rx * (ry * transMatrix))); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * (ry * (rx * transMatrix))); }

                if (parent.parent == null) { break; }
                // transMatrix = pos * (rx * (ry * (rz * transMatrix)));
                parentT = parentT.parent;
            }



                //ans= org.clone();
                ans = Matrix3D.matrixTimesVector3D(transMatrix, org);



            return ans;
        }


        public void setRotationInRad(Vector3D v3d)
        {
            this.rotation.X = (float)(v3d.X / Math.PI * 180);
            this.rotation.Y = (float)(v3d.Y / Math.PI * 180);
            this.rotation.Z = (float)(v3d.Z / Math.PI * 180);
        }

        

        public Vector3D[] getRotCircleX()
        {

            Vector3D[] ans = new Vector3D[8];


            //This is for Y circle
            /*ans[0] = new Vector3D(2,0,0);
            ans[1] = new Vector3D(0, 0, 2);
            ans[2] = new Vector3D(-2, 0, 0);
            ans[3] = new Vector3D(0, 0, -2);*/


            ans[0] = new Vector3D(0, 2, 0);
            ans[1] = new Vector3D(0, 1.5f, 1.5f);
            ans[2] = new Vector3D(0, 0, 2);
            ans[3] = new Vector3D(0, -1.5f, 1.5f);
            ans[4] = new Vector3D(0, -2, 0);
            ans[5] = new Vector3D(0, -1.5f, -1.5f);
            ans[6] = new Vector3D(0, 0, -2);
            ans[7] = new Vector3D(0, 1.5f, -1.5f);

            float factor = 1f;

            if (rotOrder == RotationOrder.XYZ) { factor = 1; }
            if (rotOrder == RotationOrder.XZY) { factor = 1; }
            if (rotOrder == RotationOrder.YXZ) { factor = 0.85f; }
            if (rotOrder == RotationOrder.YZX) { factor = 0.7f; }
            if (rotOrder == RotationOrder.ZXY) { factor = 0.85f; }
            if (rotOrder == RotationOrder.ZYX) { factor = 0.7f; }

            foreach (var v in ans)
            {
                v.X *= factor;
                v.Y *= factor;
                v.Z *= factor;
            }


            Matrix3D transMatrix = new Matrix3D();
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(position.X, position.Y, position.Z);


                //   transMatrix = pos * (rx * (ry * rz));
                //transMatrix = pos * (rx );

                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * rx); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz * rx)); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * rx); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * (ry * rx)); }



            }


            Transform3D parentT = null;

            parentT = this.parent;
            while (parentT != null)
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(parentT.rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(parentT.rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(parentT.rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(parentT.position.X, parentT.position.Y, parentT.position.Z);


                transMatrix = pos * (rx * (ry * (rz * transMatrix)));
                parentT = parent.parent;
            }


            for (int i = 0; i < ans.Length; i++) { ans[i] = Matrix3D.matrixTimesVector3D(transMatrix, ans[i]); }
            return ans;
        }

        public Vector3D[] getRotCircleZ()
        {

            Vector3D[] ans = new Vector3D[8];


            //This is for Y circle
            /*ans[0] = new Vector3D(2,0,0);
            ans[1] = new Vector3D(0, 0, 2);
            ans[2] = new Vector3D(-2, 0, 0);
            ans[3] = new Vector3D(0, 0, -2);*/


            ans[0] = new Vector3D(2, 0, 0);
            ans[1] = new Vector3D(1.5f, 1.5f, 0);
            ans[2] = new Vector3D(0, 2, 0);
            ans[3] = new Vector3D(-1.5f, 1.5f, 0);
            ans[4] = new Vector3D(-2, 0, 0);
            ans[5] = new Vector3D(-1.5f, -1.5f, 0);
            ans[6] = new Vector3D(0, -2, 0);
            ans[7] = new Vector3D(1.5f, -1.5f, 0);

            float factor = 1;

            if (rotOrder == RotationOrder.XYZ) { factor = 0.7f; }
            if (rotOrder == RotationOrder.XZY) { factor = 0.85f; }
            if (rotOrder == RotationOrder.YXZ) { factor = 0.7f; }
            if (rotOrder == RotationOrder.YZX) { factor = 0.85f; }
            if (rotOrder == RotationOrder.ZXY) { factor = 1f; }
            if (rotOrder == RotationOrder.ZYX) { factor = 1f; }

            foreach (var v in ans)
            {
                v.X *= factor;
                v.Y *= factor;
                v.Z *= factor;
            }

            Matrix3D transMatrix = new Matrix3D();
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(position.X, position.Y, position.Z);


                //   transMatrix = pos * (rx * (ry * rz));
                //  transMatrix = pos * (rx * (ry * rz));


                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry * rz)); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * rz); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry * (rx * rz)); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry * (rz)); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz); }

            }


            Transform3D parentT = null;

            parentT = this.parent;
            while (parentT != null)
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(parentT.rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(parentT.rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(parentT.rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(parentT.position.X, parentT.position.Y, parentT.position.Z);


                transMatrix = pos * (rx * (ry * (rz * transMatrix)));
                parentT = parent.parent;
            }


            for (int i = 0; i < ans.Length; i++) { ans[i] = Matrix3D.matrixTimesVector3D(transMatrix, ans[i]); }
            return ans;
        }

        public Vector3D[] getRotCircleY()
        {

            Vector3D[] ans = new Vector3D[8];


            //This is for Y circle
            /*ans[0] = new Vector3D(1.7f,0,0);
            ans[1] = new Vector3D(0, 0, 1.7f);
            ans[2] = new Vector3D(-1.7f, 0, 0);
            ans[3] = new Vector3D(0, 0, -1.7f);
            */

            ans[0] = new Vector3D(2, 0, 0);
            ans[1] = new Vector3D(1.5f, 0, 1.5f);
            ans[2] = new Vector3D(0, 0, 2);
            ans[3] = new Vector3D(-1.5f, 0, 1.5f);
            ans[4] = new Vector3D(-2, 0, 0);
            ans[5] = new Vector3D(-1.5f, 0, -1.5f);
            ans[6] = new Vector3D(0, 0, -2);
            ans[7] = new Vector3D(1.5f, 0, -1.5f);


            float factor = 1;

            if (rotOrder == RotationOrder.XYZ) { factor = 0.85f; }
            if (rotOrder == RotationOrder.XZY) { factor = 0.7f; }
            if (rotOrder == RotationOrder.YXZ) { factor = 1f; }
            if (rotOrder == RotationOrder.YZX) { factor = 1f; }
            if (rotOrder == RotationOrder.ZXY) { factor = 0.7f; }
            if (rotOrder == RotationOrder.ZYX) { factor = 0.85f; }

            foreach (var v in ans)
            {
                v.X *= factor;
                v.Y *= factor;
                v.Z *= factor;
            }



            Matrix3D transMatrix = new Matrix3D();
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(position.X, position.Y, position.Z);


                //   transMatrix = pos * (rx * (ry * rz));
                //transMatrix = pos * (rx * ry);

                if (rotOrder == RotationOrder.XYZ) { transMatrix = pos * (rx * (ry)); }
                if (rotOrder == RotationOrder.XZY) { transMatrix = pos * (rx * (rz * ry)); }
                if (rotOrder == RotationOrder.YXZ) { transMatrix = pos * (ry); }
                if (rotOrder == RotationOrder.YZX) { transMatrix = pos * (ry); }
                if (rotOrder == RotationOrder.ZXY) { transMatrix = pos * (rz * (rx * ry)); }
                if (rotOrder == RotationOrder.ZYX) { transMatrix = pos * (rz * ry); }

            }


            Transform3D parentT = null;

            parentT = this.parent;
            while (parentT != null)
            {
                Matrix3D rx = Matrix3D.generateRotXMatrix(parentT.rotation.X);
                Matrix3D ry = Matrix3D.generateRotYMatrix(parentT.rotation.Y);
                Matrix3D rz = Matrix3D.generateRotZMatrix(parentT.rotation.Z);
                Matrix3D pos = Matrix3D.generateTranslationMatrix(parentT.position.X, parentT.position.Y, parentT.position.Z);


                transMatrix = pos * (rx * (ry * (rz * transMatrix)));
                parentT = parent.parent;
            }


            for (int i = 0; i < ans.Length; i++) { ans[i] = Matrix3D.matrixTimesVector3D(transMatrix, ans[i]); }
            return ans;
        }

    }
}
