using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using SoulsFormats;

namespace MySFformat
{
    public sealed class HkxPlaybackForm : Form
    {
        public const string BloodborneRoot = @"G:\bloodborne_pc-win\Games\dvdroot_ps4\chr";
        string skeletonPath = Path.Combine(BloodborneRoot, @"c0000-anibnd-dcx\chr\c0000\hkx\skeleton.hkx");
        string animationPath;
        readonly Label status = new Label { AutoSize = false, Height = 76, Dock = DockStyle.Top };
        readonly TextBox skeletonText = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        readonly TextBox animationText = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        readonly TrackBar slider = new TrackBar { Name = "frameSlider", Dock = DockStyle.Fill, Minimum = 0, Maximum = 1, TickStyle = TickStyle.None };
        readonly NumericUpDown frame = new NumericUpDown { Name = "frameNumber", Width = 85, Minimum = 0 };
        readonly NumericUpDown speed = new NumericUpDown { Name = "speed", Width = 72, DecimalPlaces = 2, Minimum = 0.05m, Maximum = 4, Increment = 0.25m, Value = 1 };
        readonly CheckBox loop = new CheckBox { Name = "loop", Text = "Loop", Checked = true, AutoSize = true };
        readonly Button play = new Button { Name = "playPause", Text = "Play", AutoSize = true };
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100, ShowAlways = true };
        readonly Timer timer = new Timer { Interval = 16 };
        readonly Stopwatch clock = new Stopwatch();
        HkxAnimationClip clip;
        HkxPoseBinding binding;
        FLVER2 model;
        public HkxPlaybackState Playback { get; private set; }
        bool updating;
        public HkxPlaybackForm()
        {
            Text = "HKX Animation"; Width = 760; Height = 350; MinimumSize = new Size(620, 350);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            root.Controls.Add(FileRow("loadSkeleton", "Skeleton HKX…", skeletonText, ChooseSkeleton), 0, 0);
            root.Controls.Add(FileRow("loadAnimation", "Load HKX…", animationText, ChooseAnimation), 0, 1);
            root.Controls.Add(slider, 0, 2);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            controls.Controls.Add(play); play.Click += (s, e) => { if (Playback == null) return; if (Playback.Playing) Pause(); else { Playback.Play(); clock.Restart(); timer.Start(); RefreshLabels(); } };
            AddButton(controls, "stop", "Stop", () => { Pause(); SeekFrame(0); });
            AddButton(controls, "previousFrame", "◀", () => SeekFrame((Playback == null ? 0 : Playback.Frame) - 1));
            AddButton(controls, "nextFrame", "▶", () => SeekFrame((Playback == null ? 0 : Playback.Frame) + 1));
            controls.Controls.Add(frame); controls.Controls.Add(new Label { Text = "Speed", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); controls.Controls.Add(speed); controls.Controls.Add(loop);
            AddButton(controls, "bindPose", "Bind Pose", () => { Pause(); Program.ClearHkxPreview(); });
            root.Controls.Add(controls, 0, 3); root.Controls.Add(status, 0, 4);
            tips.SetToolTip(Controls.Find("loadSkeleton", true).Single(), "Select the character's skeleton.hkx.\n选择角色对应的 skeleton.hkx 骨架文件。");
            tips.SetToolTip(Controls.Find("loadAnimation", true).Single(), "Load an animation HKX using the selected skeleton.\n使用已选骨架加载 HKX 动画文件。");
            tips.SetToolTip(play, "Play or pause the animation at the current frame.\n从当前帧播放动画，或暂停播放。");
            tips.SetToolTip(Controls.Find("stop", true).Single(), "Stop playback and return to frame 0.\n停止播放并返回第 0 帧。");
            tips.SetToolTip(Controls.Find("previousFrame", true).Single(), "Pause and step back one frame.\n暂停播放并后退一帧。");
            tips.SetToolTip(Controls.Find("nextFrame", true).Single(), "Pause and step forward one frame.\n暂停播放并前进一帧。");
            tips.SetToolTip(Controls.Find("bindPose", true).Single(), "Pause and clear the HKX preview to restore the static pose.\n暂停并清除 HKX 动画预览，恢复静态姿态。");
            tips.SetToolTip(loop, "Repeat the animation when it reaches the end.\n动画到达末尾后重新循环播放。");
            tips.SetToolTip(slider, "Drag to a sampled frame; seeking pauses playback.\n拖动到指定采样帧；跳帧时暂停播放。");
            tips.SetToolTip(frame, "Enter the sampled frame number (60 Hz timeline).\n输入采样帧编号（时间轴按 60 Hz 采样）。");
            tips.SetToolTip(speed, "Playback speed multiplier; 1.00 is normal speed.\n播放速度倍率；1.00 为正常速度。");
            tips.SetToolTip(skeletonText, "Path to the selected skeleton HKX.\n当前选中的骨架 HKX 文件路径。");
            tips.SetToolTip(animationText, "Path to the selected animation HKX.\n当前选中的动画 HKX 文件路径。");
            skeletonText.Text = File.Exists(skeletonPath) ? skeletonPath : "Choose skeleton.hkx first";
            status.Text = "Load a FLVER, choose its skeleton.hkx, then Load HKX.\nPreview only: no model or game files are modified.\nTimeline uses 60 Hz samples; frame 0 is the first sample.";
            slider.ValueChanged += (s, e) => { if (!updating) SeekFrame(slider.Value); };
            frame.ValueChanged += (s, e) => { if (!updating) SeekFrame((int)frame.Value); };
            speed.ValueChanged += (s, e) => { if (Playback != null) Playback.Speed = (float)speed.Value; };
            loop.CheckedChanged += (s, e) => { if (Playback != null) Playback.Loop = loop.Checked; };
            timer.Tick += (s, e) => {
                if (Playback == null || !Object.ReferenceEquals(model, Program.targetFlver)) { Pause(); Program.ClearHkxPreview(); return; }
                float elapsed = (float)clock.Elapsed.TotalSeconds; clock.Restart();
                Playback.Advance(elapsed); ApplyFrame(); if (!Playback.Playing) Pause();
            };
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); Program.ClearHkxPreview(); };
        }
        static Control FileRow(string name, string label, TextBox text, Action action)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var button = new Button { Name = name, Text = label, Dock = DockStyle.Fill }; button.Click += (s, e) => action(); row.Controls.Add(button, 0, 0); row.Controls.Add(text, 1, 0); return row;
        }
        static void AddButton(Control panel, string name, string text, Action action)
        { var b = new Button { Name = name, Text = text, AutoSize = true }; b.Click += (s, e) => action(); panel.Controls.Add(b); }
        void ChooseSkeleton()
        {
            Pause();
            using (var dlg = new OpenFileDialog { Title = "Select skeleton.hkx", Filter = "Havok skeleton (*.hkx)|*.hkx", FileName = "skeleton.hkx", InitialDirectory = ExistingDirectory(skeletonPath) })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (animationPath == null) { skeletonPath = dlg.FileName; skeletonText.Text = skeletonPath; }
                else TryLoad(dlg.FileName, animationPath);
            }
        }
        void ChooseAnimation()
        {
            Pause(); if (!File.Exists(skeletonPath)) { ChooseSkeleton(); if (!File.Exists(skeletonPath)) return; }
            using (var dlg = new OpenFileDialog { Title = "Select Bloodborne animation HKX", Filter = "Havok animation (*.hkx)|*.hkx", InitialDirectory = animationPath == null ? Path.Combine(BloodborneRoot, @"c0000_a00_hi-anibnd-dcx\chr\c0000\hkx\a000") : ExistingDirectory(animationPath) })
            { if (dlg.ShowDialog(this) == DialogResult.OK) TryLoad(skeletonPath, dlg.FileName); }
        }
        static string ExistingDirectory(string file) { var dir = Path.GetDirectoryName(file); return Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
        void TryLoad(string skeleton, string animation)
        {
            try { UseWaitCursor = true; LoadFiles(skeleton, animation); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "HKX load failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { UseWaitCursor = false; }
        }
        public void LoadFiles(string skeleton, string animation)
        {
            Pause();
            if (Program.targetFlver == null || Program.targetFlver.Nodes.Count == 0) throw new InvalidOperationException("Load a FLVER with bones first.");
            var loaded = HkxAnimationClip.Load(skeleton, animation, Program.HkxToolsPath);
            var mapped = Program.BindHkx(loaded);
            // Commit only after both conversion and matching succeed. Failed loads keep the old clip.
            clip = loaded; binding = mapped; model = Program.targetFlver; skeletonPath = skeleton; animationPath = animation;
            Playback = new HkxPlaybackState(clip.Duration, HkxAnimationClip.SampleRate) { Speed = (float)speed.Value, Loop = loop.Checked };
            skeletonText.Text = skeleton; animationText.Text = animation; slider.Maximum = Playback.LastFrame; frame.Maximum = Playback.LastFrame;
            ApplyFrame();
        }
        public void SeekFrame(int value) { if (Playback == null) return; Pause(); Playback.SeekFrame(value); ApplyFrame(); }
        public void Pause() { timer.Stop(); clock.Stop(); if (Playback != null) Playback.Pause(); RefreshLabels(); }
        void ApplyFrame()
        {
            if (!Object.ReferenceEquals(model, Program.targetFlver)) { Pause(); return; }
            Program.SetHkxPreview(model, binding.Sample(Playback.Time)); RefreshLabels();
        }
        void RefreshLabels()
        {
            if (Playback == null) return;
            updating = true; slider.Value = Playback.Frame; frame.Value = Playback.Frame; updating = false;
            play.Text = Playback.Playing ? "Pause" : "Play";
            status.Text = String.Format("Frame {0} / {1}    {2:F3} / {3:F3} s    {4} Hz samples\nMatched bones: {5}/{6}. Unmatched bones follow their FLVER parent.\n{7}", Playback.Frame, Playback.LastFrame, Playback.Time, Playback.Duration, Playback.Fps, binding.MatchedCount, model.Nodes.Count,
                binding.UnmatchedNames.Length == 0 ? "Preview only; close this window to restore the previous static pose." : "Unmatched: " + String.Join(", ", binding.UnmatchedNames.Take(12)));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }
    static partial class Program
    {
        public static HkxPlaybackForm hkxPlaybackForm;
        internal static Matrix3D[] HkxWorldPose;
        static FLVER2 hkxModel;
        static readonly object hkxPreviewLock = new object();
        static int hkxRefreshPending;
        internal static void OpenHkxPlayback()
        {
            var ui = nodeUIForm;
            if (ui == null || ui.IsDisposed || !ui.IsHandleCreated) return;
            // Same STA owner as the existing pose editor. Never parent a dialog
            // to MonoGame's window or block its message pump with Invoke.
            ui.BeginInvoke(new Action(() => {
                if (ui.IsDisposed || targetFlver == null) return;
                if (hkxPlaybackForm == null || hkxPlaybackForm.IsDisposed)
                    hkxPlaybackForm = new HkxPlaybackForm();
                if (!hkxPlaybackForm.Visible) hkxPlaybackForm.Show(ui);
            }));
        }
        public static string HkxToolsPath { get { return Path.Combine(Path.GetDirectoryName(typeof(HkxAnimationClip).Assembly.Location), "tools", "HavokToolset"); } }
        internal static HkxPoseBinding BindHkx(HkxAnimationClip clip)
        {
            var nodes = targetFlver.Nodes;
            var local = nodes.Select(n => Matrix4x4.CreateScale(n.Scale) * Matrix4x4.CreateRotationX(n.Rotation.X) * Matrix4x4.CreateRotationZ(n.Rotation.Z) * Matrix4x4.CreateRotationY(n.Rotation.Y) * Matrix4x4.CreateTranslation(n.Translation)).ToArray();
            return new HkxPoseBinding(clip, nodes.Select(n => n.Name).ToArray(), nodes.Select(n => (int)n.ParentIndex).ToArray(), local);
        }
        internal static void SetHkxPreview(FLVER2 model, Matrix4x4[] matrices)
        {
            var pose = matrices.Select(HkxPoseBinding.ToEditorMatrix).ToArray();
            lock (hkxPreviewLock) { hkxModel = model; HkxWorldPose = pose; }
            System.Threading.Interlocked.Exchange(ref hkxRefreshPending, 1);
        }
        internal static Matrix3D[] CurrentHkxPose { get { lock (hkxPreviewLock) return Object.ReferenceEquals(hkxModel, targetFlver) ? HkxWorldPose : null; } }
        internal static void ClearHkxPreview()
        {
            lock (hkxPreviewLock) { HkxWorldPose = null; hkxModel = null; }
            System.Threading.Interlocked.Exchange(ref hkxRefreshPending, 1);
        }
        internal static void ConsumeHkxRefresh()
        {
            if (System.Threading.Interlocked.Exchange(ref hkxRefreshPending, 0) != 0 && mono != null && targetFlver != null)
                UpdateVerticesCore(true);
        }
    }
}
