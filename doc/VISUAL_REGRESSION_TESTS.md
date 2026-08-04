# Visual regression tests

This document is the visual-rendering backlog. A checked item means that a
deterministic scene, a native-resolution PNG reference, and a test are all
checked in. API, serialization, and shader-compiler tests are still valuable,
but they do not replace a screenshot for behavior that reaches the rasterizer.

The graphics review covered the classes in `src/RedBolt/RedBolt/Graphics` and
the render-pipeline passes they feed. The plan deliberately groups related
properties into gallery captures where that gives better coverage without
creating hundreds of nearly identical tests.

## Current status

- [x] Visual tests run by default when a display exists.
- [x] `REDBOLT_TEST_GRAPHICS_API=opengl|vulkan` selects the graphics backend.
- [x] Hidden native-resolution OpenGL capture; no downsampling.
- [x] PNG references, actual captures, amplified difference images, and
  side-by-side `<reference>_shot.png` failure captures.
- [x] `../RedBolt.Tests/scripts/update_visual_references.sh` updates all matching shots.
- [x] `./RedBolt.Tests/scripts/with_software_renderer.sh` runs the suite through headless
  Xvfb/llvmpipe software rendering when no display is available.
- [x] `Mushroom_model_renders_with_zone_ambient_lighting`: one real model,
  no `Light` component, zone ambient lighting only.
- [x] Reusable scene/capture fixture with per-test dimensions, thresholds,
  render path, and reference name.
- [ ] CI job that runs the visual suite through a software renderer and
  publishes failed shots and diffs.
- [x] Repeat-capture determinism check: render the same scene twice and prove
  the two native PNGs are identical or within an explicitly documented bound.
- [x] Per-capture comparison policy: all readbacks are normalized to RGBA,
  alpha is compared, output color space is selected explicitly by the
  pipeline test, and edge-sensitive cases can set their own tolerance.

## Capture contract

Every visual test should:

- use a fixed scene, transform, camera, viewport rectangle, resource root,
  renderer settings, and frame time;
- avoid wall-clock time, random values, asynchronous particle state, and
  first-frame shader/resource compilation in the captured frame;
- render one or more warm-up frames, then capture the native target size as
  RGBA PNG without resizing;
- use a black or explicitly configured background and an explicit zone so
  missing state cannot come from the renderer's default zone;
- isolate one rendering behavior, or use a labeled/grid gallery when the
  behavior is a state matrix such as blend modes;
- run with OpenGL + llvmpipe first, then add a backend/settings matrix only
  where the output is expected to be portable;
- keep reference names grouped by feature, for example
  `References/materials/blend-modes.png`;
- update references only with the shot utility after reviewing the image.

The initial deterministic fixture should expose:

- fixed output size, currently `128x96`, with optional `256x192` detail runs;
- hidden window, VSync off, MSAA explicitly selected, sRGB explicitly selected;
- fixed `Renderer` texture quality/filter/anisotropy and skinning mode;
- explicit `Scene`, `Octree`, `Zone`, `Camera`, `Viewport`, and frame stepping;
- reference path, shot path, diff path, and optional per-case tolerance;
- a stable resource fixture using `RbfxTestData` and tracked CoreData/Data
  assets.

When one test produces several related images, it should pass independent
captures to `VisualRegressionFixture.AssertMatches`, for example:

```csharp
fixture.AssertMatches(
    new VisualRegressionImageCapture(first, "materials/state-first.png"),
    new VisualRegressionImageCapture(second, "materials/state-second.png"));
```

Each item is compared at its own native size and writes its own
`<reference>_shot.png` on failure. Viewport packing is reserved for a test that
is specifically exercising multiple viewports in one render target; it is not
used to combine unrelated cases.

## Graphics class coverage map

