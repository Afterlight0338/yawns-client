// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: settings for <see cref="StreamOrganiser"/> with a live preview on the selected stream.
    /// Everything done while the popover is open is one undo step.
    /// </summary>
    public partial class StreamOrganiserPopover : OsuPopover
    {
        private readonly StreamOrganiser organiser;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IDistanceSnapProvider? distanceSnapProvider { get; set; }

        private HitCircle[] stream = null!;
        private Vector2[] originalPositions = null!;
        private double[] times = null!;

        private FormSliderBar<double> strengthSlider = null!;
        private FormSliderBar<double> followSlider = null!;

        private bool changeOpen;

        // Bound copies, so this popover's callbacks go away with it.
        private readonly Bindable<StreamOrganiser.SpeedMode> speed = new Bindable<StreamOrganiser.SpeedMode>();
        private readonly Bindable<StreamOrganiser.ShapeMode> shape = new Bindable<StreamOrganiser.ShapeMode>();
        private readonly Bindable<StreamOrganiser.SpacingSource> spacing = new Bindable<StreamOrganiser.SpacingSource>();
        private readonly BindableDouble strength = new BindableDouble();
        private readonly BindableDouble wiggle = new BindableDouble(); // YAWNS
        private readonly BindableDouble followShape = new BindableDouble();

        public StreamOrganiserPopover(StreamOrganiser organiser)
        {
            this.organiser = organiser;

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
                    new FormEnumDropdown<StreamOrganiser.ShapeMode>
                    {
                        Caption = "Shape",
                        HintText = "Clean curve: a smooth curve through the overall shape you placed (S bends and hooks kept, wobble removed). Arc: the circle that best fits all objects. Straight line.",
                        Current = organiser.Shape,
                    },
                    followSlider = new FormSliderBar<double>
                    {
                        Caption = "Follow my shape",
                        HintText = "Low: turns as evenly as possible (bends get rounder). High: stays closest to how you placed it. Wobble between neighbouring objects is always removed.",
                        Current = organiser.FollowShape,
                        TabbableContentContainer = this,
                    },
                    new FormEnumDropdown<StreamOrganiser.SpacingSource>
                    {
                        Caption = "Spacing",
                        HintText = "Fit: the first and last object stay put and the rest share the space. Distance snap: gaps follow your distance snap, the last object moves.",
                        Current = organiser.Spacing,
                    },
                    new FormEnumDropdown<StreamOrganiser.SpeedMode>
                    {
                        Caption = "Speed",
                        Current = organiser.Speed,
                    },
                    strengthSlider = new FormSliderBar<double>
                    {
                        Caption = "Strength",
                        HintText = "How many times bigger the widest gap is than the tightest.",
                        Current = organiser.Strength,
                        TabbableContentContainer = this,
                    },
                    new FormSliderBar<double>
                    {
                        Caption = "Wiggle (px)",
                        HintText = "Every other object moves this far to alternating sides of the stream line, for wiggle streams. 0 is off.",
                        Current = organiser.Wiggle,
                        TabbableContentContainer = this,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            speed.BindTo(organiser.Speed);
            shape.BindTo(organiser.Shape);
            spacing.BindTo(organiser.Spacing);
            strength.BindTo(organiser.Strength);
            wiggle.BindTo(organiser.Wiggle);
            followShape.BindTo(organiser.FollowShape);

            speed.BindValueChanged(s =>
            {
                strengthSlider.Alpha = s.NewValue == StreamOrganiser.SpeedMode.Even ? 0.5f : 1;
                apply();
            }, true);
            shape.BindValueChanged(s =>
            {
                followSlider.Alpha = s.NewValue == StreamOrganiser.ShapeMode.Clean ? 1 : 0.5f;
                apply();
            }, true);
            spacing.BindValueChanged(_ => apply());
            strength.BindValueChanged(_ => apply());
            wiggle.BindValueChanged(_ => apply());
            followShape.BindValueChanged(_ => apply());
        }

        protected override void PopIn()
        {
            base.PopIn();

            stream = editorBeatmap.SelectedHitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();
            originalPositions = stream.Select(h => h.Position).ToArray();
            times = stream.Select(h => h.StartTime).ToArray();

            editorBeatmap.BeginChange();
            changeOpen = true;
            apply();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (!changeOpen)
                return;

            changeOpen = false;
            editorBeatmap.EndChange();
        }

        private void apply()
        {
            if (!changeOpen)
                return;

            // Always from the original positions, so tweaking a setting back and forth is lossless.
            var positions = organiser.Organise(originalPositions, times,
                distanceSnapProvider == null ? null : (start, duration) => distanceSnapProvider.DurationToDistance(duration, start));

            for (int i = 0; i < stream.Length; i++)
            {
                stream[i].Position = positions[i];
                editorBeatmap.Update(stream[i]);
            }
        }
    }
}
