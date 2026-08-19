# DataAddonMod

A BepInEx plugin for Ostranauts that loads data extensions from installed (and not disabled) mods and injects them after the game finishes loading its data. Allows modification of Loot or CondOwner (and other) objects (like kiosk inventories or the Crew01 CO) without overwriting base game or other mod data. Created to be light weight and so that addon errors don't interfere with the base game or mods: if this plugin is broken by a game update mods utilizing it will still work, causing no data load errors or other issues, just missing provided functionality.

## What it does

The plugin scans each enabled Ostranauts mod for JSON files under:

```text
<mod>/data/addons/<data-type>/**/*.json
```

Entries are keyed by their `strName` property. New entries are added to the game's data dictionaries. When an entry already exists, string-array properties are appended to the existing value.

Supported data types are:

- `condowners`
- `cooverlays`
- `slot_effects`
- `items`
- `loot`
- `conditions`
- `cond_rules`
- `cond_trigs`
- `interactions`
- `personspecs`
- `pledges`
- `plot_manager`

The plugin subscribes to the game's data-load completion event, so addon data is processed after the base data is available.

## Project layout

```text
DataAddonMod/
|-- DataAddonMod.csproj       .NET Framework 4.8 BepInEx project
|-- Config.Build.user.props   Local Ostranauts and BepInEx paths
|-- NuGet.Config              NuGet.org and BepInEx package sources
|-- src/
|   |-- BepinexPlugin.cs      Plugin entry point and configuration
|   `-- DataAddons.cs         Addon discovery, parsing, and merging
|-- scripts/
|   `-- deploy_release.py     Copies the Release DLL to BepInEx/plugins
|-- packages.lock.json        NuGet lock file
`-- README.md
```

## Requirements

- Ostranauts installed locally
- BepInEx 5 installed for Ostranauts
- .NET SDK with support for building `net48`
- The Ostranauts managed assemblies and BepInEx core assemblies available on disk
- Network access to the configured NuGet feeds on the first restore

## Configuration

Edit `Config.Build.user.props` for the paths on your machine:

- `DependsDir`: Ostranauts `Ostranauts_Data/Managed` directory
- `BepInExDir`: Ostranauts `BepInEx/core` directory

These paths are imported by `DataAddonMod.csproj` and are intentionally kept outside the project file because they are machine-specific.

`NuGet.Config` is required because `BepInEx.Core` and `BepInEx.Analyzers` are provided by the BepInEx NuGet feed rather than the default `nuget.org` source.

To verify the evaluated values without compiling, run:

```bash
dotnet msbuild DataAddonMod.csproj -getProperty:DependsDir -getProperty:BepInExDir
```

Every normal build also runs `VerifyBuildProperties`, which prints both resolved paths and stops with an explicit error if either property is empty or points to a missing directory. To inspect the fully evaluated project, run:

```bash
dotnet msbuild DataAddonMod.csproj -preprocess:project.evaluated.xml
```

Search `project.evaluated.xml` for `DependsDir` and `BepInExDir`. The generated file is temporary diagnostic output and should not be committed.

## Build

From this directory, run:

```bash
dotnet restore
dotnet build
```

The compiled plugin is written beneath `bin/`. Copy `DataAddonMod.dll` to the game's BepInEx plugins directory, normally:

```text
<Ostranauts>/BepInEx/plugins/
```

Restart the game and inspect `BepInEx/LogOutput.txt` for the plugin load message and addon parsing errors.

## Release building

Release builds should be created from a clean working tree with the local game paths configured in `Config.Build.user.props`.

```bash
dotnet clean
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
```

The release plugin is written to:

```text
bin/Release/net48/DataAddonMod.dll
```

To create the Steam Workshop package in the configured game directory, run:

```bash
python3 scripts/deploy_release.py
```

The script reads `BepInExDir` from `Config.Build.user.props` and creates:

```text
Ostranauts/Ostranauts_Data/Mods/DataAddonMod/
|-- mod_info.json
|-- preview.png
`-- plugins/
    `-- DataAddonMod.dll
```

The root `manifest.json` is copied as `mod_info.json`, preserving its metadata and BepInEx dependency array. Add a `preview.png` file to the project before running the script, or provide another image with `--preview <path>`. Use `--dry-run` to inspect the package paths without copying:

```bash
python3 scripts/deploy_release.py --dry-run
```

Use `--source`, `--props`, `--metadata`, or `--preview` when deploying from different build, game, metadata, or image locations.

Before publishing, verify that the DLL exists and that the build output does not contain unexpected source or dependency files. A minimal Workshop release should contain:

```text
DataAddonMod/
|-- mod_info.json
|-- preview.png
`-- plugins/
    `-- DataAddonMod.dll
```

Do not include `Config.Build.user.props`, `NuGet.Config`, `obj/`, or the local Ostranauts DLL references in the Workshop package. Test the packaged mod in a clean game installation and check `BepInEx/LogOutput.txt` for the successful plugin load and addon injection messages.

## Addon example

A mod can provide an addon file such as:

```text
MyMod/
`-- data/
    `-- addons/
        `-- loot/
            `-- kiosks.json
```

The JSON file must contain an array of objects matching the corresponding Ostranauts data type. Each object needs a non-empty `strName` value. Files are read recursively, and malformed files are skipped with a warning in the BepInEx log.

## Debug logging

The plugin creates a BepInEx configuration entry named `General/DebugMode`. Set it to `true` to enable detailed addon processing logs.

## Current limitations

- Reference paths are local to the developer's installation.
- The merger currently appends only writable `string[]` properties; other fields are not merged.
- Addon files are loaded during the data-load completion callback, so invalid JSON or incompatible fields are reported at runtime.

## License

See [LICENSE](LICENSE) if a license file is added to this project.