| Area | Classes reviewed | Visual responsibility |
| --- | --- | --- |
| Frame and views | `Graphics`, `Renderer`, `Camera`, `Viewport`, `RenderSurface` | Swap chain, clear, projection, view rectangles, stereo, off-screen targets, screenshots |
| Scene state | `Octree`, `Drawable`, `Zone`, `GlobalIllumination` | Visibility, masks, zones, ambient/fog/background, GI inputs |
| Lighting | `Light`, `LightProbeGroup`, `ReflectionProbe`, `CubemapRenderer`, `LightBaker` | Direct lighting, shadows, probes, cubemaps, baked lighting |
| Materials | `Material`, `Technique`, `Pass`, `Shader`, `ShaderVariation` | Shader selection, textures, parameters, blend/depth/cull/fill state |
| Meshes and buffers | `Model`, `ModelView`, `Geometry`, `VertexBuffer`, `IndexBuffer`, `CustomGeometry` | Vertex formats, indices, primitive topology, LOD, dynamic updates |
| Model components | `StaticModel`, `StaticModelGroup`, `AnimatedModel`, `Skybox` | Static, instanced, skinned, morphed, and sky geometry |
| Animation | `Animation`, `AnimationController`, `AnimationState`, `AnimationTrack`, `Skeleton`, `SoftwareModelAnimator` | Poses, blending, morphs, hardware/software skinning, animated attributes |
| Screen-facing geometry | `BillboardSet`, `ParticleEffect`, `ParticleEmitter`, `RibbonTrail`, `DecalSet` | Alpha geometry, sorting, particles, trails, projected decals |
| World geometry | `Terrain`, `TerrainPatch`, `Text3D`, `Font` | Heightfields, stitching/LOD, 3D text, fonts and layout |
| Diagnostics | `OutlineGroup`, `DebugRenderer`, `DebugGeometry`, `OcclusionBuffer` | Outlines, debug primitives, depth-tested overlays, occlusion behavior |
| Pipeline | `RenderPipeline`, `RenderPath`, `DefaultRenderPipeline`, built-in render passes | Forward/deferred paths, G-buffer, tone mapping, exposure, bloom, AO, outlines |
| Resource plumbing | `Texture2D`, `Texture2DArray`, `Texture3D`, `TextureCube`, `RenderSurface` | Texture sampling, array/volume/cubemap data, render-to-texture |
| Render API boundary | `RenderDevice`, `RenderContext`, `DrawCommandQueue`, `RawBuffer`, `RawTexture`, `PipelineState` | Native target binding, clear, queue execution, shader parameters, buffer/texture upload, and readback |

`GraphicsUtils`, shader compiler/translator/optimizer classes, buffer packing,
`ModelView` serialization, octree query math, and animation data containers
should remain primarily unit/diagnostic coverage unless a missing screenshot
reveals a consumer-side regression.

The Render API boundary follows the same rule: a screenshot is required for
an operation that can change raster output, while descriptor equality,
reflection, resource lifetime, pool reuse, and error handling remain unit
tests. The direct boundary captures are intentionally small and use one
native `Texture2D` target so a failure can be attributed to the API call or
the first pipeline consumer rather than to a large scene.

## RenderAPI and RenderPipeline pixel boundary

The following matrix is the executable plan for the two requested layers.
Every checked row has a `[VisualFact]` method and a tracked PNG reference;
the companion unit tests cover behavior that cannot produce pixels.

| Layer | Pixel contract | Visual test and reference(s) | Companion non-visual coverage |
| --- | --- | --- | --- |
| RenderAPI `RenderContext` | Bind a native color target, set its viewport, clear it, and read it back at native resolution | `VisualRegressionRenderApiTests.Render_context_clear_writes_the_bound_native_texture` — `References/render-api/context-clear.png` | `RenderContextTests` target binding, viewport, MRT, and state tracking |
| RenderAPI `DrawCommandQueue` | Submit reflected custom uniforms, scissor state, geometry buffers, and indexed draws through `RenderContext.Execute` | `VisualRegressionRenderApiTests.Draw_command_queue_renders_parameterized_scissor_regions` — `References/render-api/draw-command-queue.png` | `DrawCommandQueueTests` command snapshots, constant-buffer reuse, and resource ranges |
| RenderAPI `RawTexture` + pipeline copy | Upload RGBA pixels to a raw-backed `Texture2D`, sample them through the fullscreen copy pipeline, and read the destination | `VisualRegressionRenderApiTests.Raw_texture_upload_and_render_pipeline_copy_preserve_pixels` — `References/render-api/raw-texture-copy.png` | `RawTextureTests`, `RawBufferTests`, and `RawShaderTests` |
| RenderPipeline render buffers | Copy/clear intermediate buffers and preserve output dimensions, slices, faces, and resolve state | `VisualRegressionRenderSurfaceTests` and `VisualRegressionRenderApiTests` — `References/surfaces/*` and `References/render-api/*` | `RenderBufferManagerTargetTests`, `RenderBufferManagerTests`, and `RenderPoolTests` |
| RenderPipeline path and post-processing | Select forward/deferred paths and execute fullscreen pass, color-space, HDR, exposure, AO, bloom, and outline changes | `VisualRegressionPipelineTests` — `References/pipeline/*` | `RenderPipelineTests`, `RenderPathTests`, and built-in pass tests |
| RenderPipeline shadow configuration | Render the active 16-bit, 24-bit, PCF, and variance-shadow branches | `VisualRegressionZoneLightingTests.Shadow_quality_formats_and_filters_render_stable_variants` — `References/lighting/shadow-quality-*.png` | `ShadowMapAllocatorTests`, `PipelineStateBuilderTests`, and `RenderPipelineSettingsTests` |
| RenderPipeline environment techniques | Submit the tracked `M_Skymesh` and `M_Skyplane` techniques with fixed resources | `VisualRegressionEnvironmentTests.Skymesh_and_skyplane_techniques_render_fixed_resources` — `References/environment/skymesh.png` and `skyplane.png` | shader/technique resource loading tests |

