// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Edit.Checks;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor.Checks
{
    /// <summary>
    /// YAWNS: <see cref="CheckUnevenStreams"/> and <see cref="CheckOffAxisSliders"/>.
    /// </summary>
    [TestFixture]
    public class YawnsChecksTest
    {
        private static IEnumerable<HitObject> stream(params float[] xs) =>
            xs.Select((x, i) => new HitCircle { StartTime = 1000 + i * 125, Position = new Vector2(x, 200) });

        [Test]
        public void TestEvenStreamIsFine()
        {
            Assert.That(run(new CheckUnevenStreams(), stream(100, 120, 140, 160, 180, 200)), Is.Empty);
        }

        [Test]
        public void TestUnevenStreamIsReported()
        {
            var issues = run(new CheckUnevenStreams(), stream(100, 110, 140, 150, 190, 200)).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Template.Type, Is.EqualTo(IssueType.Warning));
            Assert.That(issues[0].HitObjects, Has.Count.EqualTo(6));
        }

        [Test]
        public void TestAcceleratingStreamIsFine()
        {
            Assert.That(run(new CheckUnevenStreams(), stream(100, 110, 125, 145, 170, 200)), Is.Empty);
        }

        [Test]
        public void TestJumpsAreNotStreams()
        {
            // 1/2 at 60 BPM is 500 ms apart: not a stream, however uneven.
            var jumps = new[] { 100f, 110, 140, 150, 190, 200 }.Select((x, i) => new HitCircle { StartTime = 1000 + i * 500, Position = new Vector2(x, 200) });
            Assert.That(run(new CheckUnevenStreams(), jumps), Is.Empty);
        }

        [Test]
        public void TestOffAxisSlider()
        {
            var sliders = new[] { 12.0, -12, 102, 78, 192, 30 }.Select((a, i) => straight(1000 + i * 1000, a)).ToList();
            var issues = run(new CheckOffAxisSliders(), sliders).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].HitObjects.Single(), Is.SameAs(sliders[^1]));
            Assert.That(issues[0].Template.Type, Is.EqualTo(IssueType.Negligible));
        }

        [Test]
        public void TestTooFewSlidersToTell()
        {
            Assert.That(run(new CheckOffAxisSliders(), new[] { straight(1000, 12), straight(2000, 40) }), Is.Empty);
        }

        [Test]
        public void TestOrganiseFixEvensTheStream()
        {
            var beatmap = editorBeatmap(stream(100, 110, 140, 150, 190, 200));
            var issue = run(new CheckUnevenStreams(), beatmap).Single();

            ((IHasFix)issue.Template).Fix(issue, beatmap, null!);

            Assert.That(run(new CheckUnevenStreams(), beatmap), Is.Empty);
        }

        [Test]
        public void TestAlignFixPutsSliderOnAxis()
        {
            var beatmap = editorBeatmap(new[] { 12.0, -12, 102, 78, 192, 30 }.Select((a, i) => straight(1000 + i * 1000, a)));
            var issue = run(new CheckOffAxisSliders(), beatmap).Single();
            var slider = (Slider)issue.HitObjects.Single();
            Vector2 head = slider.Position;

            ((IHasFix)issue.Template).Fix(issue, beatmap, null!);

            Assert.That(run(new CheckOffAxisSliders(), beatmap), Is.Empty);
            Assert.That(slider.Position, Is.EqualTo(head));
            Assert.That(slider.Path.Distance, Is.EqualTo(100).Within(0.01));
        }

        [Test]
        public void TestAutoFailOnLong2B()
        {
            // A 10 second slider with circles during it: stable's search skips the slider once they have all ended.
            var slider = new Slider
            {
                StartTime = 0,
                Position = new Vector2(100),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(400, 0)) }),
                RepeatCount = 30,
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            var circles = Enumerable.Range(0, 10).Select(i => new HitCircle { StartTime = 1000 + i * 100, Position = new Vector2(300) }).ToList();
            var result = CheckAutoFail.Detect(circles.Cast<HitObject>().Prepend(slider), 5, 5);

            Assert.That(slider.EndTime, Is.GreaterThan(5000));
            Assert.That(result.Unloading, Is.EqualTo(new[] { slider }));
        }

        [Test]
        public void TestNoAutoFailWithoutOverlap()
        {
            var circles = Enumerable.Range(0, 50).Select(i => new HitCircle { StartTime = 1000 + i * 300, Position = new Vector2(300) });
            var result = CheckAutoFail.Detect(circles, 9, 9);

            Assert.That(result.Unloading, Is.Empty);
            Assert.That(result.PotentiallyUnloading, Is.Empty);
        }

        private static EditorBeatmap editorBeatmap(IEnumerable<HitObject> objects)
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.HitObjects.AddRange(objects.Cast<OsuHitObject>());
            return new EditorBeatmap(beatmap);
        }

        private static Slider straight(double time, double angle)
        {
            double rad = angle * Math.PI / 180;

            var slider = new Slider
            {
                StartTime = time,
                Position = new Vector2(256, 192),
                Path = new SliderPath(new[]
                {
                    new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                    new PathControlPoint(100 * new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad))),
                }),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }

        private static IEnumerable<Issue> run(ICheck check, IEnumerable<HitObject> objects) => run(check, new Beatmap<HitObject> { HitObjects = objects.ToList() });

        private static IEnumerable<Issue> run(ICheck check, IBeatmap beatmap) => check.Run(new BeatmapVerifierContext(beatmap, new TestWorkingBeatmap(beatmap), DifficultyRating.Expert));
    }
}
