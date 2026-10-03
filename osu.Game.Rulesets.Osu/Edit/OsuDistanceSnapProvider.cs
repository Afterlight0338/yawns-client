// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    public partial class OsuDistanceSnapProvider : ComposerDistanceSnapProvider
    {
        public override double ReadCurrentDistanceSnap(HitObject before, HitObject after)
        {
            // If the pair of hit objects in question here could feasibly be on the same stack, do not provide a distance snap value -
            // they're likely too close to one another for the distance snap value to be useful anyway even if they somehow are not.
            if (Vector2.Distance(((OsuHitObject)before).EndPosition, ((OsuHitObject)after).Position) < OsuBeatmapProcessor.STACK_DISTANCE)
                return 0;

            var lastObjectWithVelocity = EditorBeatmap.HitObjects.TakeWhile(ho => ho != after).OfType<IHasSliderVelocity>().LastOrDefault();

            float expectedDistance = DurationToDistance(after.StartTime - before.GetEndTime(), before.StartTime, lastObjectWithVelocity);
            float actualDistance = Vector2.Distance(((OsuHitObject)before).StackedEndPosition, ((OsuHitObject)after).StackedPosition);

            return actualDistance / expectedDistance;
        }

        // YAWNS: sets the distance spacing so it continues the distance between an object and the one before it
        // (the selected object, or the last one at or before the current time). Unbound by default.
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e) =>
            e.Action == GlobalAction.EditorContinueDistanceSnap && !e.Repeat ? ContinueDistanceSnap() : base.OnPressed(e);

        public bool ContinueDistanceSnap()
        {
            var after = EditorBeatmap.SelectedHitObjects.OrderBy(h => h.StartTime).LastOrDefault()
                        ?? EditorBeatmap.HitObjects.LastOrDefault(h => h.StartTime <= editorClock.CurrentTime);
            var before = after == null ? null : EditorBeatmap.HitObjects.TakeWhile(h => h != after).LastOrDefault();

            if (after == null || before == null)
                return false;

            double spacing = ReadCurrentDistanceSnap(before, after);

            if (spacing <= 0)
                return false;

            DistanceSpacingMultiplier.Value = System.Math.Round(spacing, 2);
            DistanceSnapToggle.Value = TernaryState.True;
            return true;
        }

        protected override bool AdjustDistanceSpacing(GlobalAction action, float amount)
        {
            // To allow better visualisation, ensure that the spacing grid is visible before adjusting.
            DistanceSnapToggle.Value = TernaryState.True;

            return base.AdjustDistanceSpacing(action, amount);
        }

        public override IEnumerable<DrawableTernaryButton> CreateTernaryButtons() => new[]
        {
            new DrawableTernaryButton<OsuAction>
            {
                Current = DistanceSnapToggle,
                Description = "Distance Snap",
                CreateIcon = () => new SpriteIcon { Icon = OsuIcon.EditorDistanceSnap },
                Action = OsuAction.EditorToggleDistanceSnap,
                Hotkey = new Hotkey(OsuRuleset.SHORT_NAME, Ruleset.EDITOR_VARIANT, (int)OsuAction.EditorToggleDistanceSnap),
            }
        };
    }
}