The `ShadowQuality` enum also names `BlurVsm`, but no active RenderPipeline
setting selects a separate blur pass in this tree; it is therefore not
pretended to be screenshot coverage. Likewise, `RenderPipelineDebugger`
produces structured pass/batch data rather than user-visible raster output;
its snapshot state machine is covered by `RenderPipelineDebuggerTests`.

## Test backlog

### 1. Harness and frame lifecycle

- [x] `Visual_native_capture_dimensions`: assert the captured PNG dimensions
  equal the viewport and reference dimensions exactly.
- [x] `Visual_clear_color_and_depth_are_reset_each_frame`: render two scenes
  back-to-back and prove color and depth do not leak between viewports.
- [x] `Visual_warmup_frame_is_not_captured`: compare a warmed-up capture with
  a second repeat capture after resource and shader creation.
- [x] `Visual_resize_recreates_view_and_render_buffers`: render before and
  after `Graphics.SetMode`/swap-chain resize.
- [x] `Visual_srgb_output_is_explicit`: compare the same material under the
  selected sRGB and linear output settings with separate references.
- [x] `Visual_msaa_resolve_is_stable`: cover MSAA 1 and the highest supported
  small sample count; check edges, alpha, and resolved render-surface output.
- [x] `Visual_opengl_llvmpipe_smoke`: make the rootless software-renderer
  command a repeatable documented/CI entry point.
- [ ] `Visual_backend_smoke_matrix`: the visual fixture can now select a
  requested backend, but the available Vulkan/lavapipe runtime currently
  crashes in native initialization in this environment; keep this row
  pending until a stable second-backend runtime is part of CI.

### 2. Camera, view, and viewport behavior

- [x] `Visual_perspective_projection_gallery`: fixed FOV, aspect ratio, near
  clip, far clip, and zoom changes using the same model.
- [x] `Visual_orthographic_projection_gallery`: orthographic mode and multiple
  ortho sizes, including alignment at the near and far planes.
- [x] `Visual_near_far_clipping`: geometry crossing the near plane, far plane,
  behind-camera plane, and frustum side planes.
- [x] `Visual_flip_vertical_and_projection_offset`: `FlipVertical`, zoom, and
  projection offset with an asymmetric test object.
- [x] `Visual_custom_clip_and_reflection_planes`: camera clip plane and
  reflection plane with geometry on both sides.
- [x] `Visual_fill_modes`: solid, wireframe, and point fill modes with a
  non-trivial indexed mesh.
- [x] `Visual_view_masks`: primary/shadow view masks hiding and revealing
  selected drawables.
- [x] `Visual_view_override_flags`: low material quality, disabled shadows,
  and disabled occlusion overrides.
- [x] `Visual_single_viewport_rect`: non-fullscreen viewport rectangles and
  clear behavior outside the rectangle.
- [x] `Visual_multiple_viewports`: two and four viewports with different
  scenes/cameras and no state leakage between them.
- [x] `Visual_cull_camera`: camera used for culling differs from the camera
  used for rendering.
