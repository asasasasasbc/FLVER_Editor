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
            row.Controls.AddRange(new Control[] { showFlverMeshes, loadExtra, remove, extraFlverList, meshPreviewStatus });
            row.SetFlowBreak(showFlverMeshes, true);
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
                if (!ReferenceEquals(currentFlverSource, Program.targetFlver))
                {
                    // 当前编辑器模型也快照成独立内存副本，缓存不与编辑操作共享可变顶点。
                    var source = Program.targetFlver;
                    var snapshot = source == null ? null :
                        new ClothSkinnedModel(FLVER2.Read(source.Write()), "Current FLVER");
                    currentFlverPreview = snapshot;
                    currentFlverSource = source;
                }
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
    }

    // 复用主 Viewer 的蒙皮缓存。只缓存拓扑、inverse bind 和权重，不逐帧重新 GetFaces。
    internal sealed class ClothSkinnedModel
    {
        public readonly string Name;
        public readonly HkxGeometryCache.MeshData[] Meshes;
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
        struct MeshTriangle { public PointF A, B, C; public float Depth; public int Shade; }
        static readonly SolidBrush[] meshBrushes = Enumerable.Range(0, 64)
            .Select(i => new SolidBrush(Color.FromArgb(42 + i * 2, 56 + i * 2, 67 + i * 2))).ToArray();
        void DrawFlverMeshes(Graphics graphics)
        {
            if (MeshModels == null) return;
            var view = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
            var triangles = new List<MeshTriangle>();
            foreach (var model in MeshModels)
            foreach (var mesh in model.Meshes)
            {
                var projected = new PointF[mesh.Positions.Length];
                var transformed = new Vector3[mesh.Positions.Length];
                for (int i = 0; i < projected.Length; i++)
                {
                    transformed[i] = Vector3.Transform(mesh.Positions[i] - center, view);
                    projected[i] = new PointF(Width * .5f + pan.X + transformed[i].X * PixelScale,
                        Height * .5f + pan.Y - transformed[i].Y * PixelScale);
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
                        Depth = (transformed[face[0]].Z + transformed[face[1]].Z + transformed[face[2]].Z) / 3 });
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
                graphics.FillPolygon(meshBrushes[triangle.Shade], polygon);
            }
            graphics.SmoothingMode = smoothing;
        }
    }
}
