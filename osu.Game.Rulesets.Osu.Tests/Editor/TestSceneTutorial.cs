// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools.Tutorial;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the Tutorial tab and its diagrams.
    /// </summary>
    public partial class TestSceneTutorial : TestSceneOsuEditor
    {
        [Test]
        public void TestEveryTopicHasAllItsParts()
        {
            Assert.That(TutorialTopics.ALL.Select(t => t.Name).Distinct().Count(), Is.EqualTo(TutorialTopics.ALL.Length), "names are unique");

            foreach (var topic in TutorialTopics.ALL)
            {
                Assert.That(topic.What, Is.Not.Empty, topic.Name);
                Assert.That(topic.Steps, Is.Not.Empty, topic.Name);
                Assert.That(topic.Where, Is.Not.Empty, topic.Name);
                Assert.That(topic.What + string.Concat(topic.Steps) + topic.Where, Does.Not.Contain("—"), $"{topic.Name}: no em dashes");
            }
        }

        [Test]
        public void TestDiagramsStayOnTheCanvas()
        {
            var problems = new System.Collections.Generic.List<string>();

            foreach (var topic in TutorialTopics.ALL)
            {
                var canvas = new DiagramCanvas();
                topic.Draw(canvas);

                var shapes = canvas.Shapes.ToArray();

                if (shapes.Length <= 5)
                    problems.Add($"{topic.Name}: diagram is nearly empty");

                foreach (var shape in shapes)
                {
                    if (!float.IsFinite(shape.Position.X) || !float.IsFinite(shape.Position.Y) || !float.IsFinite(shape.Rotation))
                        problems.Add($"{topic.Name}: {shape.GetType().Name} is not finite");

                    if (shape.Position.X < -10 || shape.Position.X > DiagramCanvas.WIDTH + 10 || shape.Position.Y < -10 || shape.Position.Y > DiagramCanvas.HEIGHT + 10)
                        problems.Add($"{topic.Name}: {shape.GetType().Name} at {shape.Position} is off the canvas");

                    if (shape is osu.Game.Graphics.Sprites.OsuSpriteText label)
                    {
                        // About half an em per character, centred on its position.
                        float half = label.Text.ToString().Length * label.Font.Size * 0.5f / 2;

                        if (shape.Position.X - half < -4 || shape.Position.X + half > DiagramCanvas.WIDTH + 4)
                            problems.Add($"{topic.Name}: \"{label.Text}\" runs off the side (x {shape.Position.X}, half width {half})");
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TestTabShowsAndEveryTopicOpens()
        {
            AddStep("open the tutorial tab", () => Editor.Mode.Value = EditorScreenMode.Tutorial);
            AddUntilStep("tutorial shown", () => Editor.ChildrenOfType<TutorialScreen>().Any(s => s.IsLoaded));

            AddStep("click through every topic", () =>
            {
                var screen = Editor.ChildrenOfType<TutorialScreen>().Single();

                foreach (var topic in TutorialTopics.ALL)
                    screen.ChildrenOfType<RoundedButton>().First(b => b.Text.ToString() == topic.Name).TriggerClick();
            });

            AddAssert("last topic is shown", () => Editor.ChildrenOfType<TutorialScreen>().Single().ChildrenOfType<DiagramCanvas>().Any());
            AddAssert("a diagram is on screen", () => Editor.ChildrenOfType<TutorialScreen>().Single().ChildrenOfType<DiagramCanvas>().Single().Shapes.Count(), () => Is.GreaterThan(5));
        }
    }
}
