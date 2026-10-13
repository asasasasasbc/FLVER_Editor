using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace MySFformat
{
    public sealed partial class ClothCanvas
    {
        public readonly HashSet<int> SelectedParticles=new HashSet<int>();
        public int SelectionMode,BrushRadius=35;
        public bool VisibleOnly=true;
        PointF pan;
        MouseButtons gesture;
        Point cursor;
        bool subtract,add;
        public void SetSelection(IEnumerable<int> ids)
        {
            var values=ids.ToArray();SelectedParticles.Clear();
            if(Domain!=null)foreach(int id in values)if(id>=0&&id<Domain.Positions.Length)SelectedParticles.Add(id);
            Selected=SelectedParticles.Count==0?-1:SelectedParticles.Min();Invalidate();if(SelectionChanged!=null)SelectionChanged();
        }
        void InitializeSelectionInput()
        {
            MouseDown+=(s,e)=>{
                Focus();down=last=cursor=e.Location;drag=false;gesture=e.Button;Capture=true;
                subtract=(ModifierKeys&Keys.Alt)!=0;add=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;
                if(gesture==MouseButtons.Left&&Domain!=null&&SelectionMode==2&&PickValueRequested==null){if(!add&&!subtract)SelectedParticles.Clear();BrushAt(e.Location);}
            };
            MouseMove+=(s,e)=>{
                cursor=e.Location;
                if(!Capture){if(SelectionMode==2)Invalidate();return;}
                int dx=e.X-last.X,dy=e.Y-last.Y;if(Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>4)drag=true;
                if(gesture==MouseButtons.Right){yaw+=dx*.01f;pitch+=dy*.01f;}
                else if(gesture==MouseButtons.Middle){pan.X+=dx;pan.Y+=dy;}
                else if(gesture==MouseButtons.Left&&SelectionMode==2&&Domain!=null&&PickValueRequested==null)
                {
                    int steps=Math.Max(1,(int)(Math.Sqrt(dx*dx+dy*dy)/Math.Max(2,BrushRadius/2)));
                    for(int n=1;n<=steps;n++)BrushAt(new Point(last.X+dx*n/steps,last.Y+dy*n/steps));
                }
                last=e.Location;Invalidate();
            };
            MouseUp+=(s,e)=>{
                var active=gesture;gesture=MouseButtons.None;Capture=false;
                if(active!=MouseButtons.Left||Domain==null)return;
                if(PickValueRequested!=null)
                {
                    float distance=144;int source=-1;
                    for(int i=0;i<DisplayPositions.Length;i++){var point=Project(DisplayPositions[i]);float dx=point.X-e.X,dy=point.Y-e.Y;float squared=dx*dx+dy*dy;if(squared<distance&&IsParticleVisible(i)){distance=squared;source=i;}}
                    if(source>=0)PickValueRequested(source);
                    return;
                }
                if(SelectionMode==2){NotifySelection();return;}
                if(!add&&!subtract)SelectedParticles.Clear();
                if(SelectionMode==1&&drag)
                {
                    var rect=SelectionRectangle(e.Location);for(int i=0;i<DisplayPositions.Length;i++)if(rect.Contains(Project(DisplayPositions[i]))&&IsParticleVisible(i))Pick(i);
                }
                else
                {
                    float best=144;int candidate=-1;
                    for(int i=0;i<DisplayPositions.Length;i++){var p=Project(DisplayPositions[i]);float dist=(p.X-e.X)*(p.X-e.X)+(p.Y-e.Y)*(p.Y-e.Y);if(dist<best&&IsParticleVisible(i)){best=dist;candidate=i;}}
                    if(candidate>=0)Pick(candidate);
                }
                NotifySelection();
            };
            MouseCaptureChanged+=(s,e)=>{if(!Capture)gesture=MouseButtons.None;};
        }
        void Pick(int id){if(subtract)SelectedParticles.Remove(id);else SelectedParticles.Add(id);}
        void BrushAt(Point point)
        {
            for(int i=0;i<DisplayPositions.Length;i++){var p=Project(DisplayPositions[i]);float dx=p.X-point.X,dy=p.Y-point.Y;if(dx*dx+dy*dy<=BrushRadius*BrushRadius&&IsParticleVisible(i))Pick(i);}
            NotifySelection();
        }
        void NotifySelection(){Selected=SelectedParticles.Count==0?-1:SelectedParticles.Min();Invalidate();if(SelectionChanged!=null)SelectionChanged();}
        RectangleF SelectionRectangle(Point point){return RectangleF.FromLTRB(Math.Min(down.X,point.X),Math.Min(down.Y,point.Y),Math.Max(down.X,point.X),Math.Max(down.Y,point.Y));}
        float Depth(Vector3 point){return Vector3.Transform(point-center,Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateRotationX(pitch)).Z;}
        bool IsParticleVisible(int id)
        {
            if(!VisibleOnly)return true;
            var positions=DisplayPositions;var p=Project(positions[id]);float depth=Depth(positions[id]),epsilon=Math.Max(1e-5f,span*.0001f);
            for(int j=0;j<Domain.Triangles.Length;j+=3)
            {
                int ia=Domain.Triangles[j],ib=Domain.Triangles[j+1],ic=Domain.Triangles[j+2];if(ia==id||ib==id||ic==id)continue;
                var a=Project(positions[ia]);var b=Project(positions[ib]);var c=Project(positions[ic]);
                float det=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);if(Math.Abs(det)<1e-6f)continue;
                float u=((b.Y-c.Y)*(p.X-c.X)+(c.X-b.X)*(p.Y-c.Y))/det;
                float v=((c.Y-a.Y)*(p.X-c.X)+(a.X-c.X)*(p.Y-c.Y))/det,w=1-u-v;
                if(u>=-1e-5f&&v>=-1e-5f&&w>=-1e-5f&&u*Depth(positions[ia])+v*Depth(positions[ib])+w*Depth(positions[ic])<depth-epsilon)return false;
            }
            return true;
        }
        public float[] SaveCamera(){return new[]{yaw,pitch,zoom,span,center.X,center.Y,center.Z,pan.X,pan.Y};}
        public void RestoreCamera(float[] data){yaw=data[0];pitch=data[1];zoom=data[2];span=data[3];center=new Vector3(data[4],data[5],data[6]);pan=new PointF(data[7],data[8]);Invalidate();}
        public void FocusSelection()
        {
            if(Domain==null)return;var points=SelectedParticles.Count==0?DisplayPositions:SelectedParticles.Select(i=>DisplayPositions[i]).ToArray();
            if(points.Length==0)return;var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);center=(min+max)*.5f;span=Math.Max(.05f,(max-min).Length());pan=PointF.Empty;zoom=1;Invalidate();
        }
        void DrawSelectionOverlay(Graphics g)
        {
            if(SelectionMode==2){using(var pen=new Pen(Color.Magenta)){g.DrawEllipse(pen,cursor.X-BrushRadius,cursor.Y-BrushRadius,BrushRadius*2,BrushRadius*2);}}
            if(SelectionMode==1&&gesture==MouseButtons.Left&&drag){var r=SelectionRectangle(cursor);g.DrawRectangle(Pens.Magenta,r.X,r.Y,r.Width,r.Height);}
        }
    }
}
