// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="StreamOrganiser.Wiggled"/>.
    /// </summary>
    [TestFixture]
    public class StreamWiggleTest
    {
        private static readonly Vector2[] line = Enumerable.Range(0, 6).Select(i => new Vector2(100 + i * 20, 200)).ToArray();

        [Test]
        public void TestZeroChangesNothing()
        {
            Assert.That(StreamOrganiser.Wiggled(line, 0), Is.EqualTo(line));
        }

        [Test]
        public void TestObjectsAlternateSidesOfAHorizontalStream()
        {
            var wiggled = StreamOrganiser.Wiggled(line, 8);

            Assert.That(wiggled[0], Is.EqualTo(line[0]), "the first object stays on the line");

            for (int i = 1; i < line.Length; i++)
            {
                Assert.That(wiggled[i].X, Is.EqualTo(line[i].X).Within(0.01f), "moves sideways only");
                Assert.That(wiggled[i].Y - 200, Is.EqualTo(i % 2 == 1 ? 8 : -8).Within(0.01f), $"object {i}");
            }
        }

        [Test]
        public void TestSideIsPerpendicularOnADiagonal()
        {
            var diagonal = Enumerable.Range(0, 5).Select(i => new Vector2(100 + i * 20, 100 + i * 20)).ToArray();
            var wiggled = StreamOrganiser.Wiggled(diagonal, 10);

            Vector2 offset = wiggled[1] - diagonal[1];
            Assert.That(offset.Length, Is.EqualTo(10).Within(0.01f));
            Assert.That(Vector2.Dot(offset, new Vector2(1, 1)), Is.EqualTo(0).Within(0.01f), "perpendicular to the stream");
        }
    }
}