- [x] `Visual_stereo_viewport`: left/right eye cameras and stereo viewport
  layout at a deterministic convergence distance.
- [x] `Visual_camera_operator_step`: one fixed input/action sequence changes
  the camera over several captured frames without using wall-clock input.

### 3. Static models and mesh submission

- [x] `Visual_mushroom_ambient`: `Mushroom.mdl` with `Mushroom.xml`, native
  capture, zone ambient lighting, and no direct lights.
- [x] `Visual_box_unlit_baseline`: procedural/CoreData box with the unlit
  `FirstCube` material; this is the smallest geometry-to-pixel smoke test.
- [x] `Visual_primitive_model_gallery`: `Box`, `Plane`, `Sphere`, `Cone`,
  `Cylinder`, `Dome`, `Pyramid`, `TeaPot`, and `Torus` with explicit camera
  framing and identical material state.
- [x] `Visual_model_geometry_lod`: force or frame separate LOD levels and
  verify the selected geometry changes at the intended distance.
- [x] `Visual_model_lod_distance_and_bias`: draw distance, LOD bias, and
  occlusion LOD behavior using a repeated model row.
- [x] `Visual_model_multiple_geometries_and_materials`: one model with
  multiple submeshes and per-geometry material assignment.
- [x] `Visual_static_model_masks`: view, light, shadow, zone, occluder, and
  occludee flags in a scene where each flag changes visible pixels.
- [x] `Visual_static_model_group_instancing`: repeated boxes/mushrooms through
  `StaticModelGroup`, including instance transforms and material reuse.
- [x] `Visual_mesh_winding_and_culling`: front/back/counter-clockwise culling,
  mirrored transforms, and the projected winding diagnostic fixture.
- [x] `Visual_custom_geometry_topologies`: `CustomGeometry` triangle, line,
  and point primitives with several geometry batches.
- [x] `Visual_custom_geometry_vertex_attributes`: position, normal, color,
  UV, tangent, and multiple vertex-buffer layouts.
- [x] `Visual_dynamic_geometry_update`: update vertex and index data between
  two fixed frames and capture both results.
- [x] `Visual_16_and_32_bit_indices`: identical geometry submitted with both
  index widths.
- [x] `Visual_draw_range_and_lod_range`: index/vertex draw ranges and geometry
  LOD distances.

### 4. Materials, techniques, shaders, and texture sampling

- [x] `Visual_material_lighting_modes`: unlit, per-vertex, and per-pixel
  materials on the same sphere and box.
- [x] `Visual_material_quality_levels`: Low, Medium, and High technique
  selection with a material that has quality-specific entries.
- [x] `Visual_material_technique_lod`: technique entries selected by distance.
- [x] `Visual_material_shader_parameters`: `MatDiffColor`, specular,
  roughness, metallic, emissive, and custom shader parameters.
- [x] `Visual_material_parameter_animation`: deterministic parameter curve
  changes captured at fixed animation times.
- [x] `Visual_texture_slots`: albedo, normal, specular/properties, emission,
  AO, and lightmap slots in isolated material scenes.
- [x] `Visual_normal_and_tangent_mapping`: Stone/Mushroom normal maps with
  tangent generation and a directional light that makes the difference clear.
- [x] `Visual_pbr_metallic_roughness_gallery`: dielectric, metal, roughness,
  normal, AO, and emissive PBR materials on a sphere/teapot gallery.
- [x] `Visual_material_color_space`: sRGB versus linear texture/color handling.
- [x] `Visual_texture_filter_gallery`: nearest, bilinear, trilinear,
  anisotropic, and anisotropy levels on a tiled oblique surface.
- [x] `Visual_texture_mip_and_lod`: mip transitions and texture quality at
  controlled distances.
- [x] `Visual_texture_formats`: representative PNG, DDS compressed, float,
  and packed formats where the software backend supports them.
- [x] `Visual_texture_2d_array`: array-layer selection and animated layer
  sampling with `Texture2DArray`.
- [x] `Visual_texture_3d`: volume texture slices and trilinear interpolation.
- [x] `Visual_texture_cube`: all cubemap faces, seams, and orientation.
- [x] `Visual_material_cull_modes`: none, clockwise, and counter-clockwise
  culling on a two-sided test mesh.
- [x] `Visual_material_fill_modes`: material-level solid, wireframe, and point
  state where applicable.
