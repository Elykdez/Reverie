# Splat Library

Open `Reverie > Splats` for the Splat Library. Its Collection tab automatically finds the scene's collection, `Assets/Bundles/Resources/Config/SplatCollection.asset`, which contains Ginger and Tomatoes. Each entry assigns an imported preview, streaming manifest, local position/rotation/scale, world-space camera focus and attribution. The tool supports Gaussian splat captures of any subject. Every capture uses the import layout, `Assets/SceneData/Splats/<name>` and `Assets/StreamingAssets/Splats/<name>`; script and asset GUIDs are retained for compatibility.

`SplatSwitcher` on GameManager is wired to the scene's existing renderer, streamer, loading blur and camera controls. It selects a splat uniformly at startup. Disable `randomOnStartup` to use `defaultIndex`. Swaps choose a different entry, cancel the previous stream, release its buffers, update both camera targets and restart the loading reveal. Music and island stay in place. Only previews and manifests are referenced by the collection; the full source PLYs are editor source assets, not runtime references.

On mobile, two opposing acceleration peaks above `shakeThreshold` within `shakeWindow`, with a release between them, trigger a swap. Gravity is filtered out. The default is 1.5 g, a 0.75-second window and a 2.5-second cooldown. Input is ignored during loading, pause, focus loss or an input-blocking UI. Sensor discovery is retried for devices registered late. The accelerometer is shared with the gravity camera and is not disabled by the switcher. A physical-device check is needed to tune comfort for the target phone.

Press N on desktop, use `SwapSplat()` / `SelectSplat(index)` at runtime, or use Show Selected Splat in the library. Edit Mode preview does not change random startup selection. To adjust a pose, choose a splat on the Collection tab and edit Position Offset, Rotation and Scale. Increase Y to raise it. Placement changes update the scene preview in Edit Mode; use Save Collection to persist the configuration. Camera focus is explicit because a scan can contain distant, faint splats that would distort its bounding-box center. GameManager's Splat Switcher Inspector also links to the same library and retains its placement controls.

## Live Opacity Pruning

Select the scene's splat object and expand **Gsplat Lod Streamer > Splat Simplify**,
or tap the HUD's splat icon during Play. The HUD shows **Budget**, **LOD**, and
**Opacity**, with Reset beside Close. These map to the Inspector's **Target**,
**LOD Base**, and **Opacity Prune**, using the same streamer settings.
Budget sliders stop at the current capture's finest available count, so dragging
does not waste travel on budgets larger than the capture. Opening the panel does
not overwrite a larger authored budget; Reset still restores that authored value.
LOD uses a logarithmic distance slider (0.1 to 100), with larger values keeping
more detail. The Inspector also accepts a typed distance. Budget and LOD select
discrete imported levels, so changes appear in steps as replacement chunks load.
The opacity slider ranges from 0 to 1; zero disables the added filter and equality
is retained. Changes update the assigned edit-mode preview and live streamed
renderers. LOD and budget selection run during Play; an edit-mode preview is a
separate low-resolution asset. New chunks and capture swaps inherit the settings.
HUD **Reset** restores all three scene-start values. Edit Mode changes can be saved
in the scene; Play Mode changes last for that session. Source assets stay unchanged.

Reverie's device-scaled budget is unchanged. `OpacityPassingSplatCount` counts
selected leaf splats before camera/pixel culling and excludes the environment,
matching `ActiveSplatCount`. Pruning narrows each renderer's active ranges, so
rejected splats skip sorting and drawing; loaded memory is unchanged, and each
threshold change rescans the selected splats on the CPU.
Higher thresholds can remove fine detail and create holes. Set opacity back to zero
to restore the unpruned view. The Inspector's count cards compare the finest available leaf
count with the requested budget, not a newly generated output asset. Device budget,
active count, and passing-opacity count show what the live scene is doing. Coarse
coverage may exceed a very small target; distance and visibility may keep it below
a large target. HUD editing holds the existing camera/shake input gate, and Back
closes the panel. The panel scales inside the HUD safe area.

Reverie currently has no Gaussian-to-mesh bake tool. Pruning does not alter the
island mesh, colliders, or imported geometry. If a splat bake workflow is added,
it must explicitly capture this threshold and filter its source before baking;
existing meshes cannot change merely by moving the rendering slider.

UnitySplats is a third-party dependency used as-is from the pinned git URL in
`Packages/manifest.json`. Opacity pruning uses its public
`GsplatRenderer.SetActiveRanges`, and its settings live at
`Assets/Settings/Resources/GsplatSettings.asset`, where the package loads them.
Extend the package from project code; do not patch it.
This is part of the [personal Gaussian splat project template](Gaussian-splat-template.md).

## Importing

Node.js/npx and PowerShell are required. In the library's Import PLY tab, choose a Gaussian splat PLY and a unique name, then Import into collection. This copies the source and adjacent license, generates six SOG LOD levels with pinned `@playcanvas/splat-transform@3.4.2`, creates preview/manifest assets, and adds an entry to the selected collection. New imports use `Assets/SceneData/Splats/<name>` and `Assets/StreamingAssets/Splats/<name>`. Existing folders are never overwritten. Conversion logs are in `Logs/splat-import.log`; failed imports retain their source files. On success, the library switches to the new entry for placement. The Gsplat package's own tools are unchanged.

The converter can also be run directly, followed by `SplatLibraryWindow.ImportGenerated` from editor code. The returned SplatEntry can be added to a collection. Import uses RUB coordinates, matching the supplied PLY/SOG captures. Fit each new capture's orientation and scale in the collection. The original source remains unchanged.

Ginger's author and license were checked on its supplied SuperSplat page without downloading the asset. The Tomatoes capture was imported with this tool; its supplied license is retained with its source. Full attribution ships in `Assets/StreamingAssets/Splat-Credits.txt` and each splat's collection entry.
