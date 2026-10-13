using System;

namespace MySFformat.Cloth.Preview
{
    // Preview-only settings. Never applied to the editor's author document.
    public sealed class PreviewEnvironment
    {
        public bool OverrideGravity;
        public float GravityX=0,GravityY=-9.81f,GravityZ=0;
        public bool WindEnabled;
        public float WindX=1,WindY=0,WindZ=0,WindSpeed=3,WindMaxDrag=1;
        public PreviewEnvironment Clone(){return (PreviewEnvironment)MemberwiseClone();}
        public void Validate()
        {
            foreach(float f in new[]{GravityX,GravityY,GravityZ,WindX,WindY,WindZ,WindSpeed,WindMaxDrag})if(float.IsNaN(f)||float.IsInfinity(f))throw new ArgumentException("Environment values must be finite.");
            if(Math.Abs(GravityX)>100||Math.Abs(GravityY)>100||Math.Abs(GravityZ)>100||Math.Abs(WindX)>1||Math.Abs(WindY)>1||Math.Abs(WindZ)>1||WindSpeed<0||WindSpeed>50||WindMaxDrag<0||WindMaxDrag>100)throw new ArgumentOutOfRangeException("environment");
            if(WindEnabled&&WindX*WindX+WindY*WindY+WindZ*WindZ<1e-10f)throw new ArgumentException("Enabled wind needs a nonzero direction.");
        }
    }
}
