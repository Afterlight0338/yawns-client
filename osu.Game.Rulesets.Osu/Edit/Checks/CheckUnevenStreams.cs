// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Checks
{
    /// <summary>
    /// YAWNS: streams (4+ circles in an even rhythm at 1/3 or faster) whose spacing jumps around.
    /// Streams that steadily speed up or slow down are left alone, those look intended.
    /// </summary>
    public class CheckUnevenStreams : ICheck
    {
        /// <summary>
        /// Widest gap over narrowest gap above which a stream counts as uneven.
        /// </summary>
        public const double MAX_RATIO = 1.35;

        public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Uneven stream spacing");

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            new IssueTemplateUnevenStream(this)
        };

        public IEnumerable<Issue> Run(BeatmapVerifierContext context)
        {
            var playable = context.CurrentDifficulty.Playable;
            var objects = playable.HitObjects.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToList();
            var stream = new List<HitCircle>();

            foreach (var h in objects.Append(null))
            {
                if (h is HitCircle circle && continues(stream, circle, playable.ControlPointInfo.TimingPointAt(circle.StartTime).BeatLength))
                {
                    stream.Add(circle);
                    continue;
                }

                if (isUneven(stream, out double narrowest, out double widest))
                    yield return new IssueTemplateUnevenStream(this).Create(stream.ToArray(), narrowest, widest);

                stream.Clear();

                if (h is HitCircle start)
                    stream.Add(start);
            }
        }

        private static bool continues(List<HitCircle> stream, HitCircle next, double beatLength)
        {
            if (stream.Count == 0)
                return true;

            double gap = next.StartTime - stream[^1].StartTime;

            // Faster than 1/2 and the same rhythm as the stream so far.
            if (gap <= 0 || gap > beatLength / 3 + 1)
                return false;

            return stream.Count < 2 || Math.Abs(gap - (stream[1].StartTime - stream[0].StartTime)) <= 2;
        }

        private static bool isUneven(List<HitCircle> stream, out double narrowest, out double widest)
        {
            narrowest = widest = 0;

            if (stream.Count < 4)
                return false;

            double[] gaps = stream.Zip(stream.Skip(1), (a, b) => (double)Vector2.Distance(a.StackedPosition, b.StackedPosition)).ToArray();
            narrowest = gaps.Min();
            widest = gaps.Max();

            // Steadily speeding up or slowing down is intended.
            bool increasing = gaps.Zip(gaps.Skip(1), (a, b) => b >= a).All(x => x);
            bool decreasing = gaps.Zip(gaps.Skip(1), (a, b) => b <= a).All(x => x);

            return !increasing && !decreasing && widest > narrowest * MAX_RATIO;
        }

        public class IssueTemplateUnevenStream : IssueTemplate, IHasFix
        {
            public IssueTemplateUnevenStream(ICheck check)
                : base(check, IssueType.Warning, "Uneven stream: gaps between {0:0} and {1:0} px. Organise evens it out (right-click, Tools, Organise stream has more options).")
            {
            }

            public Issue Create(HitCircle[] stream, double narrowest, double widest) => new Issue(stream, this, narrowest, widest);

            public string FixText => "Organise";

            // Even spacing along a clean curve through the placed circles.
            public void Fix(Issue issue, EditorBeatmap beatmap, BeatmapManager beatmaps)
            {
                var stream = issue.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();
                var positions = StreamOrganiser.Organise(stream.Select(h => h.Position).ToArray(), stream.Select(h => h.StartTime).ToArray(), 1, 0.5);

                for (int i = 0; i < stream.Length; i++)
                {
                    stream[i].Position = positions[i];
                    beatmap.Update(stream[i]);
                }
            }
        }
    }
}
