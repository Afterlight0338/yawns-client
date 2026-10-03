// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the Rhythm Guide in the Tools tab.
    /// </summary>
    public partial class RhythmGuidePanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private readonly Dictionary<BeatmapInfo, BindableBool> chosen = new Dictionary<BeatmapInfo, BindableBool>();
        private readonly Bindable<RhythmGuide.Events> events = new Bindable<RhythmGuide.Events>();
        private readonly BindableBool newCombo = new BindableBool();

        private OsuSpriteText result = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            var others = editorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps
                                      .Where(b => !b.Equals(editorBeatmap.BeatmapInfo) && b.Ruleset.Equals(editorBeatmap.BeatmapInfo.Ruleset))
                                      .OrderBy(b => b.StarRating)
                                      .ToList() ?? new List<BeatmapInfo>();

            if (others.Count == 0)
                Add(new OsuSpriteText { Text = "This set has no other difficulties." });

            foreach (var difficulty in others)
                Add(new FormCheckBox { Caption = difficulty.DifficultyName, Current = chosen[difficulty] = new BindableBool(true) });

            AddRange(new Drawable[]
            {
                new FormEnumDropdown<RhythmGuide.Events> { Caption = "Rhythm of", Current = events },
                new FormCheckBox { Caption = "New combo on every circle", Current = newCombo },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Add rhythm guide circles to this difficulty", Action = apply },
                result = new OsuSpriteText(),
            });
        }

        private void apply()
        {
            var sources = chosen.Where(c => c.Value.Value)
                                .Select(c => beatmapManager.GetWorkingBeatmap(c.Key).GetPlayableBeatmap(editorBeatmap.BeatmapInfo.Ruleset))
                                .ToList();

            var added = RhythmGuide.AddTo(editorBeatmap, RhythmGuide.Times(sources, events.Value, editorBeatmap), newCombo.Value);
            result.Text = $"Added {added.Count} circles (selected). Undo removes them.";
        }
    }
}
