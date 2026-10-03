// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: hovering the beat divisor shows its BPM at the current time.
    /// </summary>
    public partial class TestSceneDivisorBpmTooltip : TestSceneOsuEditor
    {
        private string tooltip => Editor.ChildrenOfType<BeatDivisorControl>().Single().ChildrenOfType<IHasTooltip>().Select(t => t.TooltipText.ToString()).FirstOrDefault(t => t.Contains(" BPM at 1/")) ?? string.Empty;

        [Test]
        public void TestShowsTheBpmAtTheDivisor()
        {
            AddStep("120 BPM, divisor 1/6", () =>
            {
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                Editor.Dependencies.Get<BindableBeatDivisor>().SetArbitraryDivisor(6);
            });
            AddUntilStep("180 BPM at 1/6", () => tooltip, () => Is.EqualTo("180 BPM at 1/6"));

            AddStep("divisor 1/3", () => Editor.Dependencies.Get<BindableBeatDivisor>().SetArbitraryDivisor(3));
            AddUntilStep("90 BPM at 1/3", () => tooltip, () => Is.EqualTo("90 BPM at 1/3"));
        }
    }
}
