// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: the centre the symmetry tools (radial copy, mirror copy, radial guide, perfect it) work around.
    /// </summary>
    public class SymmetryCentre
    {
        public enum Mode
        {
            [Description("Playfield centre")]
            Playfield,

            [Description("Selection centre")]
            Selection,

            [Description("Radial guide centre")]
            Guide,
        }

        public readonly Bindable<Mode> Around = new Bindable<Mode>();

        /// <summary>
        /// The radial guide's centre, set by dragging its marker.
        /// </summary>
        public readonly Bindable<Vector2> Guide = new Bindable<Vector2>(OsuPlayfield.BASE_SIZE / 2);

        public Vector2 Resolve(IReadOnlyCollection<OsuHitObject> selection) => Resolve(selection.Select(h => h.Position).ToArray());

        /// <summary>
        /// The centre for objects at these positions (so a preview can ask about the original positions while objects are moved).
        /// </summary>
        public Vector2 Resolve(IReadOnlyCollection<Vector2> selection)
        {
            switch (Around.Value)
            {
                case Mode.Selection when selection.Count > 0:
                    return (new Vector2(selection.Min(p => p.X), selection.Min(p => p.Y))
                            + new Vector2(selection.Max(p => p.X), selection.Max(p => p.Y))) / 2;

                case Mode.Guide:
                    return Guide.Value;

                default:
                    return OsuPlayfield.BASE_SIZE / 2;
            }
        }
    }
}
