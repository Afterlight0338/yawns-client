// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: the Hitsounds tab's lanes over time. Like the editor timeline, the current time sits under the centre marker and moving the view seeks.
    /// </summary>
    public partial class HitsoundSequencer : ZoomableScrollContainer
    {
        private const float hit_width = 10;
        private const float hit_tolerance_px = 7;

        private readonly HitsoundsScreen screen;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        private readonly Container grid;
        private readonly Container ghosts;
        private readonly Container hits;
        private readonly Box selectionBox;
        private readonly WaveformGraph waveform;

        private double trackLengthForZoom;
        private double lastScrollPosition;
        private double lastTrackTime;

        private enum DragMode
        {
            None,
            Paint,
            Erase,
            Select,
        }

        private DragMode dragMode;
        private Vector2 dragStart;
        private bool rightDragged;

        public HitsoundSequencer(HitsoundsScreen screen)
        {
            this.screen = screen;

            RelativeSizeAxes = Axes.Both;
            ScrollbarVisible = false;

            Children = new Drawable[]
            {
                waveform = new WaveformGraph
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.35f,
                },
                grid = new Container { RelativeSizeAxes = Axes.Both },
                ghosts = new Container { RelativeSizeAxes = Axes.Both },
                hits = new Container { RelativeSizeAxes = Axes.Both },
                selectionBox = new Box { Colour = Color4.White, Alpha = 0 },
            };

            AddInternal(new CentreMarker
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Width = 16,
            });
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            waveform.BaseColour = colours.Blue.Opacity(0.2f);
            waveform.LowColour = colours.BlueLighter;
            waveform.MidColour = colours.BlueDark;
            waveform.HighColour = colours.BlueDarker;
            waveform.Waveform = beatmap.Value.Waveform;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screen.StateChanged += onStateChanged;
            screen.GhostChanged += onGhostChanged;
            beatDivisor.BindValueChanged(_ => Scheduler.AddOnce(redrawGrid));
        }

        private void onStateChanged() => Scheduler.AddOnce(redrawHits);

        private void onGhostChanged() => Scheduler.AddOnce(redrawGhosts);

        private double trackLength => Math.Max(1, editorClock.TrackLength);

        private float relative(double time) => (float)(time / trackLength);

        #region Drawing

        private void redrawGrid()
        {
            grid.Clear();

            var timingPoints = editorBeatmap.ControlPointInfo.TimingPoints;
            int divisor = beatDivisor.Value;

            for (int p = 0; p < timingPoints.Count; p++)
            {
                var tp = timingPoints[p];
                double end = p + 1 < timingPoints.Count ? timingPoints[p + 1].Time : trackLength;
                double step = tp.BeatLength / divisor;

                if (step <= 0)
                    continue;

                int beatsPerBar = Math.Max(1, tp.TimeSignature.Numerator);
                int i = 0;

                for (double t = tp.Time; t < end; t = tp.Time + ++i * step)
                {
                    float alpha = i % (divisor * beatsPerBar) == 0 ? 0.45f : i % divisor == 0 ? 0.22f : 0.08f;

                    grid.Add(new Box
                    {
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Y,
                        X = relative(t),
                        Width = 1,
                        Alpha = alpha,
                    });
                }
            }
        }

        private void redrawGhosts()
        {
            ghosts.Clear();

            // As Hitsound Studio: a pip in the ghost row plus a faint guideline through the lanes, sliders get a soft wash to their tail.
            foreach (var (start, end) in screen.GhostObjects)
            {
                ghosts.Add(new Box
                {
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    X = relative(start),
                    Width = 1,
                    Alpha = 0.12f,
                });

                if (end > start)
                {
                    ghosts.Add(new Box
                    {
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Both,
                        X = relative(start),
                        Width = relative(end - start),
                        Colour = Color4.SkyBlue,
                        Alpha = 0.04f,
                    });
                }

                ghosts.Add(new Box
                {
                    RelativePositionAxes = Axes.X,
                    X = relative(start),
                    Y = 5,
                    Height = HitsoundsScreen.GHOST_ROW_HEIGHT - 10,
                    Width = 3,
                    Colour = Color4.Gray,
                });

                if (end > start)
                {
                    ghosts.Add(new Box
                    {
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.X,
                        X = relative(start),
                        Width = relative(end - start),
                        Y = HitsoundsScreen.GHOST_ROW_HEIGHT / 2 - 1,
                        Height = 2,
                        Colour = Color4.Gray,
                        Alpha = 0.6f,
                    });
                }
            }
        }

        // ponytail: rebuilds every hit drawable on each edit, fine for a few thousand hits; pool them if big maps hitch.
        private void redrawHits()
        {
            hits.Clear();

            for (int i = 0; i < screen.Lanes.Count; i++)
            {
                var lane = screen.Lanes[i];
                float top = HitsoundsScreen.GHOST_ROW_HEIGHT + i * HitsoundsScreen.ROW_HEIGHT;

                hits.Add(new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Y = top + HitsoundsScreen.ROW_HEIGHT - 1,
                    Height = 1,
                    Alpha = 0.15f,
                });

                foreach (var hit in screen.Triggers.Where(t => t.Sound == lane.Sound))
                {
                    bool selected = screen.Selection.Contains(hit);

                    hits.Add(new Container
                    {
                        RelativePositionAxes = Axes.X,
                        Origin = Anchor.Centre,
                        X = relative(hit.Time),
                        Y = top + HitsoundsScreen.ROW_HEIGHT / 2,
                        Size = new Vector2(hit_width, HitsoundsScreen.ROW_HEIGHT - 10),
                        Masking = true,
                        CornerRadius = 3,
                        BorderThickness = selected ? 2 : 0,
                        BorderColour = Color4.White,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = selected ? Color4.White : lane.Colour,
                            Alpha = 0.4f + 0.6f * hit.Volume / 100,
                        },
                    });
                }
            }
        }

        #endregion

        #region Following the clock (as the editor timeline does)

        protected override void Update()
        {
            base.Update();

            Content.Margin = new MarginPadding { Horizontal = DrawWidth / 2 };

            if (editorClock.IsRunning)
                scrollToTrackTime();

            if (editorClock.TrackLength != trackLengthForZoom && editorClock.TrackLength > 0)
            {
                float zoomFor(double ms) => Math.Max(1, (float)(editorClock.TrackLength / ms));

                SetupZoom(zoomFor(5000), zoomFor(20000), zoomFor(600));
                trackLengthForZoom = editorClock.TrackLength;

                redrawGrid();
                redrawGhosts();
                redrawHits();
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (!editorClock.IsRunning)
            {
                if (Current != lastScrollPosition && editorClock.CurrentTime == lastTrackTime && !editorClock.IsSeeking)
                    editorClock.Seek(Math.Min(editorClock.TrackLength, timeAt(Current)));
                else
                    scrollToTrackTime();
            }

            lastScrollPosition = Current;
            lastTrackTime = editorClock.CurrentTime;
        }

        private void scrollToTrackTime()
        {
            if (editorClock.TrackLength > 0)
                ScrollTo((float)(editorClock.CurrentTime / editorClock.TrackLength * Content.DrawWidth), false);
        }

        private double timeAt(double x) => x / Content.DrawWidth * editorClock.TrackLength;

        protected override bool OnScroll(ScrollEvent e)
        {
            // Plain wheel seeks with snapping (the editor handles it), alt zooms, precise (touchpad) scrolls.
            if (!e.AltPressed && !e.IsPrecise)
                return false;

            return base.OnScroll(e);
        }

        #endregion

        #region Editing

        private (HitsoundLane? Lane, double Time) at(Vector2 screenSpace)
        {
            var local = Content.ToLocalSpace(screenSpace);
            int index = (int)Math.Floor((local.Y - HitsoundsScreen.GHOST_ROW_HEIGHT) / HitsoundsScreen.ROW_HEIGHT);
            var lane = local.Y >= HitsoundsScreen.GHOST_ROW_HEIGHT && index < screen.Lanes.Count ? screen.Lanes[index] : null;
            return (lane, timeAt(local.X));
        }

        private double toleranceMs => hit_tolerance_px / Math.Max(1, Content.DrawWidth) * editorClock.TrackLength;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            dragStart = e.ScreenSpaceMousePosition;

            switch (e.Button)
            {
                case MouseButton.Right:
                    dragMode = DragMode.Erase;
                    rightDragged = false;
                    return true;

                case MouseButton.Left when e.ControlPressed:
                    dragMode = DragMode.Paint;
                    return true;

                case MouseButton.Left when e.ShiftPressed:
                    dragMode = DragMode.Select;
                    return true;

                default:
                    dragMode = DragMode.None;
                    return base.OnMouseDown(e);
            }
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            // A right click without dragging removes the hit under the cursor.
            if (e.Button == MouseButton.Right && !rightDragged)
            {
                var (lane, time) = at(e.ScreenSpaceMousePosition);
                var hit = lane == null ? null : screen.FindHit(lane, time, toleranceMs);

                if (hit != null && screen.RemoveHit(hit))
                    screen.Commit();
            }

            if (e.Button == MouseButton.Right)
                dragMode = DragMode.None;

            base.OnMouseUp(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left || dragMode != DragMode.None)
                return false;

            var (lane, time) = at(e.ScreenSpaceMousePosition);

            if (lane == null)
            {
                screen.Select(Enumerable.Empty<HitsoundTrigger>());
                return false;
            }

            screen.Toggle(lane, time, toleranceMs);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (dragMode == DragMode.None)
                return base.OnDragStart(e);

            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            switch (dragMode)
            {
                case DragMode.Paint:
                {
                    var (lane, time) = at(e.ScreenSpaceMousePosition);
                    if (lane != null)
                        screen.AddHit(lane, time);
                    break;
                }

                case DragMode.Erase:
                {
                    rightDragged = true;
                    var (lane, time) = at(e.ScreenSpaceMousePosition);
                    var hit = lane == null ? null : screen.FindHit(lane, time, toleranceMs);
                    if (hit != null)
                        screen.RemoveHit(hit);
                    break;
                }

                case DragMode.Select:
                {
                    var a = Content.ToLocalSpace(dragStart);
                    var b = Content.ToLocalSpace(e.ScreenSpaceMousePosition);
                    selectionBox.Position = Vector2.ComponentMin(a, b);
                    selectionBox.Size = Vector2.ComponentMax(a, b) - selectionBox.Position;
                    selectionBox.Alpha = 0.15f;
                    break;
                }

                default:
                    base.OnDrag(e);
                    break;
            }
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            switch (dragMode)
            {
                case DragMode.Paint:
                case DragMode.Erase:
                    screen.Commit();
                    break;

                case DragMode.Select:
                    selectionBox.Alpha = 0;

                    var (_, timeA) = at(dragStart);
                    var (_, timeB) = at(e.ScreenSpaceMousePosition);
                    var a = Content.ToLocalSpace(dragStart);
                    var b = Content.ToLocalSpace(e.ScreenSpaceMousePosition);
                    float top = Math.Min(a.Y, b.Y), bottom = Math.Max(a.Y, b.Y);
                    double from = Math.Min(timeA, timeB), to = Math.Max(timeA, timeB);

                    screen.Select(screen.Triggers.Where(t =>
                    {
                        int index = screen.Lanes.ToList().FindIndex(l => l.Sound == t.Sound);
                        float centre = HitsoundsScreen.GHOST_ROW_HEIGHT + (index + 0.5f) * HitsoundsScreen.ROW_HEIGHT;
                        return t.Time >= from && t.Time <= to && centre >= top && centre <= bottom;
                    }));
                    break;

                default:
                    base.OnDragEnd(e);
                    break;
            }

            dragMode = DragMode.None;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            screen.StateChanged -= onStateChanged;
            screen.GhostChanged -= onGhostChanged;
        }
    }
}
