// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="PatternGallery"/>.
    /// </summary>
    [TestFixture]
    public class PatternGalleryTest
    {
        private TemporaryNativeStorage storage = null!;
        private PatternGallery gallery = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("pattern-gallery");
            gallery = new PatternGallery(storage);
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public void TestSaveLoadRoundTrip()
        {
            var source = beatmap(500, 1.4);
            source.HitObjects.Add(new HitCircle
            {
                StartTime = 1000,
                Position = new Vector2(100, 100),
                NewCombo = true,
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT) },
            });
            source.HitObjects.Add(new Slider
            {
                StartTime = 1250,
                Position = new Vector2(200, 150),
                SliderVelocityMultiplier = 1.5,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(120, 0)) }),
            });
            source.HitObjects.Add(new Spinner { StartTime = 2000, Duration = 1000 });

            // Like objects in the editor.
            foreach (var h in source.HitObjects)
                h.ApplyDefaults(source.ControlPointInfo, source.Difficulty);

            string name = gallery.Save("kick/sliders", source.HitObjects, source);

            Assert.That(name, Is.EqualTo("kicksliders"), "made into a valid file name");
            Assert.That(gallery.List(), Is.EqualTo(new[] { "kicksliders" }));

            var pattern = gallery.Load(name, new OsuRuleset().RulesetInfo);
            var objects = pattern.HitObjects.ToArray();

            Assert.That(objects.Select(h => h.StartTime), Is.EqualTo(new double[] { 0, 250, 1000 }));
            Assert.That(objects[0], Is.TypeOf<HitCircle>());
            Assert.That(((HitCircle)objects[0]).Position, Is.EqualTo(new Vector2(100, 100)));
            Assert.That(objects[0].Samples.Select(s => s.Name), Does.Contain(HitSampleInfo.HIT_WHISTLE));
            Assert.That(objects[0].Samples.First().Bank, Is.EqualTo(HitSampleInfo.BANK_SOFT));

            var slider = (Slider)objects[1];
            Assert.That(slider.Position, Is.EqualTo(new Vector2(200, 150)));
            Assert.That(slider.Path.Distance, Is.EqualTo(120).Within(0.5));
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1.5).Within(0.01));

            Assert.That(objects[2], Is.TypeOf<Spinner>());
            Assert.That(objects[2].GetEndTime() - objects[2].StartTime, Is.EqualTo(1000).Within(1));
            Assert.That(pattern.ControlPointInfo.TimingPointAt(0).BeatLength, Is.EqualTo(500));

            gallery.Delete(name);
            Assert.That(gallery.List(), Is.Empty);
        }

        [Test]
        public void TestFitToOtherBpmKeepsBeats()
        {
            var pattern = beatmap(500, 1.4);
            pattern.HitObjects.Add(new HitCircle { StartTime = 0 });
            pattern.HitObjects.Add(new HitCircle { StartTime = 250 }); // 1/2
            pattern.HitObjects.Add(new HitCircle { StartTime = 500 + 500 / 3.0 }); // 1 + 1/3
            pattern.HitObjects.Add(new Spinner { StartTime = 1000, Duration = 1000 }); // 2 beats long

            var fitted = PatternGallery.FitTo(pattern, beatmap(300, 1.4), 5000);

            Assert.That(fitted.Select(h => h.StartTime), Is.EqualTo(new[] { 5000, 5150, 5400, 5600 }).Within(0.01));
            Assert.That(fitted[3].GetEndTime() - fitted[3].StartTime, Is.EqualTo(600).Within(0.01));
        }

        [Test]
        public void TestFitToOtherSliderMultiplierKeepsSliderBeats()
        {
            var pattern = beatmap(500, 1.4);
            var slider = new Slider
            {
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(140, 0)) }),
            };
            pattern.HitObjects.Add(slider);
            slider.ApplyDefaults(pattern.ControlPointInfo, pattern.Difficulty);
            double beatsBefore = slider.Duration / 500;

            var target = beatmap(300, 2.8);
            PatternGallery.FitTo(pattern, target, 0);
            slider.ApplyDefaults(target.ControlPointInfo, target.Difficulty);

            Assert.That(slider.Path.Distance, Is.EqualTo(140).Within(0.5), "same shape");
            Assert.That(slider.Duration / 300, Is.EqualTo(beatsBefore).Within(0.01), "same length in beats");
        }

        private static OsuBeatmap beatmap(double beatLength, double sliderMultiplier)
        {
            var b = new OsuBeatmap
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
                Difficulty = { SliderMultiplier = sliderMultiplier },
            };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });
            return b;
        }
    }
}
