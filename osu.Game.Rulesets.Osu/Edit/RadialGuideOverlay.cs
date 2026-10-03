// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: draws a <see cref="RadialGuide"/> under the objects.
    /// </summary>
    public partial class RadialGuideOverlay : CompositeDrawable
    {
        private readonly RadialGuide guide;
        private readonly Container shapes;

        private readonly Bindable<Vector2> centre = new Bindable<Vector2>();

        public RadialGuideOverlay(RadialGuide guide)
        {
            this.guide = guide;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChild = shapes = new Container { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            centre.BindTo(guide.Centre.Guide);

            guide.Enabled.BindValueChanged(e => this.FadeTo(e.NewValue ? 1 : 0, 200), true);
            guide.Spokes.BindValueChanged(_ => Scheduler.AddOnce(redraw));
            guide.Offset.BindValueChanged(_ => Scheduler.AddOnce(redraw));
            guide.Rings.BindValueChanged(_ => Scheduler.AddOnce(redraw));
            guide.RingSpacing.BindValueChanged(_ => Scheduler.AddOnce(redraw));
            centre.BindValueChanged(_ => Scheduler.AddOnce(redraw), true);
        }

        private void redraw()
        {
            shapes.Clear();

            Vector2 middle = centre.Value;

            for (int r = 1; r <= guide.Rings.Value; r++)
            {
                float radius = r * guide.RingSpacing.Value;

                shapes.Add(new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = middle,
                    Size = new Vector2(radius * 2),
                    Masking = true,
                    BorderThickness = 1.5f,
                    BorderColour = Color4.Cyan,
                    Alpha = 0.35f,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                });
            }

            for (int s = 0; s < guide.Spokes.Value; s++)
            {
                Vector2 direction = guide.SpokeDirection(s);

                shapes.Add(new Box
                {
                    Origin = Anchor.CentreLeft,
                    Position = middle,
                    Size = new Vector2(700, 1.5f),
                    Rotation = (float)(System.Math.Atan2(direction.Y, direction.X) * 180 / System.Math.PI),
                    Colour = Color4.Cyan,
                    Alpha = 0.35f,
                });
            }

            shapes.Add(new Circle { Origin = Anchor.Centre, Position = middle, Size = new Vector2(10), Colour = Color4.Cyan });
        }
    }

    /// <summary>
    /// YAWNS: the draggable centre of a <see cref="RadialGuide"/>. It sits above the objects and only takes input on the marker itself.
    /// </summary>
    public partial class RadialGuideHandle : CompositeDrawable
    {
        private const float grab_radius = 12;

        private readonly RadialGuide guide;
        private readonly Bindable<Vector2> centre = new Bindable<Vector2>();

        public RadialGuideHandle(RadialGuide guide)
        {
            this.guide = guide;

            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            centre.BindTo(guide.Centre.Guide);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            guide.Enabled.Value && Vector2.Distance(ToScreenSpace(centre.Value), screenSpacePos) <= grab_radius * (ScreenSpaceDrawQuad.Width / DrawWidth);

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e)
        {
            Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
            centre.Value = new Vector2(System.Math.Clamp(local.X, -100, 612), System.Math.Clamp(local.Y, -100, 484));
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
        }

        protected override bool OnHover(HoverEvent e) => true;
    }
}
