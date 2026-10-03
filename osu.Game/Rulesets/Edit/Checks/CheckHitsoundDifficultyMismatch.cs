// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;

namespace osu.Game.Rulesets.Edit.Checks
{
    /// <summary>
    /// YAWNS: objects whose hitsounds differ from the set's hitsound difficulty, which catches drift after editing a difficulty.
    /// Matches moments the same way the Hitsound Copier does, and its fix runs the copier on that object.
    /// </summary>
    public class CheckHitsoundDifficultyMismatch : ICheck
    {
        public CheckMetadata Metadata => new CheckMetadata(CheckCategory.Audio, "Hitsounds differing from the hitsound difficulty");

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            new IssueTemplateMismatch(this),
        };

        /// <summary>
        /// A difficulty made for hitsounds: everything in the middle (the Hitsounds tab's kind), or named like one (as Hitsound Studio decides).
        /// </summary>
        public static bool IsHitsoundDifficulty(IBeatmap beatmap)
        {
            string name = beatmap.BeatmapInfo.DifficultyName.ToLowerInvariant();
            return beatmap.HitObjects.Count > 0 && (name.Contains("hitsound") || name == "hs" || HitsoundProject.IsHitsoundDifficulty(beatmap));
        }

        public IEnumerable<Issue> Run(BeatmapVerifierContext context)
        {
            var current = context.CurrentDifficulty.Playable;

            if (IsHitsoundDifficulty(current))
                yield break;

            var hitsounds = context.OtherDifficulties.Select(d => d.Playable).FirstOrDefault(IsHitsoundDifficulty);

            if (hitsounds == null)
                yield break;

            foreach (var difference in new HitsoundCopier().Differences(hitsounds.HitObjects, current.HitObjects).DistinctBy(d => d.Target))
            {
                yield return new IssueTemplateMismatch(this).Create(difference, hitsounds);
            }
        }

        public class IssueTemplateMismatch : IssueTemplate, IHasFix
        {
            public IssueTemplateMismatch(ICheck check)
                : base(check, IssueType.Warning, "Plays {0} where the hitsound difficulty \"{2}\" plays {1}.")
            {
            }

            public Issue Create(HitsoundCopier.Difference difference, IBeatmap hitsounds)
                => new Issue(difference.Target, this, describe(difference.Has), difference.Source == null ? "nothing" : describe(difference.Source), hitsounds.BeatmapInfo.DifficultyName, hitsounds)
                {
                    Time = difference.Time
                };

            public string FixText => "Copy";

            // Runs the copier with only the hitsound difficulty's sounds around this object.
            public void Fix(Issue issue, EditorBeatmap beatmap, BeatmapManager beatmaps)
            {
                var target = issue.HitObjects.Single();
                var hitsounds = (IBeatmap)issue.Arguments[3];
                var copier = new HitsoundCopier();
                double leniency = copier.TemporalLeniency.Value + 1;

                copier.Copy(hitsounds.HitObjects.Where(h => h.GetEndTime() >= target.StartTime - leniency && h.StartTime <= target.GetEndTime() + leniency), 0, beatmap);

                // Additions with nothing to copy over them are removed.
                foreach (var difference in copier.Differences(hitsounds.HitObjects, new[] { target }).Where(d => d.Source == null))
                {
                    var normal = difference.Has.Where(s => s.Name == HitSampleInfo.HIT_NORMAL).ToList();
                    difference.Has.Clear();
                    foreach (var s in normal)
                        difference.Has.Add(s);
                }

                beatmap.Update(target);
            }

            private static string describe(IList<HitSampleInfo> samples)
            {
                var normal = samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL);
                var additions = samples.Where(s => s.Name != HitSampleInfo.HIT_NORMAL).Select(s => s.Name.Replace("hit", string.Empty)).ToList();

                string bank = normal == null ? string.Empty : $"{normal.Bank}{(string.IsNullOrEmpty(normal.Suffix) ? string.Empty : ":" + normal.Suffix)} {normal.Volume}%";
                return additions.Count == 0 ? bank : $"{bank} + {string.Join(", ", additions)}".Trim();
            }
        }
    }
}
