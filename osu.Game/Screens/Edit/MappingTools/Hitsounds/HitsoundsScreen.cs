// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Screens.Edit.Reference;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: the Hitsounds tab, a native port of Hitsound Studio (https://hitsound.vivlos.dev).
    /// Lanes of hits on a timeline, written to a hitsound difficulty (circles in the middle of the playfield) and copied onto the other difficulties.
    /// </summary>
    public partial class HitsoundsScreen : EditorScreen
    {
        public const float ROW_HEIGHT = 34;
        public const float GHOST_ROW_HEIGHT = 22;
        public const float HEADER_WIDTH = 270;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private EditorReferenceBeatmap? reference { get; set; }

        /// <summary>
        /// Which map's objects are drawn as ghost notes behind the lanes.
        /// </summary>
        public readonly Bindable<GhostChoice> Ghost = new Bindable<GhostChoice>(GhostChoice.OVERLAY);

        /// <summary>
        /// The ghost notes changed (another choice, or the overlay map or its offset changed).
        /// </summary>
        public event Action? GhostChanged;

        private IBeatmap? ghostDifficulty;
        private PopoverButton ghostButton = null!;

        private readonly List<HitsoundLane> lanes = new List<HitsoundLane>();
        private List<HitsoundTrigger> triggers = new List<HitsoundTrigger>();
        private List<HitsoundTrigger> clipboard = new List<HitsoundTrigger>();

        public IReadOnlyList<HitsoundLane> Lanes => lanes;

        public IReadOnlyList<HitsoundTrigger> Triggers => triggers;

        public readonly HashSet<HitsoundTrigger> Selection = new HashSet<HitsoundTrigger>();

        /// <summary>
        /// Lanes or hits changed (the sequencer redraws).
        /// </summary>
        public event Action? StateChanged;

        /// <summary>
        /// Only a hitsound difficulty is edited here: its objects are rewritten from the lanes. A gameplay difficulty is shown read-only.
        /// </summary>
        public bool Editable { get; private set; }

        private bool writing;

        private FillFlowContainer laneHeaders = null!;
        private LaneScrollContainer laneScroll = null!;
        private GridContainer lanesArea = null!;
        private Container laneList = null!;
        private OsuTextFlowContainer status = null!;
        private Container notEditableBanner = null!;

        public HitsoundsScreen()
            : base(EditorScreenMode.Hitsounds)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Child = new EditorSkinProvidingContainer(EditorBeatmap).WithChild(new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background4 },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding(10),
                                    Spacing = new Vector2(8),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(8),
                                            Children = new Drawable[]
                                            {
                                                new PopoverButton("Add lane", () => new AddLanePopover(this)) { Width = 120 },
                                                new PopoverButton("Import hitsounds from...", () => new DifficultyListPopover(this, DifficultyListPopover.Purpose.Import)) { Width = 200 },
                                                new PopoverButton("Copy to difficulties...", () => new DifficultyListPopover(this, DifficultyListPopover.Purpose.CopyTo)) { Width = 200 },
                                                ghostButton = new PopoverButton(string.Empty, () => new GhostPopover(this)) { Width = 280 },
                                            }
                                        },
                                        status = new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 13))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Text = "Click a lane to add or remove a hit. Ctrl+drag paints, right-drag erases, Shift+drag selects (Delete, Ctrl+C/X/V at the playhead). "
                                                   + "Scroll or drag to move in time, Alt+scroll zooms.",
                                        },
                                        notEditableBanner = new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Alpha = 0,
                                            Child = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(10),
                                                Children = new Drawable[]
                                                {
                                                    new OsuTextFlowContainer
                                                    {
                                                        Width = 520,
                                                        AutoSizeAxes = Axes.Y,
                                                        Colour = colourProvider.Highlight1,
                                                        Text = "This difficulty has gameplay objects, so it is shown read-only. Hitsounds are made on a separate hitsound difficulty: "
                                                               + "create one, then import this difficulty's hitsounds into it.",
                                                    },
                                                    new RoundedButton
                                                    {
                                                        Width = 220,
                                                        Text = "Create hitsound difficulty",
                                                        Action = () => editor?.CreateNewDifficulty(EditorBeatmap.BeatmapInfo.Ruleset),
                                                    },
                                                }
                                            }
                                        },
                                    }
                                },
                            }
                        },
                    },
                    new Drawable[]
                    {
                        // YAWNS: lanes scroll vertically when there are more than fit; the editor's own green behind them, not the beatmap background.
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background4 },
                                laneScroll = new LaneScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = lanesArea = new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.Absolute, HEADER_WIDTH),
                                            new Dimension(),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                laneList = new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Children = new Drawable[]
                                                    {
                                                        new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background5 },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Children = new Drawable[]
                                                            {
                                                                new Container
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    Height = GHOST_ROW_HEIGHT,
                                                                    Child = new OsuSpriteText
                                                                    {
                                                                        Anchor = Anchor.CentreLeft,
                                                                        Origin = Anchor.CentreLeft,
                                                                        X = 10,
                                                                        Text = "overlay map",
                                                                        Font = OsuFont.Default.With(size: 12),
                                                                        Colour = colourProvider.Content2,
                                                                    },
                                                                },
                                                                laneHeaders = new FillFlowContainer
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Vertical,
                                                                },
                                                            }
                                                        },
                                                    }
                                                },
                                                new HitsoundSequencer(this),
                                            },
                                        },
                                    },
                                },
                            }
                        },
                    },
                },
            }).With(c => c.Add(new HitsoundPlayer(this)));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EditorBeatmap.HitObjectAdded += onExternalChange;
            EditorBeatmap.HitObjectRemoved += onExternalChange;

            laneScroll.LaneList = laneList;

            reimport();

            // As Hitsound Studio: a hitsound difficulty ghosts the hardest playable difficulty, anything else the overlay map.
            var hardest = OtherDifficulties.LastOrDefault();
            if (Editable && hardest != null)
                Ghost.Value = GhostChoices.First(c => hardest.Equals(c.Difficulty));

            Ghost.BindValueChanged(e =>
            {
                ghostDifficulty = e.NewValue.Difficulty == null ? null : beatmapManager.GetWorkingBeatmap(e.NewValue.Difficulty).GetPlayableBeatmap(EditorBeatmap.BeatmapInfo.Ruleset);
                ghostButton.Text = $"Ghost notes: {e.NewValue.Name}";
                GhostChanged?.Invoke();
            }, true);

            if (reference != null)
            {
                reference.Beatmap.BindValueChanged(_ => overlayChanged());
                reference.DisplayOffset.BindValueChanged(_ => overlayChanged());
                reference.Pattern.BindValueChanged(_ => overlayChanged());
            }
        }

        private void overlayChanged()
        {
            if (Ghost.Value == GhostChoice.OVERLAY)
                GhostChanged?.Invoke();
        }

        public IEnumerable<GhostChoice> GhostChoices => new[] { GhostChoice.NONE, GhostChoice.OVERLAY }
            .Concat(OtherDifficulties.Select(d => new GhostChoice(d.DifficultyName, d)));

        /// <summary>
        /// Start and end times of the ghost notes.
        /// </summary>
        public IEnumerable<(double Start, double End)> GhostObjects
        {
            get
            {
                if (ghostDifficulty != null)
                    return ghostDifficulty.HitObjects.Select(h => (h.StartTime, h.GetEndTime()));

                if (Ghost.Value != GhostChoice.OVERLAY || reference == null)
                    return Enumerable.Empty<(double, double)>();

                double offset = reference.DisplayOffset.Value;
                return reference.DisplayedObjects.Select(h => (h.StartTime + offset, h.GetEndTime() + offset));
            }
        }

        protected override void Update()
        {
            base.Update();

            // As tall as the lanes need, and at least the visible area so the waveform and grid fill it.
            lanesArea.Height = Math.Max(laneScroll.DrawHeight, GHOST_ROW_HEIGHT + lanes.Count * ROW_HEIGHT + ROW_HEIGHT);
        }

        /// <summary>
        /// The wheel scrolls the lanes while the cursor is over the lane list. Over the sequencer it moves in time as everywhere in the editor.
        /// </summary>
        private partial class LaneScrollContainer : OsuScrollContainer
        {
            public Drawable? LaneList;

            protected override bool OnScroll(ScrollEvent e) => LaneList?.ReceivePositionalInputAt(e.ScreenSpaceMousePosition) == true && base.OnScroll(e);
        }

        // Undo, redo and the other tabs change the objects: read the hits back. Our own writes are skipped.
        private void onExternalChange(HitObject _)
        {
            if (!writing)
                Scheduler.AddOnce(reimport);
        }

        private void reimport()
        {
            Editable = HitsoundProject.IsHitsoundDifficulty(EditorBeatmap);
            notEditableBanner.FadeTo(Editable ? 0 : 1, 200);

            triggers = HitsoundProject.Import(EditorBeatmap);
            Selection.Clear();

            foreach (var sound in triggers.Select(t => t.Sound).Distinct())
                ensureLane(sound);

            updateCanCopyPaste();
            notifyChanged();
        }

        private HitsoundLane ensureLane(HitsoundSound sound)
        {
            var lane = lanes.FirstOrDefault(l => l.Sound == sound);

            if (lane == null)
            {
                lanes.Add(lane = new HitsoundLane(sound, lanes.Count));
                laneHeaders.Add(new HitsoundLaneHeader(lane, () => RemoveLane(lane)) { Height = ROW_HEIGHT });
            }

            return lane;
        }

        private void notifyChanged() => StateChanged?.Invoke();

        #region Editing

        /// <summary>
        /// Adds a lane for a sound (or returns the existing one).
        /// </summary>
        public void AddLane(HitsoundSound sound)
        {
            ensureLane(sound);
            notifyChanged();
        }

        public void RemoveLane(HitsoundLane lane)
        {
            if (!Editable && triggers.Any(t => t.Sound == lane.Sound))
                return;

            lanes.Remove(lane);
            laneHeaders.RemoveAll(h => ((HitsoundLaneHeader)h).Lane == lane, true);
            triggers.RemoveAll(t => t.Sound == lane.Sound);
            Commit();
        }

        public HitsoundTrigger? FindHit(HitsoundLane lane, double time, double tolerance) =>
            triggers.Where(t => t.Sound == lane.Sound && Math.Abs(t.Time - time) <= tolerance).MinBy(t => Math.Abs(t.Time - time));

        public double Snap(double time) => Math.Round(EditorBeatmap.SnapTime(time, null));

        /// <summary>
        /// Adds a hit (snapped) where there is none, without writing the difficulty yet (see <see cref="Commit"/>).
        /// </summary>
        public bool AddHit(HitsoundLane lane, double time)
        {
            if (!Editable)
                return false;

            time = Snap(time);

            if (FindHit(lane, time, 1) != null)
                return false;

            triggers.Add(new HitsoundTrigger(time, lane.Sound, lane.Volume.Value));
            notifyChanged();
            return true;
        }

        public bool RemoveHit(HitsoundTrigger hit)
        {
            if (!Editable || !triggers.Remove(hit))
                return false;

            Selection.Remove(hit);
            notifyChanged();
            return true;
        }

        /// <summary>
        /// Clicking a lane: removes the hit under the cursor, or adds one at the snapped time.
        /// </summary>
        public void Toggle(HitsoundLane lane, double time, double tolerance)
        {
            if (!Editable)
                return;

            var existing = FindHit(lane, time, tolerance);

            if (existing != null)
                RemoveHit(existing);
            else
                AddHit(lane, time);

            Commit();
        }

        public void Select(IEnumerable<HitsoundTrigger> hits)
        {
            Selection.Clear();
            Selection.UnionWith(hits);
            updateCanCopyPaste();
            notifyChanged();
        }

        public void DeleteSelection()
        {
            if (!Editable || Selection.Count == 0)
                return;

            triggers.RemoveAll(Selection.Contains);
            Selection.Clear();
            Commit();
        }

        public override void Copy()
        {
            clipboard = Selection.OrderBy(t => t.Time).ToList();
            updateCanCopyPaste();
        }

        public override void Cut()
        {
            Copy();
            DeleteSelection();
        }

        /// <summary>
        /// Pastes the copied hits with the first at the playhead (snapped).
        /// </summary>
        public override void Paste()
        {
            if (!Editable || clipboard.Count == 0)
                return;

            double offset = Snap(clock.CurrentTime) - clipboard[0].Time;
            var pasted = clipboard.Select(t => t with { Time = t.Time + offset }).Where(t => !triggers.Contains(t)).ToList();

            foreach (var t in pasted)
                ensureLane(t.Sound);

            triggers.AddRange(pasted);
            Selection.Clear();
            Selection.UnionWith(pasted);
            Commit();
        }

        private void updateCanCopyPaste()
        {
            CanCopy.Value = CanCut.Value = Selection.Count > 0;
            CanPaste.Value = Editable && clipboard.Count > 0;
        }

        /// <summary>
        /// Writes the hits into the difficulty as one undoable change.
        /// </summary>
        public void Commit()
        {
            notifyChanged();
            updateCanCopyPaste();

            if (!Editable)
                return;

            writing = true;

            try
            {
                var legacy = HitsoundProject.Generate(triggers, (time, position) => HitsoundProject.LegacyCircle(time, position));
                var objects = HitsoundProject.ToRulesetObjects(legacy, EditorBeatmap);

                EditorBeatmap.BeginChange();
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(objects);
                EditorBeatmap.EndChange();
            }
            finally
            {
                writing = false;
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Delete && !e.Repeat && Selection.Count > 0)
            {
                DeleteSelection();
                return true;
            }

            return base.OnKeyDown(e);
        }

        #endregion

        #region Other difficulties

        public IEnumerable<BeatmapInfo> OtherDifficulties => EditorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps
                                                                          .Where(b => !b.Equals(EditorBeatmap.BeatmapInfo) && b.Ruleset.Equals(EditorBeatmap.BeatmapInfo.Ruleset))
                                                                          .OrderBy(b => b.StarRating) ?? Enumerable.Empty<BeatmapInfo>();

        /// <summary>
        /// Adds another difficulty's hitsounds as hits (this difficulty must be a hitsound difficulty).
        /// </summary>
        public int ImportFrom(BeatmapInfo difficulty)
        {
            if (!Editable)
                return 0;

            var imported = HitsoundProject.Import(beatmapManager.GetWorkingBeatmap(difficulty).GetPlayableBeatmap(EditorBeatmap.BeatmapInfo.Ruleset));
            var added = imported.Where(t => !triggers.Contains(t)).ToList();

            foreach (var t in added)
                ensureLane(t.Sound);

            triggers.AddRange(added);
            Commit();
            return added.Count;
        }

        /// <summary>
        /// Copies this difficulty's hitsounds onto other difficulties with the Hitsound Copier and saves them.
        /// </summary>
        public int CopyTo(IEnumerable<BeatmapInfo> difficulties, HitsoundCopier copier)
        {
            int copied = 0;

            foreach (var difficulty in difficulties)
            {
                try
                {
                    var working = beatmapManager.GetWorkingBeatmap(difficulty);
                    var target = new EditorBeatmap(working.GetPlayableBeatmap(difficulty.Ruleset), working.GetSkin(), working.Storyboard, difficulty);

                    copier.Copy(EditorBeatmap.HitObjects, 0, target);
                    beatmapManager.Save(difficulty, target.PlayableBeatmap, working.GetSkin(), working.Storyboard);
                    copied++;
                }
                catch (Exception e)
                {
                    // Logged errors are also shown to the user as a notification.
                    Logger.Error(e, $"Could not copy hitsounds to {difficulty.DifficultyName}.");
                }
            }

            return copied;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (EditorBeatmap.IsNotNull())
            {
                EditorBeatmap.HitObjectAdded -= onExternalChange;
                EditorBeatmap.HitObjectRemoved -= onExternalChange;
            }
        }

        /// <summary>
        /// A button that opens a popover.
        /// </summary>
        private partial class PopoverButton : RoundedButton, IHasPopover
        {
            private readonly Func<Popover> createPopover;

            public PopoverButton(string text, Func<Popover> createPopover)
            {
                Text = text;
                this.createPopover = createPopover;
                Action = this.ShowPopover;
            }

            public Popover GetPopover() => createPopover();
        }

        /// <summary>
        /// Plays the hits passing the playhead while the clock runs (the editor only plays hitsounds on the compose screen).
        /// Lives inside the beatmap's skin, so custom samples of the map are found.
        /// </summary>
        private partial class HitsoundPlayer : Component
        {
            private readonly HitsoundsScreen screen;

            [Resolved]
            private ISkinSource skin { get; set; } = null!;

            [Resolved]
            private EditorClock clock { get; set; } = null!;

            private double lastTime;

            public HitsoundPlayer(HitsoundsScreen screen)
            {
                this.screen = screen;
            }

            protected override void Update()
            {
                base.Update();

                double now = clock.CurrentTime;

                // Only while playing normally, not on seeks.
                if (clock.IsRunning && screen.IsPresent && now > lastTime && now - lastTime < 250)
                {
                    bool anySolo = screen.Lanes.Any(l => l.Solo.Value);

                    foreach (var hit in screen.Triggers.Where(t => t.Time > lastTime && t.Time <= now))
                    {
                        var lane = screen.Lanes.FirstOrDefault(l => l.Sound == hit.Sound);

                        if (lane == null || lane.Muted.Value || (anySolo && !lane.Solo.Value))
                            continue;

                        var sample = skin.GetSample(hit.Sound.ToSample(hit.Volume));
                        if (sample == null)
                            continue;

                        var channel = sample.GetChannel();
                        channel.Volume.Value = hit.Volume / 100.0;
                        channel.Play();
                    }
                }

                lastTime = now;
            }
        }
    }
}