- [x] `Visual_material_depth_modes`: Always, Equal, Less, LessEqual, Greater,
  GreaterEqual, and depth-write on/off with overlapping geometry.
- [x] `Visual_material_color_write_and_alpha_coverage`: color-write masks and
  alpha-to-coverage on a cutout mesh.
- [x] `Visual_blend_mode_gallery`: Replace, Add, Multiply, Alpha, AddAlpha,
  PremulAlpha, InvDestAlpha, Subtract, SubtractAlpha, and Decal.
- [x] `Visual_alpha_cutout_and_translucency`: alpha test, translucent sorting,
  transparent depth behavior, and two-sided transparent materials.
- [x] `Visual_soft_particle_materials`: soft-particle depth fade against a
  solid occluder.
- [x] `Visual_shader_define_variants`: normal-map, environment-cube,
  emissive, PBR, vegetation, and alpha shader permutations.
- [x] `Visual_shader_error_fallback`: invalid shader/material behavior is
  captured as a stable diagnostic image if the renderer has a visible fallback.

### 5. Zones, direct lighting, and shadows

- [x] `Visual_zone_ambient_only`: ambient-only baseline used by the current
  mushroom test.
- [x] `Visual_zone_ambient_brightness`: ambient color and brightness sweep.
- [x] `Visual_zone_ambient_gradient`: two-zone gradient and transition across
  a large model row.
- [x] `Visual_zone_priority_and_override`: overlapping zones, priority, and
  override selection.
- [x] `Visual_zone_fog`: linear fog start/end and fog color.
- [x] `Visual_zone_height_fog`: height, height scale, and camera/model
  movement through the height fog.
- [x] `Visual_zone_background_brightness`: background versus object lighting.
- [x] `Visual_directional_light`: direction, color, temperature, brightness,
  specular intensity, and negative-light behavior.
- [x] `Visual_point_light`: range, radius, attenuation, physical values, and
  specular response.
- [x] `Visual_spot_light`: FOV, aspect ratio, range, fade, ramp texture, and
  shape texture.
- [x] `Visual_multiple_lights_and_importance`: one/two/many lights plus
  Auto/Important/NotImportant light importance paths.
- [x] `Visual_light_and_drawable_masks`: light, shadow, and zone masks isolate
  selected objects.
- [x] `Visual_realtime_mixed_baked_lights`: Realtime, Mixed, and Baked light
  modes with explicit expected contribution.
- [x] `Visual_shadow_quality_gallery`: active simple 16/24-bit, PCF, and VSM
  branches; the unimplemented `BlurVsm` enum value is tracked as a unit-only
  limitation below.
- [x] `Visual_directional_cascaded_shadows`: cascade splits, fade, focus, and
  stable shadow transitions.
- [x] `Visual_point_and_spot_shadows`: point/spot shadow projection and atlas
  faces.
- [x] `Visual_shadow_bias_gallery`: constant, slope, normal offset, near/far
  ratio, and extrusion without acne or peter-panning regressions.
- [x] `Visual_shadow_resolution_and_distance`: resolution, shadow fade, and
  shadow distance changes.
- [x] `Visual_forward_and_deferred_lighting_equivalence`: same controlled
  scene through the available forward/deferred paths.

### 6. Environment lighting, sky, probes, and baked data

- [x] `Visual_skybox_cubemap`: `Skybox` with all six faces, rotation, and
  material/texture orientation.
- [x] `Visual_skybox_hdr_scale`: HDR scale and background brightness.
- [x] `Visual_skymesh_and_skyplane`: tracked `M_Skymesh` and `M_Skyplane`
  resources rendered through their real material techniques.
- [x] `Visual_zone_cubemap_ambient`: zone texture contributing environment
  lighting and background.
- [x] `Visual_reflection_probe_baked`: baked probe selection and priority.
- [x] `Visual_reflection_probe_mixed`: mixed probe and zone blending.
- [x] `Visual_reflection_probe_dynamic`: dynamic probe update against a fixed
  scene, including first update and stable subsequent update.
- [x] `Visual_reflection_probe_sliced_update`: sliced cubemap update over
  multiple deterministic frames.
- [x] `Visual_reflection_probe_box_projection`: projection box versus world
  reflection direction.
