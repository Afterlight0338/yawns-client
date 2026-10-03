// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
    /// YAWNS: a popover that adds copies of the selection with a live preview: the copies are on the playfield while the settings change.
    /// Everything done while it is open is one undo step. Closing keeps the copies (selected), Cancel removes them.
    /// </summary>
    public abstract partial class CopyPreviewPopover : OsuPopover
    {
        [Resolved]
        protected EditorBeatmap EditorBeatmap { get; private set; } = null!;

        /// <summary>
        /// The selection when the popover opened, in time order.
        /// </summary>
        protected IReadOnlyList<OsuHitObject> Original { get; private set; } = new List<OsuHitObject>();

        private List<OsuHitObject> preview = new List<OsuHitObject>();
        private OsuSpriteText status = null!;
        private bool changeOpen;

        protected CopyPreviewPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        protected abstract IEnumerable<Drawable> CreateSettings();

        /// <summary>
        /// The copies for the current settings, made from <see cref="Original"/>.
        /// </summary>
        protected abstract List<OsuHitObject> CreateCopies();

        /// <summary>
        /// A line under the result count, for example what the settings make.
        /// </summary>
        protected virtual string Describe() => string.Empty;

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = CreateSettings().Concat(new Drawable[]
                {
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
                    status = new OsuSpriteText(),
                }).ToArray(),
            };
        }

        protected override void PopIn()
        {
            base.PopIn();

            Original = EditorBeatmap.SelectedHitObjects.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToList();
            preview = new List<OsuHitObject>();

            EditorBeatmap.BeginChange();
            changeOpen = true;
            UpdatePreview();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (!changeOpen)
                return;

            changeOpen = false;

            if (preview.Count > 0)
            {
                EditorBeatmap.SelectedHitObjects.Clear();
                EditorBeatmap.SelectedHitObjects.AddRange(preview);
            }

            EditorBeatmap.EndChange();
        }

        /// <summary>
        /// Replaces the preview copies with ones for the current settings. Call it when a setting changes.
        /// </summary>
        protected void UpdatePreview() => Scheduler.AddOnce(updatePreview); // sliders fire many changes while dragged: once per frame

        private void updatePreview()
        {
            if (!changeOpen)
                return;

            EditorBeatmap.RemoveRange(preview);
            preview = Original.Count == 0 ? new List<OsuHitObject>() : CreateCopies();
            EditorBeatmap.AddRange(preview);

            string description = Describe();
            status.Text = $"{preview.Count} objects{(description.Length > 0 ? $", {description}" : string.Empty)}. Close or Keep adds them.";
        }

        private void cancel()
        {
            EditorBeatmap.RemoveRange(preview);
            preview = new List<OsuHitObject>();
            Hide();
        }
    }
}
