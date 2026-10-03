// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: <see cref="SvEqualiser"/> on the selected sliders (every slider when none are selected): they keep the speed they would have at a reference BPM.
    /// It changes velocities each time it is used, so apply it once after BPM changes (undo is one step).
    /// </summary>
    public partial class SvEqualiserPopover : OsuPopover
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableDouble reference = new BindableDouble(120) { MinValue = 20, MaxValue = 1000, Precision = 0.1 };
        private OsuTextFlowContainer status = null!;

        public SvEqualiserPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // The reference is the BPM at the first slider in play, which is where the map's speed was set.
            var first = (editorBeatmap.SelectedHitObjects.OfType<Slider>().OrderBy(s => s.StartTime).FirstOrDefault() ?? editorBeatmap.HitObjects.OfType<Slider>().OrderBy(s => s.StartTime).FirstOrDefault());

            if (first != null)
                reference.Value = editorBeatmap.ControlPointInfo.TimingPointAt(first.StartTime).BPM;

            Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new FormSliderBar<double>
                    {
                        Caption = "Reference BPM",
                        HintText = "Sliders end up as fast as they would be at this BPM with their current velocity. Starts as the BPM at the first slider.",
                        Current = reference,
                        TabbableContentContainer = this,
                    },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Equalise slider velocity", Action = apply },
                    status = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = "Selected sliders, or every slider when none are selected. Use it once: it changes the velocities each time." },
                }
            };
        }

        private void apply()
        {
            var sliders = editorBeatmap.SelectedHitObjects.OfType<Slider>().ToArray();
            bool all = sliders.Length == 0;

            if (all)
                sliders = editorBeatmap.HitObjects.OfType<Slider>().ToArray();

            int changed = 0, outOfRange = 0;

            editorBeatmap.BeginChange();

            foreach (var slider in sliders)
            {
                double bpm = editorBeatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BPM;

                if (SvEqualiser.Equalised(slider.SliderVelocityMultiplier, reference.Value, bpm) is not double equalised)
                {
                    outOfRange++;
                    continue;
                }

                if (equalised == slider.SliderVelocityMultiplier)
                    continue;

                slider.SliderVelocityMultiplier = equalised;
                editorBeatmap.Update(slider);
                changed++;
            }

            editorBeatmap.EndChange();

            status.Text = $"{changed} of {sliders.Length} {(all ? "sliders in the map" : "selected sliders")} changed" + (outOfRange > 0 ? $", {outOfRange} would need a velocity outside 0.1 to 10 and were left alone." : ".");
        }
    }
}
