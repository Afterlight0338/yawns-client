// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: Tumour Generator on the selected sliders, keeping their durations (the slider velocity goes up with the longer path).
    /// </summary>
    public partial class TumourGeneratorPopover : OsuPopover
    {
        private readonly TumourGenerator generator;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuTextFlowContainer result = null!;

        public TumourGeneratorPopover(TumourGenerator generator)
        {
            this.generator = generator;
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
                    new FormEnumDropdown<TumourGenerator.Shape> { Caption = "Shape", Current = generator.Template },
                    new FormEnumDropdown<TumourGenerator.Sides> { Caption = "Side", Current = generator.Side },
                    new FormSliderBar<double> { Caption = "Length (px)", Current = generator.Length, TabbableContentContainer = this },
                    new FormSliderBar<double> { Caption = "Height (px)", Current = generator.Height, TabbableContentContainer = this },
                    new FormSliderBar<double> { Caption = "Distance (px)", HintText = "From the start of one tumour to the next.", Current = generator.Distance, TabbableContentContainer = this },
                    new FormSliderBar<double> { Caption = "From", HintText = "Part of the slider, 0 to 1.", Current = generator.Start, TabbableContentContainer = this },
                    new FormSliderBar<double> { Caption = "To", Current = generator.End, TabbableContentContainer = this },
                    new FormSliderBar<int> { Caption = "At most", HintText = "0: as many as fit.", Current = generator.Count, TabbableContentContainer = this },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Grow tumours on selected sliders", Action = apply },
                    result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                }
            };
        }

        private void apply()
        {
            var sliders = editorBeatmap.SelectedHitObjects.OfType<Slider>().ToList();
            var skipped = new List<string>();
            int done = 0;

            editorBeatmap.BeginChange();

            foreach (var slider in sliders)
            {
                string? problem = Grow(slider, generator, editorBeatmap);

                if (problem == null)
                    done++;
                else
                    skipped.Add(problem);
            }

            editorBeatmap.EndChange();

            result.Text = $"Grew tumours on {done} of {sliders.Count}." + (skipped.Count > 0 ? $" Skipped: {string.Join("; ", skipped.Distinct())}." : " Undo reverts it.");
        }

        /// <returns>Why the slider was left alone, or null when it was changed.</returns>
        public static string? Grow(Slider slider, TumourGenerator generator, EditorBeatmap beatmap)
        {
            var path = new List<Vector2>();
            slider.Path.GetPathToProgress(path, 0, 1);
            double oldLength = slider.Path.Distance;

            var points = generator.Generate(path);

            if (points.Count < 2)
                return "too short";

            var newPath = new SliderPath(points.Select(p => new PathControlPoint(p.Position - points[0].Position, p.Type)).ToArray());
            double newLength = newPath.CalculatedDistance;
            double multiplier = slider.SliderVelocityMultiplier * newLength / Math.Max(1, oldLength);

            if (multiplier > 10)
                return "would need over 10x slider velocity to keep its duration";

            slider.Position += points[0].Position;
            slider.Path = newPath;
            slider.SliderVelocityMultiplier = multiplier;
            beatmap.Update(slider);
            return null;
        }
    }
}
