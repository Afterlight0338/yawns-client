// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: draws the <see cref="EditorReferenceBeatmap"/> under the edited beatmap, following the editor clock shifted by the reference offset.
    /// Display only: it takes no input and plays no hitsounds.
    /// </summary>
    public partial class ReferenceBeatmapLayer<TObject> : CompositeDrawable, ISamplePlaybackDisabler
        where TObject : HitObject
    {
        private readonly EditorClock editorClock;
        private readonly Func<IBeatmap, DrawableRuleset<TObject>> createDrawableRuleset;
        private readonly FramedOffsetClock clock;

        private readonly EditorReferenceBeatmap reference;

        private readonly Bindable<IBeatmap?> beatmap = new Bindable<IBeatmap?>();
        private readonly Bindable<(double Start, double End)?> pattern = new Bindable<(double Start, double End)?>();
        private readonly IBindable<double> offset;
        private readonly BindableFloat opacity = new BindableFloat();
        private readonly Bindable<Live<SkinInfo>> skin = new Bindable<Live<SkinInfo>>();

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        /// <summary>
        /// The reference currently shown (or loading), if any.
        /// </summary>
        public DrawableRuleset<TObject>? DrawableRuleset { get; private set; }

        public ReferenceBeatmapLayer(EditorReferenceBeatmap reference, EditorClock editorClock, Func<IBeatmap, DrawableRuleset<TObject>> createDrawableRuleset)
        {
            this.reference = reference;
            this.editorClock = editorClock;
            this.createDrawableRuleset = createDrawableRuleset;

            RelativeSizeAxes = Axes.Both;

            // The editor processes its own clock each frame, so only this offset clock is processed here.
            Clock = clock = new FramedOffsetClock(editorClock, processSource: false);

            beatmap.BindTo(reference.Beatmap);
            pattern.BindTo(reference.Pattern);
            offset = reference.DisplayOffset.GetBoundCopy();
            opacity.BindTo(reference.Opacity);
            skin.BindTo(reference.Skin);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            offset.BindValueChanged(o => clock.Offset = -o.NewValue, true);
            opacity.BindValueChanged(o => Alpha = o.NewValue, true);
            // Loading a new reference also resets the pattern, show once for both.
            beatmap.BindValueChanged(_ => Scheduler.AddOnce(show), true);
            pattern.BindValueChanged(_ => Scheduler.AddOnce(show));
            skin.BindValueChanged(_ => Scheduler.AddOnce(show));
        }

        private void show()
        {
            ClearInternal();
            DrawableRuleset = null;

            if (beatmap.Value is not IBeatmap full)
                return;

            IBeatmap newBeatmap = full;

            // While a pattern is overlaid, only its objects are drawn. They keep the reference timing, the offset places them.
            if (pattern.Value != null)
            {
                // A shallow clone keeps the ruleset's own beatmap type (osu! autoplay needs it), only the object list is replaced.
                var patternBeatmap = (Beatmap<TObject>)full.Clone();
                patternBeatmap.HitObjects = full.HitObjects.OfType<TObject>().Where(reference.IsDisplayed).ToList();
                newBeatmap = patternBeatmap;
            }

            DrawableRuleset<TObject> drawableRuleset;

            try
            {
                drawableRuleset = createDrawableRuleset(newBeatmap);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not display the overlay map.");
                return;
            }

            drawableRuleset.FrameStablePlayback = false;
            drawableRuleset.Playfield.DisplayJudgements.Value = false;

            DrawableRuleset = drawableRuleset;

            Drawable content = drawableRuleset;

            // Its own skin chain, so neither the editor skin nor the edited map's beatmap skin leaks into the overlay.
            if (!skin.Value.Equals(EditorReferenceBeatmap.SAME_AS_EDITOR))
            {
                content = new OverlaySkinSource(skin.Value.PerformRead(skinManager.GetSkin), skinManager)
                {
                    Child = new RulesetSkinProvidingContainer(drawableRuleset.Ruleset, newBeatmap, null)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = drawableRuleset,
                    },
                };
            }

            LoadComponentAsync(content, loaded =>
            {
                // A different reference was picked while this one was loading.
                if (drawableRuleset != DrawableRuleset)
                {
                    loaded.Dispose();
                    return;
                }

                AddInternal(loaded);

                // Autoplay hits the reference objects so they animate like the edited ones.
                var autoplay = drawableRuleset.Mods.OfType<ModAutoplay>().SingleOrDefault();
                if (autoplay != null)
                    drawableRuleset.SetReplayScore(autoplay.CreateScoreFromReplayData(drawableRuleset.Beatmap, drawableRuleset.Mods));
            });
        }

        protected override void Update()
        {
            base.Update();

            if (DrawableRuleset?.IsLoaded != true)
                return;

            // Same reasoning as DrawableEditorRulesetWrapper: frame stability only while playing, never across large seeks.
            bool inLargeSeek = Math.Abs(DrawableRuleset.FrameStableClock.CurrentTime - clock.CurrentTime) > 1000;
            DrawableRuleset.FrameStablePlayback = editorClock.IsRunning && !inLargeSeek;
        }

        public override bool PropagatePositionalInputSubTree => false;

        public override bool PropagateNonPositionalInputSubTree => false;

        // Only the edited beatmap should be heard. The interface is [Cached], so this covers every sample inside the layer.
        IBindable<bool> ISamplePlaybackDisabler.SamplePlaybackDisabled { get; } = new Bindable<bool>(true);

        /// <summary>
        /// The chosen skin with the same fallbacks as <see cref="SkinManager.AllSources"/>, and nothing from the parents.
        /// </summary>
        private partial class OverlaySkinSource : SkinProvidingContainer
        {
            private readonly List<Skin> owned = new List<Skin>();

            protected override bool AllowFallingBackToParent => false;

            public OverlaySkinSource(Skin skin, SkinManager skins)
            {
                owned.Add(skin);

                var sources = new List<ISkin> { skin };

                if (skin is LegacySkin)
                    sources.Add(skins.DefaultClassicSkin);

                if (skin is not TrianglesSkin)
                {
                    var triangles = new TrianglesSkin(skins);
                    owned.Add(triangles);
                    sources.Add(triangles);
                }

                SetSources(sources);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                foreach (var s in owned)
                    s.Dispose();
            }
        }
    }
}
