// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: the Timing Copier as a popover in the compose screen's overlay toolbox.
    /// </summary>
    public partial class ReferenceTimingPopover : OsuPopover
    {
        public ReferenceTimingPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
            Child = new TimingCopierPanel { Width = 320 };
        }
    }

    /// <summary>
    /// YAWNS: replaces the edited beatmap's timing with the overlay map's (Mapping Tools' Timing Copier).
    /// Used by the overlay toolbox popover and the Tools tab.
    /// </summary>
    public partial class TimingCopierPanel : FillFlowContainer
    {
        [Resolved]
        private EditorReferenceBeatmap reference { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuSpriteText result = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var copier = reference.TimingCopier;

            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new FormEnumDropdown<TimingCopier.ObjectHandling>
                {
                    Caption = "Objects, bookmarks and kiai",
                    Current = copier.Objects,
                },
                new FormCheckBox
                {
                    Caption = "Snap objects to the new timing",
                    HintText = "Timing always comes from the whole overlay map, lined up by its offset.",
                    Current = copier.Resnap,
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Copy timing from the overlay map",
                    Action = copy,
                },
                result = new OsuSpriteText(),
            };
        }

        private void copy()
        {
            if (reference.Beatmap.Value is not IBeatmap beatmap)
            {
                result.Text = "Pick a map to overlay first.";
                return;
            }

            result.Text = reference.TimingCopier.Copy(beatmap, reference.Offset.Value, editorBeatmap)
                ? "Timing copied. Undo reverts it."
                : "The overlay map has no timing to copy.";
        }
    }
}
