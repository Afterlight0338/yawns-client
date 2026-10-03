// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="SvEqualiser"/> and its popover: after a BPM change, sliders keep the speed they had.
    /// </summary>
    public partial class TestSceneSvEqualiser : TestSceneOsuEditor
    {
        private Slider[] sliders => EditorBeatmap.HitObjects.OfType<Slider>().OrderBy(s => s.StartTime).ToArray();

        [TestCase(1.0, 120, 240, 0.5)]
        [TestCase(1.0, 240, 120, 2.0)]
        [TestCase(1.4, 150, 100, 2.1)]
        [TestCase(1.0, 120, 120, 1.0)]
        public void TestFormula(double sv, double reference, double bpm, double expected) =>
            Assert.That(SvEqualiser.Equalised(sv, reference, bpm), Is.EqualTo(expected).Within(1e-9));

        [Test]
        public void TestFormulaRefusesWhatCannotExist()
        {
            Assert.That(SvEqualiser.Equalised(1, 60, 1000), Is.Null, "0.06 is below 0.1");
            Assert.That(SvEqualiser.Equalised(5, 300, 100), Is.Null, "15 is above 10");
            Assert.That(SvEqualiser.Equalised(1, 120, 0), Is.Null);
        }

        [Test]
        public void TestPopoverEqualisesAfterABpmChange()
        {
            AddStep("120 BPM, then 240 BPM at 3 s, a slider on each side", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.ControlPointInfo.Add(3000, new TimingControlPoint { BeatLength = 250 });

                for (int i = 0; i < 2; i++)
                {
                    EditorBeatmap.Add(new Slider
                    {
                        StartTime = 1000 + i * 3000,
                        Position = new Vector2(100, 100 + i * 80),
                        Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(100, 0)) }),
                    });
                }
            });

            AddStep("open the equaliser (nothing selected: every slider)", () => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == "SV equaliser").TriggerClick());
            AddUntilStep("popover shown", () => this.ChildrenOfType<SvEqualiserPopover>().Any());
            AddStep("equalise", () => this.ChildrenOfType<SvEqualiserPopover>().Single().ChildrenOfType<RoundedButton>().Single().TriggerClick());

            AddAssert("first slider (the reference) untouched", () => sliders[0].SliderVelocityMultiplier, () => Is.EqualTo(1));
            AddAssert("slider at double the BPM got half the velocity", () => sliders[1].SliderVelocityMultiplier, () => Is.EqualTo(0.5).Within(1e-9));
        }
    }
}
