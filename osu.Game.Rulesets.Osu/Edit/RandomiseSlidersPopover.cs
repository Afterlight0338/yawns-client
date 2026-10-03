// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// YAWNS: random variation for the selected sliders (all sliders in the map when none are selected): turn each by a random angle, or jitter its shape.
    /// </summary>
    public partial class RandomiseSlidersPopover : OsuPopover
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableFloat maxAngle = new BindableFloat(30) { MinValue = 1, MaxValue = 180, Precision = 1 };
        private readonly BindableFloat jitter = new BindableFloat(12) { MinValue = 1, MaxValue = 60, Precision = 1 };

        private OsuTextFlowContainer status = null!;

        public RandomiseSlidersPopover()
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
                    new FormSliderBar<float> { Caption = "Rotate up to (degrees)", HintText = "Each slider turns around its head by a random angle in either direction.", Current = maxAngle, TabbableContentContainer = this },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Rotate sliders randomly", Action = () => run(rotate) },
                    new FormSliderBar<float> { Caption = "Shape jitter (px)", HintText = "Each anchor moves by up to this far. Lengths stay.", Current = jitter, TabbableContentContainer = this },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Randomise slider shapes", Action = () => run(shape) },
                    status = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                }
            };
        }

        private int rotate(Slider[] sliders) => SliderRandomiser.RotateRandomly(sliders, maxAngle.Value, new Random(), editorBeatmap.Update);

        private int shape(Slider[] sliders) => SliderRandomiser.JitterShapes(sliders, jitter.Value, new Random(), editorBeatmap.Update);

        private void run(Func<Slider[], int> action)
        {
            var sliders = editorBeatmap.SelectedHitObjects.OfType<Slider>().ToArray();
            bool all = sliders.Length == 0;

            if (all)
                sliders = editorBeatmap.HitObjects.OfType<Slider>().ToArray();

            editorBeatmap.BeginChange();
            int changed = action(sliders);
            editorBeatmap.EndChange();

            status.Text = $"{changed} of {sliders.Length} {(all ? "sliders in the map" : "selected sliders")} changed" + (changed < sliders.Length ? " (the rest would not fit on the playfield)." : ".");
        }
    }
}
