// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Desktop.IPC;
using osu.Desktop.Performance;
using osu.Desktop.Security;
using osu.Framework.Platform;
using osu.Game;
using osu.Desktop.Updater;
using osu.Framework;
using osu.Framework.Logging;
using osu.Game.Updater;
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Performance;
using osu.Game.Utils;

namespace osu.Desktop
{
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly HighPerformanceSessionManager highPerformanceSessionManager = new HighPerformanceSessionManager();

        public bool IsFirstRun { get; init; }

        public bool EnableWebSocketServer { get; init; }

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
        }

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = getStableInstallPath();
                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry("osustable.File.osz");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;

                    stableInstallPath = getStableInstallPathFromRegistry("osu!");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry(string progId)
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey(progId))
                return key?.OpenSubKey(WindowsAssociationManager.SHELL_OPEN_COMMAND)?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        public static bool IsPackageManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER"));

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream specified by the installation target.
            //
            // This ensures that if a user is trying to recover from a failed startup, it will keep them
            // on the stream which is imminently being reinstalled.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, Version.Contains("-tachyon") ? ReleaseStream.Tachyon : ReleaseStream.Lazer);

            // YAWNS: never updates itself (updates are merges of lazer releases, built by hand).
            return new NoActionUpdateManager();
        }

        public override bool RestartAppWhenExited()
        {
            RestartOnExitAction = () => Velopack.UpdateExe.Start(waitPid: (uint)Environment.ProcessId);
            return true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // YAWNS: no Discord rich presence (it would show under the osu! application).

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    LoadComponentAsync(new GameplayWinKeyBlocker(), Add);
                    break;

                // YAWNS: macOS is not a target, the MacOSAppLocationChecker is removed.
            }

            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);

            if (EnableWebSocketServer)
                Add(new OsuWebSocketProvider());
        }

        public override unsafe void SetHost(GameHost host)
        {
            base.SetHost(host);

            // YAWNS: the library (database schema) is shared with lazer, so refuse to open it when lazer last ran a different release.
            // This runs before the database opens; run.sh does the same check for source builds.
            string lazerVersion = LocalConfig.Get<string>(OsuSetting.Version);

            if (!string.IsNullOrEmpty(lazerVersion) && lazerVersion != LAZER_VERSION)
            {
                SDL.SDL3.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "YAWNS can't open your lazer library",
                    $"Your osu!lazer last ran {lazerVersion}, but this YAWNS is built for lazer {LAZER_VERSION}.\n"
                    + "YAWNS shares lazer's library and must match its version, otherwise it could damage the database.\n\n"
                    + $"Get the YAWNS release built for lazer {lazerVersion}, or run lazer {LAZER_VERSION}.", null);
                Environment.Exit(1);
            }

            // Apple operating systems use a better icon provided via external assets.
            if (!RuntimeInfo.IsApple)
            {
                var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico");
                if (iconStream != null)
                    host.Window.SetIconFromStream(iconStream);
            }

            host.Window.Title = $"{Name} v{Version}"; // YAWNS
        }

        protected override BatteryInfo CreateBatteryInfo() => FrameworkEnvironment.UseSDL3 ? new SDL3BatteryInfo() : new SDL2BatteryInfo();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }
    }
}
