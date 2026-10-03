// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Reference;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the Tools tab. Whole-map tools live here; selection tools are in the compose screen's right-click menu.
    /// </summary>
    public partial class ToolsScreen : EditorScreen
    {
        private record Tool(string Name, IconUsage Icon, string Description, bool NeedsOverlay, Func<Drawable> CreatePanel);

        private static readonly Tool[] tools =
        {
            new Tool("Hitsound Copier", FontAwesome.Solid.VolumeUp,
                "Copies hitsounds from the overlay map (or the overlaid pattern) onto this difficulty.",
                true, () => new HitsoundCopierPanel { Width = 450 }),
            new Tool("Timing Copier", FontAwesome.Solid.Clock,
                "Replaces this difficulty's timing with the overlay map's, optionally moving or snapping objects to it.",
                true, () => new TimingCopierPanel { Width = 450 }),
            new Tool("Property Transformer", FontAwesome.Solid.SlidersH,
                "Multiplies and adds to timing, slider velocity, hitsound volume and index, object, bookmark, break and preview times of this difficulty. One undo step.",
                false, () => new PropertyTransformerPanel { Width = 450 }),
            new Tool("Rhythm Guide", FontAwesome.Solid.Drum,
                "Adds circles in the middle of the playfield at the rhythm of other difficulties (or only their hitsounds), snapped to 1/16 or 1/12, where this difficulty has nothing yet.",
                false, () => new RhythmGuidePanel { Width = 450 }),
            new Tool("Map Cleaner", FontAwesome.Solid.Broom,
                "Resnaps objects and bookmarks, removes or adds muting and deletes hitsound files nobody plays. Lazer already writes clean green lines on save, so that part of Mapping Tools' cleaner is not needed. One undo step.",
                false, () => new MapCleanerPanel { Width = 450 }),
            new Tool("Mapset Merger", FontAwesome.Solid.ObjectGroup,
                "Brings a difficulty of another set in your library (a guest difficulty, say) into this set. Its custom hitsound indices move past the ones this set uses and the files come along; it takes this set's metadata, audio and background.",
                false, () => new MapsetMergerPanel { Width = 450 }),
            new Tool("Combo Colour Studio", FontAwesome.Solid.Palette,
                "Colour haxing: colour points work like timing points for combo colours. From a point on, combos cycle through its colours; a burst point colours just the one short combo it sits on. Can also read the points back from a map.",
                false, () => new ComboColourStudioPanel { Width = 450 }),
            new Tool("Timing Helper", FontAwesome.Solid.Stopwatch,
                "Put markers (objects, bookmarks) exactly on the sounds, then it changes BPMs and adds red lines so every marker is snapped, preferring round BPMs. One undo step.",
                false, () => new TimingHelperPanel { Width = 450 }),
            new Tool("Pattern Gallery", FontAwesome.Solid.Shapes,
                "Saved patterns, ready to insert at the current time. They fit this map's BPM by beats. Each is a .osupattern file you can share.",
                false, () => new PatternGalleryPanel { RelativeSizeAxes = Axes.X }),
        };

        [Resolved]
        private EditorReferenceBeatmap reference { get; set; } = null!;

        private EditorToolButton[] toolButtons = null!;
        private OsuSpriteText title = null!;
        private OsuTextFlowContainer description = null!;
        private OsuSpriteText overlayStatus = null!;
        private Container panel = null!;

        private Tool? current;

        public ToolsScreen()
            : base(EditorScreenMode.Tools)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 250),
                    new Dimension(),
                },
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
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding(10),
                                        Spacing = new Vector2(5),
                                        Children = new Drawable[]
                                        {
                                            overlayStatus = new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Font = OsuFont.Default.With(size: 12),
                                                Colour = colourProvider.Content2,
                                            },
                                            new OsuSpriteText
                                            {
                                                Text = "Tools",
                                                Font = OsuFont.Default.With(weight: FontWeight.Bold),
                                                Margin = new MarginPadding { Top = 10 },
                                            },
                                        }.Concat(toolButtons = tools.Select(createButton).ToArray()).ToArray(),
                                    },
                                },
                            },
                        },
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(20),
                                Spacing = new Vector2(10),
                                Children = new Drawable[]
                                {
                                    title = new OsuSpriteText { Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold) },
                                    description = new OsuTextFlowContainer { Width = 450, AutoSizeAxes = Axes.Y },
                                    panel = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            reference.Beatmap.BindValueChanged(b =>
            {
                overlayStatus.Text = b.NewValue is IBeatmap beatmap
                    ? $"Overlay: {beatmap.BeatmapInfo.Metadata.Title} [{beatmap.BeatmapInfo.DifficultyName}]"
                    : "Overlay: nothing";
                updateDescription();
            }, true);

            toolButtons[0].Selected.Value = true;
        }

        private EditorToolButton createButton(Tool tool)
        {
            var button = new EditorToolButton(tool.Name, () => new SpriteIcon { Icon = tool.Icon }, () => null);

            button.Selected.BindValueChanged(s =>
            {
                if (s.NewValue)
                    show(tool, button);
                else if (current == tool)
                    Schedule(() => button.Selected.Value = true); // one tool always stays open
            });

            return button;
        }

        private void show(Tool tool, EditorToolButton button)
        {
            current = tool;

            foreach (var other in toolButtons.Where(b => b != button))
                other.Selected.Value = false;

            title.Text = tool.Name;
            panel.Child = tool.CreatePanel();
            updateDescription();
        }

        private void updateDescription()
        {
            if (current == null)
                return;

            description.Text = current.NeedsOverlay && reference.Beatmap.Value == null
                ? $"{current.Description}\nPick a map to overlay first: compose screen, overlay section, Map."
                : current.Description;
        }
    }
}
