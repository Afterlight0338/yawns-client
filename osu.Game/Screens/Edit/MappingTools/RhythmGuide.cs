// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of the Rhythm Guide from Mapping Tools (https://github.com/OliBomby/Mapping_Tools).
    /// Circles at the rhythm of other difficulties, as a reference for hitsounding or mapping.
    /// </summary>
    public static class RhythmGuide
    {
        public enum Events
        {
            [Description("Every object (heads, repeats, tails, spinner ends)")]
            All,

            [Description("Only hitsounds (whistle, finish, clap, custom samples)")]
            Hitsounds,
        }

        /// <summary>
        /// The times of <paramref name="events"/> in <paramref name="sources"/>, snapped to 1/16 or 1/12 of <paramref name="timing"/> (whichever is closer).
        /// </summary>
        public static List<double> Times(IEnumerable<IBeatmap> sources, Events events, IBeatmap timing)
        {
            var times = new SortedSet<double>();

            foreach (var source in sources)
            {
                foreach (var moment in HitsoundProject.Import(source).GroupBy(t => t.Time))
                {
                    if (events == Events.Hitsounds && !moment.Any(t => t.Sound.IsAddition || t.Sound.File != null))
                        continue;

                    double sixteenth = timing.ControlPointInfo.GetClosestSnappedTime(moment.Key, 16);
                    double twelfth = timing.ControlPointInfo.GetClosestSnappedTime(moment.Key, 12);

                    times.Add(Math.Round(Math.Abs(sixteenth - moment.Key) <= Math.Abs(twelfth - moment.Key) ? sixteenth : twelfth));
                }
            }

            return times.ToList();
        }

        /// <summary>
        /// Adds a circle in the middle of the playfield at each of <paramref name="times"/> where <paramref name="beatmap"/> has no object yet, as one undo step.
        /// </summary>
        /// <returns>The circles added.</returns>
        public static List<HitObject> AddTo(EditorBeatmap beatmap, IEnumerable<double> times, bool newComboEverything)
        {
            var taken = beatmap.HitObjects.Select(h => h.StartTime).ToList();
            var legacy = times.Where(t => !taken.Any(o => Math.Abs(o - t) < 2))
                              .Select(t => HitsoundProject.LegacyCircle(t, HitsoundProject.POSITION, newComboEverything))
                              .ToList();

            if (legacy.Count == 0)
                return new List<HitObject>();

            var circles = HitsoundProject.ToRulesetObjects(legacy, beatmap).ToList();

            beatmap.BeginChange();
            beatmap.AddRange(circles);
            beatmap.SelectedHitObjects.Clear();
            beatmap.SelectedHitObjects.AddRange(circles);
            beatmap.EndChange();

            return circles;
        }
    }
}
