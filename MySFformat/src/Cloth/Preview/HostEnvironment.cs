// Uses HCT's managed wind controls, not a particle displacement approximation.
using System;
using System.Reflection;
using System.Windows.Forms;

namespace MySFformat.Cloth.Preview
{
    internal sealed class HostEnvironment : IDisposable
    {
        readonly object wind;
        readonly Type type;
        readonly bool oldEnabled;
        readonly float oldX,oldY,oldZ,oldMin,oldMax,oldDrag;
        PreviewEnvironment lastApplied;
        public HostEnvironment(Type previewType,object control)
        {
            wind=previewType.GetField("m_clothWindForm",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);
            if(wind==null)throw new NotSupportedException("HCT native wind control was not initialized; no fake wind is used.");
            type=wind.GetType();oldEnabled=(bool)Invoke("getWindEnabled");oldMin=(float)Invoke("getWindMinSpeed");oldMax=(float)Invoke("getWindMaxSpeed");oldDrag=(float)Invoke("getWindMaxDrag");
            oldX=Direction("windDirectionXNumericUpDown");oldY=Direction("windDirectionYNumericUpDown");oldZ=Direction("windDirectionZNumericUpDown");
        }
        object Invoke(string name,params object[] args){return type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(wind,args);}
        float Direction(string field){return (float)((NumericUpDown)type.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wind)).Value;}
        public void Apply(PreviewEnvironment environment)
        {
            environment.Validate();
            if(lastApplied!=null&&lastApplied.WindEnabled==environment.WindEnabled&&lastApplied.WindX==environment.WindX&&lastApplied.WindY==environment.WindY&&lastApplied.WindZ==environment.WindZ&&lastApplied.WindSpeed==environment.WindSpeed&&lastApplied.WindMaxDrag==environment.WindMaxDrag)return;
            Invoke("setWindEnabled",false);
            if(environment.WindEnabled)
            {
                double length=Math.Sqrt(environment.WindX*environment.WindX+environment.WindY*environment.WindY+environment.WindZ*environment.WindZ);
                Invoke("setWindDirection",(float)(environment.WindX/length),(float)(environment.WindY/length),(float)(environment.WindZ/length));
                Invoke("setWindMinSpeed",environment.WindSpeed);Invoke("setWindMaxSpeed",environment.WindSpeed);Invoke("setWindMaxDrag",environment.WindMaxDrag);
                if(Math.Abs((float)Invoke("getWindMinSpeed")-environment.WindSpeed)>.001f||Math.Abs((float)Invoke("getWindMaxSpeed")-environment.WindSpeed)>.001f||Math.Abs((float)Invoke("getWindMaxDrag")-environment.WindMaxDrag)>.001f)throw new InvalidOperationException("HCT clamped an environment value; requested wind was not applied.");
            }
            Invoke("setWindEnabled",environment.WindEnabled);Invoke("applyWindSettings");
            if((bool)Invoke("getWindEnabled")!=environment.WindEnabled)throw new InvalidOperationException("HCT wind enable readback failed.");
            lastApplied=environment.Clone();
        }
        public void Dispose()
        {
            Invoke("setWindEnabled",false);Invoke("setWindDirection",oldX,oldY,oldZ);Invoke("setWindMinSpeed",oldMin);Invoke("setWindMaxSpeed",oldMax);Invoke("setWindMaxDrag",oldDrag);Invoke("setWindEnabled",oldEnabled);Invoke("applyWindSettings");
        }
    }
}
