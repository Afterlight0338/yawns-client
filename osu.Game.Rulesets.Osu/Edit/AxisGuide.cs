// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: draws the map's four base axes through the selection, and the selection's own axis
    /// (first object to last object's end, so a slider's head to tail) in green when it is on a base axis and orange when it is not.
    /// Lines between consecutive selected objects show the axes circles imply.
    /// </summary>
    public partial class AxisGuide : CompositeDrawable
    {
        /// <summary>
        /// Lines within this many degrees of a base axis count as on it.
        /// </summary>
        public const double ON_AXIS_TOLERANCE = 3;

        public readonly BindableBool Enabled = new BindableBool();

        /// <summary>
        /// The map's tilt, detected from its straight sliders.
        /// </summary>
        public readonly BindableDouble Tilt = new BindableDouble(AxisFinder.DEFAULT_TILT);

        /// <summary>
        /// Whether <see cref="Tilt"/> was detected (false when the map has too few straight sliders and the default is used).
        /// </summary>
        public bool TiltDetected { get; private set; }

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();

        private readonly Container lines;
        private readonly OsuSpriteText label;

        private static readonly Color4 on_axis = Color4.LimeGreen;
        private static readonly Color4 off_axis = Color4.Orange;

        public AxisGuide()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                lines = new Container { RelativeSizeAxes = Axes.Both },
                label = new OsuSpriteText
                {
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    Shadow = true,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => Scheduler.AddOnce(redraw));

            // New sliders change the detected tilt. Movement is picked up every frame in Update().
            // ponytail: reshaping a slider does not re-detect the tilt, adding or removing objects (or toggling the guide) does.
            editorBeatmap.HitObjectAdded += onHitObjectAddedOrRemoved;
            editorBeatmap.HitObjectRemoved += onHitObjectAddedOrRemoved;

            Enabled.BindValueChanged(e =>
            {
                this.FadeTo(e.NewValue ? 1 : 0, 200);
                Scheduler.AddOnce(detectTilt);
            }, true);
        }

        private void onHitObjectAddedOrRemoved(HitObject _) => Scheduler.AddOnce(detectTilt);

        private (Vector2 StartSum, Vector2 EndSum, int Count) drawnFor;

        protected override void Update()
        {
            base.Update();

            if (!Enabled.Value || selectedHitObjects.Count == 0)
                return;

            // Dragging moves objects without committing until the drag ends, so follow their live positions.
            var objects = selectedHitObjects.OfType<OsuHitObject>().ToArray();
            var current = (objects.Aggregate(Vector2.Zero, (s, h) => s + h.StackedPosition), objects.Aggregate(Vector2.Zero, (s, h) => s + h.StackedEndPosition), objects.Length);

            if (current != drawnFor)
            {
                drawnFor = current;
                redraw();
            }
        }

        private void detectTilt()
        {
            if (!Enabled.Value)
                return;

            double? tilt = AxisFinder.DetectTilt(editorBeatmap.HitObjects);
            TiltDetected = tilt != null;
            Tilt.Value = tilt ?? AxisFinder.DEFAULT_TILT;
            redraw();
        }

        /// <summary>
        /// The selection's axis angle in degrees, or null when the selection has no direction (nothing, or a single circle).
        /// </summary>
        public static double? SelectionAngle(IEnumerable<HitObject> selection)
        {
            var objects = selection.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToArray();

            if (objects.Length == 0)
                return null;

            Vector2 from = objects[0].StackedPosition;
            Vector2 to = objects[^1].StackedEndPosition;

            return Vector2.Distance(from, to) < 1 ? null : AxisFinder.AngleOf(from, to);
        }

        private void redraw()
        {
            lines.Clear();
            label.Text = string.Empty;

            if (!Enabled.Value)
                return;

            var objects = selectedHitObjects.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToArray();

            if (objects.Length == 0)
                return;

            Vector2 anchor = objects[0].StackedPosition;
            double tilt = Tilt.Value;

            foreach (double axis in AxisFinder.BaseAxes(tilt))
                lines.Add(line(anchor, axis, 2000, Color4.White.Opacity(0.25f), 1.5f));

            // Implied axes between consecutive objects, for small selections where they are readable.
            if (objects.Length <= 8)
            {
                for (int i = 1; i < objects.Length; i++)
                    lines.Add(segment(objects[i - 1].StackedEndPosition, objects[i].StackedPosition, tilt, 0.6f, 2));
            }

            string tiltText = TiltDetected ? $"tilt {tilt:0}°" : $"tilt {tilt:0}° (guessed, few straight sliders)";

            if (SelectionAngle(objects) is double angle)
            {
                Vector2 from = objects[0].StackedPosition;
                Vector2 to = objects[^1].StackedEndPosition;
                lines.Add(segment(from, to, tilt, 1, 4));

                double off = AxisFinder.RotationToNearestAxis(angle, tilt);
                label.Text = Math.Abs(off) <= ON_AXIS_TOLERANCE ? $"{tiltText} · on axis" : $"{tiltText} · off by {Math.Abs(off):0}°";
                label.Colour = Math.Abs(off) <= ON_AXIS_TOLERANCE ? on_axis : off_axis;
            }
            else
            {
                label.Text = tiltText;
                label.Colour = Color4.White;
            }

            label.Position = anchor + new Vector2(12, 12);
        }

        private static Drawable segment(Vector2 from, Vector2 to, double tilt, float alpha, float thickness)
        {
            double angle = AxisFinder.AngleOf(from, to);
            bool onAxis = Math.Abs(AxisFinder.RotationToNearestAxis(angle, tilt)) <= ON_AXIS_TOLERANCE;

            var box = line((from + to) / 2, angle, Vector2.Distance(from, to), (onAxis ? on_axis : off_axis).Opacity(alpha), thickness);
            return box;
        }

        private static Drawable line(Vector2 centre, double angle, float length, Color4 colour, float thickness) => new Box
        {
            Origin = Anchor.Centre,
            Position = centre,
            Size = new Vector2(length, thickness),
            Rotation = (float)angle,
            Colour = colour,
            EdgeSmoothness = new Vector2(1),
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorBeatmap.IsNotNull())
            {
                editorBeatmap.HitObjectAdded -= onHitObjectAddedOrRemoved;
                editorBeatmap.HitObjectRemoved -= onHitObjectAddedOrRemoved;
            }
        }
    }
}
