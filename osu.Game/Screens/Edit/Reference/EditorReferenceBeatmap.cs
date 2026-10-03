// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.MappingTools;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: another beatmap shown under the edited one for comparison. It is read-only and never saved.
    /// One instance lives for the whole editor session, so the reference survives switching between editor screens.
    /// </summary>
    public class EditorReferenceBeatmap
    {
        /// <summary>
        /// The reference converted to the edited beatmap's ruleset, or null when none is loaded.
        /// </summary>
        public readonly Bindable<IBeatmap?> Beatmap = new Bindable<IBeatmap?>();

        /// <summary>
        /// How far the reference is moved in time, in milliseconds. Positive values move it later.
        /// </summary>
        public readonly BindableDouble Offset = new BindableDouble
        {
            MinValue = -10000,
            MaxValue = 10000,
            Precision = 1,
        };

        /// <summary>
        /// Opacity of the reference objects. Zero hides the reference without unloading it.
        /// </summary>
        public readonly BindableFloat Opacity = new BindableFloat(0.4f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.05f,
        };

        /// <summary>
        /// When set, only the reference objects starting in this range (the reference's own time) are overlaid, placed by <see cref="PatternOffset"/>.
        /// </summary>
        public readonly Bindable<(double Start, double End)?> Pattern = new Bindable<(double Start, double End)?>();

        /// <summary>
        /// How far the overlaid <see cref="Pattern"/> is moved in time, in milliseconds.
        /// </summary>
        public readonly BindableDouble PatternOffset = new BindableDouble();

        /// <summary>
        /// The time offset the overlay is shown with: <see cref="PatternOffset"/> while a pattern is overlaid, <see cref="Offset"/> otherwise.
        /// </summary>
        public IBindable<double> DisplayOffset => displayOffset;

        private readonly BindableDouble displayOffset = new BindableDouble();

        /// <summary>
        /// Mark hitsounds of the edited beatmap and the reference which have no counterpart in the other, on the timeline rhythm lanes.
        /// </summary>
        public readonly BindableBool HighlightRhythmDifferences = new BindableBool(true);

        /// <summary>
        /// Settings for copying the reference's hitsounds onto the edited beatmap.
        /// </summary>
        public readonly HitsoundCopier HitsoundCopier = new HitsoundCopier();

        /// <summary>
        /// Settings for copying the reference's timing onto the edited beatmap.
        /// </summary>
        public readonly TimingCopier TimingCopier = new TimingCopier();

        private readonly BeatmapManager beatmapManager;
        private readonly IRulesetInfo ruleset;

        public EditorReferenceBeatmap(BeatmapManager beatmapManager, IRulesetInfo ruleset)
        {
            this.beatmapManager = beatmapManager;
            this.ruleset = ruleset;

            Offset.BindValueChanged(_ => updateDisplayOffset());
            PatternOffset.BindValueChanged(_ => updateDisplayOffset());
            Pattern.BindValueChanged(_ => updateDisplayOffset());

            // A newly loaded reference is overlaid whole.
            Beatmap.BindValueChanged(_ => Pattern.Value = null);
        }

        private void updateDisplayOffset() => displayOffset.Value = Pattern.Value == null ? Offset.Value : PatternOffset.Value;

        /// <summary>
        /// Whether a reference object is overlaid (always, unless only a <see cref="Pattern"/> is).
        /// </summary>
        public bool IsDisplayed(HitObject hitObject) => Pattern.Value is not (double start, double end) || (hitObject.StartTime >= start && hitObject.StartTime <= end);

        /// <summary>
        /// The reference objects which are overlaid.
        /// </summary>
        public IEnumerable<HitObject> DisplayedObjects => Beatmap.Value?.HitObjects.Where(IsDisplayed) ?? Enumerable.Empty<HitObject>();

        /// <summary>
        /// Overlays only the reference objects starting between <paramref name="start"/> and <paramref name="end"/> (the reference's own time),
        /// with the first of them at <paramref name="time"/> of the edited beatmap.
        /// </summary>
        public void OverlayPattern(double start, double end, double time)
        {
            var first = Beatmap.Value?.HitObjects.Where(h => h.StartTime >= start && h.StartTime <= end).MinBy(h => h.StartTime);

            if (first == null)
                return;

            PatternOffset.Value = time - first.StartTime;
            Pattern.Value = (start, end);
        }

        /// <summary>
        /// Goes back to overlaying the whole reference, aligned by <see cref="Offset"/>.
        /// </summary>
        public void ShowWholeMap() => Pattern.Value = null;

        // ponytail: converts on the update thread, so a marathon map can hitch for a moment when picked. Move to a background task if that gets annoying.
        public void Load(BeatmapInfo beatmapInfo)
        {
            try
            {
                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo).GetPlayableBeatmap(ruleset);
            }
            catch (Exception e)
            {
                // Logged errors are also shown to the user as a notification.
                Logger.Error(e, $"Could not load {beatmapInfo} as an overlay.");
            }
        }

        public void Clear() => Beatmap.Value = null;

        /// <summary>
        /// What carries over to the next editor when switching difficulties.
        /// </summary>
        /// <param name="Beatmap">The reference.</param>
        /// <param name="ShownAgainst">The beatmap that was being edited with it.</param>
        /// <param name="Offset">See <see cref="EditorReferenceBeatmap.Offset"/>.</param>
        /// <param name="Opacity">See <see cref="EditorReferenceBeatmap.Opacity"/>.</param>
        public record ReferenceState(BeatmapInfo Beatmap, BeatmapInfo ShownAgainst, double Offset, float Opacity);

        public ReferenceState? GetState(BeatmapInfo editing) =>
            Beatmap.Value == null ? null : new ReferenceState(Beatmap.Value.BeatmapInfo, editing, Offset.Value, Opacity.Value);

        public void RestoreState(ReferenceState state, BeatmapInfo editing)
        {
            Offset.Value = state.Offset;
            Opacity.Value = state.Opacity;

            // Switching to the reference itself shows the previously edited difficulty instead, so spread comparisons work both ways.
            Load(state.Beatmap.Equals(editing) ? state.ShownAgainst : state.Beatmap);
        }
    }
}
