using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsFormats;

namespace MySFformat
{
    public sealed partial class ClothInspectorForm
    {
        readonly CheckBox showFlverMeshes = new CheckBox
        {
            Name = "showClothFlverMeshes", Text = "Render FLVER Meshes (Bone Weights)", AutoSize = true, Checked = false
        };
        readonly ListBox extraFlverList = new ListBox { Name = "clothExtraFlvers", Width = 390, Height = 70 };
        readonly Label meshPreviewStatus = new Label { Name = "clothMeshPreviewStatus", AutoSize = true, MaximumSize = new Size(420, 0) };
        readonly List<ClothSkinnedModel> extraFlvers = new List<ClothSkinnedModel>();
        ClothSkinnedModel currentFlverPreview;
        FLVER2 currentFlverSource;
        bool meshLoading;

        void InitializeMeshPreview(FlowLayoutPanel environment)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(430, 0) };
            var loadExtra = new Button { Name = "loadClothExtraFlver", Text = "Add FLVER…", AutoSize = true };
            var remove = new Button { Name = "removeClothExtraFlver", Text = "Remove Selected", AutoSize = true };
            var submeshes = new Button { Name = "clothSubmeshVisibility", Text = "Toggle Submesh Visibility…", AutoSize = true };
            row.Controls.AddRange(new Control[] { showFlverMeshes, submeshes, loadExtra, remove, extraFlverList, meshPreviewStatus });
            row.SetFlowBreak(showFlverMeshes, true);
            row.SetFlowBreak(submeshes, true);
            row.SetFlowBreak(remove, true);
            environment.SetFlowBreak(environment.Controls[environment.Controls.Count - 1], true);
            environment.Controls.Add(row);
            meshPreviewStatus.Text = "Off by default. Shaded geometry, original bone weights; no textures or game shaders. Skinning only, not simulated Cloth mesh deformation.";
            tips.SetToolTip(showFlverMeshes,
                "Preview the current FLVER and extra FLVERs using their original bone weights. " +
                "HKX animates geometry by bone name. This is a shaded geometry overlay, not game-material rendering or native Cloth output. No model edits.\n" +
                "按原始骨骼权重预览当前及额外FLVER，HKX按骨名联动；仅着色几何，不含游戏材质/贴图，也不是布料求解后的网格；不改模型。");
            tips.SetToolTip(loadExtra,
                "Load a render-only FLVER overlay without replacing the editor's model or adding physics colliders. Loading does not enable rendering.\n" +
                "额外加载仅显示的FLVER模型，不替换编辑器模型、不新增物理碰撞体；加载不会自动开启显示。");
            tips.SetToolTip(remove, "Remove the selected overlay from memory only.\n仅移除选中的内存预览模型，不删除文件。");
            showFlverMeshes.CheckedChanged += (s, e) => UpdateMeshPreview();
            submeshes.Click += (s, e) => ShowSubmeshVisibility();
            tips.SetToolTip(submeshes,"Show/hide individual submeshes of the current and extra FLVER overlays. Preview-only; does not edit models or Cloth physics.\n逐个切换当前及额外FLVER的子网格显示；仅影响预览，不修改模型或布料物理。");
            remove.Click += (s, e) =>
            {
                int index = extraFlverList.SelectedIndex;
                if (index < 0) return;
                extraFlvers.RemoveAt(index);
                extraFlverList.Items.RemoveAt(index);
                UpdateMeshPreview();
            };
            loadExtra.Click += async (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "FLVER|*.flver;*.flver.dcx", Multiselect = true, Title = "Add preview FLVERs" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    loadExtra.Enabled = false;
                    try { foreach (string path in dialog.FileNames) await LoadExtraPreviewFlverAsync(path); }
                    catch (Exception ex) { if (!IsDisposed) meshPreviewStatus.Text = "Mesh load failed: " + ex.GetBaseException().Message; }
                    finally { if (!IsDisposed) loadExtra.Enabled = true; }
                }
            };
        }

        void ShowSubmeshVisibility()
        {
            try { EnsureCurrentFlverPreview(); }
            catch(Exception ex){meshPreviewStatus.Text="Mesh preview unavailable: "+ex.GetBaseException().Message;return;}
            UpdateMeshPreview();
            var models = new List<ClothSkinnedModel>();
            if(currentFlverPreview!=null)models.Add(currentFlverPreview);
            models.AddRange(extraFlvers);
            using(var dialog=new Form {Name="clothSubmeshDialog",Text="FLVER Submesh Visibility — Preview Only",Size=new Size(650,480),MinimumSize=new Size(420,300),StartPosition=FormStartPosition.CenterParent})
            using(var list=new CheckedListBox {Name="clothSubmeshList",Dock=DockStyle.Fill,CheckOnClick=true,HorizontalScrollbar=true,IntegralHeight=false})
            {
                var entries=new List<Tuple<ClothSkinnedModel,int>>();
                foreach(var model in models)for(int i=0;i<model.Meshes.Length;i++)
                {entries.Add(Tuple.Create(model,i));list.Items.Add(Path.GetFileName(model.Name)+" · "+model.MeshNames[i],model.VisibleMeshes[i]);}
                list.ItemCheck+=(s,e)=>{var item=entries[e.Index];item.Item1.VisibleMeshes[item.Item2]=e.NewValue==CheckState.Checked;canvas.Invalidate();};
                var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true};
                var all=new Button {Text="Show All",AutoSize=true};var none=new Button {Text="Hide All",AutoSize=true};var invert=new Button {Text="Invert",AutoSize=true};var close=new Button {Text="Close",AutoSize=true,DialogResult=DialogResult.OK};
                all.Click+=(s,e)=>{for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,true);};
                none.Click+=(s,e)=>{for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,false);};
                invert.Click+=(s,e)=>{for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,!list.GetItemChecked(i));};
                buttons.Controls.AddRange(new Control[]{all,none,invert,close});
                dialog.Controls.Add(list);dialog.Controls.Add(buttons);
                dialog.Controls.Add(new Label {Dock=DockStyle.Top,AutoSize=true,Text=entries.Count==0?"Enable Render FLVER Meshes and load a FLVER first.":"Checked = visible. Changes apply immediately to overlays only."});
                dialog.AcceptButton=close;dialog.CancelButton=close;dialog.ShowDialog(this);
            }
        }

        public async Task LoadExtraPreviewFlverAsync(string path)
        {
            if (meshLoading) throw new InvalidOperationException("A mesh is already loading.");
            meshLoading = true;
            try
            {
                // 额外模型只读，失败时保留已经加载的模型；绝不 Set targetFlver。
                var model = await Task.Run(() => new ClothSkinnedModel(FLVER2.Read(path), Path.GetFullPath(path)));
                if (IsDisposed) return;
                extraFlvers.Add(model);
                extraFlverList.Items.Add(model.Name);
                UpdateMeshPreview();
            }
            finally { meshLoading = false; }
        }

        void InvalidateMeshPreview()
        {
            currentFlverSource = null;
            currentFlverPreview = null;
            UpdateMeshPreview();
        }

        void UpdateMeshPreview()
        {
            if (!showFlverMeshes.Checked)
            {
                canvas.MeshModels = null;
                canvas.Invalidate();
                return;
            }
            try
            {
                EnsureCurrentFlverPreview();
                var models = new List<ClothSkinnedModel>();
                if (currentFlverPreview != null) models.Add(currentFlverPreview);
                models.AddRange(extraFlvers);
                var clip = clothAnimation != null && !animationRest ? clothAnimation : null;
                float time = ClothAnimationPlayback == null ? 0 : ClothAnimationPlayback.Time;
                foreach (var model in models) model.Update(clip, time);
                canvas.MeshModels = models;
                meshPreviewStatus.Text = models.Count + " model(s) · original bone-weight skinning · shaded geometry only. " +
                    string.Join("; ", models.Where(m => m.BindingWarning != null).Select(m => m.Name + ": " + m.BindingWarning));
            }
            catch (Exception ex)
            {
                canvas.MeshModels = null;
                meshPreviewStatus.Text = "Mesh preview unavailable: " + ex.Message;
            }
            canvas.Invalidate();
        }

        void EnsureCurrentFlverPreview()
        {
            if(ReferenceEquals(currentFlverSource,Program.targetFlver))return;
            // 独立快照，仅保存预览显隐；不与主窗口的隐藏网格或作者顶点共享状态。
            var source=Program.targetFlver;
            var snapshot=source==null?null:new ClothSkinnedModel(FLVER2.Read(source.Write()),"Current FLVER");
            currentFlverPreview=snapshot;currentFlverSource=source;
        }
    }

    // 复用主 Viewer 的蒙皮缓存。只缓存拓扑、inverse bind 和权重，不逐帧重新 GetFaces。
    internal sealed class ClothSkinnedModel
    {
        public readonly string Name;
        public readonly HkxGeometryCache.MeshData[] Meshes;
        public readonly bool[] VisibleMeshes;
        public readonly string[] MeshNames;
        public string BindingWarning;
        readonly Matrix4x4[] local, rest;
        readonly string[] names;
        readonly int[] parents;
        HkxAnimationClip boundClip;
        HkxPoseBinding binding;
        public ClothSkinnedModel(FLVER2 model, string name)
        {
            Name = name;
            if (model.Meshes.Sum(m => (long)m.Vertices.Count) > 1000000) throw new InvalidDataException("Preview model exceeds one million vertices.");
            names = model.Nodes.Select(n => n.Name).ToArray();
            parents = model.Nodes.Select(n => (int)n.ParentIndex).ToArray();
            local = model.Nodes.Select(n => Matrix4x4.CreateScale(n.Scale) * Matrix4x4.CreateRotationX(n.Rotation.X) *
                Matrix4x4.CreateRotationZ(n.Rotation.Z) * Matrix4x4.CreateRotationY(n.Rotation.Y) * Matrix4x4.CreateTranslation(n.Translation)).ToArray();
            rest = new Matrix4x4[local.Length];
            var state = new byte[local.Length];
            Action<int> resolve = null;
            resolve = i =>
            {
                if (state[i] == 2) return;
                if (state[i] == 1 || parents[i] < -1 || parents[i] >= local.Length) throw new InvalidDataException("Invalid FLVER hierarchy.");
                state[i] = 1;
                if (parents[i] >= 0) resolve(parents[i]);
                rest[i] = parents[i] < 0 ? local[i] : local[i] * rest[parents[i]];
                state[i] = 2;
            };
            for (int i = 0; i < local.Length; i++) resolve(i);
            var inverse = new List<Matrix3D>();
            foreach (var matrix in rest)
            {
                Matrix4x4 value;
                if (!Matrix4x4.Invert(matrix, out value)) throw new InvalidDataException("Singular FLVER bind transform.");
                inverse.Add(HkxPoseBinding.ToEditorMatrix(value));
            }
            Meshes = model.Meshes.Select(m => new HkxGeometryCache.MeshData(m, inverse)).ToArray();
            VisibleMeshes = Enumerable.Repeat(true,Meshes.Length).ToArray();
            MeshNames = model.Meshes.Select((m,i)=>"Mesh "+i+" · "+
                (m.MaterialIndex>=0&&m.MaterialIndex<model.Materials.Count?model.Materials[m.MaterialIndex].Name:"No material")).ToArray();
            Update(null, 0);
        }
        public void Update(HkxAnimationClip clip, float time)
        {
            if (!ReferenceEquals(clip, boundClip))
            {
                boundClip = clip;
                binding = null;
                BindingWarning = null;
                if (clip != null)
                {
                    try { binding = new HkxPoseBinding(clip, names, parents, local); }
                    catch (InvalidDataException ex) { BindingWarning = ex.Message + " (rest pose shown)"; }
                }
            }
            var pose = (binding == null ? rest : binding.Sample(time)).Select(HkxPoseBinding.ToEditorMatrix).ToList();
            foreach (var mesh in Meshes) mesh.Skin(pose);
        }
    }

    public sealed partial class ClothCanvas
    {
        internal List<ClothSkinnedModel> MeshModels;
        struct MeshTriangle { public PointF A, B, C; public float Depth; public int Shade; public bool Cloth; }
        static readonly SolidBrush[] meshBrushes = Enumerable.Range(0, 64)
            .Select(i => new SolidBrush(Color.FromArgb(42 + i * 2, 56 + i * 2, 67 + i * 2))).ToArray();
        static readonly SolidBrush[] clothBrushes = Enumerable.Range(0,64)
            .Select(i=>new SolidBrush(Color.FromArgb(155+i,159+i,164+i))).ToArray();
        void DrawFlverMeshes(Graphics graphics)
        {
            if (MeshModels == null && ClothRenderMode == 0) return;
            var view = ViewTransform;
            var triangles = new List<MeshTriangle>();
            foreach (var model in MeshModels ?? new List<ClothSkinnedModel>())
            for(int meshIndex=0;meshIndex<model.Meshes.Length;meshIndex++)
            {
                if(!model.VisibleMeshes[meshIndex])continue;
                var mesh=model.Meshes[meshIndex];
                var projected = new PointF[mesh.Positions.Length];
                var transformed = new Vector3[mesh.Positions.Length];
                for (int i = 0; i < projected.Length; i++)
                {
                    transformed[i] = Vector3.Transform(mesh.Positions[i] - center, view);
                    projected[i] = ProjectViewPoint(transformed[i]);
                }
                foreach (var face in mesh.Indices)
                {
                    var a = projected[face[0]];var b = projected[face[1]];var c = projected[face[2]];
                    if (Math.Max(a.X, Math.Max(b.X, c.X)) < 0 || Math.Min(a.X, Math.Min(b.X, c.X)) > Width ||
                        Math.Max(a.Y, Math.Max(b.Y, c.Y)) < 0 || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > Height) continue;
                    var normal = Vector3.Cross(transformed[face[1]] - transformed[face[0]], transformed[face[2]] - transformed[face[0]]);
                    if (normal.LengthSquared() < 1e-16f) continue;
                    normal = Vector3.Normalize(normal);
                    int shade = Math.Min(63, (int)(63 * Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-.3f, .6f, -1))))));
                    triangles.Add(new MeshTriangle { A = a, B = b, C = c, Shade = shade,
                         Depth = -(transformed[face[0]].Z + transformed[face[1]].Z + transformed[face[2]].Z) / 3 });
                }
            }
            if(ClothRenderMode!=0)
            {
                var transformed=DisplayPositions.Select(p=>Vector3.Transform(p-center,view)).ToArray();
                for(int i=0;i<Domain.Triangles.Length;i+=3)
                {
                    var a=transformed[Domain.Triangles[i]];var b=transformed[Domain.Triangles[i+1]];var c=transformed[Domain.Triangles[i+2]];
                    var normal=Vector3.Cross(b-a,c-a);if(normal.LengthSquared()<1e-16f)continue;
                    int shade=Math.Min(63,(int)(63*Math.Abs(Vector3.Dot(Vector3.Normalize(normal),Vector3.Normalize(new Vector3(-.3f,.6f,-1))))));
                    triangles.Add(new MeshTriangle {A=ProjectViewPoint(a),B=ProjectViewPoint(b),C=ProjectViewPoint(c),Depth=-(a.Z+b.Z+c.Z)/3,Shade=shade,Cloth=true});
                }
            }
            // 几何层按深度排序；粒子和碰撞体仍是可读的叠加层，不影响选择遮挡规则。
            triangles.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            var smoothing = graphics.SmoothingMode;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            var polygon = new PointF[3];
            foreach (var triangle in triangles)
            {
                polygon[0] = triangle.A;polygon[1] = triangle.B;polygon[2] = triangle.C;
                graphics.FillPolygon(triangle.Cloth?clothBrushes[triangle.Shade]:meshBrushes[triangle.Shade], polygon);
            }
            graphics.SmoothingMode = smoothing;
        }
    }
}
