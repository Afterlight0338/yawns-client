// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Tutorial
{
    /// <summary>
    /// YAWNS: the Tutorial tab: every YAWNS feature explained, with what it does, how to use it, where to find it and a diagram.
    /// </summary>
    public partial class TutorialScreen : EditorScreen
    {
        private const float content_width = 520;

        private readonly Dictionary<TutorialTopic, RoundedButton> buttons = new Dictionary<TutorialTopic, RoundedButton>();

        private FillFlowContainer detail = null!;
        private OsuScrollContainer detailScroll = null!;
        private OverlayColourProvider colourProvider = null!;
        private int currentIndex = -1;

        public TutorialScreen()
            : base(EditorScreenMode.Tutorial)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;

            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(10),
                Spacing = new Vector2(4),
            };

            string? category = null;

            foreach (var topic in TutorialTopics.ALL)
            {
                if (topic.Category != category)
                {
                    category = topic.Category;
                    list.Add(new OsuSpriteText
                    {
                        Text = category,
                        Font = OsuFont.Default.With(weight: FontWeight.Bold),
                        Colour = colourProvider.Highlight1,
                        Margin = new MarginPadding { Top = list.Count == 0 ? 0 : 12, Bottom = 2 },
                    });
                }

                var button = new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Text = topic.Name,
                    BackgroundColour = colourProvider.Background3,
                    Action = () => show(topic),
                };

                buttons[topic] = button;
                list.Add(button);
            }

            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 250), new Dimension() },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background4 },
                                new OsuScrollContainer { RelativeSizeAxes = Axes.Both, Child = list },
                            },
                        },
                        detailScroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = detail = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(20),
                                Spacing = new Vector2(12),
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            show(TutorialTopics.ALL[0]);
        }

        private void show(TutorialTopic topic)
        {
            currentIndex = System.Array.IndexOf(TutorialTopics.ALL, topic);

            foreach (var (t, button) in buttons)
                button.BackgroundColour = t == topic ? colourProvider.Highlight1 : colourProvider.Background3;

            var canvas = new DiagramCanvas(480);
            topic.Draw(canvas);

            detail.Clear();
            detail.AddRange(new Drawable[]
            {
                new OsuSpriteText { Text = topic.Name, Font = OsuFont.Default.With(size: 26, weight: FontWeight.Bold) },
                new OsuSpriteText { Text = topic.Category, Font = OsuFont.Default.With(size: 14), Colour = colourProvider.Highlight1 },
                canvas,
                heading("What it does"),
                text(topic.What),
                heading("How to use it"),
                text(string.Join("\n", topic.Steps.Select((s, i) => $"{i + 1}. {s}"))),
                heading("Where"),
                text(topic.Where),
                new GridContainer
                {
                    Width = content_width,
                    AutoSizeAxes = Axes.Y,
                    Margin = new MarginPadding { Top = 10 },
                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 8), new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = "Previous",
                                Enabled = { Value = currentIndex > 0 },
                                Action = () => show(TutorialTopics.ALL[currentIndex - 1]),
                            },
                            Empty(),
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = "Next",
                                Enabled = { Value = currentIndex < TutorialTopics.ALL.Length - 1 },
                                Action = () => show(TutorialTopics.ALL[currentIndex + 1]),
                            },
                        },
                    },
                },
            });

            detailScroll.ScrollToStart(false);
        }

        private Drawable heading(string title) => new OsuSpriteText
        {
            Text = title,
            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
            Margin = new MarginPadding { Top = 6 },
        };

        private Drawable text(string content) => new OsuTextFlowContainer { Width = content_width, AutoSizeAxes = Axes.Y, Text = content };
    }
}
