// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: Snapping Tools on the playfield. Draws the virtual points, lines and circles of the objects nearest the current time
    /// (selected ones excluded, so dragging them does not snap to themselves) and offers them to the composer's snapping.
    /// </summary>
    public partial class SnappingToolsOverlay : CompositeDrawable
    {
        /// <summary>
        /// How many objects around the current time the virtual objects are made from.
        /// </summary>
        public const int OBJECT_COUNT = 8;

        /// <summary>
        /// Snap distance in playfield pixels.
        /// </summary>
        public const float SNAP_RADIUS = 8;

        public readonly BindableBool Enabled = new BindableBool();

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Container shapes;
        private readonly Container marker;

        private SnappingTools.Geometry geometry = new SnappingTools.Geometry();
        private int drawnFor;

        public SnappingToolsOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                shapes = new Container { RelativeSizeAxes = Axes.Both },
                marker = new CircularContainer
                {
                    Size = new Vector2(12),
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = Color4.White,
                    Alpha = 0,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(e => this.FadeTo(e.NewValue ? 1 : 0, 200), true);
        }

        /// <summary>
        /// The snapped position in this overlay's (playfield) space, if near a virtual object.
        /// </summary>
        public Vector2? TrySnap(Vector2 position) => Enabled.Value ? SnappingTools.Snap(geometry, position, SNAP_RADIUS) : null;

        protected override void Update()
        {
            base.Update();

            if (!Enabled.Value)
                return;

            double now = editorClock.CurrentTime;
            var sources = editorBeatmap.HitObjects.OfType<OsuHitObject>()
                                       .Where(h => !editorBeatmap.SelectedHitObjects.Contains(h) && h is not Spinner)
                                       .OrderBy(h => Math.Abs(h.StartTime - now))
                                       .Take(OBJECT_COUNT)
                                       .OrderBy(h => h.StartTime)
                                       .ToList();

            int key = sources.Aggregate(sources.Count, (k, h) => HashCode.Combine(k, h, h.Position, (h as Slider)?.Path.Version.Value));

            if (key != drawnFor)
            {
                drawnFor = key;
                geometry = SnappingTools.Generate(sources.Select(toSource).ToList());
                redraw();
            }

            var cursor = ToLocalSpace(GetContainingInputManager()!.CurrentState.Mouse.Position);
            var snapped = SnappingTools.Snap(geometry, cursor, SNAP_RADIUS);

            marker.Alpha = snapped == null ? 0 : 1;
            if (snapped is Vector2 p)
                marker.Position = p;
        }

        private static SnappingTools.SnapSource toSource(OsuHitObject h)
        {
            if (h is not Slider slider)
                return new SnappingTools.SnapSource(h.Position, Array.Empty<Vector2>());

            var points = slider.Path.ControlPoints;
            var anchors = points.Skip(1).Select(p => slider.Position + p.Position).ToArray();
            var type = points.FirstOrDefault()?.Type;

            return new SnappingTools.SnapSource(slider.Position, anchors, slider.EndPosition,
                Linear: points.Count == 2 && type == PathType.LINEAR,
                PerfectCircle: points.Count == 3 && type == PathType.PERFECT_CURVE ? points.Select(p => slider.Position + p.Position).ToArray() : null);
        }

        private void redraw()
        {
            shapes.Clear();

            // ponytail: one drawable per shape, fine for the few hundred eight objects make; batch into a path if it ever stutters.
            foreach (var line in geometry.Lines)
            {
                shapes.Add(new Box
                {
                    Origin = Anchor.Centre,
                    Position = line.Project(new Vector2(256, 192)),
                    Size = new Vector2(1500, 1),
                    Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(line.Direction.Y, line.Direction.X)),
                    Colour = Color4.SkyBlue,
                    Alpha = 0.12f,
                });
            }

            foreach (var circle in geometry.Circles.Where(c => c.Radius < 1000))
            {
                shapes.Add(new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = circle.Centre,
                    Size = new Vector2(circle.Radius * 2),
                    Masking = true,
                    BorderThickness = 1,
                    BorderColour = Color4.Orange,
                    Alpha = 0.15f,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                });
            }

            foreach (var point in geometry.Points)
            {
                shapes.Add(new Circle
                {
                    Origin = Anchor.Centre,
                    Position = point,
                    Size = new Vector2(3),
                    Colour = OsuColour.Gray(0.9f),
                    Alpha = 0.5f,
                });
            }
        }
    }
}
