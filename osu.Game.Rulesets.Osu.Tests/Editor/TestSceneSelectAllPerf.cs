// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: frame time with the whole map selected (Ctrl+A lag). Prints ms per frame, asserts nothing.
    /// </summary>
    [Explicit("benchmark")]
    public partial class TestSceneSelectAllPerf : TestSceneOsuEditor
    {
        private const int frames = 60;

        private readonly FrameCounter counter = new FrameCounter();

        private int usages;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("big map", () =>
            {
                EditorBeatmap.Clear();
                var rng = new Random(1);

                for (int i = 0; i < 2000; i++)
                {
                    var pos = new Vector2(rng.Next(50, 460), rng.Next(50, 330));
                    HitObject h = i % 2 == 0
                        ? new HitCircle { StartTime = 1000 + i * 150, Position = pos }
                        : new Slider
                        {
                            StartTime = 1000 + i * 150,
                            Position = pos,
                            Path = new SliderPath(new[]
                            {
                                new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                                new PathControlPoint(new Vector2(40, 60)),
                                new PathControlPoint(new Vector2(-30, 90)),
                            }),
                        };
                    EditorBeatmap.Add(h);
                }
            });
            AddStep("add counter", () => Add(counter));
            AddStep("count usage", () =>
            {
                var playfield = Editor.ChildrenOfType<osu.Game.Rulesets.UI.Playfield>().First();
                Action<HitObject> began = _ => usages++;
                typeof(osu.Game.Rulesets.UI.Playfield).GetEvent("HitObjectUsageBegan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetAddMethod(true)!.Invoke(playfield, new object[] { began });
            });
        }

        [Test]
        public void TestSelectAll()
        {
            measure("nothing selected");
            AddStep("select all", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects.Except(EditorBeatmap.SelectedHitObjects).ToArray()));
            measure("all selected, first frames");
            measure("all selected, steady");
            AddStep("deselect", () => EditorBeatmap.SelectedHitObjects.Clear());
            measure("deselected again");
        }

        private void measure(string name)
        {
            long start = 0;
            int startFrames = 0;
            int startUsages = 0;
            AddWaitStep("settle", 5);
            AddStep($"start {name}", () =>
            {
                startFrames = counter.Frames;
                startUsages = usages;
                start = Stopwatch.GetTimestamp();
            });
            AddUntilStep($"wait {frames} frames", () => counter.Frames - startFrames >= frames);
            AddStep($"report {name}", () =>
            {
                double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds / (counter.Frames - startFrames);
                Console.WriteLine($"PERF {name}: {ms:0.00} ms/frame, {usages - startUsages} usage begins");
            });
        }

        private partial class FrameCounter : Drawable
        {
            public int Frames;

            protected override void Update()
            {
                base.Update();
                Frames++;
            }
        }
    }
}
