// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.MappingTools.Tutorial
{
    /// <summary>
    /// YAWNS: a small drawing surface for the tutorial's diagrams. Everything is drawn in playfield coordinates (512 x 384) and scaled to fit.
    /// </summary>
    public partial class DiagramCanvas : Container
    {
        public const float WIDTH = 512;
        public const float HEIGHT = 384;

        public static readonly Color4 ORIGINAL = new Color4(80, 160, 255, 255);
        public static readonly Color4 RESULT = new Color4(90, 230, 140, 255);
        public static readonly Color4 GUIDE = new Color4(255, 220, 60, 255);
        public static readonly Color4 AXIS = new Color4(70, 220, 240, 255);
        public static readonly Color4 ROUGH = new Color4(255, 100, 110, 255);
        public static readonly Color4 FAINT = new Color4(255, 255, 255, 90);
        public static readonly Color4 TEXT = new Color4(235, 235, 235, 255);

        private readonly Container content;

        /// <summary>
        /// Everything drawn so far, in playfield coordinates (for tests).
        /// </summary>
        public IEnumerable<Drawable> Shapes => content.Children;

        public DiagramCanvas(float width = 480)
        {
            Size = new Vector2(width, width * HEIGHT / WIDTH);
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 2;
            BorderColour = new Color4(255, 255, 255, 40);

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(22, 24, 30, 255) },
                content = new Container { Size = new Vector2(WIDTH, HEIGHT), Scale = new Vector2(width / WIDTH) },
            };

            // The 4:3 playfield outline, so it is clear this is playfield space.
            Frame(new Vector2(0, 0), new Vector2(WIDTH, HEIGHT), new Color4(255, 255, 255, 25), 2);
        }

        public static Vector2 Polar(Vector2 centre, float radius, double degrees) =>
            centre + radius * new Vector2((float)Math.Cos(degrees * Math.PI / 180), (float)Math.Sin(degrees * Math.PI / 180));

        public void Frame(Vector2 topLeft, Vector2 bottomRight, Color4 colour, float thickness = 2)
        {
            var tr = new Vector2(bottomRight.X, topLeft.Y);
            var bl = new Vector2(topLeft.X, bottomRight.Y);
            Line(topLeft, tr, colour, thickness);
            Line(tr, bottomRight, colour, thickness);
            Line(bottomRight, bl, colour, thickness);
            Line(bl, topLeft, colour, thickness);
        }

        public void Line(Vector2 a, Vector2 b, Color4 colour, float thickness = 2)
        {
            Vector2 d = b - a;
            float length = d.Length;

            if (length < 0.01f)
                return;

            content.Add(new Box
            {
                Origin = Anchor.Centre,
                Position = (a + b) / 2,
                Size = new Vector2(length, thickness),
                Rotation = (float)(Math.Atan2(d.Y, d.X) * 180 / Math.PI),
                Colour = colour,
            });
        }

        public void Dashed(Vector2 a, Vector2 b, Color4 colour, float thickness = 2, float dash = 8)
        {
            Vector2 d = b - a;
            float length = d.Length;

            if (length < 0.01f)
                return;

            Vector2 unit = d / length;

            for (float t = 0; t < length; t += dash * 2)
                Line(a + unit * t, a + unit * Math.Min(t + dash, length), colour, thickness);
        }

        public void Arrow(Vector2 a, Vector2 b, Color4 colour, float thickness = 3)
        {
            Line(a, b, colour, thickness);

            Vector2 d = (b - a).Normalized();
            Vector2 side = new Vector2(-d.Y, d.X);
            Line(b, b - d * 14 + side * 8, colour, thickness);
            Line(b, b - d * 14 - side * 8, colour, thickness);
        }

        public void Polyline(IReadOnlyList<Vector2> points, Color4 colour, float thickness = 3)
        {
            for (int i = 1; i < points.Count; i++)
                Line(points[i - 1], points[i], colour, thickness);
        }

        public void Curve(IReadOnlyList<Vector2> bezierPoints, Color4 colour, float thickness = 4, int segments = 40)
        {
            var points = new List<Vector2>();

            for (int i = 0; i <= segments; i++)
                points.Add(BezierTools.Evaluate(bezierPoints, i / (float)segments));

            Polyline(points, colour, thickness);
        }

        /// <summary>
        /// A circle: filled, with an optional number like a hit circle.
        /// </summary>
        public void Circle(Vector2 position, Color4 colour, float radius = 22, string? number = null, float alpha = 1)
        {
            content.Add(new CircularContainer
            {
                Origin = Anchor.Centre,
                Position = position,
                Size = new Vector2(radius * 2),
                Masking = true,
                BorderThickness = 3,
                BorderColour = Color4.White,
                Alpha = alpha,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = 0.55f },
                }
            });

            if (number != null)
                Label(number, position, 16, Color4.White);
        }

        public void Ring(Vector2 centre, float radius, Color4 colour, float thickness = 2)
        {
            content.Add(new CircularContainer
            {
                Origin = Anchor.Centre,
                Position = centre,
                Size = new Vector2(radius * 2),
                Masking = true,
                BorderThickness = thickness,
                BorderColour = colour,
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
            });
        }

        public void Dot(Vector2 position, Color4 colour, float size = 9)
        {
            content.Add(new CircularContainer
            {
                Origin = Anchor.Centre,
                Position = position,
                Size = new Vector2(size),
                Masking = true,
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
            });
        }

        /// <summary>
        /// A slider: a thick translucent body along the curve, a head circle and an end circle.
        /// </summary>
        public void Slider(IReadOnlyList<Vector2> bezierPoints, Color4 colour, float radius = 18, float alpha = 1)
        {
            var points = new List<Vector2>();

            for (int i = 0; i <= 30; i++)
                points.Add(BezierTools.Evaluate(bezierPoints, i / 30f));

            var body = new Color4(colour.R, colour.G, colour.B, 0.35f * alpha);
            Polyline(points, body, radius * 2);

            foreach (var p in points)
                Dot(p, body, radius * 2);

            Circle(points[0], colour, radius, null, alpha);
            Circle(points[^1], colour, radius * 0.8f, null, alpha * 0.7f);
        }

        public void Label(string text, Vector2 position, float size = 16, Color4? colour = null, Anchor origin = Anchor.Centre)
        {
            content.Add(new OsuSpriteText
            {
                Origin = origin,
                Position = position,
                Text = text,
                Font = OsuFont.Default.With(size: size, weight: FontWeight.SemiBold),
                Colour = colour ?? TEXT,
            });
        }

        /// <summary>
        /// A labelled rounded box, for the diagrams of tools that are not about the playfield.
        /// </summary>
        public void Box(Vector2 topLeft, Vector2 size, string text, Color4 colour, float textSize = 16)
        {
            content.Add(new Container
            {
                Position = topLeft,
                Size = size,
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 2,
                BorderColour = colour,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = 0.18f },
                }
            });

            Label(text, topLeft + size / 2, textSize, Color4.White);
        }

        public static Vector2 Mirror(Vector2 p, Vector2 through, double lineDegrees) => Symmetry.Linear.Reflection(lineDegrees).ApplyAround(through, p);
    }
}
