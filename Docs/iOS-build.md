# iOS Test Build

The iOS build has two stages: Unity exports an Xcode project on Windows, then
GitHub Actions compiles it into an unsigned device IPA on macOS. The IPA must be
signed with an Apple account before installation on an iPhone. Keep Apple account
credentials on the local device; the GitHub workflow does not use them.

## Export from Unity on Windows

Install Unity 6000.3.14f1 with iOS Build Support and Git LFS assets. From the
repository root, run `Reverie > iOS > Export Xcode Project` in the editor, or:

```powershell
$unity = 'D:\Unity\Editor\6000.3.14f1\Editor\Unity.exe' # Adjust for your installation.
$project = (Resolve-Path .).Path
New-Item -ItemType Directory -Force Logs | Out-Null
& $unity -batchmode -nographics -quit -projectPath $project -buildTarget iOS `
  -executeMethod Hypocycloid.Reverie.Editor.IOSBuild.Export `
  -logFile (Join-Path $project 'Logs\ios-unity.log')
Get-Content 'Logs\ios-build-result.txt'
```

The export is `Builds/iOS/Reverie` and the result report is
`Logs/ios-build-result.txt`. Both paths are ignored by Git. Check that the
report begins with `Succeeded` before proceeding.

## Compile on GitHub

The [Build unsigned iOS test IPA](../.github/workflows/ios-unsigned.yml)
workflow downloads `Reverie-Xcode.zip` from a GitHub release, compiles for a
physical iPhone without signing, and uploads `Reverie-unsigned.ipa` as a run
artifact. Create the zip with the `Reverie` directory at its root:

```powershell
Push-Location Builds/iOS
& 'C:\Program Files\7-Zip\7z.exe' a -tzip Reverie-Xcode.zip Reverie
Pop-Location
$tag = 'v0.1.20260930-main.0'
gh release upload $tag 'Builds/iOS/Reverie-Xcode.zip' --repo Elykdez/Reverie
gh workflow run ios-unsigned.yml --repo Elykdez/Reverie --ref main -f release_tag=$tag
```

Use an existing release tag and change `$tag` for later builds. After the run
completes, download its `Reverie-unsigned-ipa` artifact from the Actions page,
or use `gh run download <run-id> --repo Elykdez/Reverie --name Reverie-unsigned-ipa --dir Builds/iOS`.
The workflow retains the artifact for seven days; rerun it if it expires.
The [reference release](https://github.com/Elykdez/Reverie/releases/tag/v0.1.20260930-main.0)
also holds the unsigned IPA for later download.

## Install on iPhone from Windows

The IPA is unsigned and cannot be opened directly on an iPhone. Use
[AltStore Classic's Windows setup](https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows)
to install AltServer and pair the iPhone. Hold Shift while clicking the
AltServer tray icon, choose `Sideload .ipa...`, and select
`Builds/iOS/Reverie-unsigned.ipa`. AltServer signs it with the Apple account
entered locally. Enable Developer Mode and trust that account on the iPhone
when prompted. [AltStore's notes](https://faq.altstore.io/release-notes/altserver)
say apps sideloaded this way need reinstalling after seven days; installing
through AltStore Classic allows refreshing during that period.

The successful reference run for this workflow is
[36701769741](https://github.com/Elykdez/Reverie/actions/runs/36701769741).