- [x] `Visual_reflection_probe_masks_and_clips`: view mask, near clip, far
  clip, and texture-size changes.
- [x] `Visual_light_probe_group`: explicit probe placement, auto placement,
  zone/light masks, and spherical-harmonic interpolation.
- [x] `Visual_global_illumination_emission`: GI file input, offset, and
  emission brightness.
- [x] `Visual_lightmap_direct_indirect`: baked direct, indirect, emissive,
  lightmap UVs, and lightmap material variants.
- [x] `Visual_baked_scene_gallery`: the tracked BakedLighting/RenderingShowcase
  scenes rendered through a deterministic reduced viewport.
- [x] `Visual_cubemap_renderer_faces`: deterministic six-face cubemap capture,
  near/far clips, view masks, render budget, and update stages.

### 7. Animated, skinned, and morphed models

- [x] `Visual_animated_model_pose`: fixed-pose `Jack`, `Kachujin`, or `Mutant`
  model with its material and skeleton.
- [x] `Visual_animation_fixed_times`: the same animation at time 0, midpoint,
  and end; no wall-clock timing.
- [x] `Visual_animation_loop_and_speed`: loop, speed, and wrap behavior.
- [x] `Visual_animation_blending`: two animations blended at fixed weights.
- [x] `Visual_animation_layers`: multiple controller layers and masks.
- [x] `Visual_animation_additive_and_reverse`: additive, reverse, and negative
  speed playback where supported.
- [x] `Visual_animation_controller_parameters`: named time/weight/speed
  parameters and skeleton reset.
- [x] `Visual_animated_model_morphs`: named and indexed morph weights at 0,
  0.5, and 1.0.
- [x] `Visual_animated_model_bone_attachment`: attached geometry follows a
  deterministic bone pose.
- [x] `Visual_hardware_skinning`: hardware skinning at a fixed pose.
- [x] `Visual_software_skinning`: software skinning at the same pose and
  software bone-count limit.
- [x] `Visual_auto_skinning_selection`: Auto mode selects the expected path.
- [ ] `Visual_skinning_lod_and_invisible_update`: animation LOD bias and
  update-invisible behavior.
- [x] `Visual_animation_triggers_and_attributes`: trigger points and animated
  node/material attributes with fixed event times.

### 8. Billboards, particles, trails, decals, and overlays

- [x] `Visual_billboard_face_modes`: None, RotateXyz, RotateY, LookAtXyz,
  LookAtY, LookAtMixed, Direction, and AxisAngle.
- [x] `Visual_billboard_sorting`: sorted/unsorted alpha billboards at crossing
  depths.
- [x] `Visual_billboard_relative_scaled`: relative, scaled, fixed-screen-size,
  minimum-angle, and animation LOD behavior.
- [x] `Visual_billboard_per_object_data`: individual position, size, UV,
  rotation, color, and enabled state.
- [x] `Visual_particle_emitter_shapes`: Sphere, Box, SphereVolume, Cylinder,
  and Ring with fixed serialized particles.
- [x] `Visual_particle_fixed_timestep`: fixed emission/velocity/TTL/rotation
  frames at identical time steps.
- [x] `Visual_particle_color_and_texture_frames`: color ramps, UV frame
  animation, texture animation, and looping.
- [x] `Visual_particle_modes`: relative, scaled, sorted, fixed-screen-size,
  face-camera modes, and update-invisible.
- [x] `Visual_particle_forces`: direction range, constant force, damping,
  size add/multiply, active/inactive time, and emission rate.
- [x] `Visual_particle_materials`: unlit, lit, additive, alpha, soft-particle,
  smoke, fire, and snow materials.
- [x] `Visual_ribbon_face_camera`: face-camera ribbon path with width, scale,
  color gradient, lifetime, and vertex distance.
- [x] `Visual_ribbon_bone_trail`: bone trail type with a fixed animated pose.
- [x] `Visual_ribbon_sorting_and_tail_columns`: sorted trails and tail-column
  texture animation.
- [x] `Visual_decal_static_projection`: opaque/alpha decal on box, sphere,
  and sloped geometry.
- [x] `Visual_decal_skinned_projection`: decal vertices with blend weights and
  indices on an animated model.
- [x] `Visual_decal_lifetime_and_capacity`: expiry, max vertex/index capacity,
  and optimized buffer resizing.
