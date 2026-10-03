# Third-party software in YAWNS release builds

The release builds (AppImage, Windows zip) bundle these libraries next to YAWNS itself. Each keeps its own licence.

| Library | Licence | Source |
|---|---|---|
| FFmpeg (libavcodec, libavformat, libavutil, libswscale) | LGPL 2.1+ | https://ffmpeg.org, built by osu-framework's native libraries |
| OpenTabletDriver | LGPL 3.0 | https://github.com/OpenTabletDriver/OpenTabletDriver |
| BASS, BASS_FX, BASSmix, BASSWASAPI (un4seen) | free for non-commercial use | https://www.un4seen.com |
| SDL2, SDL3 | zlib | https://libsdl.org |
| Realm | Apache 2.0 | https://github.com/realm/realm-dotnet |
| SQLite | public domain | https://sqlite.org |
| .NET runtime and libraries | MIT | https://github.com/dotnet |
| osu-framework, osu! game code | MIT | https://github.com/ppy |
| osu! game resources (without Torus/Venera fonts) | CC BY-NC 4.0 | https://github.com/ppy/osu-resources |

The LGPL libraries are used as separate shared libraries (`.so` / `.dll`) and can be replaced with your own builds of the same version.
Other managed dependencies (NuGet packages) are under permissive licences (MIT, Apache 2.0, BSD); see each package for details.
