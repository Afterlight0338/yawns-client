// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Osu.Edit.Checks
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' AutoFail Detector. osu!stable finds the objects to load with a binary search over end times on a list sorted by start time,
    /// so with objects during long objects ("2B") an object can be skipped, its judgement never counts, and the play fails at the end.
    /// Checked without mods and with Hard Rock. Mapping Tools' fix (invisible negative-length spinners) cannot be made in lazer, so there is no fix button.
    /// </summary>
    public class CheckAutoFail : ICheck
    {
        /// <summary>
        /// How long stable needs an object loaded after it ends for the judgement to count (Mapping Tools' default).
        /// </summary>
        public const int PHYSICS_TIME = 9;

        public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Auto-fail in osu!stable (2B)");

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            new IssueTemplateUnloading(this),
            new IssueTemplatePotentiallyUnloading(this),
        };

        public IEnumerable<Issue> Run(BeatmapVerifierContext context)
        {
            var beatmap = context.CurrentDifficulty.Playable;
            var reported = new HashSet<HitObject>();

            foreach (bool hardRock in new[] { false, true })
            {
                var difficulty = beatmap.Difficulty;
                double ar = hardRock ? Math.Min(10, difficulty.ApproachRate * 1.4) : difficulty.ApproachRate;
                double od = hardRock ? Math.Min(10, difficulty.OverallDifficulty * 1.4) : difficulty.OverallDifficulty;

                var result = Detect(beatmap.HitObjects, ar, od, beatmap.AudioLeadIn);
                string mods = hardRock ? " with Hard Rock" : string.Empty;

                foreach (var h in result.Unloading.Where(reported.Add))
                    yield return new IssueTemplateUnloading(this).Create(h, mods);

                foreach (var h in result.PotentiallyUnloading.Where(reported.Add))
                    yield return new IssueTemplatePotentiallyUnloading(this).Create(h, mods);
            }
        }

        public record Result(List<HitObject> Unloading, List<HitObject> PotentiallyUnloading);

        /// <summary>
        /// Mapping Tools' detection, unchanged in its logic. Times are whole milliseconds as stable uses them.
        /// </summary>
        public static Result Detect(IEnumerable<HitObject> hitObjects, double approachRate, double overallDifficulty, double audioLeadIn = 0)
        {
            // Stable's order: by start time, new combos first at the same time.
            var objects = hitObjects.OrderBy(h => h.StartTime).ThenByDescending(h => h is IHasComboInformation { NewCombo: true }).ToList();
            var result = new Result(new List<HitObject>(), new List<HitObject>());

            if (objects.Count == 0)
                return result;

            int approachTime = (int)(approachRate < 5 ? 1800 - 120 * approachRate : 1200 - 150 * (approachRate - 5));
            int window50 = (int)Math.Ceiling(200 - 10 * overallDifficulty);
            int autoFailCheckTime = (int)objects.Max(h => h.GetEndTime()) + 200;
            int mapStartTime = -(int)(Math.Max(audioLeadIn, approachTime - objects[0].StartTime) + window50 + 1000);

            int start(int i) => (int)objects[i].StartTime;
            int end(int i) => (int)objects[i].GetEndTime();

            int adjustedEnd(int i) => objects[i] switch
            {
                IHasPath or IHasDuration => end(i),
                _ => start(i) + window50,
            };

            // Times at which stable's start index can change.
            var timesToCheckStartIndex = new SortedSet<int>(Enumerable.Range(0, objects.Count).SelectMany(i => new[] { end(i) + approachTime, end(i) + approachTime + 1 }));

            var problems = new List<(int Index, HashSet<int> Times)>();

            for (int i = 0; i < objects.Count; i++)
            {
                int adjEnd = adjustedEnd(i);
                bool negative = adjEnd < start(i) - approachTime;

                // Problems inside an earlier problem get fixed with it.
                if (problems.Count > 0 && !negative)
                {
                    int lastAdjEnd = adjustedEnd(problems[^1].Index);

                    if (adjEnd <= lastAdjEnd || timesToCheckStartIndex.GetViewBetween(lastAdjEnd, adjEnd + PHYSICS_TIME).Count == 0)
                        continue;
                }

                bool hasDisruptors = false;

                for (int j = i + 1; j < objects.Count && !hasDisruptors; j++)
                    hasDisruptors = end(j) < adjEnd + PHYSICS_TIME - approachTime;

                if (!hasDisruptors && !negative)
                    continue;

                int firstRequiredLoadTime = adjEnd;
                if (i > 0)
                    firstRequiredLoadTime = Math.Max(adjEnd, start(i - 1) - approachTime + 1);
                firstRequiredLoadTime = Math.Max(firstRequiredLoadTime, mapStartTime);

                var times = new HashSet<int>(timesToCheckStartIndex.GetViewBetween(firstRequiredLoadTime, firstRequiredLoadTime + PHYSICS_TIME)) { firstRequiredLoadTime + PHYSICS_TIME };

                problems.Add((i, times));
                result.PotentiallyUnloading.Add(objects[i]);
            }

            foreach (var (index, times) in problems)
            {
                foreach (int time in times)
                {
                    int startIndex = osuBinarySearch(time - approachTime);
                    int endIndex = objects.FindIndex(Math.Min(startIndex, objects.Count), h => h.StartTime > time + approachTime);
                    if (endIndex < 0)
                        endIndex = objects.Count - 1;

                    bool loaded = index >= startIndex && index <= endIndex;

                    if (!loaded || time > autoFailCheckTime)
                    {
                        result.Unloading.Add(objects[index]);
                        result.PotentiallyUnloading.Remove(objects[index]);
                        break;
                    }
                }
            }

            return result;

            // Stable's search: over end times, on a list sorted by start time.
            int osuBinarySearch(int time)
            {
                int min = 0, max = objects.Count - 1;

                while (min <= max)
                {
                    int mid = min + (max - min) / 2;
                    int t = end(mid);

                    if (time == t)
                        return mid;

                    if (time > t)
                        min = mid + 1;
                    else
                        max = mid - 1;
                }

                return min;
            }
        }

        public class IssueTemplateUnloading : IssueTemplate
        {
            public IssueTemplateUnloading(ICheck check)
                : base(check, IssueType.Problem, "osu!stable never loads this object{0}, so its judgement is missed and plays auto-fail. Move objects out of the long object before it.")
            {
            }

            public Issue Create(HitObject hitObject, string mods) => new Issue(hitObject, this, mods);
        }

        public class IssueTemplatePotentiallyUnloading : IssueTemplate
        {
            public IssueTemplatePotentiallyUnloading(ICheck check)
                : base(check, IssueType.Negligible, "Objects during this one could make osu!stable unload it{0}. It loads fine as the map is now.")
            {
            }

            public Issue Create(HitObject hitObject, string mods) => new Issue(hitObject, this, mods);
        }
    }
}