- [x] `Visual_outline_group`: outline color, render order, multiple drawables,
  and debug mode.
- [x] `Visual_debug_geometry`: depth-tested and non-depth-tested lines,
  triangles, bounding boxes, normals, and octree/debug overlays.

### 9. Terrain and large-world visibility

- [x] `Visual_terrain_heightmap`: tracked heightmap with explicit spacing and
  material.
- [x] `Visual_terrain_smoothing`: smoothed versus unsmoothed height samples.
- [x] `Visual_terrain_patch_sizes`: patch sizes and patch-boundary continuity.
- [x] `Visual_terrain_lod_levels`: camera-distance LOD transitions and bias.
- [x] `Visual_terrain_neighbor_stitching`: north/south/east/west neighbors and
  every edge stitch combination.
- [x] `Visual_terrain_material_blend`: legacy and PBR terrain blend materials.
- [x] `Visual_terrain_lighting_and_shadows`: terrain light/shadow masks,
  cast-shadow, draw distance, and shadow distance.
- [x] `Visual_terrain_occlusion_and_debug`: occluder/occludee and terrain
  debug geometry.
- [x] `Visual_octree_visibility`: objects entering/leaving the camera frustum,
  octant boundaries, and occlusion-buffer behavior.
- [x] `Visual_octree_query_debug`: point, sphere, box, frustum, ray, and all
  content queries shown with deterministic debug geometry.

### 10. Text and font rendering

- [x] `Visual_text3d_bitmap_font`: fixed text with a tracked font and baseline
  material.
- [x] `Visual_text3d_sdf_font`: SDF font rendering at several sizes.
- [x] `Visual_text3d_alignment`: horizontal/vertical/text alignment and all
  custom alignment modes.
- [x] `Visual_text3d_multiline_wrap`: multiline text, width, word wrap, row
  spacing, and line metrics.
- [x] `Visual_text3d_corner_colors`: four-corner color interpolation and
  opacity.
- [x] `Visual_text3d_depth_modes`: depth-tested and overlay text against a
  model.
- [x] `Visual_text3d_unicode_shaping`: Unicode, HarfBuzz shaping, and
  combining-mark cases supported by the tracked font.
- [ ] `Visual_text3d_fallback_and_rtl`: fallback fonts and right-to-left text
  once a tracked font/resource supports those glyphs.

### 11. Render surfaces, render paths, and post-processing

- [x] `Visual_render_to_texture`: render a scene to a `Texture2D` and read it
  back at native resolution as a PNG.
- [x] `Visual_render_surface_update_modes`: ManualUpdate, UpdateVisible, and
  UpdateAlways.
- [x] `Visual_render_surface_linked_targets`: linked color/depth targets and
  resolve behavior.
- [x] `Visual_render_surface_array_slice`: render to a `Texture2DArray` slice.
- [x] `Visual_render_surface_cubemap_face`: render to each cubemap face.
- [x] `Visual_default_render_path`: baseline forward/deferred pass ordering,
  opaque, alpha, transparent, depth, and shadow passes.
- [x] `Visual_custom_render_path_toggle`: enable/disable render passes and
  verify the expected image change.
- [x] `Visual_fullscreen_shader_pass`: full-screen shader, defines, parameters,
  blend mode, read/write color, and bilinear sampling.
- [x] `Visual_tone_mapping`: exposure, tone-mapping mode, and HDR highlights.
- [x] `Visual_auto_exposure`: deterministic multi-frame exposure adaptation.
- [x] `Visual_bloom`: threshold, strength, radius, dirt/blur behavior, and
  bright emissive source.
- [x] `Visual_ambient_occlusion`: AO strength, radius, fade, and normal/depth
  inputs on contact geometry.
- [x] `Visual_outline_render_pass`: pipeline outline pass with the outline
  group and final compositing.
- [x] `Visual_render_path_parameters`: runtime parameter update and resource
  reload produce the expected frame.
- [ ] `Visual_render_pipeline_debug_snapshot`: the current debugger is a
  structured diagnostic snapshot, not a user-visible raster output; its
  state machine and pass ordering remain covered by unit tests.

### 12. Renderer settings and portability matrix

- [x] `Visual_texture_quality_matrix`: Low/Medium/High texture quality.
- [x] `Visual_texture_filter_matrix`: all supported filter modes and
  anisotropy values.
