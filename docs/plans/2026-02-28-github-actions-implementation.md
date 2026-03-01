# GitHub Actions Build Pipeline Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a GitHub Actions workflow that compiles the CS2 MapChooser plugin suite and produces a ready-to-install zip archive, with automatic GitHub Release creation on version tags.

**Architecture:** Single workflow file with two jobs. The `build` job compiles all three plugins via `dotnet publish`, cherry-picks only the needed files into a staging directory, zips it, and uploads as an artifact. The `release` job (tag-only) downloads the artifact and creates a GitHub Release.

**Tech Stack:** GitHub Actions, .NET 8.0 SDK, `softprops/action-gh-release@v2`

---

### Task 1: Create the workflow directory and file

**Files:**
- Create: `.github/workflows/build.yml`

**Step 1: Create directory structure**

Run: `mkdir -p .github/workflows`

**Step 2: Write the complete workflow file**

Create `.github/workflows/build.yml` with the following content:

```yaml
name: Build & Release

on:
  push:
    branches: [main]
    tags: ['v*']

permissions:
  contents: write

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore cs2-mapchooser.slnx

      - name: Publish MapChooser
        run: dotnet publish src/MapChooser/MapChooser.csproj -c Release -o publish/MapChooser

      - name: Publish Nominations
        run: dotnet publish src/Nominations/Nominations.csproj -c Release -o publish/Nominations

      - name: Publish RockTheVote
        run: dotnet publish src/RockTheVote/RockTheVote.csproj -c Release -o publish/RockTheVote

      - name: Stage archive
        run: |
          mkdir -p staging/MapChooser staging/Nominations staging/RockTheVote

          # MapChooser: plugin DLL + contracts DLL + lang + maplist
          cp publish/MapChooser/MapChooser.dll staging/MapChooser/
          cp publish/MapChooser/MapChooser.Contracts.dll staging/MapChooser/
          cp -r publish/MapChooser/lang staging/MapChooser/
          cp publish/MapChooser/maplist.txt staging/MapChooser/

          # Nominations: plugin DLL + contracts DLL + lang
          cp publish/Nominations/Nominations.dll staging/Nominations/
          cp publish/Nominations/MapChooser.Contracts.dll staging/Nominations/
          cp -r publish/Nominations/lang staging/Nominations/

          # RockTheVote: plugin DLL + contracts DLL + lang
          cp publish/RockTheVote/RockTheVote.dll staging/RockTheVote/
          cp publish/RockTheVote/MapChooser.Contracts.dll staging/RockTheVote/
          cp -r publish/RockTheVote/lang staging/RockTheVote/

      - name: Determine version
        id: version
        run: |
          if [[ "$GITHUB_REF" == refs/tags/v* ]]; then
            echo "name=${GITHUB_REF#refs/tags/}" >> "$GITHUB_OUTPUT"
          else
            echo "name=build-$(echo $GITHUB_SHA | cut -c1-7)" >> "$GITHUB_OUTPUT"
          fi

      - name: Create zip
        run: |
          cd staging
          zip -r "../cs2-mapchooser-${{ steps.version.outputs.name }}.zip" .

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: cs2-mapchooser
          path: cs2-mapchooser-*.zip

  release:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: cs2-mapchooser

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: cs2-mapchooser-*.zip
          generate_release_notes: true
```

**Step 3: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: add build and release workflow"
```

### Task 2: Update .gitignore for staging/publish directories

**Files:**
- Modify: `.gitignore`

**Step 1: Add build output directories to .gitignore**

Append to `.gitignore`:

```
## Build pipeline
publish/
staging/
```

These directories are used locally during `dotnet publish` testing and should not be committed.

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add publish/staging dirs to gitignore"
```

### Task 3: Verify the workflow locally

**Step 1: Run the same publish + staging commands locally to verify the archive contents**

```bash
dotnet publish src/MapChooser/MapChooser.csproj -c Release -o publish/MapChooser
dotnet publish src/Nominations/Nominations.csproj -c Release -o publish/Nominations
dotnet publish src/RockTheVote/RockTheVote.csproj -c Release -o publish/RockTheVote

mkdir -p staging/MapChooser staging/Nominations staging/RockTheVote

cp publish/MapChooser/MapChooser.dll staging/MapChooser/
cp publish/MapChooser/MapChooser.Contracts.dll staging/MapChooser/
cp -r publish/MapChooser/lang staging/MapChooser/
cp publish/MapChooser/maplist.txt staging/MapChooser/

cp publish/Nominations/Nominations.dll staging/Nominations/
cp publish/Nominations/MapChooser.Contracts.dll staging/Nominations/
cp -r publish/Nominations/lang staging/Nominations/

cp publish/RockTheVote/RockTheVote.dll staging/RockTheVote/
cp publish/RockTheVote/MapChooser.Contracts.dll staging/RockTheVote/
cp -r publish/RockTheVote/lang staging/RockTheVote/
```

**Step 2: Verify the staging directory matches expected layout**

Run: `find staging -type f | sort`

Expected output:
```
staging/MapChooser/MapChooser.Contracts.dll
staging/MapChooser/MapChooser.dll
staging/MapChooser/lang/en.json
staging/MapChooser/lang/fr.json
staging/MapChooser/lang/hu.json
staging/MapChooser/lang/lv.json
staging/MapChooser/lang/pl.json
staging/MapChooser/lang/pt-BR.json
staging/MapChooser/lang/ru.json
staging/MapChooser/lang/tr.json
staging/MapChooser/lang/ua.json
staging/MapChooser/lang/zh-Hans.json
staging/MapChooser/maplist.txt
staging/Nominations/MapChooser.Contracts.dll
staging/Nominations/Nominations.dll
staging/Nominations/lang/en.json
staging/Nominations/lang/fr.json
staging/Nominations/lang/hu.json
staging/Nominations/lang/lv.json
staging/Nominations/lang/pl.json
staging/Nominations/lang/pt-BR.json
staging/Nominations/lang/ru.json
staging/Nominations/lang/tr.json
staging/Nominations/lang/ua.json
staging/Nominations/lang/zh-Hans.json
staging/RockTheVote/MapChooser.Contracts.dll
staging/RockTheVote/RockTheVote.dll
staging/RockTheVote/lang/en.json
staging/RockTheVote/lang/fr.json
staging/RockTheVote/lang/hu.json
staging/RockTheVote/lang/lv.json
staging/RockTheVote/lang/pl.json
staging/RockTheVote/lang/pt-BR.json
staging/RockTheVote/lang/ru.json
staging/RockTheVote/lang/tr.json
staging/RockTheVote/lang/ua.json
staging/RockTheVote/lang/zh-Hans.json
```

No CounterStrikeSharp.API.dll, no .pdb files, no .deps.json, no Microsoft.Extensions.* DLLs.

**Step 3: Clean up**

```bash
rm -rf publish/ staging/
```

### Task 4: Push and verify on GitHub

**Step 1: Push to main**

```bash
git push origin main
```

**Step 2: Verify the workflow runs**

Check the Actions tab on GitHub. The `build` job should:
- Complete successfully
- Show the `cs2-mapchooser` artifact available for download

The `release` job should be skipped (not a tag push).

**Step 3: Test a release (when ready)**

```bash
git tag v1.0.0
git push origin v1.0.0
```

Both jobs should run. The release job creates a GitHub Release at `https://github.com/GabeHirakawa/cs2-mapchooser/releases/tag/v1.0.0` with the zip attached.
