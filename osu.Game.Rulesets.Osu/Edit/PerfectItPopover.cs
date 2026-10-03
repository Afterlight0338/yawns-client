// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// YAWNS: "perfect it" (<see cref="PerfectIt"/>) on the selection: roughly placed objects move onto the exact shape, previewed live.
    /// Only the positions of the objects move (a slider moves as a whole, its shape stays). Keep makes it one undo step, Cancel puts everything back.
    /// </summary>
    public partial class PerfectItPopover : OsuPopover
    {
        public enum Mode
        {
            [Description("Regular polygon or star")]
            Polygon,

            [Description("Rotational symmetry (radial copy groups)")]
            Rotational,

            [Description("Mirror pair (second half mirrors the first)")]
            Mirror,
        }

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly Bindable<Mode> mode = new Bindable<Mode>();
        private readonly BindableInt divisions = new BindableInt(5) { MinValue = 3, MaxValue = 36 };
        private readonly BindableInt step = new BindableInt(1) { MinValue = 1, MaxValue = 17 };
        private readonly BindableInt folds = new BindableInt(2) { MinValue = 2, MaxValue = 18 };

        private List<OsuHitObject> objects = new List<OsuHitObject>();
        private Vector2[] original = Array.Empty<Vector2>();
        private OsuTextFlowContainer status = null!;
        private bool changeOpen;

        public PerfectItPopover()
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
                    new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = "Fixes objects you have ALREADY placed by hand so a shape is exact. It moves them, it does not add any (unlike the polygon generator, which makes new objects).",
                    },
                    new FormEnumDropdown<Mode> { Caption = "Make it a", Current = mode },
                    new FormSliderBar<int>
                    {
                        Caption = "Divisions",
                        HintText = "Polygon: the corners of the full shape (5 is a pentagon). Fewer objects than that fit part of it, in time order.",
                        Current = divisions,
                        TabbableContentContainer = this,
                    },
                    new FormSliderBar<int>
                    {
                        Caption = "Star step",
                        HintText = "1 goes round the polygon, 2 with 5 divisions is a pentagram.",
                        Current = step,
                        TabbableContentContainer = this,
                    },
                    new FormSliderBar<int>
                    {
                        Caption = "Folds",
                        HintText = "Rotational: the objects are this many equal groups, each the first turned by 360 / folds more.",
                        Current = folds,
                        TabbableContentContainer = this,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 5), new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Keep", Action = Hide },
                                Empty(),
                                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Cancel", Action = cancel },
                            }
                        }
                    },
                    status = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            step.MaxValue = divisions.Value - 1;
            divisions.BindValueChanged(d =>
            {
                step.MaxValue = d.NewValue - 1;
                update();
            });
            mode.BindValueChanged(_ => update());
            step.BindValueChanged(_ => update());
            folds.BindValueChanged(_ => update());
        }

        protected override void PopIn()
        {
            base.PopIn();

            objects = editorBeatmap.SelectedHitObjects.OfType<OsuHitObject>().Where(h => h is not Spinner).OrderBy(h => h.StartTime).ToList();
            original = objects.Select(h => h.Position).ToArray();

            if (objects.Count >= 3)
                divisions.Value = Math.Min(objects.Count, divisions.MaxValue);

            editorBeatmap.BeginChange();
            changeOpen = true;
            update();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (!changeOpen)
                return;

            changeOpen = false;
            editorBeatmap.EndChange();
        }

        private void update()
        {
            if (!changeOpen)
                return;

            var result = mode.Value switch
            {
                Mode.Polygon => PerfectIt.FitPolygon(original, divisions.Value, step.Value),
                Mode.Rotational => PerfectIt.FitRotational(original, folds.Value),
                _ => PerfectIt.FitMirror(original),
            };

            apply(result.Positions);

            string? problem = mode.Value switch
            {
                Mode.Polygon when objects.Count < 3 => "Select at least 3 objects.",
                Mode.Rotational when objects.Count % folds.Value != 0 || objects.Count < folds.Value => $"{objects.Count} objects do not split into {folds.Value} equal groups.",
                Mode.Mirror when objects.Count < 2 || objects.Count % 2 != 0 => "Select an even number of objects: the first half and its mirrored second half.",
                _ => null,
            };

            string what = mode.Value switch
            {
                Mode.Polygon => $"Polygon: your objects, in time order, become corners of an exact {divisions.Value}-sided regular shape (a star if the step is above 1).",
                Mode.Rotational => $"Rotational: the objects are {folds.Value} equal groups; every group becomes the first group turned by 360 / {folds.Value} degrees. Use it to clean up a radial pattern.",
                _ => "Mirror: the second half of the objects becomes the exact mirror image of the first half.",
            };

            status.Text = problem ?? $"{what}\n\nMoved {result.Moved:0.0} px on average. Keep or close to apply, Cancel to put them back.";
        }

        private void apply(IReadOnlyList<Vector2> positions)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Position == positions[i])
                    continue;

                objects[i].Position = positions[i];
                editorBeatmap.Update(objects[i]);
            }
        }

        private void cancel()
        {
            apply(original);
            Hide();
        }
    }
}
