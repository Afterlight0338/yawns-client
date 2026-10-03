// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// YAWNS: every path out of the menus leads into the editor. The only gameplay is the editor's test play.
    /// </summary>
    public partial class TestSceneMappingClient : OsuGameTestScene
    {
        private ButtonSystem buttons => ((MainMenu)Game.ScreenStack.CurrentScreen).ChildrenOfType<ButtonSystem>().Single();

        [Test]
        public void TestYawnsLogoReplacesCookie()
        {
            AddAssert("menu logo texture is the YAWNS one", () =>
            {
                byte[] expected;
                using (var stream = typeof(OsuGameBase).Assembly.GetManifestResourceStream("osu.Game.Textures.Menu.logo.png")!)
                using (var copy = new System.IO.MemoryStream())
                {
                    stream.CopyTo(copy);
                    expected = copy.ToArray();
                }

                return Game.Resources.Get("Textures/Menu/logo.png").SequenceEqual(expected);
            });
        }

        [Test]
        public void TestMainMenuOnlyLeadsToEditing()
        {
            AddStep("press P", () => InputManager.Key(Key.P));
            AddAssert("state is top level", () => buttons.State == ButtonSystemState.TopLevel);

            AddStep("press former play/multi/playlists/daily/browse keys", () =>
            {
                InputManager.Key(Key.M);
                InputManager.Key(Key.L);
                InputManager.Key(Key.D);
                InputManager.Key(Key.B);
            });
            AddAssert("still at top level", () => Game.ScreenStack.CurrentScreen is MainMenu && buttons.State == ButtonSystemState.TopLevel);

            AddStep("press P", () => InputManager.Key(Key.P));
            AddUntilStep("entered edit song select", () => Game.ScreenStack.CurrentScreen is EditSongSelect);
        }

        [Test]
        public void TestPickEditTestPlayAndBack()
        {
            BeatmapSetInfo beatmapSet = null!;

            AddStep("import test beatmap", () => Game.BeatmapManager.Import(TestResources.GetTestBeatmapForImport()).WaitSafely());
            AddStep("retrieve beatmap", () => beatmapSet = Game.BeatmapManager.QueryBeatmapSet(set => !set.Protected).AsNonNull().Value.Detach());

            AddStep("present beatmap", () => Game.PresentBeatmap(beatmapSet));
            AddUntilStep("wait for edit song select", () => Game.Beatmap.Value.BeatmapSetInfo.Equals(beatmapSet)
                                                             && Game.ScreenStack.CurrentScreen is EditSongSelect songSelect
                                                             && songSelect.CarouselItemsPresented);

            AddUntilStep("footer buttons loaded", () => Game.ChildrenOfType<FooterButtonRandom>().Any());
            AddAssert("no mods button", () => !Game.ChildrenOfType<FooterButtonMods>().Any());
            AddAssert("first beatmap action is edit", () =>
            {
                var first = ((EditSongSelect)Game.ScreenStack.CurrentScreen).GetForwardActions(Game.Beatmap.Value.BeatmapInfo).First();
                return first.Type == MenuItemType.Highlighted && first.Icon.Equals(FontAwesome.Solid.PencilAlt);
            });

            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("editor opened", () => Game.ScreenStack.CurrentScreen is Editor editor && editor.ReadyForUse);

            AddStep("test gameplay", () => ((Editor)Game.ScreenStack.CurrentScreen).TestGameplay());
            AddUntilStep("wait for test play", () =>
            {
                // notifications can block the player from loading.
                Game.CloseAllOverlays();
                return Game.ScreenStack.CurrentScreen is EditorPlayer editorPlayer && editorPlayer.IsLoaded;
            });

            AddStep("exit test play", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back in editor", () => Game.ScreenStack.CurrentScreen is Editor editor && editor.ReadyForUse);

            AddStep("exit editor", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back at edit song select", () => Game.ScreenStack.CurrentScreen is EditSongSelect);
        }
    }
}
