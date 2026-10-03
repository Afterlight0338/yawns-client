// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using static osu.Game.Screens.Edit.MappingTools.ComboColourStudio;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: Combo Colour Studio in the Tools tab. The colour points of each difficulty are kept in the game folder (combo-colour-studio/).
    /// </summary>
    public partial class ComboColourStudioPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        private readonly BindableInt maxBurstLength = new BindableInt(1) { MinValue = 1, MaxValue = 8 };
        private readonly List<Row> rows = new List<Row>();

        private Storage storage = null!;
        private FillFlowContainer swatches = null!;
        private FillFlowContainer rowFlow = null!;
        private OsuTextFlowContainer result = null!;

        private int colourCount => editorBeatmap.BeatmapSkin?.ComboColours.Count ?? 0;

        private string fileName => $"{editorBeatmap.BeatmapInfo.ID}.json";

        [BackgroundDependencyLoader]
        private void load(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory("combo-colour-studio");

            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            AddRange(new Drawable[]
            {
                swatches = new FillFlowContainer { AutoSizeAxes = Axes.Both, Spacing = new Vector2(4) },
                new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 13))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "Colours are numbered as in Setup, Colours (edit them there). A point's sequence is colour numbers like \"1 3\". "
                           + "A burst point only colours the one short combo it sits on.",
                },
                rowFlow = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(3) },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Add a colour point at the current time", Action = () => addRow(new ColourPoint(Math.Round(clock.CurrentTime), new[] { 0 })) },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Read colour points from this difficulty", Action = readFromMap },
                new FormSliderBar<int> { Caption = "Burst length", HintText = "Combos with at most this many objects can take a burst point.", Current = maxBurstLength },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Apply to this difficulty", Action = apply },
                result = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
            });

            if (editorBeatmap.BeatmapSkin != null)
                editorBeatmap.BeatmapSkin.ComboColours.BindCollectionChanged((_, _) => Schedule(updateSwatches), true);

            foreach (var point in loadPoints())
                addRow(point);
        }

        private void updateSwatches()
        {
            swatches.Clear();

            if (colourCount == 0)
            {
                swatches.Add(new OsuSpriteText { Text = "This difficulty has no combo colours of its own. Add some in Setup, Colours first." });
                return;
            }

            var colours = editorBeatmap.BeatmapSkin!.ComboColours;

            for (int i = 0; i < colours.Count; i++)
            {
                swatches.Add(new CircularContainer
                {
                    Size = new Vector2(26),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours[i] },
                        new OsuSpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = (i + 1).ToString(), Colour = Colour4.Black, Font = OsuFont.Default.With(weight: FontWeight.Bold) },
                    }
                });
            }
        }

        private void addRow(ColourPoint point)
        {
            var row = new Row(point, r =>
            {
                rows.Remove(r);
                rowFlow.Remove(r, true);
                savePoints();
            }, savePoints);

            rows.Add(row);
            rowFlow.Add(row);
            savePoints();
        }

        private void readFromMap()
        {
            if (colourCount == 0)
            {
                result.Text = "No combo colours to read.";
                return;
            }

            foreach (var row in rows.ToArray())
                rowFlow.Remove(row, true);
            rows.Clear();

            foreach (var point in Read(editorBeatmap, colourCount, maxBurstLength.Value))
                addRow(point);

            result.Text = $"Read {rows.Count} colour points.";
        }

        private void apply()
        {
            if (colourCount == 0)
            {
                result.Text = "Add combo colours in Setup, Colours first.";
                return;
            }

            var points = rows.Select(r => r.Point).ToList();

            if (points.Any(p => p.Sequence.Any(c => c < 0 || c >= colourCount)))
            {
                result.Text = $"Sequences can only use colours 1 to {colourCount}.";
                return;
            }

            int changed = ComboColourStudio.Apply(editorBeatmap, points, colourCount, maxBurstLength.Value);
            result.Text = $"Changed the colour of {changed} combos. One undo step.";
        }

        private IEnumerable<ColourPoint> loadPoints()
        {
            try
            {
                using var stream = storage.GetStream(fileName);
                return stream == null ? Enumerable.Empty<ColourPoint>() : JsonConvert.DeserializeObject<List<ColourPoint>>(new StreamReader(stream).ReadToEnd()) ?? new List<ColourPoint>();
            }
            catch
            {
                return Enumerable.Empty<ColourPoint>();
            }
        }

        private void savePoints()
        {
            using var stream = storage.CreateFileSafely(fileName);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(rows.Select(r => r.Point).OrderBy(p => p.Time).ToList()));
        }

        private partial class Row : FillFlowContainer
        {
            private readonly Bindable<string> time = new Bindable<string>();
            private readonly Bindable<string> sequence = new Bindable<string>();
            private readonly BindableBool burst = new BindableBool();

            public ColourPoint Point => new ColourPoint(
                double.TryParse(time.Value, out double t) ? t : 0,
                sequence.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out int c) ? c - 1 : -1).ToArray(),
                burst.Value);

            public Row(ColourPoint point, Action<Row> remove, Action changed)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(5);

                time.Value = point.Time.ToString("0");
                sequence.Value = string.Join(' ', point.Sequence.Select(c => c + 1));
                burst.Value = point.Burst;

                Children = new Drawable[]
                {
                    new FormTextBox { Width = 120, Caption = "Time (ms)", Current = time },
                    new FormTextBox { Width = 170, Caption = "Colours", Current = sequence },
                    new FormCheckBox { Width = 90, Caption = "Burst", Current = burst },
                    new IconButton { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Icon = osu.Framework.Graphics.Sprites.FontAwesome.Solid.Times, Action = () => remove(this) },
                };

                time.BindValueChanged(_ => changed());
                sequence.BindValueChanged(_ => changed());
                burst.BindValueChanged(_ => changed());
            }
        }
    }
}
