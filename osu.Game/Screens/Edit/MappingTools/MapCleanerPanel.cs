// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: Map Cleaner in the Tools tab.
    /// </summary>
    public partial class MapCleanerPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        private readonly BindableBool sixteenths = new BindableBool(true);
        private readonly BindableBool twelfths = new BindableBool(true);
        private readonly BindableBool resnapObjects = new BindableBool(true);
        private readonly BindableBool resnapBookmarks = new BindableBool(true);
        private readonly BindableBool removeMuting = new BindableBool();
        private readonly BindableBool muteUnclickable = new BindableBool();
        private readonly BindableBool removeUnusedSamples = new BindableBool();

        private OsuTextFlowContainer result = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            AddRange(new Drawable[]
            {
                new FormCheckBox { Caption = "Snap to 1/16 (and 1/1, 1/2, 1/4, 1/8)", Current = sixteenths },
                new FormCheckBox { Caption = "Snap to 1/12 (and 1/3, 1/6)", Current = twelfths },
                new FormCheckBox { Caption = "Resnap objects", HintText = "Starts and ends (slider lengths are adjusted to end on the snap).", Current = resnapObjects },
                new FormCheckBox { Caption = "Resnap bookmarks", Current = resnapBookmarks },
                new FormCheckBox { Caption = "Remove muting", HintText = "Objects at 5% volume or lower get the volume of the object before them.", Current = removeMuting },
                new FormCheckBox { Caption = "Mute unclickable hitsounds", HintText = "Slider repeats and tails go to 5%.", Current = muteUnclickable },
                new FormCheckBox { Caption = "Delete unused hitsound files", HintText = "Hitsound files of the set that no difficulty plays. This cannot be undone.", Current = removeUnusedSamples },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Clean this difficulty", Action = apply },
                result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
            });
        }

        private void apply()
        {
            int[] divisors = new[] { (sixteenths.Value, 16), (twelfths.Value, 12) }.Where(d => d.Item1).Select(d => d.Item2).ToArray();
            var report = new List<string>();

            editorBeatmap.BeginChange();

            if (divisors.Length > 0 && resnapObjects.Value)
                report.Add($"resnapped {MapCleaner.Resnap(editorBeatmap, editorBeatmap.HitObjects, divisors)} objects");

            if (divisors.Length > 0 && resnapBookmarks.Value)
                report.Add($"resnapped {MapCleaner.ResnapBookmarks(editorBeatmap, divisors)} bookmarks");

            if (removeMuting.Value)
                report.Add($"unmuted {MapCleaner.Unmute(editorBeatmap, editorBeatmap.HitObjects)} objects");

            if (muteUnclickable.Value)
                report.Add($"muted the ends of {MapCleaner.MuteUnclickable(editorBeatmap)} sliders");

            editorBeatmap.EndChange();

            if (removeUnusedSamples.Value && editorBeatmap.BeatmapInfo.BeatmapSet is BeatmapSetInfo set)
            {
                var difficulties = set.Beatmaps.Where(b => !b.Equals(editorBeatmap.BeatmapInfo))
                                      .Select(b => beatmapManager.GetWorkingBeatmap(b).GetPlayableBeatmap(b.Ruleset))
                                      .Prepend(editorBeatmap);
                var unused = MapCleaner.UnusedHitsoundFiles(set, difficulties, working.Value.Storyboard).ToList();

                foreach (string file in unused)
                    beatmapManager.DeleteFile(set, set.Files.First(f => f.Filename == file));

                report.Add($"deleted {unused.Count} unused hitsound files");
            }

            result.Text = report.Count == 0 ? "Nothing to do." : $"Done: {string.Join(", ", report)}. Undo reverts everything except deleted files.";
        }
    }
}
