# STS2 Workshop Uploader

In-game Steam Workshop manager and uploader for local Slay the Spire 2 mod workspaces.

## Overview

STS2 Workshop Uploader adds a RitsuLib settings-menu entry that opens a dedicated Workshop upload UI. It is intended for
managing local loaded or unloaded mods and publishing them to Steam Workshop without using the original external
uploader flow directly.

Core features:

- Detect local mods from the game `mods` directory.
- Bind or unbind a local mod to a Steam Workshop item.
- Create uploads for new Workshop items.
- Upload metadata only, or upload content and metadata together.
- Edit title, description, tags, visibility, dependencies, main and additional preview images, game version requirements, and changelog.
- Manage localized title and description files.
- Convert Markdown descriptions and changelogs to Steam BBCode.
- Compare local metadata and package file state against the stored upload baseline.
- Check Steam Workshop legal agreement status before uploading.

## Requirements

- Slay the Spire 2
- Steam client running with a user that can access Steam Workshop
- RitsuLib `0.4.29` or newer

The mod manifest depends on `STS2-RitsuLib`, and the project references the `STS2.RitsuLib` NuGet package.

## Build

Build the project with:

```powershell
dotnet build .\STS2-WorkshopUploader.csproj
```

By default, the build copies this mod into the configured Slay the Spire 2 `mods` directory. The RitsuLib package also
copies its runtime files when `RitsuLibDeployDir` is set.

Local machine paths can be overridden with `local.props`.

## Repository

https://github.com/BAKAOLC/STS2-WorkshopUploaderMod

## Author

OLC

## License

GNU Affero General Public License v3.0 only.
