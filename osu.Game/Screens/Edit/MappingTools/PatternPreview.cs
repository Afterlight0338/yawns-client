// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: a small drawing of a pattern, zoomed to fit, for the pattern gallery.
    /// </summary>
    public partial class PatternPreview : CompositeDrawable
    {
        private readonly IBeatmap pattern;

        public PatternPreview(IBeatmap pattern)
        {
            this.pattern = pattern;
            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            float radius = 54.4f - 4.48f * pattern.Difficulty.CircleSize;

            // Earlier objects are drawn on top, like in gameplay.
            var objects = pattern.HitObjects.Where(h => h is IHasPosition).OrderBy(h => h.StartTime).ToList();

            var canvas = new Container { Size = new Vector2(512, 384) };

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var h = objects[i];
                var position = ((IHasPosition)h).Position;
                Color4 colour = i == 0 ? colours.Yellow : Color4.White;

                if (h is IHasPath slider)
                {
                    var body = new SmoothPath
                    {
                        PathRadius = radius * 0.85f,
                        Colour = colour.Opacity(0.3f),
                        Vertices = slider.Path.CalculatedPath.ToList(),
                    };
                    body.Position = position;
                    body.OriginPosition = body.PositionInBoundingBox(Vector2.Zero);
                    canvas.Add(body);
                }

                if (h is IHasDuration && h is not IHasPath)
                {
                    // Spinner.
                    canvas.Add(ring(new Vector2(256, 192), 80, colour, null));
                    continue;
                }

                int? number = (h as IHasComboInformation)?.IndexInCurrentCombo + 1;
                canvas.Add(ring(position, radius, colour, number));
            }

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background5 },
                canvas,
            };

            fit(canvas, objects, radius);
        }

        /// <summary>
        /// Zooms <paramref name="canvas"/> (in playfield coordinates) so the pattern fills this preview.
        /// </summary>
        private void fit(Container canvas, List<HitObject> objects, float radius)
        {
            var points = objects.SelectMany(h =>
            {
                var position = ((IHasPosition)h).Position;
                return h is IHasPath slider ? slider.Path.CalculatedPath.Select(p => p + position) : new[] { position };
            }).ToList();

            if (points.Count == 0)
                return;

            Vector2 min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y)) - new Vector2(radius * 1.2f);
            Vector2 max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y)) + new Vector2(radius * 1.2f);
            Vector2 size = max - min;

            float scale = Math.Min(Width / size.X, Height / size.Y);

            canvas.Scale = new Vector2(scale);
            // Centre the pattern in the preview.
            canvas.Position = (new Vector2(Width, Height) - size * scale) / 2 - min * scale;
        }

        private static Drawable ring(Vector2 position, float radius, Color4 colour, int? number) => new CircularContainer
        {
            Origin = Anchor.Centre,
            Position = position,
            Size = new Vector2(radius * 2),
            Masking = true,
            BorderThickness = radius * 0.18f,
            BorderColour = colour,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Opacity(0.15f) },
                new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = number?.ToString() ?? string.Empty,
                    Font = OsuFont.Default.With(size: radius, weight: FontWeight.Bold),
                    Colour = colour,
                },
            }
        };
    }
}
