# GitHub Actions Build & Release Pipeline

## Goal

Automate compilation of the CS2 MapChooser plugin suite and produce a single zip archive that server admins can extract directly into their CounterStrikeSharp plugins directory. On version tags, auto-create a GitHub Release with the archive attached.

## Triggers

- **Push to `main`** — CI build, upload artifact
- **Push of tag `v*`** — CI build + create GitHub Release with attached zip

## Workflow Structure

Single file: `.github/workflows/build.yml`

### Job 1: `build`

Runs on `ubuntu-latest` for every trigger.

1. Checkout code
2. Setup .NET 8.0 SDK
3. `dotnet publish` each plugin project in Release mode:
   - `src/MapChooser/MapChooser.csproj` → `staging/MapChooser/`
   - `src/Nominations/Nominations.csproj` → `staging/Nominations/`
   - `src/RockTheVote/RockTheVote.csproj` → `staging/RockTheVote/`
4. Create zip from `staging/` directory
5. Upload zip as GitHub Actions artifact

### Job 2: `release`

Runs only on tag pushes (`if: startsWith(github.ref, 'refs/tags/v')`). Depends on `build`.

1. Download artifact from build job
2. Create GitHub Release via `softprops/action-gh-release@v2`
3. Attach zip as release asset
4. Auto-generate release notes from commits since last tag

## Archive Layout

Filename: `cs2-mapchooser-{version}.zip`

```
MapChooser/
  MapChooser.dll
  MapChooser.Contracts.dll
  lang/*.json (10 languages)
  maplist.txt
Nominations/
  Nominations.dll
  MapChooser.Contracts.dll
  lang/*.json (10 languages)
RockTheVote/
  RockTheVote.dll
  MapChooser.Contracts.dll
  lang/*.json (10 languages)
```

Admin extracts into `csgo/addons/counterstrikesharp/plugins/` and all three plugins are ready.

## Excluded from Archive

- CounterStrikeSharp.API assemblies (already on the server)
- `*.pdb` debug symbols
- `*.deps.json` dependency manifests
- Build artifacts (`obj/`, intermediate files)

## Dependencies

- `actions/checkout@v4`
- `actions/setup-dotnet@v4`
- `actions/upload-artifact@v4`
- `actions/download-artifact@v4`
- `softprops/action-gh-release@v2`

## Permissions

Workflow requires `contents: write` for creating releases.

## Decisions

- Single workflow over split ci/release workflows — simpler, shared build logic
- Single zip over per-plugin zips — the plugins are designed to work together
- Plugin-ready folder layout — zero configuration for server admins
