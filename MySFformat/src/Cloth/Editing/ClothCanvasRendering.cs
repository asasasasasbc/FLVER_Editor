using System;
using System.Drawing;
using System.Numerics;

namespace MySFformat
{
    public sealed partial class ClothCanvas
    {
        // 绘制分层：三角网格、参考/动画碰撞体、粒子、选区范围、骨架叠加。
        // 此处只读数据，不能把显示用的变换回写到作者文档。
        void DrawWorldAxes(Graphics graphics)
        {
            // Same positive FLVER-space axes as Mono3D.DrawGround (its XZY
            // display conversion must not be applied to these Y-up positions).
            var origin = Project(Vector3.Zero);
            graphics.DrawLine(Pens.Red, origin, Project(Vector3.UnitX * 10));
            graphics.DrawLine(Pens.Yellow, origin, Project(Vector3.UnitY * 10));
            graphics.DrawLine(Pens.Blue, origin, Project(Vector3.UnitZ * 10));
        }

        void DrawAxisIndicator(Graphics graphics)
        {
            // Keep orientation readable even when the world origin is off-screen.
            // Rotation is shared with all geometry; pan and zoom do not affect it.
            var origin = new PointF(60, Height - 60);
            var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            var colors = new[] { Color.Red, Color.Yellow, Color.Blue };
            var labels = new[] { "+X", "+Y", "+Z" };
            for (int i = 0; i < axes.Length; i++)
            {
                var direction = Vector3.TransformNormal(axes[i], ViewTransform);
                var end = new PointF(origin.X + direction.X * 38, origin.Y - direction.Y * 38);
                using (var pen = new Pen(colors[i], 2))
                using (var brush = new SolidBrush(colors[i]))
                {
                    graphics.DrawLine(pen, origin, end);
                    graphics.FillEllipse(brush, end.X - 2, end.Y - 2, 4, 4);
                    graphics.DrawString(labels[i], Font, brush, end.X + 3, end.Y - 8);
                }
            }
        }

        void DrawClothTriangles(Graphics graphics, Vector3[] positions)
        {
            if (ClothRenderMode == 1) return;
            using (var pen = new Pen(Color.FromArgb(100, 150, 175)))
            {
                for (int i = 0; i < Domain.Triangles.Length; i += 3)
                {
                    var a = Project(positions[Domain.Triangles[i]]);
                    var b = Project(positions[Domain.Triangles[i + 1]]);
                    var c = Project(positions[Domain.Triangles[i + 2]]);
                    graphics.DrawLine(pen, a, b);
                    graphics.DrawLine(pen, b, c);
                    graphics.DrawLine(pen, c, a);
                }
            }
        }

        void DrawClothColliders(Graphics graphics)
        {
            if (!ShowColliders) return;
            using (var pen = new Pen(Color.FromArgb(180, 255, 150, 50)))
            {
                foreach (var collider in AnimationColliders ?? Domain.Colliders)
                {
                    var axis = collider.End - collider.Start;
                    var unit = axis.LengthSquared() > 1e-12f ? Vector3.Normalize(axis) : Vector3.UnitY;
                    var tangent = Vector3.Normalize(Vector3.Cross(unit,
                        Math.Abs(unit.Y) < .9f ? Vector3.UnitY : Vector3.UnitX));
                    var bitangent = Vector3.Cross(unit, tangent);
                    DrawColliderRing(graphics, pen, collider.Start, collider.RadiusA, tangent, bitangent);
                    DrawColliderRing(graphics, pen, collider.End, collider.RadiusB, tangent, bitangent);
                    DrawColliderRing(graphics, pen, collider.Start, collider.RadiusA, tangent, unit);
                    DrawColliderRing(graphics, pen, collider.Start, collider.RadiusA, bitangent, unit);
                    DrawColliderRing(graphics, pen, collider.End, collider.RadiusB, tangent, unit);
                    DrawColliderRing(graphics, pen, collider.End, collider.RadiusB, bitangent, unit);
                    for (int side = 0; side < 4; side++)
                    {
                        float angle = side * (float)Math.PI / 2;
                        var direction = tangent * (float)Math.Cos(angle) + bitangent * (float)Math.Sin(angle);
                        graphics.DrawLine(pen,
                            Project(collider.Start + direction * collider.RadiusA),
                            Project(collider.End + direction * collider.RadiusB));
                    }
                }
            }
        }

        void DrawColliderRing(Graphics graphics, Pen pen, Vector3 center, float radius, Vector3 first, Vector3 second)
        {
            for (int segment = 0; segment < 32; segment++)
            {
                float angle = segment * (float)Math.PI / 16;
                float next = (segment + 1) * (float)Math.PI / 16;
                var a = center + radius * (first * (float)Math.Cos(angle) + second * (float)Math.Sin(angle));
                var b = center + radius * (first * (float)Math.Cos(next) + second * (float)Math.Sin(next));
                graphics.DrawLine(pen, Project(a), Project(b));
            }
        }

        void DrawClothParticles(Graphics graphics, Vector3[] positions)
        {
            if (ShowParticles && ClothRenderMode != 1)
                for (int i = 0; i < positions.Length; i++)
                {
                    var point = Project(positions[i]);
                    graphics.FillEllipse(Domain.Fixed[i] ? Brushes.Gold : Brushes.Cyan,
                        point.X - 2, point.Y - 2, 4, 4);
                }
            foreach (int id in SelectedParticles)
            {
                if (id < 0 || id >= positions.Length) continue;
                var point = Project(positions[id]);
                graphics.FillEllipse(Brushes.White, point.X - 3, point.Y - 3, 6, 6);
                graphics.DrawEllipse(Pens.Magenta, point.X - 5, point.Y - 5, 10, 10);
            }
        }

        void DrawSelectedRanges(Graphics graphics)
        {
            if (!ShowRange) return;
            // 大选区降低线条透明度，不填充多选范围：避免大量重叠把视图染成实色。
            int alpha = Math.Max(12, (int)(100 / Math.Sqrt(Math.Max(1, SelectedParticles.Count))));
            using (var pen = new Pen(Color.FromArgb(alpha, Color.Lime)))
            using (var fill = new SolidBrush(Color.FromArgb(3, Color.Lime)))
                foreach (int id in SelectedParticles)
                {
                    if (id < 0 || id >= Domain.Ranges.Length || Domain.Ranges[id] <= 0) continue;
                    var anchor = Project(Domain.Positions[id]);
                    float radius = Domain.Ranges[id] * PixelScale;
                    if (float.IsInfinity(radius) || float.IsNaN(radius) || radius > 1000000) continue;
                    if (SelectedParticles.Count == 1)
                        graphics.FillEllipse(fill, anchor.X - radius, anchor.Y - radius, radius * 2, radius * 2);
                    graphics.DrawEllipse(pen, anchor.X - radius, anchor.Y - radius, radius * 2, radius * 2);
                }
        }

        void DrawAnimationSkeleton(Graphics graphics)
        {
            if (AnimationBones == null || AnimationParents == null) return;
            for (int i = 0; i < AnimationBones.Length; i++)
            {
                var point = Project(AnimationBones[i]);
                int parent = AnimationParents[i];
                if (parent >= 0 && parent < AnimationBones.Length)
                    graphics.DrawLine(Pens.Plum, point, Project(AnimationBones[parent]));
                graphics.FillEllipse(Brushes.Plum, point.X - 2, point.Y - 2, 4, 4);
            }
        }
    }
}
