// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: settings for <see cref="SliderCompletionator"/>, applied to the selected sliders as one undo step.
    /// </summary>
    public partial class SliderCompletionatorPopover : OsuPopover
    {
        private readonly SliderCompletionator tool;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private OsuSpriteText result = null!;

        public SliderCompletionatorPopover(SliderCompletionator tool)
        {
            this.tool = tool;
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
                    new FormEnumDropdown<SliderCompletionator.DurationSource> { Caption = "Duration", Current = tool.Duration },
                    new FormSliderBar<double> { Caption = "Beats per span", Current = tool.Beats, TabbableContentContainer = this },
                    new FormSliderBar<double>
                    {
                        Caption = "Length",
                        HintText = "Relative to the whole path as drawn (1 uses all of it).",
                        Current = tool.Length,
                        TabbableContentContainer = this,
                    },
                    new FormEnumDropdown<SliderCompletionator.FreeVariable> { Caption = "Then", Current = tool.Free },
                    new FormSliderBar<double> { Caption = "Velocity to keep", HintText = "Used when working out the length.", Current = tool.Velocity, TabbableContentContainer = this },
                    new FormCheckBox { Caption = "Scale the control points to the new length", Current = tool.MoveAnchors },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Complete selected sliders", Action = apply },
                    result = new OsuSpriteText(),
                }
            };
        }

        private void apply()
        {
            var (completed, clamped) = tool.Apply(editorBeatmap.SelectedHitObjects.OfType<Slider>(), editorBeatmap, editorClock.CurrentTime);

            result.Text = clamped > 0
                ? $"Completed {completed}; {clamped} needed a velocity outside 0.1x to 10x, so their length changed instead."
                : $"Completed {completed} slider{(completed == 1 ? "" : "s")}. Undo reverts it.";
        }
    }
}
