// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: the Hitsound Copier as a popover in the compose screen's overlay toolbox.
    /// </summary>
    public partial class ReferenceHitsoundsPopover : OsuPopover
    {
        public ReferenceHitsoundsPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
            Child = new HitsoundCopierPanel { Width = 320 };
        }
    }

    /// <summary>
    /// YAWNS: copies the overlay's hitsounds onto the edited beatmap (Mapping Tools' Hitsound Copier).
    /// It copies what is overlaid: the whole map, or just the overlaid pattern where it is placed.
    /// Used by the overlay toolbox popover and the Tools tab.
    /// </summary>
    public partial class HitsoundCopierPanel : FillFlowContainer
    {
        [Resolved]
        private EditorReferenceBeatmap reference { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuSpriteText result = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var copier = reference.HitsoundCopier;

            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new FormSliderBar<double>
                {
                    Caption = "Temporal leniency (ms)",
                    HintText = "How far apart two hitsounds can be and still count as the same moment.",
                    Current = copier.TemporalLeniency,
                    TabbableContentContainer = this,
                },
                new FormCheckBox
                {
                    Caption = "Overwrite everything",
                    HintText = "Hitsounds with no counterpart in the overlay lose their additions and take the overlay's sample set and volume at that moment.",
                    Current = copier.OverwriteEverything,
                },
                new FormCheckBox
                {
                    Caption = "Copy sample sets",
                    HintText = "Banks and custom sample indices. When off, only whistles, finishes and claps are copied.",
                    Current = copier.CopySampleSets,
                },
                new FormCheckBox
                {
                    Caption = "Copy volumes",
                    Current = copier.CopyVolumes,
                },
                new FormCheckBox
                {
                    Caption = "Copy slider body hitsounds",
                    Current = copier.CopySliderBodyHitsounds,
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Copy hitsounds from the overlay",
                    Action = copy,
                },
                result = new OsuSpriteText(),
            };
        }

        private void copy()
        {
            if (reference.Beatmap.Value == null)
            {
                result.Text = "Pick a map to overlay first.";
                return;
            }

            // Copy what is overlaid: the whole map, or just the overlaid pattern where it is placed.
            int matched = reference.HitsoundCopier.Copy(reference.DisplayedObjects, reference.DisplayOffset.Value, editorBeatmap);
            result.Text = $"Copied onto {matched} hitsound{(matched == 1 ? "" : "s")}. Undo reverts it.";
        }
    }
}
