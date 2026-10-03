// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit.Checks;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Verify;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the fix buttons on the verify screen.
    /// </summary>
    public partial class TestSceneVerifyFixes : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [Test]
        public void TestResnapFromVerify()
        {
            HitCircle circle = null!;

            AddStep("one unsnapped circle", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.Add(circle = new HitCircle { StartTime = 1010 });
            });
            AddStep("open verify", () => Editor.Mode.Value = EditorScreenMode.Verify);
            AddUntilStep("unsnap listed with a fix", () => fixButtonFor<CheckUnsnappedObjects>() != null);

            AddStep("click resnap", () => fixButtonFor<CheckUnsnappedObjects>()!.TriggerClick());

            AddAssert("circle on the beat", () => circle.StartTime, () => Is.EqualTo(1000));
            AddUntilStep("issue gone", () => fixButtonFor<CheckUnsnappedObjects>() == null);

            AddStep("undo", () => Editor.Undo());
            AddUntilStep("unsnapped again", () => EditorBeatmap.HitObjects.Single().StartTime, () => Is.EqualTo(1010));
        }

        private RoundedButton? fixButtonFor<T>()
            => Editor.ChildrenOfType<IssueTable.DrawableIssue>()
                     .Where(d => d.Current.Value?.Check is T)
                     .SelectMany(d => d.ChildrenOfType<RoundedButton>())
                     .FirstOrDefault(b => b.IsPresent);
    }
}
