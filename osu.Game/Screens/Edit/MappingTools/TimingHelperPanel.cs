// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the Timing Helper in the Tools tab.
    /// </summary>
    public partial class TimingHelperPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly TimingHelper helper = new TimingHelper();
        private OsuTextFlowContainer result = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            AddRange(new Drawable[]
            {
                new FormCheckBox { Caption = "Objects are markers", Current = helper.UseObjects },
                new FormCheckBox { Caption = "Bookmarks are markers", Current = helper.UseBookmarks },
                new FormCheckBox { Caption = "Red lines are markers", HintText = "When off, every red line but the first is removed and placed again.", Current = helper.UseRedLines },
                new FormCheckBox { Caption = "Omit the first bar line of new red lines", Current = helper.OmitFirstBarLine },
                new FormSliderBar<double> { Caption = "Leniency (ms)", HintText = "How far off a marker may end up.", Current = helper.Leniency, TabbableContentContainer = this },
                new FormSliderBar<double>
                {
                    Caption = "Beats between markers",
                    HintText = "0 works it out from the current timing, which then has to be close already. Set it when every marker is, say, one beat apart.",
                    Current = helper.BeatsBetween,
                    TabbableContentContainer = this,
                },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Time this difficulty", Action = () => result.Text = $"Added {helper.Apply(editorBeatmap)} red lines and adjusted the BPM of the rest. One undo step." },
                result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
            });
        }
    }
}
