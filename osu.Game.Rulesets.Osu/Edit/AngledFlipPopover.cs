// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
    /// YAWNS: settings for <see cref="AngledFlip"/> with a live preview and the mirror line drawn.
    /// Flipping in place only moves the selected objects (cheap, no objects are added or removed while you adjust it).
    /// "Keep original" adds a mirrored copy instead. Keep (or closing) makes it one undo step, Cancel puts everything back.
    /// </summary>
    public partial class AngledFlipPopover : OsuPopover
    {
        private readonly AngledFlip flip;
        private readonly SymmetryCentre centre;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableFloat angle = new BindableFloat();
        private readonly BindableBool anticlockwise = new BindableBool();
        private readonly BindableBool keep = new BindableBool();
        private readonly BindableDouble beats = new BindableDouble();
        private readonly Bindable<SymmetryCentre.Mode> around = new Bindable<SymmetryCentre.Mode>();
        private readonly Bindable<Vector2> guide = new Bindable<Vector2>();

        private List<OsuHitObject> objects = new List<OsuHitObject>();
        private Vector2[] originalPositions = Array.Empty<Vector2>();
        private Vector2[][] originalPaths = Array.Empty<Vector2[]>();
        private List<OsuHitObject> copies = new List<OsuHitObject>();
        private OsuTextFlowContainer status = null!;
        private bool changeOpen;

        public AngledFlipPopover(AngledFlip flip, SymmetryCentre centre)
        {
            this.flip = flip;
            this.centre = centre;
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
                        Text = "Mirrors the selected objects across the yellow line, like a mirror held at that angle. Nothing is added unless you tick \"Keep original\".",
                    },
                    new FormSliderBar<float>
                    {
                        Caption = "Line angle",
                        HintText = "0 is horizontal (flips top to bottom), 90 is vertical (flips left to right), 45 swaps x and y.",
                        Current = flip.Angle,
                        TabbableContentContainer = this,
                    },
                    new FormCheckBox { Caption = "Anticlockwise", Current = flip.Anticlockwise },
                    new FormEnumDropdown<SymmetryCentre.Mode>
                    {
                        Caption = "Line goes through",
                        Current = centre.Around,
                    },
                    new FormCheckBox
                    {
                        Caption = "Keep original (mirror copy)",
                        HintText = "Adds the mirrored objects after the originals instead of flipping them.",
                        Current = flip.KeepOriginal,
                    },
                    new FormSliderBar<double>
                    {
                        Caption = "Beats to the copy",
                        HintText = "Only for a mirror copy: how long after the originals the copy starts.",
                        Current = flip.Beats,
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

            angle.BindTo(flip.Angle);
            anticlockwise.BindTo(flip.Anticlockwise);
            keep.BindTo(flip.KeepOriginal);
            beats.BindTo(flip.Beats);
            around.BindTo(centre.Around);
            guide.BindTo(centre.Guide);

            // Sliders fire many changes while dragged: do the work once per frame.
            angle.BindValueChanged(_ => Scheduler.AddOnce(update));
            anticlockwise.BindValueChanged(_ => Scheduler.AddOnce(update));
            keep.BindValueChanged(_ => Scheduler.AddOnce(update));
            beats.BindValueChanged(_ => Scheduler.AddOnce(update));
            around.BindValueChanged(_ => Scheduler.AddOnce(update));
            guide.BindValueChanged(_ => Scheduler.AddOnce(update));
        }

        protected override void PopIn()
        {
            base.PopIn();

            objects = editorBeatmap.SelectedHitObjects.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToList();
            originalPositions = objects.Select(h => h.Position).ToArray();
            originalPaths = objects.Select(h => h is Slider s ? s.Path.ControlPoints.Select(p => p.Position).ToArray() : Array.Empty<Vector2>()).ToArray();
            copies = new List<OsuHitObject>();

            editorBeatmap.BeginChange();
            changeOpen = true;
            update();
        }

        protected override void PopOut()
        {
            base.PopOut();
            flip.GuideCentre.Value = null;

            if (!changeOpen)
                return;

            changeOpen = false;

            if (copies.Count > 0)
            {
                editorBeatmap.SelectedHitObjects.Clear();
                editorBeatmap.SelectedHitObjects.AddRange(copies);
            }

            editorBeatmap.EndChange();
        }

        private void update()
        {
            if (!changeOpen || objects.Count == 0)
                return;

            Vector2 middle = centre.Resolve(originalPositions);
            flip.GuideCentre.Value = middle;

            editorBeatmap.RemoveRange(copies);
            copies = new List<OsuHitObject>();

            if (flip.KeepOriginal.Value)
            {
                restoreOriginals();

                double beatLength = editorBeatmap.ControlPointInfo.TimingPointAt(objects.Min(h => h.StartTime)).BeatLength;
                copies = flip.Create(objects, middle, beatLength);
                editorBeatmap.AddRange(copies);
            }
            else
            {
                var map = Symmetry.Linear.Reflection(flip.LineAngle);

                for (int i = 0; i < objects.Count; i++)
                {
                    objects[i].Position = map.ApplyAround(middle, originalPositions[i]);

                    if (objects[i] is Slider slider)
                    {
                        for (int p = 0; p < originalPaths[i].Length; p++)
                            slider.Path.ControlPoints[p].Position = map.Apply(originalPaths[i][p]);
                    }

                    editorBeatmap.Update(objects[i]);
                }
            }

            status.Text = flip.KeepOriginal.Value ? $"{copies.Count} mirrored objects added." : $"{objects.Count} objects flipped in place. Ctrl+Z undoes it after closing.";
        }

        private void restoreOriginals()
        {
            for (int i = 0; i < objects.Count; i++)
            {
                objects[i].Position = originalPositions[i];

                if (objects[i] is Slider slider)
                {
                    for (int p = 0; p < originalPaths[i].Length; p++)
                        slider.Path.ControlPoints[p].Position = originalPaths[i][p];
                }

                editorBeatmap.Update(objects[i]);
            }
        }

        private void cancel()
        {
            editorBeatmap.RemoveRange(copies);
            copies = new List<OsuHitObject>();
            restoreOriginals();
            Hide();
        }
    }
}
