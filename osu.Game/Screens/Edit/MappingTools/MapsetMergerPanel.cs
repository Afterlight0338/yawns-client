// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Edit.Reference;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: Mapset Merger in the Tools tab. Search the library, click a difficulty of another set to bring it into this one.
    /// </summary>
    public partial class MapsetMergerPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly Bindable<string> search = new Bindable<string>(string.Empty);
        private FillFlowContainer results = null!;
        private OsuTextFlowContainer result = null!;
        private ScheduledDelegate? pendingSearch;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            AddRange(new Drawable[]
            {
                new FormTextBox
                {
                    Caption = "Difficulty to bring in",
                    PlaceholderText = "search your library (artist, title, mapper, difficulty)",
                    Current = search,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 300,
                    Child = results = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(2),
                    },
                },
                result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            search.BindValueChanged(_ =>
            {
                pendingSearch?.Cancel();
                pendingSearch = Scheduler.AddDelayed(updateResults, 200);
            }, true);
        }

        private void updateResults()
        {
            results.Clear();

            if (string.IsNullOrWhiteSpace(search.Value))
                return;

            Guid thisSet = editorBeatmap.BeatmapInfo.BeatmapSet?.ID ?? Guid.Empty;

            var matches = realm.Run(r => ReferencePickerPopover.Search(r, editorBeatmap.BeatmapInfo, search.Value)
                                                               .Where(b => b.BeatmapSet?.ID != thisSet)
                                                               .Select(b => (b.ID, label: $"{b.Metadata.Artist} - {b.Metadata.Title} [{b.DifficultyName}] ({b.Metadata.Author.Username})"))
                                                               .ToList());

            if (matches.Count == 0)
                results.Add(new OsuSpriteText { Text = "No other beatmaps found." });

            foreach (var (id, label) in matches)
            {
                results.Add(new OsuHoverContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Action = () => merge(id),
                    Child = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = label },
                });
            }
        }

        private void merge(Guid id)
        {
            var source = beatmapManager.QueryBeatmap(b => b.ID == id);

            if (source == null || editorBeatmap.BeatmapInfo.BeatmapSet is not BeatmapSetInfo target)
                return;

            try
            {
                var merged = MapsetMerger.Merge(beatmapManager, target, source);
                result.Text = $"Added \"{merged.DifficultyName}\" to this set, with its hitsounds. Switch to it from the difficulty menu.";
            }
            catch (Exception e)
            {
                result.Text = $"Could not merge: {e.Message}";
            }
        }
    }
}
