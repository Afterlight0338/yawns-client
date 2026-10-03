// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: the yellow mirror line of <see cref="AngledFlip"/>, shown while its popover is open.
    /// </summary>
    public partial class FlipGuide : CompositeDrawable
    {
        private readonly AngledFlip flip;
        private readonly Box line;

        public FlipGuide(AngledFlip flip)
        {
            this.flip = flip;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChild = line = new Box
            {
                Origin = Anchor.Centre,
                Size = new Vector2(2000, 2),
                Colour = Color4.Yellow,
                EdgeSmoothness = new Vector2(1),
            };
        }

        protected override void Update()
        {
            base.Update();

            Alpha = flip.GuideCentre.Value == null ? 0 : 1;

            if (flip.GuideCentre.Value is Vector2 centre)
            {
                line.Position = centre;
                line.Rotation = (float)flip.LineAngle;
            }
        }
    }
}
