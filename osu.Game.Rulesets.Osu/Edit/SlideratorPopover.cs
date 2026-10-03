// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: Sliderator on the selected sliders: the ball follows an easing curve along the slider (speeding up, slowing down...)
    /// in the same duration. Mapping Tools draws the speed by hand in a graph; here the curve is one of the standard easings.
    /// </summary>
    public partial class SlideratorPopover : OsuPopover
    {
        public readonly Bindable<Easing> Easing = new Bindable<Easing>(osu.Framework.Graphics.Easing.InQuad);

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuTextFlowContainer result = null!;

        public SlideratorPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new FormEnumDropdown<Easing> { Caption = "Ball movement", HintText = "\"In\" easings speed up, \"Out\" easings slow down.", Current = Easing },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Sliderate selected sliders", Action = apply },
                    result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                }
            };
        }

        private void apply()
        {
            var sliders = editorBeatmap.SelectedHitObjects.OfType<Slider>().ToList();
            int done = 0;
            var skipped = new List<string>();

            editorBeatmap.BeginChange();

            foreach (var slider in sliders)
            {
                string? problem = Sliderate(slider, Easing.Value, editorBeatmap);

                if (problem == null)
                    done++;
                else
                    skipped.Add(problem);
            }

            editorBeatmap.EndChange();

            result.Text = $"Sliderated {done} of {sliders.Count}." + (skipped.Count > 0 ? $" Skipped: {string.Join("; ", skipped.Distinct())}." : " Undo reverts it.");
        }

        /// <returns>Why the slider was left alone, or null when it was changed.</returns>
        public static string? Sliderate(Slider slider, Easing easing, EditorBeatmap beatmap)
        {
            if (slider.RepeatCount > 0)
                return "sliders with repeats";

            var relative = new List<Vector2>();
            slider.Path.GetPathToProgress(relative, 0, 1);

            double length = slider.Path.Distance;
            double duration = slider.Duration;

            if (relative.Count < 2 || length < 2 || duration <= 0)
                return "too short";

            Func<double, double> position = t => length * Interpolation.ApplyEasing(easing, Math.Clamp(t / duration, 0, 1));
            double velocity = SliderSpeed(position, duration);
            double baseVelocity = 100 * beatmap.Difficulty.SliderMultiplier / beatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength;
            double multiplier = velocity / baseVelocity;

            if (multiplier > 10)
                return "too fast at its fastest (over 10x slider velocity)";

            var core = new Sliderator { PositionFunction = position, MaxT = duration, Velocity = velocity };
            core.SetPath(relative.Select(p => slider.Position + p).ToList());
            var anchors = core.Sliderate();

            slider.Position = anchors[0];
            slider.SliderVelocityMultiplier = Math.Max(0.1, multiplier);
            slider.Path = new SliderPath(ToControlPoints(anchors), core.MaxS);

            beatmap.Update(slider);
            return null;
        }

        /// <summary>
        /// The ball has to keep up with the curve's fastest moment, so that is the slider velocity (in px/ms).
        /// </summary>
        public static double SliderSpeed(Func<double, double> position, double duration) => Math.Max(Sliderator.MaxSpeed(position, duration), 1e-3);

        /// <summary>
        /// Anchors as the .osu format lists them (a repeated point is a red anchor) to control points relative to the first.
        /// </summary>
        public static PathControlPoint[] ToControlPoints(IReadOnlyList<Vector2> anchors)
        {
            var points = new List<PathControlPoint> { new PathControlPoint(Vector2.Zero, PathType.BEZIER) };

            for (int i = 1; i < anchors.Count; i++)
            {
                if (anchors[i] == anchors[i - 1])
                {
                    points[^1].Type = PathType.BEZIER;
                    continue;
                }

                points.Add(new PathControlPoint(anchors[i] - anchors[0]));
            }

            return points.ToArray();
        }
    }
}
