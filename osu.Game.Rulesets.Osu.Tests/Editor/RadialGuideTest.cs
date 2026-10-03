// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="RadialGuide"/>.
    /// </summary>
    [TestFixture]
    public class RadialGuideTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        private static RadialGuide create(int spokes, int rings, float spacing = 50, bool enabled = true) =>
            new RadialGuide(new SymmetryCentre())
            {
                Enabled = { Value = enabled },
                Spokes = { Value = spokes },
                Rings = { Value = rings },
                RingSpacing = { Value = spacing },
            };

        private static void assertSnap(Vector2? actual, Vector2 expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(Vector2.Distance(actual!.Value, expected), Is.LessThan(0.01f), $"{actual} vs {expected}");
        }

        [Test]
        public void TestSnapsToCrossingThenSpokeThenRing()
        {
            var guide = create(4, 2);

            assertSnap(guide.Snap(new Vector2(303, 195), 8), new Vector2(306, 192));
            assertSnap(guide.Snap(new Vector2(330, 195), 8), new Vector2(330, 192));

            // Between two spokes, 30 degrees round: on the first ring only.
            Vector2 onRing = centre + 50 * new Vector2(MathF.Cos(MathF.PI / 6), MathF.Sin(MathF.PI / 6));
            assertSnap(guide.Snap(onRing + new Vector2(2, 2), 8), centre + (onRing - centre + new Vector2(2, 2)).Normalized() * 50);
        }

        [Test]
        public void TestSpokesAreRaysAndDisabledDoesNothing()
        {
            // Three spokes (0, 120, 240 degrees): nothing points left, even though the first spoke's line would.
            Assert.That(create(3, 0).Snap(new Vector2(200, 192), 8), Is.Null);
            Assert.That(create(4, 2, enabled: false).Snap(new Vector2(306, 192), 8), Is.Null);
        }

        [Test]
        public void TestFollowsTheCentre()
        {
            var guide = create(4, 1);
            guide.Centre.Guide.Value = new Vector2(100, 100);

            assertSnap(guide.Snap(new Vector2(102, 98), 8), new Vector2(100, 100));
            assertSnap(guide.Snap(new Vector2(148, 101), 8), new Vector2(150, 100));
        }
    }
}