- [x] `Visual_skinning_mode_matrix`: Auto/Hardware/Software on the same pose.
- [x] `Visual_shadow_quality_matrix`: all active renderer-selected shadow
  formats/filters; `BlurVsm` has no active selecting setting.
- [x] `Visual_material_quality_matrix`: material quality and fallback paths.
- [x] `Visual_msaa_matrix`: no MSAA and supported MSAA levels.
- [x] `Visual_opengl_software_matrix`: llvmpipe with the stable reference set.
- [ ] `Visual_second_backend_matrix`: a second backend only after its color,
  depth, and clip-space conventions are explicitly normalized.
- [x] `Visual_reference_alpha_policy`: readbacks are always normalized to
  RGBA, 1-3 component inputs become opaque, and all four channels participate
  in comparison; transparent captures retain and compare their alpha.

The remaining unchecked items are intentionally deferred rather than silently
treated as covered. CI artifact publishing belongs to the repository's
external CI configuration; the current rootless runner is the executable
entry point. `BlurVsm` is an enum value without an active selecting render
pass. Fallback/RTL text needs a tracked font with the required glyphs. A
second backend needs a stable native software runtime and normalized
clip/color behavior; the current Vulkan/lavapipe probe aborts the native test
host during initialization. Animation-LOD/invisible-update coverage needs a
fixed non-zero frame-time hook, while the debugger produces structured data
and is already covered by RenderPipeline unit tests rather than PNGs.

## Suggested implementation order

The RenderAPI/RenderPipeline pixel-boundary matrix above is implemented and
reference-reviewed. The broader graphics backlog below remains the order for
future feature additions outside that boundary.

1. Build the reusable fixture and finish the camera/viewport tests.
2. Add the box baseline, primitive gallery, static-model LOD, and material
   state galleries.
3. Add one focused image per light type, then shadows and zones.
4. Add PBR/environment/probe scenes before animation, because they exercise
   the largest render-pipeline surface.
5. Add animated/skinned/morphed models with fixed frame times and both skinning
   paths.
6. Add billboards, particles, trails, decals, terrain, and text.
7. Add render-to-texture and post-processing passes.
8. Finish with the portability/settings matrix and CI artifact publishing.

The backlog should be checked off only after the test passes repeatedly in the
software-rendered session and the reference has been reviewed visually. A
feature that is not deterministic should first receive a fixed-seed/fixed-time
test harness rather than a relaxed comparison threshold.

## Running and updating references

From the workspace root, run the visual tests against the checked-in
references. They run by default when a display exists. For headless software
rendering, wrap the same command with `./RedBolt.Tests/scripts/with_software_renderer.sh`; failure
shots are beside the files that the updater will replace:

```bash
dotnet test ./RedBolt.Tests/RedBolt.Tests.csproj \
  --filter FullyQualifiedName~VisualRegression \
  --logger "console;verbosity=minimal"
```

```bash
./RedBolt.Tests/scripts/with_software_renderer.sh dotnet test ./RedBolt.Tests/RedBolt.Tests.csproj \
  --filter FullyQualifiedName~VisualRegression \
  --logger "console;verbosity=minimal"
```

Select the graphics backend with `REDBOLT_TEST_GRAPHICS_API=opengl|vulkan`.
For example:

```bash
REDBOLT_TEST_GRAPHICS_API=vulkan \
  ./RedBolt.Tests/scripts/with_software_renderer.sh dotnet test ./RedBolt.Tests/RedBolt.Tests.csproj \
  --filter FullyQualifiedName~VisualRegression \
  --logger "console;verbosity=minimal"
```

On mismatch, the capture is saved beside its reference as
`<reference-name>_shot.png`. The actual capture and an amplified difference PNG
are additionally written to `/tmp/RedBoltVisualRegression` by default. Set
`REDBOLT_VISUAL_ARTIFACTS` to use another directory.

To update all references from reviewed shots, run:

```bash
bash ../RedBolt.Tests/scripts/update_visual_references.sh
```

The script searches the checked-in reference directory recursively, copies
every `*_shot.png` over its matching reference, and leaves the shot files in
place for review. Pass another reference directory as the first argument when
using a different `REDBOLT_VISUAL_REFERENCE_ROOT`.
