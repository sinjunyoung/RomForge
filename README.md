# RomForge

Multi-Platform ROM Patching & Compression Utility

RomForge is an all-in-one Windows desktop tool for managing, converting, patching and compressing title/ROM files across multiple console platforms.

<img width="850" height="683" alt="image" src="https://github.com/user-attachments/assets/a7643baa-c7bb-4154-97a7-1fe7a9fa0fae" />


## User Guide

https://sinjunyoung.github.io/RomForge/

## Features

| Platform | What you can do |
| -------- | --------------- |
| Nintendo Switch | NSP/XCI unpack & repack, merge (base + update + DLC), version ignore, format conversion, Saturn ROM NSP conversion |
| Nintendo 3DS | CCI/CIA decrypt, repack, install to console / PC, format conversion |
| Wii U | Repack, format conversion |
| Wii / GameCube | Format conversion (WBFS / ISO / RVZ) |
| PSP / PS1 | PBP packing & unpacking, format conversion (CSO / ISO) |
| PS Vita | PKG / NoNpDrm handling, patching |
| Disc-based consoles | PS1, PS2, Sega Saturn, Dreamcast, etc. — CHD compression / extraction and patching |

Common to all platforms:

* **Patching** — IPS, IPS32, BPS, UPS, xdelta, and Dreamcast DCP patches, with multi-patch chaining
* **Compression & conversion** — a unified conversion tab for compression, decompression and format conversion
* **Hash display** — CRC32 / MD5 / SHA-1 for the selected source ROM
* **Utilities** — Tistory attachment downloader, and more
* **Multi-language UI** — Korean, English, Japanese, Simplified Chinese, Traditional Chinese, French, German, Russian, Spanish

## Requirements

* Windows x64
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
* Console key files for the platforms you use (e.g. `prod.keys` for Switch). Key files are **not** included with RomForge — see the [User Guide](https://sinjunyoung.github.io/RomForge/installation/) for details.

## Download

Prebuilt binaries are available on the [Releases](https://github.com/sinjunyoung/RomForge/releases) page. No installation is required.

* **DLLs** — the exe and its dependent DLLs in separate files (recommended; less likely to trigger antivirus false positives)
* **Single** — everything bundled into a single exe

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
BUILD.bat          (framework-dependent, multiple files)
BUILD_Single.bat   (single-file)
```

The output is placed in `RomForge\bin\x64\Release\net10.0-windows\win-x64\publish`.

The native CHD library is built separately; see `CHD/BUILD.txt`.

The documentation site is built with [MkDocs Material](https://squidfunk.github.io/mkdocs-material/) (`pip install -r requirements.txt`).

## License

This project is licensed under the GNU General Public License v2.0 or later (GPL-2.0-or-later). See the `LICENSE` file for the full license text.

## Third-Party Components

RomForge incorporates modified versions of the following open-source software:

| Component | License | Link |
| --------- | ------- | ---- |
| CHDMan (MAME) | GPL-2.0-or-later | https://github.com/mamedev/mame |
| libchdr | BSD-3-Clause | https://github.com/rtissera/libchdr |
| xdelta3 | GPL-2.0 | https://github.com/jmacd/xdelta |
| LibHac | MIT | https://git.ryujinx.app/ryubing/libhac (original: https://github.com/Thealexbarney/LibHac) |
| 7-Zip | LGPL-2.1-or-later | https://www.7-zip.org/ |

The CHDMan, xdelta3, and 7-Zip components have been modified and/or improved for use with RomForge. The original copyright notices and license terms remain applicable to the respective components.

LibHac is used under the terms of its MIT License.

## Libraries

RomForge uses the following libraries through NuGet:

| Library | Purpose | Link |
| ------- | ------- | ---- |
| ZstdSharp.Port | Zstandard compression (NSZ, etc.) | https://github.com/oleg-st/ZstdSharp |
| K4os.Compression.LZ4 | LZ4 compression | https://github.com/MiloszKrajewski/K4os.Compression.LZ4 |
| Blake3 | BLAKE3 hashing | https://github.com/xoofx/Blake3.NET |
| SharpCompress | Archive reading (patch sources) | https://github.com/adamhathcock/sharpcompress |
| SharpZipLib | Zip / deflate handling | https://github.com/icsharpcode/SharpZipLib |
| LibDeflate.NET | Fast deflate | https://www.nuget.org/packages/LibDeflate.NET |
| SevenZipSharp (Squid-Box) | 7-Zip wrapper | https://github.com/squid-box/SevenZipSharp |
| System.IO.Hashing | CRC32 and other hashes | https://github.com/dotnet/runtime |
| System.Management | Drive / system information | https://github.com/dotnet/runtime |
| NAudio | Audio playback | https://github.com/naudio/NAudio |
| NLayer | MP3 decoding | https://github.com/naudio/NLayer |
| Markdig | Markdown parsing | https://github.com/xoofx/markdig |
| Markdig.Wpf | Markdown rendering in WPF | https://github.com/Kryptos-FR/markdig.wpf |
| Ookii.Dialogs.Wpf | Native folder / file dialogs | https://github.com/ookii-dialogs/ookii-dialogs-wpf |
| HtmlAgilityPack | HTML parsing (Tistory downloader) | https://github.com/zzzprojects/html-agility-pack |
| Darnelix.Webp | WebP image decoding | https://www.nuget.org/packages/Darnelix.Webp |
| JetBrains.Annotations | Code annotations | https://github.com/JetBrains/JetBrains.Annotations |

Each library is used under its own license.

## Acknowledgements

RomForge also contains independent C# implementations of functionality and file formats supported by or related to various existing open-source projects and tools.

These projects were used as references for file formats, interoperability, algorithms, and/or expected behavior. Their source code is not incorporated into RomForge unless explicitly listed under `Third-Party Components` above.

* [hacPack](https://github.com/DarkMatterCore/hacPack)
* [Cemu](https://github.com/cemu-project/Cemu)
* [DolphinTool](https://github.com/dolphin-emu/dolphin)
* [WIT (Wiimms ISO Tools)](https://github.com/Wiimm/wiimms-iso-tools)
* [Azahar](https://github.com/azahar-emu/azahar)
* [Project_CTR (ctrtool / makerom)](https://github.com/3DSGuy/Project_CTR)
* [custom-install](https://github.com/ihaveamac/custom-install)
* [save3ds](https://github.com/wwylele/save3ds)
* [Vita3K](https://github.com/Vita3K/Vita3K)
* [pkg2zip](https://github.com/mmozeiko/pkg2zip)
* [PSX2PSP](https://github.com/Kippykip/PSX2PSP)
* [pop-fe (pop-fe-ui)](https://github.com/sahlberg/pop-fe)
* [maxcso](https://github.com/unknownbrackets/maxcso)
