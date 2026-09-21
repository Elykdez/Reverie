# Personal Unity Gaussian Splat Template

Reverie is the user's personal Unity project template for using Gaussian splat
assets. Keep the import/collection, streaming, rendering controls, and platform
integration reusable for captures of any subject. Capture names and placement
belong in collection data, not in the control code.

## Splat Simplify Controls

The template exposes budget, LOD distance, and opacity pruning in the streamer's
Inspector and the runtime HUD. The compact HUD shows only **Budget**, **LOD**, and
**Opacity**, with Reset beside Close. Source and count diagnostics remain in the
Inspector; other streamer properties are under Advanced.
Budget slider travel is capped to the capture's finest count, and LOD distance
uses logarithmic slider travel to make the smaller, more sensitive values usable.
LOD and budget change discrete streamed levels; opacity filters individual splats.

Both surfaces operate on one `GsplatLodStreamer`. Runtime callers use
`SetSplatBudget`, `SetLodBaseDistance`, and `SetOpacityPrune`. This keeps the settings
consistent across capture swaps and replacement chunks. Reset returns to authored
scene-start values. Runtime edits are reversible and do not rewrite source assets.

Target is a LOD budget, not an exact surviving-splat count. Original means the
finest available leaf count, excluding environment splats. Device scaling can lower
the effective budget, but cannot raise it above the requested target. Opacity
pruning removes whole splats from the renderers' active ranges; it does not
reduce loaded memory or generate a simplified output file.

## Reusing the Integration

- Use `com.arloopa.unitysplats` unmodified from the pinned git URL in
  `Packages/manifest.json`. Opacity pruning uses its public active-range API, and
  `GsplatSettings` must stay in a `Resources` folder
  (`Assets/Settings/Resources/GsplatSettings.asset`). Extend it from project code;
  do not patch it.
- Keep `Assets/Scripts/Splats/Streaming`, `Assets/Editor/GsplatLodStreamerEditor.cs`,
  and `Assets/Scripts/UI/SplatDetailView.cs` with their dependencies.
- Reuse the authored HUD prefab, including its capsule material, TMP font,
  safe-area component, and the shared icon button
  (`Assets/Bundles/Resources/UI/Component/Button.prefab`) with its Level Icon
  material and sprites. Wire the view's streamer reference in each scene. UI objects
  and references are serialized; there is no runtime UI construction fallback.
- Keep the input gate and Back navigation integration when retaining the HUD.
  It prevents slider gestures from moving the camera or swapping captures.
- `Reverie > Install Splat Detail Controls` installs missing HUD controls and wires
  a scene containing one streamer and one HUD. It marks the scene dirty for saving.
- Run the focused opacity tests and live HUD tests after porting or upgrading,
  including the independent Budget/LOD active-count reduction and restoration
  checks. Check target Android hardware separately for performance and touch
  comfort; Editor checks do not establish device performance.

## Geometry and Baking

The template currently has no Gaussian-to-mesh bake tool. Island geometry and
colliders are independent of rendering opacity. A future bake integration must
record the chosen source-opacity threshold and apply it before reconstruction;
changing the live slider cannot update an already baked mesh. See
[Splat Library](Splat-switching.md) for import and capture-switching behavior.
