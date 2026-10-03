// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the pattern gallery in the Tools tab. Patterns are saved from the compose screen (right-click, Tools).
    /// </summary>
    public partial class PatternGalleryPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        private PatternGallery gallery = null!;
        private FillFlowContainer list = null!;

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            gallery = new PatternGallery(storage);

            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new RoundedButton { Width = 150, Text = "Open folder", Action = () => gallery.Storage.PresentExternally() },
                        new RoundedButton { Width = 150, Text = "Refresh", Action = refresh },
                    }
                },
                list = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(15),
                },
            };

            refresh();
        }

        private void refresh()
        {
            string[] names = gallery.List().ToArray();

            list.Clear();

            if (names.Length == 0)
            {
                list.Add(new OsuTextFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = $"No patterns yet. Select objects on the compose screen, right-click, Tools, \"Save as pattern\". "
                           + $"Patterns from others go in the folder as {PatternGallery.EXTENSION} files.",
                });
                return;
            }

            foreach (string name in names)
                list.Add(createCard(name));
        }

        private Drawable createCard(string name)
        {
            const float width = 240;

            Drawable preview;
            string info;

            try
            {
                var pattern = gallery.Load(name, editorBeatmap.BeatmapInfo.Ruleset);
                preview = new PatternPreview(pattern) { Size = new Vector2(width, width * 0.75f) };
                info = $"{pattern.HitObjects.Count} object{(pattern.HitObjects.Count == 1 ? "" : "s")}";
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read the pattern \"{name}\": {e.Message}");
                preview = new Container { Size = new Vector2(width, width * 0.75f) };
                info = "Could not read this file";
            }

            return new FillFlowContainer
            {
                Width = width,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new[]
                {
                    preview,
                    new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = name,
                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText { Text = info, Font = OsuFont.Default.With(size: 12) },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4),
                        Children = new Drawable[]
                        {
                            new RoundedButton { Width = 90, Height = 30, Text = "Insert", Action = () => insert(name) },
                            new RoundedButton { Width = 80, Height = 30, Text = "Show file", Action = () => gallery.Storage.PresentFileExternally(PatternGallery.FileName(name)) },
                            new DangerousRoundedButton
                            {
                                Width = 62,
                                Height = 30,
                                Text = "Delete",
                                Action = () =>
                                {
                                    gallery.Delete(name);
                                    refresh();
                                }
                            },
                        }
                    },
                }
            };
        }

        /// <summary>
        /// Inserts a pattern with its first object at the current (snapped) time, selected, as one undoable change.
        /// </summary>
        private void insert(string name)
        {
            try
            {
                var pattern = gallery.Load(name, editorBeatmap.BeatmapInfo.Ruleset);
                var objects = PatternGallery.FitTo(pattern, editorBeatmap, editorBeatmap.SnapTime(editorClock.CurrentTime, null));

                editorBeatmap.BeginChange();
                editorBeatmap.SelectedHitObjects.Clear();
                editorBeatmap.AddRange(objects);
                editorBeatmap.SelectedHitObjects.AddRange(objects);
                editorBeatmap.EndChange();

                // Show it where it can be moved into place.
                editor?.Mode.Value = EditorScreenMode.Compose;
            }
            catch (Exception e)
            {
                // Logged errors are also shown to the user as a notification.
                Logger.Error(e, $"Could not insert the pattern \"{name}\".");
            }
        }
    }
}
