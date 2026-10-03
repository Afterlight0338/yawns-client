// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Models;
using osu.Game.Rulesets;
using osuTK;
using Realms;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: picks the map to overlay from the local library and sets its offset.
    /// With an empty search, the other difficulties of the beatmap set being edited are listed.
    /// </summary>
    public partial class ReferencePickerPopover : OsuPopover
    {
        private const int max_results = 50;

        [Resolved]
        private EditorReferenceBeatmap reference { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private readonly Bindable<string> search = new Bindable<string>(string.Empty);
        private readonly Bindable<IBeatmap?> currentReference = new Bindable<IBeatmap?>();
        private readonly Bindable<(double Start, double End)?> pattern = new Bindable<(double Start, double End)?>();

        private TruncatingSpriteText currentText = null!;
        private RoundedButton wholeMapButton = null!;
        private FillFlowContainer results = null!;
        private ScheduledDelegate? pendingSearch;

        public ReferencePickerPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            currentReference.BindTo(reference.Beatmap);
            pattern.BindTo(reference.Pattern);

            Child = new FillFlowContainer
            {
                Width = 380,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    currentText = new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                    },
                    new FormSliderBar<double>
                    {
                        Caption = "Offset (ms)",
                        HintText = "Lines the overlay map up with yours. Positive moves it later, negative earlier.",
                        Current = reference.Offset,
                        TabbableContentContainer = this,
                    },
                    new FormTextBox
                    {
                        Caption = "Pick a map to overlay",
                        PlaceholderText = "search your library (empty: other difficulties of this set)",
                        Current = search,
                        TabbableContentContainer = this,
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
                    wholeMapButton = new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Overlay the whole map again",
                        Action = reference.ShowWholeMap,
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Stop overlaying",
                        Action = reference.Clear,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentReference.BindValueChanged(_ => updateStatus());
            pattern.BindValueChanged(_ => updateStatus(), true);

            search.BindValueChanged(_ =>
            {
                pendingSearch?.Cancel();
                pendingSearch = Scheduler.AddDelayed(updateResults, 200);
            }, true);
        }

        private void updateStatus()
        {
            if (currentReference.Value == null)
                currentText.Text = "Nothing overlaid";
            else if (pattern.Value is (double start, double end))
                currentText.Text = $"Overlaying a pattern ({start.ToEditorFormattedString()} to {end.ToEditorFormattedString()}) of {describe(currentReference.Value.BeatmapInfo)}";
            else
                currentText.Text = $"Overlaying {describe(currentReference.Value.BeatmapInfo)}";

            wholeMapButton.Enabled.Value = pattern.Value != null;
        }

        private void updateResults()
        {
            var matches = realm.Run(r => Search(r, editorBeatmap.BeatmapInfo, search.Value)
                                         .Select(b => (b.ID, label: b.ID == editorBeatmap.BeatmapInfo.ID ? $"{describe(b)} (this difficulty, as last saved)" : describe(b)))
                                         .ToList());

            results.Clear();

            if (matches.Count == 0)
            {
                results.Add(new OsuSpriteText { Text = "No beatmaps found." });
                return;
            }

            foreach (var (id, label) in matches)
            {
                results.Add(new OsuHoverContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Action = () =>
                    {
                        var beatmapInfo = beatmapManager.QueryBeatmap(b => b.ID == id);

                        if (beatmapInfo != null)
                            reference.Load(beatmapInfo);
                    },
                    Child = new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = label,
                    },
                });
            }
        }

        private static string describe(BeatmapInfo b) => $"{b.Metadata.Artist} - {b.Metadata.Title} [{b.DifficultyName}] ({b.Metadata.Author.Username})";

        private static readonly string[] searchable_fields =
        {
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.Title)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.TitleUnicode)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.Artist)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.ArtistUnicode)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.Source)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.Tags)}",
            $"{nameof(BeatmapInfo.Metadata)}.{nameof(BeatmapMetadata.Author)}.{nameof(RealmUser.Username)}",
            nameof(BeatmapInfo.DifficultyName),
        };

        /// <summary>
        /// Finds reference candidates for <paramref name="editing"/>: beatmaps of the same ruleset where every search term matches some metadata field.
        /// An empty search returns the difficulties of the same beatmap set, the edited one included (for overlaying a pattern of it elsewhere).
        /// </summary>
        public static IEnumerable<BeatmapInfo> Search(Realm r, BeatmapInfo editing, string searchText)
        {
            var beatmaps = r.All<BeatmapInfo>().Filter($"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0"
                                                       + $" AND {nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false"
                                                       + $" AND {nameof(BeatmapInfo.Hidden)} == false", editing.Ruleset.ShortName);

            string[] terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length == 0)
            {
                // The usual comparison is against a difficulty of the same set.
                return beatmaps.Filter($"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.ID)} == $0", editing.BeatmapSet?.ID ?? Guid.Empty)
                               .AsEnumerable()
                               .OrderBy(b => b.StarRating);
            }

            string anyField = string.Join(" OR ", searchable_fields.Select(f => $"{f} CONTAINS[c] $0"));

            foreach (string term in terms)
                beatmaps = beatmaps.Filter(anyField, term);

            // ponytail: in-memory sort over a capped candidate set, move to a realm SORT if large libraries feel slow.
            return beatmaps.AsEnumerable()
                           .Take(500)
                           .OrderBy(b => b.Metadata.Artist, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(b => b.Metadata.Title, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(b => b.StarRating)
                           .Take(max_results);
        }
    }
}
