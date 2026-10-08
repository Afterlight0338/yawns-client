// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Reference;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: the Hitsounds tab, a native version of Hitsound Studio (https://hitsound.vivlos.dev): lanes of hits on a timeline.
    /// The lanes and hits are a project saved in game storage; Export writes them into this hitsound difficulty the way Mapping Tools does
    /// (one circle per moment, custom indices and mixed samples where needed), and the Copier copies that onto the other difficulties.
    /// </summary>
    public partial class HitsoundsScreen : EditorScreen, IKeyBindingHandler<GlobalAction>, IKeyBindingHandler<PlatformAction>
    {
        public const float TOP_BAR_HEIGHT = 48;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private EditorReferenceBeatmap? reference { get; set; }

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private OsuGame? osuGame { get; set; }

        #region State

        /// <summary>
        /// The lanes, top to bottom (their order is also the export's priority).
        /// </summary>
        public readonly List<HitsoundLane> Lanes = new List<HitsoundLane>();

        /// <summary>
        /// The hits, always sorted by time.
        /// </summary>
        public readonly List<HitsoundTrigger> Triggers = new List<HitsoundTrigger>();

        public readonly HashSet<string> SelectedIds = new HashSet<string>();

        /// <summary>
        /// Lanes that are sounding now (playback, or a preview), for the flash in the canvas and the rack.
        /// </summary>
        public HashSet<string> PlayingLaneIds { get; private set; } = new HashSet<string>();

        public readonly BindableBool Compact = new BindableBool();

        /// <summary>
        /// The difficulty shown as ghost notes (and, in <see cref="DiffMode.Hitsounds"/> mode, loaded into the lanes when picked).
        /// </summary>
        public readonly Bindable<GhostChoice> Ghost = new Bindable<GhostChoice>(GhostChoice.OVERLAY);

        public readonly Bindable<DiffMode> Mode = new Bindable<DiffMode>(DiffMode.Hitsounds);

        public IReadOnlyList<GhostObject> GhostObjects { get; private set; } = Array.Empty<GhostObject>();

        public HitsoundWaveform? Waveform { get; private set; }

        /// <summary>
        /// Lanes were added, removed, reordered or changed.
        /// </summary>
        public event Action? LanesChanged;

        /// <summary>
        /// The lanes changed since the difficulty was last written.
        /// </summary>
        public readonly BindableBool HasUnexportedChanges = new BindableBool();

        public readonly BindableInt SongVolume = new BindableInt(40) { MinValue = 0, MaxValue = 100 };

        public readonly BindableInt HitsoundVolume = new BindableInt(35) { MinValue = 0, MaxValue = 100 };

        /// <summary>
        /// Only a hitsound difficulty is edited here. A gameplay difficulty's hitsounds are shown read-only.
        /// </summary>
        public bool Editable { get; private set; }

        public int SnapDivisor => beatDivisor.Value;

        private HitsoundProjectData project = new HitsoundProjectData();

        private const int max_history = 50;
        private readonly List<(List<HitsoundLane> Lanes, List<HitsoundTrigger> Triggers)> undoStack = new List<(List<HitsoundLane>, List<HitsoundTrigger>)>();
        private readonly List<(List<HitsoundLane> Lanes, List<HitsoundTrigger> Triggers)> redoStack = new List<(List<HitsoundLane>, List<HitsoundTrigger>)>();

        private List<(string LaneId, double RelTime, int? Volume)> clipboard = new List<(string, double, int?)>();

        private readonly Dictionary<string, double> flashUntil = new Dictionary<string, double>();

        private bool writing;
        private ScheduledDelegate? saveProjectDelegate;
        private ScheduledDelegate? saveSettingsDelegate;

        private readonly BindableDouble songVolumeAdjustment = new BindableDouble(1);
        private bool songVolumeApplied;

        #endregion

        #region UI

        private HitsoundCanvas canvas = null!;
        private HitsoundRack rack = null!;
        private Container studioView = null!;
        private HitsoundCopierView copierView = null!;
        private HitsoundPlayer player = null!;
        private Toast toast = null!;
        private Container readOnlyBanner = null!;
        private Container changedBanner = null!;
        private StudioButton studioTab = null!;
        private StudioButton copierTab = null!;
        private StudioButton playButton = null!;
        private OsuSpriteText timeDisplay = null!;
        private OsuSpriteText bpmDisplay = null!;
        private Circle exportDot = null!;

        private readonly BindableFloat zoomSlider = new BindableFloat(220) { MinValue = 30, MaxValue = 3000 };
        private readonly Bindable<int> snapSelect = new Bindable<int>(4);
        private readonly Bindable<double> rateSelect = new Bindable<double>(1);

        private static readonly int[] snap_divisors = { 1, 2, 4, 3, 6, 8, 12, 16 };

        #endregion

        public HitsoundsScreen()
            : base(EditorScreenMode.Hitsounds)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            canvas = new HitsoundCanvas(this);
            rack = new HitsoundRack(this, canvas);
            player = new HitsoundPlayer(this);

            Child = new EditorSkinProvidingContainer(EditorBeatmap).WithChildren(new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BG },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, TOP_BAR_HEIGHT),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[] { createTopBar() },
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    readOnlyBanner = createBanner(
                                        "This difficulty has gameplay objects, so its hitsounds are shown read-only. Hitsounds are made on a separate hitsound difficulty: create one, then pick this difficulty in the lane rack to load its hitsounds.",
                                        ("Create hitsound difficulty", () => editor?.CreateNewDifficulty(EditorBeatmap.BeatmapInfo.Ruleset))),
                                    changedBanner = createBanner(
                                        "This difficulty was changed outside the Hitsounds tab since the lanes were last exported.",
                                        ("Reimport from difficulty", reimportFromDifficulty),
                                        ("Keep lanes", keepLanes)),
                                }
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    studioView = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding { Left = HitsoundRack.WIDTH },
                                                Children = new Drawable[]
                                                {
                                                    canvas,
                                                    new HintBar
                                                    {
                                                        Anchor = Anchor.BottomRight,
                                                        Origin = Anchor.BottomRight,
                                                        Margin = new MarginPadding { Bottom = 22, Right = 12 },
                                                    },
                                                }
                                            },
                                            rack,
                                        }
                                    },
                                    copierView = new HitsoundCopierView(this) { Alpha = 0 },
                                }
                            },
                        },
                    }
                },
                player,
                toast = new Toast(),
            });

            loadSettings();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EditorBeatmap.HitObjectAdded += onExternalChange;
            EditorBeatmap.HitObjectRemoved += onExternalChange;
            EditorBeatmap.HitObjectUpdated += onExternalChange;

            if (editor != null)
                editor.Saving += onSaving;

            if (osuGame != null)
                osuGame.FileDropOverride = handleFileDrop;

            loadProject();
            loadWaveform();

            // A hitsound difficulty ghosts the hardest playable difficulty, anything else the overlay map (no hitsounds are loaded for this first pick).
            var hardest = OtherDifficulties.LastOrDefault();

            if (Editable && hardest != null)
                Ghost.Value = GhostChoices.First(c => hardest.Equals(c.Difficulty));

            ghostChanged(false);
            Ghost.BindValueChanged(_ => ghostChanged(true));
            Mode.BindValueChanged(m =>
            {
                if (m.NewValue == DiffMode.Hitsounds)
                    loadHitsoundsFromGhost();
            });

            if (reference != null)
            {
                reference.Beatmap.BindValueChanged(_ => overlayChanged());
                reference.DisplayOffset.BindValueChanged(_ => overlayChanged());
                reference.Pattern.BindValueChanged(_ => overlayChanged());
            }

            // The slider covers 30 to 3000 px/s (as the web), the wheel goes further; moving the slider sets the canvas zoom.
            bool syncingZoom = false;
            canvas.Zoom.BindValueChanged(z =>
            {
                syncingZoom = true;
                zoomSlider.Value = Math.Clamp(z.NewValue, zoomSlider.MinValue, zoomSlider.MaxValue);
                syncingZoom = false;
            }, true);
            zoomSlider.BindValueChanged(z =>
            {
                if (!syncingZoom)
                    canvas.SetZoom(z.NewValue);
            });

            beatDivisor.BindValueChanged(d => snapSelect.Value = d.NewValue, true);
            snapSelect.BindValueChanged(s =>
            {
                if (s.NewValue != beatDivisor.Value)
                    beatDivisor.SetArbitraryDivisor(s.NewValue);
            });

            rateSelect.BindTo(clock.PlaybackSpeed);

            SongVolume.BindValueChanged(v =>
            {
                songVolumeAdjustment.Value = v.NewValue / 100.0;
                scheduleSaveSettings();
            }, true);
            HitsoundVolume.BindValueChanged(_ => scheduleSaveSettings());

            HasUnexportedChanges.BindValueChanged(c => exportDot.Alpha = c.NewValue ? 1 : 0, true);
            Compact.BindValueChanged(c =>
            {
                project.Compact = c.NewValue;
                scheduleSaveProject();
            });
        }

        /// <summary>
        /// Whether the tab is shown, so dropped audio is meant for a lane (read from the window's thread).
        /// </summary>
        private volatile bool acceptsDrops;

        protected override void PopIn()
        {
            base.PopIn();
            acceptsDrops = true;

            if (!songVolumeApplied)
            {
                clock.AudioAdjustments.AddAdjustment(AdjustableProperty.Volume, songVolumeAdjustment);
                songVolumeApplied = true;
            }
        }

        protected override void PopOut()
        {
            base.PopOut();
            acceptsDrops = false;
            removeSongVolume();
        }

        private void removeSongVolume()
        {
            if (!songVolumeApplied)
                return;

            clock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Volume, songVolumeAdjustment);
            songVolumeApplied = false;
        }

        protected override void Update()
        {
            base.Update();

            double now = clock.CurrentTime;

            int totalMs = (int)Math.Max(0, now);
            timeDisplay.Text = $"{totalMs / 60000:00}:{totalMs / 1000 % 60:00}.{totalMs % 1000:000}";
            bpmDisplay.Text = $"{canvas.ActiveBpm(now)} BPM";
            playButton.Icon = clock.IsRunning ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play;

            var playing = clock.IsRunning ? HitsoundCanvas.ActiveLaneIds(Triggers, now) : new HashSet<string>();

            foreach (var (laneId, until) in flashUntil.ToList())
            {
                if (Time.Current < until)
                    playing.Add(laneId);
                else
                    flashUntil.Remove(laneId);
            }

            PlayingLaneIds = playing;
        }

        #region Project

        private Storage projectStorage => storage.GetStorageForDirectory("hitsound-studio");

        private string projectPath => $"{EditorBeatmap.BeatmapInfo.ID}.json";

        private IEnumerable<string> setFiles => working.Value.BeatmapSetInfo?.Files.Select(f => f.Filename) ?? Enumerable.Empty<string>();

        private void loadProject()
        {
            Editable = HitsoundProject.IsHitsoundDifficulty(EditorBeatmap);
            readOnlyBanner.Alpha = Editable ? 0 : 1;

            HitsoundProjectData? saved = null;

            if (Editable && projectStorage.Exists(projectPath))
            {
                try
                {
                    using var stream = projectStorage.GetStream(projectPath);
                    using var reader = new StreamReader(stream);
                    saved = HitsoundProjectData.Deserialise(reader.ReadToEnd());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Could not read the Hitsounds tab's saved lanes.");
                }
            }

            if (saved != null)
                project = saved;
            else
            {
                project = new HitsoundProjectData();

                if (EditorBeatmap.HitObjects.Count > 0 || !Editable)
                {
                    (project.Lanes, project.Triggers) = HitsoundProject.ImportLanes(EditorBeatmap, setFiles);
                    project.ExportedHash = HitsoundProject.Hash(EditorBeatmap);
                }
                else
                    project.Lanes = HitsoundProject.DefaultLanes();
            }

            setLanesAndTriggers(project.Lanes, project.Triggers);
            Compact.Value = project.Compact;
            undoStack.Clear();
            redoStack.Clear();
            HasUnexportedChanges.Value = false;

            checkChangedOutside();
        }

        private void setLanesAndTriggers(IEnumerable<HitsoundLane> lanes, IEnumerable<HitsoundTrigger> triggers)
        {
            Lanes.Clear();
            Lanes.AddRange(lanes);
            Triggers.Clear();
            Triggers.AddRange(triggers.Where(t => Lanes.Any(l => l.Id == t.LaneId)));
            SelectedIds.Clear();
            sortTriggers();

            project.Lanes = Lanes;
            project.Triggers = Triggers;

            LanesChanged?.Invoke();
            SelectionChanged();
        }

        private void sortTriggers() => Triggers.Sort((a, b) => a.Time.CompareTo(b.Time));

        /// <summary>
        /// The lanes or hits changed: save the project soon and remember the difficulty is out of date.
        /// </summary>
        private void changed(bool lanes = false)
        {
            sortTriggers();

            if (Editable)
            {
                HasUnexportedChanges.Value = true;
                scheduleSaveProject();
            }

            if (lanes)
                LanesChanged?.Invoke();

            SelectionChanged();
        }

        private void scheduleSaveProject()
        {
            if (!Editable)
                return;

            saveProjectDelegate?.Cancel();
            saveProjectDelegate = Scheduler.AddDelayed(saveProject, 500);
        }

        private void saveProject()
        {
            saveProjectDelegate?.Cancel();
            saveProjectDelegate = null;

            if (!Editable)
                return;

            try
            {
                using var stream = projectStorage.CreateFileSafely(projectPath);
                using var writer = new StreamWriter(stream);
                writer.Write(project.Serialise());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not save the Hitsounds tab's lanes.");
            }
        }

        private const string settings_path = "settings.json";

        private void loadSettings()
        {
            try
            {
                if (!projectStorage.Exists(settings_path))
                    return;

                using var stream = projectStorage.GetStream(settings_path);
                using var reader = new StreamReader(stream);
                var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(reader.ReadToEnd());

                if (settings == null)
                    return;

                if (settings.TryGetValue("song", out int song)) SongVolume.Value = song;
                if (settings.TryGetValue("hitsounds", out int hitsounds)) HitsoundVolume.Value = hitsounds;
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read the Hitsounds tab's settings: {e.Message}");
            }
        }

        private void scheduleSaveSettings()
        {
            saveSettingsDelegate?.Cancel();
            saveSettingsDelegate = Scheduler.AddDelayed(() =>
            {
                try
                {
                    using var stream = projectStorage.CreateFileSafely(settings_path);
                    using var writer = new StreamWriter(stream);
                    writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, int> { ["song"] = SongVolume.Value, ["hitsounds"] = HitsoundVolume.Value }));
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not save the Hitsounds tab's settings: {e.Message}");
                }
            }, 500);
        }

        // Undo, redo and the other tabs change the objects. Our own writes are skipped.
        private void onExternalChange(HitObject _)
        {
            if (!writing)
                Scheduler.AddOnce(checkChangedOutside);
        }

        private void checkChangedOutside()
        {
            bool wasEditable = Editable;
            Editable = HitsoundProject.IsHitsoundDifficulty(EditorBeatmap);
            readOnlyBanner.Alpha = Editable ? 0 : 1;

            if (Editable != wasEditable)
            {
                // The difficulty became (or stopped being) a hitsound difficulty, for example after it was emptied.
                loadProject();
                return;
            }

            if (!Editable)
            {
                // A gameplay difficulty: show what it plays now.
                var (lanes, triggers) = HitsoundProject.ImportLanes(EditorBeatmap, setFiles);
                setLanesAndTriggers(lanes, triggers);
                changedBanner.Alpha = 0;
                return;
            }

            string hash = HitsoundProject.Hash(EditorBeatmap);
            bool emptyAndNeverExported = project.ExportedHash == null && EditorBeatmap.HitObjects.Count == 0;
            changedBanner.Alpha = project.ExportedHash == hash || emptyAndNeverExported ? 0 : 1;
        }

        private void reimportFromDifficulty()
        {
            PushHistory();
            var (lanes, triggers) = HitsoundProject.ImportLanes(EditorBeatmap, setFiles);
            setLanesAndTriggers(lanes, triggers);
            project.ExportedHash = HitsoundProject.Hash(EditorBeatmap);
            HasUnexportedChanges.Value = false;
            changedBanner.Alpha = 0;
            scheduleSaveProject();
            ShowToast($"Reimported {Triggers.Count} notes on {Lanes.Count} lanes");
        }

        private void keepLanes()
        {
            project.ExportedHash = HitsoundProject.Hash(EditorBeatmap);
            HasUnexportedChanges.Value = true;
            changedBanner.Alpha = 0;
            scheduleSaveProject();
        }

        private void loadWaveform()
        {
            var waveform = working.Value.Waveform;
            double length = clock.TrackLength;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var computed = HitsoundWaveform.From(waveform, length);
                    Schedule(() => Waveform = computed);
                }
                catch (Exception e)
                {
                    // The ruler just shows no waveform.
                    Logger.Log($"Could not read the song's waveform for the Hitsounds tab: {e.Message}");
                }
            });
        }

        #endregion

        #region Ghost notes and difficulties

        public IEnumerable<BeatmapInfo> OtherDifficulties => EditorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps
                                                                          .Where(b => !b.Equals(EditorBeatmap.BeatmapInfo) && b.Ruleset.Equals(EditorBeatmap.BeatmapInfo.Ruleset))
                                                                          .OrderBy(b => b.StarRating) ?? Enumerable.Empty<BeatmapInfo>();

        public IEnumerable<GhostChoice> GhostChoices => new[] { GhostChoice.NONE, GhostChoice.OVERLAY }
            .Concat(OtherDifficulties.Select(d => new GhostChoice(d.DifficultyName, d)));

        private IBeatmap? ghostBeatmap;

        private void overlayChanged()
        {
            if (Ghost.Value == GhostChoice.OVERLAY)
                ghostChanged(false);
        }

        private void ghostChanged(bool loadHitsounds)
        {
            var choice = Ghost.Value;

            ghostBeatmap = choice.Difficulty == null ? null : beatmapManager.GetWorkingBeatmap(choice.Difficulty).GetPlayableBeatmap(EditorBeatmap.BeatmapInfo.Ruleset);

            if (ghostBeatmap != null)
                GhostObjects = ghostObjectsOf(ghostBeatmap.HitObjects, 0);
            else if (choice == GhostChoice.OVERLAY && reference != null)
                GhostObjects = ghostObjectsOf(reference.DisplayedObjects, reference.DisplayOffset.Value);
            else
                GhostObjects = Array.Empty<GhostObject>();

            if (loadHitsounds && Mode.Value == DiffMode.Hitsounds)
                loadHitsoundsFromGhost();
        }

        private static List<GhostObject> ghostObjectsOf(IEnumerable<HitObject> objects, double offset) => objects.Select(h =>
        {
            double start = h.StartTime + offset;
            double end = h.GetEndTime() + offset;

            switch (h)
            {
                case IHasRepeats repeats:
                    int spans = repeats.RepeatCount + 1;
                    double span = (end - start) / spans;
                    return new GhostObject(start, end, GhostKind.Slider, Enumerable.Range(0, spans + 1).Select(i => start + i * span).ToArray());

                case IHasDuration:
                    return new GhostObject(start, end, GhostKind.Spinner, Array.Empty<double>());

                default:
                    return new GhostObject(start, start, GhostKind.Circle, Array.Empty<double>());
            }
        }).OrderBy(g => g.Time).ToList();

        /// <summary>
        /// Hitsound Studio's "Hitsounds" mode: picking a difficulty loads its hitsounds into the lanes (undo brings the old lanes back).
        /// </summary>
        private void loadHitsoundsFromGhost()
        {
            if (!Editable)
                return;

            IBeatmap? source = ghostBeatmap;

            if (source == null && Ghost.Value == GhostChoice.OVERLAY && reference?.Beatmap.Value is IBeatmap overlay)
                source = overlay;

            if (source == null)
                return;

            var (lanes, triggers) = HitsoundProject.ImportLanes(source, setFiles);

            if (lanes.Count == 0)
            {
                ShowToast($"No hitsounds in [{Ghost.Value.Name}]");
                return;
            }

            if (Ghost.Value == GhostChoice.OVERLAY && reference != null)
            {
                foreach (var t in triggers)
                    t.Time = Math.Round(t.Time + reference.DisplayOffset.Value);
            }

            PushHistory();
            setLanesAndTriggers(lanes, triggers);
            changed(true);
            ShowToast($"Loaded {triggers.Count} notes from [{Ghost.Value.Name}]. Ctrl+Z brings the old lanes back");
        }

        #endregion

        #region Editing (app.ts)

        public void PushHistory()
        {
            undoStack.Add(snapshot());
            if (undoStack.Count > max_history)
                undoStack.RemoveAt(0);

            redoStack.Clear();
        }

        private (List<HitsoundLane>, List<HitsoundTrigger>) snapshot() =>
            (Lanes.Select(l => l.Clone()).ToList(), Triggers.Select(t => t.Clone()).ToList());

        /// <summary>
        /// Brings back the lanes and hits of a snapshot. Lanes that are in both keep the edits made since (name, sample set, volume).
        /// </summary>
        private void restore((List<HitsoundLane> Lanes, List<HitsoundTrigger> Triggers) state)
        {
            var current = Lanes.ToDictionary(l => l.Id);
            var lanes = state.Lanes.Select(l => current.TryGetValue(l.Id, out var existing) ? existing : l).ToList();

            Lanes.Clear();
            Lanes.AddRange(lanes);
            Triggers.Clear();
            Triggers.AddRange(state.Triggers.Where(t => Lanes.Any(l => l.Id == t.LaneId)));
            SelectedIds.RemoveWhere(id => Triggers.All(t => t.Id != id));
            changed(true);
        }

        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                ShowToast("Nothing to undo");
                return;
            }

            redoStack.Add(snapshot());
            var state = undoStack[^1];
            undoStack.RemoveAt(undoStack.Count - 1);
            restore(state);
            ShowToast("Undone");
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                ShowToast("Nothing to redo");
                return;
            }

            undoStack.Add(snapshot());
            var state = redoStack[^1];
            redoStack.RemoveAt(redoStack.Count - 1);
            restore(state);
            ShowToast("Redone");
        }

        public void AddTrigger(HitsoundLane lane, double time)
        {
            if (!Editable)
                return;

            Triggers.Add(new HitsoundTrigger { LaneId = lane.Id, Time = Math.Max(0, Math.Round(time)) });
            changed();
        }

        public void RemoveTrigger(string id)
        {
            if (!Editable)
                return;

            Triggers.RemoveAll(t => t.Id == id);
            SelectedIds.Remove(id);
            changed();
        }

        /// <summary>
        /// Moves hits to other times and lanes; hits landing on the same lane and time merge.
        /// </summary>
        public void MoveTriggers(IReadOnlyDictionary<string, (string LaneId, double Time)> moves)
        {
            if (!Editable)
                return;

            foreach (var tr in Triggers)
            {
                if (moves.TryGetValue(tr.Id, out var move))
                {
                    tr.LaneId = move.LaneId;
                    tr.Time = move.Time;
                }
            }

            var seen = new HashSet<(string, double)>();
            Triggers.RemoveAll(t => !seen.Add((t.LaneId, t.Time)));
            SelectedIds.RemoveWhere(id => Triggers.All(t => t.Id != id));
            changed();
        }

        /// <summary>
        /// A paint or erase stroke ended.
        /// </summary>
        public void EndGesture() => changed();

        public void SetSelection(IEnumerable<string> ids)
        {
            SelectedIds.Clear();
            SelectedIds.UnionWith(ids);
            SelectionChanged();
        }

        public void SelectionChanged()
        {
            CanCopy.Value = CanCut.Value = SelectedIds.Count > 0;
            CanPaste.Value = Editable && clipboard.Count > 0;
        }

        public void SelectAll()
        {
            SetSelection(Triggers.Select(t => t.Id));
            ShowToast($"Selected all {Triggers.Count} notes");
        }

        public override void Copy()
        {
            var selected = Triggers.Where(t => SelectedIds.Contains(t.Id)).OrderBy(t => t.Time).ToList();

            if (selected.Count == 0)
            {
                ShowToast("No notes selected to copy");
                return;
            }

            double minTime = selected[0].Time;
            clipboard = selected.Select(t => (t.LaneId, t.Time - minTime, t.Volume)).ToList();
            SelectionChanged();
            ShowToast($"Copied {plural(clipboard.Count, "note")}");
        }

        public override void Cut()
        {
            if (SelectedIds.Count == 0)
            {
                ShowToast("No notes selected to cut");
                return;
            }

            Copy();
            DeleteSelection();
        }

        /// <summary>
        /// Pastes the copied hits with the first at the playhead (snapped). Hits whose lane is gone go on the first lane.
        /// </summary>
        public override void Paste()
        {
            if (!Editable)
                return;

            if (clipboard.Count == 0)
            {
                ShowToast("Clipboard empty");
                return;
            }

            if (Lanes.Count == 0)
                return;

            PushHistory();

            double pasteBaseTime = canvas.SnapTimeToGrid(clock.CurrentTime);
            var pasted = new HashSet<string>();

            foreach (var (laneId, relTime, volume) in clipboard)
            {
                string targetLane = Lanes.Any(l => l.Id == laneId) ? laneId : Lanes[0].Id;
                double targetTime = Math.Max(0, Math.Round(pasteBaseTime + relTime));
                var existing = Triggers.FirstOrDefault(t => t.LaneId == targetLane && Math.Abs(t.Time - targetTime) < 2);

                if (existing != null)
                {
                    pasted.Add(existing.Id);
                    continue;
                }

                var trigger = new HitsoundTrigger { LaneId = targetLane, Time = targetTime, Volume = volume };
                Triggers.Add(trigger);
                pasted.Add(trigger.Id);
            }

            SelectedIds.Clear();
            SelectedIds.UnionWith(pasted);
            changed();
            ShowToast($"Pasted {plural(pasted.Count, "note")}");
        }

        public void DeleteSelection()
        {
            if (!Editable || SelectedIds.Count == 0)
                return;

            PushHistory();
            int count = SelectedIds.Count;
            Triggers.RemoveAll(t => SelectedIds.Contains(t.Id));
            SelectedIds.Clear();
            changed();
            ShowToast($"Deleted {plural(count, "note")}");
        }

        /// <summary>
        /// W, E, R: puts the addition on every selected moment, or takes it off when they all have it (app.ts toggleAdditionOnSelected).
        /// </summary>
        public void ToggleAdditionOnSelected(HitsoundAddition addition)
        {
            if (!Editable)
                return;

            if (SelectedIds.Count == 0)
            {
                ShowToast($"Select notes first to toggle {addition} (W: Whistle, E: Finish, R: Clap)");
                return;
            }

            PushHistory();

            var target = Lanes.FirstOrDefault(l => l.Addition == addition && l.File == null && !l.Muted)
                         ?? Lanes.FirstOrDefault(l => l.Addition == addition && l.File == null)
                         ?? addLaneWithAddition(addition);

            var times = Triggers.Where(t => SelectedIds.Contains(t.Id)).Select(t => t.Time).Distinct().ToList();
            var existingTimes = Triggers.Where(t => t.LaneId == target.Id).Select(t => t.Time).ToHashSet();

            if (times.All(existingTimes.Contains))
            {
                var remove = times.ToHashSet();
                Triggers.RemoveAll(t => t.LaneId == target.Id && remove.Contains(t.Time));
                SelectedIds.RemoveWhere(id => Triggers.All(t => t.Id != id));
                changed();
                ShowToast($"Removed {addition} from {plural(times.Count, "note")}");
            }
            else
            {
                int added = 0;

                foreach (double time in times.Where(t => !existingTimes.Contains(t)))
                {
                    var trigger = new HitsoundTrigger { LaneId = target.Id, Time = time };
                    Triggers.Add(trigger);
                    SelectedIds.Add(trigger.Id);
                    added++;
                }

                changed();
                ShowToast($"Added {addition} to {plural(added, "note")}");
            }
        }

        private static readonly string[] new_lane_colours = { "#ff4081", "#00e5ff", "#ffc400", "#76ff03", "#e040fb", "#ff6e40", "#40c4ff", "#b2ff59" };

        public void AddLane()
        {
            PushHistory();
            Lanes.Add(new HitsoundLane
            {
                Name = $"Lane {Lanes.Count + 1}",
                Bank = Audio.HitSampleInfo.BANK_SOFT,
                Addition = HitsoundAddition.Whistle,
                Volume = 85,
                Colour = new_lane_colours[Lanes.Count % new_lane_colours.Length],
            });

            changed(true);
        }

        public void AddLaneWithAddition(HitsoundAddition addition)
        {
            PushHistory();
            addLaneWithAddition(addition);
        }

        private HitsoundLane addLaneWithAddition(HitsoundAddition addition)
        {
            string name = addition switch
            {
                HitsoundAddition.Whistle => "Soft Whistle",
                HitsoundAddition.Clap => "Soft Clap",
                HitsoundAddition.Finish => "Soft Finish",
                _ => "Soft Normal",
            };

            string colour = addition switch
            {
                HitsoundAddition.Whistle => "#00e5ff",
                HitsoundAddition.Clap => "#ff4081",
                HitsoundAddition.Finish => "#ffc400",
                _ => "#76ff03",
            };

            var lane = new HitsoundLane
            {
                Name = $"{name} {Lanes.Count(l => l.Addition == addition) + 1}",
                Bank = Audio.HitSampleInfo.BANK_SOFT,
                Addition = addition,
                Volume = 85,
                Colour = colour,
            };

            Lanes.Add(lane);
            changed(true);
            ShowToast($"Added lane: {lane.Name}");
            return lane;
        }

        public void RemoveLane(HitsoundLane lane)
        {
            if (!Editable)
                return;

            if (Lanes.Count <= 1)
            {
                ShowToast("Must keep at least 1 lane!");
                return;
            }

            PushHistory();
            Lanes.Remove(lane);
            Triggers.RemoveAll(t => t.LaneId == lane.Id);
            SelectedIds.RemoveWhere(id => Triggers.All(t => t.Id != id));
            changed(true);
        }

        /// <summary>
        /// Changes a lane (its sound changes every hit on it).
        /// </summary>
        public void EditLane(HitsoundLane lane, Action<HitsoundLane> edit, bool refreshRack = true)
        {
            edit(lane);
            changed(refreshRack);
        }

        public void ToggleMute(HitsoundLane lane)
        {
            lane.Muted = !lane.Muted;
            if (lane.Muted)
                lane.Solo = false;

            changed(true);
        }

        public void ToggleSolo(HitsoundLane lane)
        {
            lane.Solo = !lane.Solo;
            if (lane.Solo)
                lane.Muted = false;

            changed(true);
        }

        public void ClearLaneSample(HitsoundLane lane)
        {
            string? old = lane.File;
            lane.File = null;
            changed(true);
            ShowToast($"Cleared \"{old ?? "custom sample"}\" from lane. Standard hitsound controls active.");
        }

        public void ToggleCompact() => Compact.Toggle();

        public void SetSnap(int divisor) => snapSelect.Value = divisor;

        public void SeekTo(double time) => clock.Seek(Math.Min(time, clock.TrackLength));

        /// <summary>
        /// Plays a lane's sample once, flashing it in the rack.
        /// </summary>
        public void PreviewLane(HitsoundLane lane, bool complainIfMissing = false)
        {
            flashUntil[lane.Id] = Time.Current + 140;

            if (!player.Play(lane, lane.Volume) && complainIfMissing)
                ShowToast($"No sample file loaded for \"{lane.Name}\"");
        }

        private static string plural(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";

        #endregion

        #region Export and copy

        /// <summary>
        /// Writes the lanes into this hitsound difficulty, the Mapping Tools way (see <see cref="HitsoundExporter"/>), as one undoable change.
        /// </summary>
        /// <returns>Whether it was written.</returns>
        public bool Export()
        {
            if (!Editable)
                return false;

            var set = working.Value.BeatmapSetInfo;
            var files = setFiles.ToList();
            var generatedBefore = new HashSet<string>(project.GeneratedFiles, StringComparer.OrdinalIgnoreCase);
            var result = HitsoundExporter.Build(Lanes, Triggers, files, reservedIndices(files, generatedBefore));

            try
            {
                if (set != null && (result.Files.Count > 0 || generatedBefore.Count > 0))
                {
                    foreach (var file in result.Files)
                    {
                        byte[] bytes = HitsoundSampleWriter.Render(file, loadSource);
                        using var stream = new MemoryStream(bytes);
                        beatmapManager.AddFile(set, stream, file.Filename);
                    }

                    var keep = new HashSet<string>(result.Files.Select(f => f.Filename).Concat(result.UsedSetFiles), StringComparer.OrdinalIgnoreCase);

                    foreach (string stale in generatedBefore.Where(f => !keep.Contains(f)))
                    {
                        if (set.GetFile(stale) is RealmNamedFileUsage usage)
                            beatmapManager.DeleteFile(set, usage);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not write the hitsound samples into the beatmap set.");
                return false;
            }

            writing = true;

            try
            {
                var objects = HitsoundProject.ToRulesetObjects(result.Circles, EditorBeatmap).ToList();

                EditorBeatmap.BeginChange();
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(objects);
                // Every circle sits in the middle; stacking would scatter them.
                EditorBeatmap.StackLeniency = 0;
                EditorBeatmap.EndChange();
            }
            finally
            {
                writing = false;
            }

            project.GeneratedFiles = result.Files.Select(f => f.Filename).ToList();
            project.ExportedHash = HitsoundProject.Hash(EditorBeatmap);
            HasUnexportedChanges.Value = false;
            changedBanner.Alpha = 0;
            saveProject();

            ShowToast($"Exported {plural(result.Circles.Count, "note")}, {plural(result.Indices.Count, "custom index")} ({result.NewIndices} new), {plural(result.Files.Count, "sample file")}");
            return true;
        }

        /// <summary>
        /// Indices a new sample set must not take: ones with files we did not make, and ones other difficulties use that we did not make.
        /// </summary>
        private HashSet<int> reservedIndices(IEnumerable<string> files, HashSet<string> generatedBefore)
        {
            var reserved = new HashSet<int>();
            var ours = new HashSet<int>(generatedBefore.Select(HitsoundExporter.IndexOfFile).OfType<int>());

            foreach (string file in files.Where(f => !generatedBefore.Contains(f)))
            {
                if (HitsoundExporter.IndexOfFile(file) is int index)
                    reserved.Add(index);
            }

            foreach (var difficulty in OtherDifficulties)
            {
                try
                {
                    var playable = beatmapManager.GetWorkingBeatmap(difficulty).GetPlayableBeatmap(difficulty.Ruleset);

                    foreach (var sample in HitsoundProject.Import(playable))
                    {
                        if (sample.Sound.File == null && sample.Sound.Index >= 2 && !ours.Contains(sample.Sound.Index))
                            reserved.Add(sample.Sound.Index);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not read {difficulty.DifficultyName} for custom indices: {e.Message}");
                }
            }

            return reserved;
        }

        private byte[]? loadSource(HitsoundExporter.SampleSource source)
        {
            switch (source.Kind)
            {
                case HitsoundExporter.SourceKind.File:
                    string? path = working.Value.BeatmapSetInfo?.GetPathForFile(source.Name);
                    if (path == null)
                        return null;

                    using (var stream = working.Value.GetStream(path))
                    {
                        if (stream == null)
                            return null;

                        using var memory = new MemoryStream();
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }

                case HitsoundExporter.SourceKind.Default:
                    return game.Resources.Get($"Samples/Gameplay/{source.Name}.wav");

                default:
                    return null;
            }
        }

        private void onSaving()
        {
            // The difficulty is saved with what the lanes play.
            if (Editable && HasUnexportedChanges.Value)
                Export();
        }

        /// <summary>
        /// Copies this difficulty's hitsounds onto other difficulties with the Hitsound Copier and saves them. The lanes are exported first.
        /// </summary>
        public int CopyTo(IEnumerable<BeatmapInfo> difficulties, HitsoundCopier copier)
        {
            if (Editable && HasUnexportedChanges.Value)
                Export();

            int copied = 0;

            foreach (var difficulty in difficulties)
            {
                try
                {
                    var target = beatmapManager.GetWorkingBeatmap(difficulty);
                    var targetBeatmap = new EditorBeatmap(target.GetPlayableBeatmap(difficulty.Ruleset), target.GetSkin(), target.Storyboard, difficulty);

                    copier.Copy(EditorBeatmap.HitObjects, 0, targetBeatmap);
                    beatmapManager.Save(difficulty, targetBeatmap.PlayableBeatmap, target.GetSkin(), target.Storyboard);
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

        #region Dropping audio on a lane

        private static readonly string[] audio_extensions = { ".wav", ".ogg", ".mp3" };

        /// <summary>
        /// Audio dropped on the window while this tab is open goes to the lane under the cursor (instead of being imported).
        /// Called from the window's thread.
        /// </summary>
        private bool handleFileDrop(string path)
        {
            // Other tabs take dropped audio too (Setup changes the song with it).
            if (!acceptsDrops || !audio_extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                return false;

            Schedule(() =>
            {
                if (!IsPresent)
                    return;

                var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;
                var lane = mouse is Vector2 position ? rack.LaneAt(position) : null;

                if (lane == null)
                    ShowToast("Drop audio on a lane to load it as that lane's sample");
                else
                    LoadSampleIntoLane(lane, path);
            });

            return true;
        }

        /// <summary>
        /// Adds an audio file to the set under a standard name with the next free custom index (as Hitsound Studio asks for) and points the lane at it.
        /// </summary>
        public void LoadSampleIntoLane(HitsoundLane lane, string path)
        {
            if (!Editable)
                return;

            var set = working.Value.BeatmapSetInfo;

            if (set == null)
                return;

            var files = setFiles.ToList();
            string stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            string bank = new[] { "kick", "snare", "drum", "tom", "hat" }.Any(stem.Contains) ? Audio.HitSampleInfo.BANK_DRUM : Audio.HitSampleInfo.BANK_SOFT;
            var addition = stem.Contains("clap") ? HitsoundAddition.Clap
                : stem.Contains("whistle") ? HitsoundAddition.Whistle
                : stem.Contains("finish") || stem.Contains("crash") || stem.Contains("cymbal") ? HitsoundAddition.Finish
                : HitsoundAddition.None;

            var used = reservedIndices(files, new HashSet<string>(project.GeneratedFiles, StringComparer.OrdinalIgnoreCase));
            used.UnionWith(Lanes.Select(l => l.Index));
            int index = HitsoundExporter.FIRST_NEW_INDEX;
            while (used.Contains(index))
                index++;

            string sampleName = addition switch
            {
                HitsoundAddition.Whistle => Audio.HitSampleInfo.HIT_WHISTLE,
                HitsoundAddition.Finish => Audio.HitSampleInfo.HIT_FINISH,
                HitsoundAddition.Clap => Audio.HitSampleInfo.HIT_CLAP,
                _ => Audio.HitSampleInfo.HIT_NORMAL,
            };

            string filename = $"{bank}-{sampleName}{index}{Path.GetExtension(path).ToLowerInvariant()}";

            try
            {
                using var stream = File.OpenRead(path);
                beatmapManager.AddFile(set, stream, filename);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Could not add {Path.GetFileName(path)} to the beatmap set.");
                return;
            }

            PushHistory();
            lane.Bank = bank;
            lane.AdditionBank = null;
            lane.Addition = addition;
            lane.Index = index;
            lane.File = null;

            if (lane.Name.StartsWith("Lane ", StringComparison.Ordinal) || HitsoundProject.BANKS.Any(b => lane.Name.StartsWith(HitsoundProject.BankName(b) + " ", StringComparison.Ordinal)))
                lane.Name = Path.GetFileNameWithoutExtension(path);

            changed(true);

            // The beatmap skin reloads its samples when the set's files change; play once it has.
            Scheduler.AddDelayed(() => PreviewLane(lane), 100);
            ShowToast($"Loaded \"{Path.GetFileName(path)}\" to lane \"{lane.Name}\" as {filename}");
        }

        #endregion

        #region Keys

        private bool textBoxFocused => GetContainingInputManager()?.FocusedDrawable is TextBox;

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (textBoxFocused)
                return false;

            switch (e.Action)
            {
                case GlobalAction.EditorToggleWhistleSound:
                    if (!e.Repeat) ToggleAdditionOnSelected(HitsoundAddition.Whistle);
                    return true;

                case GlobalAction.EditorToggleFinishSound:
                    if (!e.Repeat) ToggleAdditionOnSelected(HitsoundAddition.Finish);
                    return true;

                case GlobalAction.EditorToggleClapSound:
                    if (!e.Repeat) ToggleAdditionOnSelected(HitsoundAddition.Clap);
                    return true;

                case GlobalAction.EditorCycleGridSpacing:
                    if (!e.Repeat) canvas.ShowGhostNotes.Toggle();
                    return true;

                case GlobalAction.EditorSelectTool:
                    SetSnap(1);
                    return true;

                // Hitsound Studio's single-key copy, paste and delete (C, V, X), which the editor otherwise uses for playback.
                case GlobalAction.EditorTogglePause when e.CurrentState.Keyboard.Keys.IsPressed(Key.C):
                    if (!e.Repeat) Copy();
                    return true;

                case GlobalAction.EditorSeekToEnd when e.CurrentState.Keyboard.Keys.IsPressed(Key.V):
                    if (!e.Repeat) Paste();
                    return true;

                case GlobalAction.EditorPlayFromStart when SelectedIds.Count > 0:
                    if (!e.Repeat) DeleteSelection();
                    return true;

                case GlobalAction.Back when SelectedIds.Count > 0:
                    SetSelection(Enumerable.Empty<string>());
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            if (textBoxFocused)
                return false;

            switch (e.Action)
            {
                case PlatformAction.Undo:
                    Undo();
                    return true;

                case PlatformAction.Redo:
                    Redo();
                    return true;

                case PlatformAction.SelectAll:
                    SelectAll();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (textBoxFocused || e.ControlPressed || e.AltPressed || e.SuperPressed)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Number1: SetSnap(1); return true;

                case Key.Number2: SetSnap(2); return true;

                case Key.Number3: SetSnap(4); return true;

                case Key.Number4: SetSnap(3); return true;

                case Key.Number5: SetSnap(6); return true;

                case Key.Number6: SetSnap(8); return true;

                case Key.Plus:
                case Key.KeypadPlus:
                    canvas.SetZoom(canvas.Zoom.Value * 1.25f);
                    return true;

                case Key.Minus:
                case Key.KeypadMinus:
                    canvas.SetZoom(canvas.Zoom.Value * 0.8f);
                    return true;

                case Key.PageUp:
                    canvas.PageBy(-0.75f);
                    return true;

                case Key.PageDown:
                    canvas.PageBy(0.75f);
                    return true;

                case Key.Delete:
                case Key.BackSpace:
                    if (SelectedIds.Count == 0)
                        break;

                    DeleteSelection();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        #endregion

        #region Layout

        private Drawable createTopBar()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.PANEL },
                    new Box { RelativeSizeAxes = Axes.X, Height = 1, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = StudioColours.BORDER },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 12 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Margin = new MarginPadding { Right = 6 },
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText { Text = "hitsound", Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold), Colour = StudioColours.TEXT },
                                                new OsuSpriteText { Text = "studio", Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold), Colour = StudioColours.ACCENT, Margin = new MarginPadding { Left = 1 } },
                                            }
                                        },
                                        tabs(),
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(12),
                                    Children = new Drawable[]
                                    {
                                        playButton = new StudioButton(FontAwesome.Solid.Play, StudioButtonStyle.Play)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            TooltipText = "Play / Pause (Space)",
                                            Action = () =>
                                            {
                                                if (clock.IsRunning) clock.Stop();
                                                else clock.Start();
                                            },
                                        },
                                        new StudioButton(FontAwesome.Solid.StepBackward)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            TooltipText = "Back to start (Home)",
                                            Action = () => SeekTo(0),
                                        },
                                        readout(),
                                        field("Rate", new StudioSelect<double>(r => $"{r:0.##}×")
                                        {
                                            Width = 60,
                                            Items = () => new[] { 0.5, 0.75, 1.0 },
                                            Current = { BindTarget = rateSelect },
                                        }),
                                        field("Snap", new StudioSelect<int>(d => $"1/{d}")
                                        {
                                            Width = 60,
                                            Items = () => snap_divisors,
                                            Current = { BindTarget = snapSelect },
                                        }),
                                        field("Zoom", new RoundedSliderBar<float>
                                        {
                                            Width = 110,
                                            Current = zoomSlider,
                                            KeyboardStep = 50,
                                        }),
                                        volumes(),
                                    }
                                },
                                new Container
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new StudioButton("Export", StudioButtonStyle.Primary)
                                        {
                                            TooltipText = "Write the lanes into this hitsound difficulty (also happens when you save)",
                                            Action = () => Export(),
                                        },
                                        exportDot = new Circle
                                        {
                                            Size = new Vector2(8),
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.Centre,
                                            Colour = StudioColours.WARN,
                                            Alpha = 0,
                                        },
                                    }
                                },
                            }
                        }
                    },
                }
            };
        }

        private Drawable tabs()
        {
            var container = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = StudioColours.BORDER_SOFT,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BG },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2),
                        Padding = new MarginPadding(2),
                        Children = new Drawable[]
                        {
                            studioTab = new StudioButton("Studio", StudioButtonStyle.Ghost, 22, 11) { Action = () => SwitchTab(false) },
                            copierTab = new StudioButton("Copier", StudioButtonStyle.Ghost, 22, 11) { Action = () => SwitchTab(true) },
                        }
                    },
                }
            };

            studioTab.Active.Value = true;
            return container;
        }

        public void SwitchTab(bool copier)
        {
            studioTab.Active.Value = !copier;
            copierTab.Active.Value = copier;
            studioView.Alpha = copier ? 0 : 1;
            copierView.Alpha = copier ? 1 : 0;

            if (copier)
                copierView.Refresh();
        }

        public bool CopierShown => copierView.Alpha > 0;

        private Drawable readout() => new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1,
            BorderColour = StudioColours.BORDER_SOFT,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BG },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8),
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 4 },
                    Children = new Drawable[]
                    {
                        timeDisplay = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold, fixedWidth: true),
                            Colour = StudioColours.TEXT,
                        },
                        bpmDisplay = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = OsuFont.Default.With(size: 10, fixedWidth: true),
                            Colour = StudioColours.MUTED,
                            Margin = new MarginPadding { Bottom = 1 },
                        },
                    }
                },
            }
        };

        private static Drawable field(string label, Drawable control) => new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(6),
            Children = new[]
            {
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = label,
                    Font = OsuFont.Default.With(size: 10.5f),
                    Colour = StudioColours.MUTED,
                },
                control.With(c =>
                {
                    c.Anchor = Anchor.CentreLeft;
                    c.Origin = Anchor.CentreLeft;
                }),
            }
        };

        private Drawable volumes()
        {
            var flow = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4),
                Padding = new MarginPadding { Left = 12 },
            };

            foreach (var (label, bindable) in new[] { ("Song", SongVolume), ("Hitsounds", HitsoundVolume) })
            {
                flow.Add(field(label, new RoundedSliderBar<int> { Width = 56, Current = bindable }).With(f => f.Margin = new MarginPadding { Left = 6 }));
                flow.Add(new StudioNumberBox(36)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Value = { BindTarget = bindable },
                });
            }

            return new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Y, Width = 1, Colour = StudioColours.BORDER_SOFT },
                    flow,
                }
            };
        }

        private static Container createBanner(string text, params (string Label, Action Action)[] buttons)
        {
            var buttonFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8),
            };

            foreach (var (label, action) in buttons)
                buttonFlow.Add(new StudioButton(label, StudioButtonStyle.Normal, 26) { Action = action });

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.RAISED },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(12),
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Children = new Drawable[]
                        {
                            new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 12))
                            {
                                Width = 640,
                                AutoSizeAxes = Axes.Y,
                                Colour = StudioColours.WARN,
                                Text = text,
                            },
                            buttonFlow,
                        }
                    },
                }
            };
        }

        public void ShowToast(string message) => toast.Show(message);

        /// <summary>
        /// The keys and gestures, bottom right of the canvas (.hint-bar).
        /// </summary>
        private partial class HintBar : CompositeDrawable
        {
            public HintBar()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 6;

                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(12, 0),
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 4 },
                };

                foreach (var (keys, text) in new[]
                         {
                             (new[] { "Click" }, "place"),
                             (new[] { "Drag" }, "select"),
                             (new[] { "W", "E", "R" }, "additions"),
                             (new[] { "C", "V" }, "copy/paste"),
                             (Array.Empty<string>(), "Drop audio on a lane to load a sample"),
                         })
                {
                    var hint = new FillFlowContainer { AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(2, 0) };

                    foreach (string key in keys)
                        hint.Add(new Kbd(key));

                    hint.Add(new OsuSpriteText { Text = text, Font = OsuFont.Default.With(size: 10), Colour = StudioColours.FAINT, Margin = new MarginPadding { Left = 2 } });
                    flow.Add(hint);
                }

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BG.Opacity(0.85f) },
                    flow,
                };
            }

            // Purely informational: let the canvas below get the mouse.
            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => false;

            private partial class Kbd : CompositeDrawable
            {
                public Kbd(string key)
                {
                    AutoSizeAxes = Axes.Both;
                    Masking = true;
                    CornerRadius = 3;
                    BorderThickness = 1;
                    BorderColour = StudioColours.BORDER;

                    InternalChild = new OsuSpriteText
                    {
                        Text = key,
                        Font = OsuFont.Default.With(size: 9, fixedWidth: true),
                        Colour = StudioColours.TEXT,
                        Margin = new MarginPadding { Horizontal = 4, Bottom = 1 },
                    };
                }
            }
        }

        /// <summary>
        /// A short message at the bottom (.toast), replaced by the next one.
        /// </summary>
        private partial class Toast : CompositeDrawable
        {
            private readonly OsuSpriteText text;

            public Toast()
            {
                Anchor = Anchor.BottomCentre;
                Origin = Anchor.BottomCentre;
                AutoSizeAxes = Axes.Both;
                Margin = new MarginPadding { Bottom = 28 };
                Masking = true;
                CornerRadius = 6;
                BorderThickness = 1;
                BorderColour = StudioColours.BORDER;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.RAISED },
                    text = new OsuSpriteText
                    {
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                        Colour = StudioColours.TEXT,
                        Margin = new MarginPadding { Horizontal = 14, Vertical = 8 },
                    },
                };
            }

            public string LastMessage { get; private set; } = string.Empty;

            public void Show(string message)
            {
                LastMessage = message;
                text.Text = message;

                ClearTransforms();
                this.FadeIn(150).MoveToY(0, 150, Easing.OutQuint)
                    .Then().Delay(1400)
                    .FadeOut(200);
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => false;
        }

        /// <summary>
        /// The last message shown at the bottom (for tests).
        /// </summary>
        public string LastToast => toast.LastMessage;

        internal HitsoundCanvas Canvas => canvas;

        internal HitsoundRack Rack => rack;

        internal HitsoundCopierView CopierView => copierView;

        internal bool ChangedBannerShown => changedBanner.Alpha > 0;

        internal bool ReadOnlyBannerShown => readOnlyBanner.Alpha > 0;

        /// <summary>
        /// Reads the saved project again, as opening the tab does.
        /// </summary>
        internal void ReloadProject()
        {
            saveProject();
            loadProject();
        }

        internal void ReimportFromDifficulty() => reimportFromDifficulty();

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            if (saveProjectDelegate != null)
                saveProject();

            if (EditorBeatmap.IsNotNull())
            {
                EditorBeatmap.HitObjectAdded -= onExternalChange;
                EditorBeatmap.HitObjectRemoved -= onExternalChange;
                EditorBeatmap.HitObjectUpdated -= onExternalChange;
            }

            if (editor != null)
                editor.Saving -= onSaving;

            if (osuGame?.FileDropOverride == handleFileDrop)
                osuGame.FileDropOverride = null;

            if (clock.IsNotNull())
                removeSongVolume();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Plays the hits passing the playhead while the clock runs, and lane previews.
        /// Lives inside the beatmap's skin, so the map's custom samples are found.
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
                    bool anySolo = screen.Lanes.Any(l => l.Solo);
                    var lanes = screen.Lanes.ToDictionary(l => l.Id);

                    for (int i = HitsoundCanvas.FirstAfter(screen.Triggers, lastTime); i < screen.Triggers.Count && screen.Triggers[i].Time <= now; i++)
                    {
                        var hit = screen.Triggers[i];

                        if (!lanes.TryGetValue(hit.LaneId, out var lane) || lane.Muted || (anySolo && !lane.Solo))
                            continue;

                        Play(lane, hit.VolumeOn(lane));
                    }
                }

                lastTime = now;
            }

            public bool Play(HitsoundLane lane, int volume)
            {
                var sample = skin.GetSample(lane.Sound.ToSample(volume));
                if (sample == null)
                    return false;

                var channel = sample.GetChannel();
                channel.Volume.Value = volume / 100.0 * screen.HitsoundVolume.Value / 100.0;
                channel.Play();
                return true;
            }
        }
    }
}
