// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: frame time of the Hitsounds tab with a big hitsound project. Prints ms per frame, asserts nothing.
    /// </summary>
    [Explicit("benchmark")]
    public partial class TestSceneHitsoundsPerf : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private const int frames = 60;

        private readonly FrameCounter counter = new FrameCounter();

        private HitsoundsScreen screen => Editor.ChildrenOfType<HitsoundsScreen>().Single();

        [Test]
        public void TestBigProject()
        {
            AddStep("empty the difficulty, 1/16 grid", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            });
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);
            AddStep("add counter", () => Add(counter));

            measure("empty");

            AddStep("16 lanes, 4000 hits", () =>
            {
                for (int i = 0; i < 12; i++)
                    screen.AddLane();

                screen.SetSnap(16);
                var rng = new Random(1);

                for (int i = 0; i < 4000; i++)
                    screen.AddTrigger(screen.Lanes[rng.Next(screen.Lanes.Count)], i * 31.25);
            });
            measure("4000 hits, default zoom");

            AddStep("zoom all the way out", () => screen.Canvas.SetZoom(HitsoundCanvas.MIN_ZOOM));
            measure("4000 hits, zoomed out");

            AddStep("select everything", () => screen.SetSelection(screen.Triggers.Select(t => t.Id)));
            measure("4000 hits selected, zoomed out");
        }

        private void measure(string name)
        {
            long start = 0;
            int startFrames = 0;
            AddWaitStep("settle", 5);
            AddStep($"start {name}", () =>
            {
                startFrames = counter.Frames;
                start = Stopwatch.GetTimestamp();
            });
            AddUntilStep($"wait {frames} frames", () => counter.Frames - startFrames >= frames);
            AddStep($"report {name}", () =>
            {
                double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds / (counter.Frames - startFrames);
                Console.WriteLine($"PERF {name}: {ms:0.00} ms/frame");
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
