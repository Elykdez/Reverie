# Rock surface shading

`Assets/Environment/RockIsland/Island Rock.mat` uses `Reverie/Rock Relief`, based on the installed URP 17.3 Lit shader. It retains the existing Poly Haven rock textures and material tint.

- Height mapping traces 12-24 layers with three refinement steps, making crevices shift and occlude as the camera moves. Height Scale controls depth (default 0.065); zero disables the offset.
- Normal Map Scale is 2.0. A second normal layer at 4x tiling and 0.55 strength adds fine surface roughness; its albedo contribution is disabled.
- Height-derived cavity shading supplements the scanned ambient occlusion. URP still supplies lighting, roughness, reflections and mesh shadows.
- Relief fades at grazing angles and small on-screen sizes to reduce streaking and shimmer. Forward and depth-normal passes use the same height sampling, with the standard URP material buffer layout.

The shader adds pixel-level depth. `Tools/GenerateRockIsland.py` supplies the actual silhouette: 256 perimeter segments, 96 body bands, eight rounded rim bands and 12 concentric top bands (59,904 triangles). The top and rim share vertices and continuous UVs, with texture distance measured around the bevel to avoid a stretched band. Slight rim height variation breaks up the straight grass-to-cliff boundary. Smoothed height displacement and reduced small crags avoid vertex spikes while the shader retains fine surface detail. The mesh is checked for degenerate triangles and watertight topology before writing. The approved 1.2x size is baked into its vertices, so import scale is 1.0.

GPU cost depends on how much of the screen the island covers; Android device performance should be checked on target hardware.

The ShaderLab pass setup is adapted from Unity's URP Lit shader under the Unity Companion License. `RockReliefInput.hlsl` contains the custom relief sampling.
