// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Tests.Editing.Checks
{
    /// <summary>
    /// YAWNS: <see cref="CheckHitsoundDifficultyMismatch"/>.
    /// </summary>
    [TestFixture]
    public class CheckHitsoundDifficultyMismatchTest
    {
        private static OsuBeatmap map(string name, Vector2 position, params (double Time, string? Addition)[] circles)
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo, DifficultyName = name } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            foreach (var (time, addition) in circles)
            {
                var circle = new HitCircle { StartTime = time, Position = position, Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) } };
                if (addition != null)
                    circle.Samples.Add(new HitSampleInfo(addition, HitSampleInfo.BANK_SOFT));
                b.HitObjects.Add(circle);
            }

            return b;
        }

        [Test]
        public void TestMismatchesFoundAndFixed()
        {
            var hitsounds = map("Hitsounds", HitsoundProject.POSITION, (1000, HitSampleInfo.HIT_CLAP), (1500, null));
            var difficulty = new EditorBeatmap(map("Insane", new Vector2(100), (1000, HitSampleInfo.HIT_WHISTLE), (1500, null), (2000, HitSampleInfo.HIT_FINISH)));

            var issues = run(difficulty, hitsounds).ToList();

            Assert.That(issues.Select(i => i.Time), Is.EqualTo(new double?[] { 1000, 2000 }));
            Assert.That(issues[0].ToString(), Does.Contain("whistle").And.Contain("clap"));

            foreach (var issue in issues)
                ((IHasFix)issue.Template).Fix(issue, difficulty, null!);

            Assert.That(run(difficulty, hitsounds), Is.Empty);
            Assert.That(difficulty.HitObjects[0].Samples.Select(s => s.Name), Is.EquivalentTo(new[] { HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_CLAP }));
            Assert.That(difficulty.HitObjects[2].Samples.Select(s => s.Name), Is.EquivalentTo(new[] { HitSampleInfo.HIT_NORMAL }));
        }

        [Test]
        public void TestNoHitsoundDifficultyNoIssues()
        {
            var other = map("Hard", new Vector2(200), (1000, HitSampleInfo.HIT_CLAP));
            var difficulty = new EditorBeatmap(map("Insane", new Vector2(100), (1000, HitSampleInfo.HIT_WHISTLE)));

            Assert.That(run(difficulty, other), Is.Empty);
        }

        private static IEnumerable<Issue> run(IBeatmap current, IBeatmap other)
        {
            var context = new BeatmapVerifierContext(
                new BeatmapVerifierContext.VerifiedBeatmap(new TestWorkingBeatmap(current), current),
                new[] { new BeatmapVerifierContext.VerifiedBeatmap(new TestWorkingBeatmap(other), other) },
                DifficultyRating.Expert);

            return new CheckHitsoundDifficultyMismatch().Run(context);
        }
    }
}
