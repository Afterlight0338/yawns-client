// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using static osu.Game.Screens.Edit.MappingTools.ComboColourStudio;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Combo Colour Studio port.
    /// </summary>
    [TestFixture]
    public class ComboColourStudioTest
    {
        private const int colour_count = 4;

        // Combos of the given lengths, one circle every 250 ms.
        private static EditorBeatmap map(params int[] comboLengths)
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            double time = 0;

            foreach (int length in comboLengths)
            {
                for (int i = 0; i < length; i++)
                {
                    b.HitObjects.Add(new HitCircle { StartTime = time, NewCombo = i == 0, Position = new Vector2(100) });
                    time += 250;
                }
            }

            var editorBeatmap = new EditorBeatmap(b);

            // As loading into the editor does: work out the combos.
            editorBeatmap.BeginChange();
            editorBeatmap.UpdateAllHitObjects();
            editorBeatmap.EndChange();

            return editorBeatmap;
        }

        private static int[] colours(EditorBeatmap beatmap) => ComboStarts(beatmap).Select(s => ColourOf(s, colour_count)).ToArray();

        [Test]
        public void TestNaturalCycle()
        {
            Assert.That(colours(map(2, 2, 2, 2, 2)), Is.EqualTo(new[] { 0, 1, 2, 3, 0 }));
        }

        [Test]
        public void TestSequenceAndBurst()
        {
            var beatmap = map(2, 2, 1, 2, 2, 2);
            double burstTime = ComboStarts(beatmap)[2].StartTime;

            Apply(beatmap, new[]
            {
                new ColourPoint(0, new[] { 0, 2 }),
                new ColourPoint(burstTime, new[] { 3 }, Burst: true),
            }, colour_count);

            // The burst only takes the one short combo, then 0, 2 carries on.
            Assert.That(colours(beatmap), Is.EqualTo(new[] { 0, 2, 3, 0, 2, 0 }));
        }

        [Test]
        public void TestReadBackGivesSameColours()
        {
            var beatmap = map(2, 2, 1, 2, 2, 2, 3, 3);
            Apply(beatmap, new[]
            {
                new ColourPoint(0, new[] { 1, 3 }),
                new ColourPoint(ComboStarts(beatmap)[2].StartTime, new[] { 0 }, Burst: true),
                new ColourPoint(ComboStarts(beatmap)[6].StartTime, new[] { 2 }),
            }, colour_count);
            int[] expected = colours(beatmap);

            var read = Read(beatmap, colour_count);

            var copy = map(2, 2, 1, 2, 2, 2, 3, 3);
            Apply(copy, read, colour_count);

            Assert.That(colours(copy), Is.EqualTo(expected));
            Assert.That(read.Count, Is.LessThanOrEqualTo(3));
        }
    }
}
