# Third-party sample assets

Only the files listed below are included. The source archives are not included.

## Kenney Nature Kit

- Source: https://kenney.nl/assets/nature-kit
- Downloaded: 2026-08-26
- Source archive SHA-256: `fa7974a0d342bfe63c38664ba9f8ec1a4aab8ea25f099bdc56870e33588c4d9d`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `fence_simple.glb`, `flower_redA.glb`, `grass.glb`, `log.glb`, `path_stone.glb`, `rock_largeA.glb`, `rock_smallA.glb`, `tree_cone.glb`, `tree_default.glb`, and `tree_fat.glb` from `Models/GLTF format/`.

## Kenney Toy Car Kit

- Source: https://kenney.nl/assets/toy-car-kit
- Downloaded: 2026-08-26
- Source archive SHA-256: `d28ed13baf60b3cb4932e2984aeac9d8cdc3449936597e5da962c61ba2dae469`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `gate-finish.glb`, `smoke.glb`, `track-striped-wide-corner-large.glb`, `track-striped-wide-straight.glb`, and `vehicle-racer.glb` from `Models/GLB format/`, plus `Models/GLB format/Textures/colormap.png`.

## Quaternius Universal Base Characters and Universal Animation Library

- Sources: https://quaternius.com/packs/universalbasecharacters.html and https://quaternius.com/packs/universalanimationlibrary.html
- Downloaded: 2026-08-26
- Standard source archive SHA-256 values: `fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40` and `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `Superhero_Male_FullBody.gltf` and its referenced files, plus `UAL1_Standard.glb`.
- Included derivative: `UniversalCharacter.glb`. Blender combined the compatible rig with `Idle_Loop`, `Jog_Fwd_Loop`, `Jump_Loop`, and `Interact`. Embedded textures were reduced to 512 by 512 pixels.

## Kenney UI Pack

- Source: https://kenney.nl/assets/ui-pack
- Downloaded: 2026-08-26
- Source archive SHA-256: `a8a14a234911eb648c062622915c93e79e94e97cb7f9f375a70f6617f1174318`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: six button, check, and slider images from `PNG/Blue/Default/`.

## Kenney Input Prompts

- Source: https://kenney.nl/assets/input-prompts
- Downloaded: 2026-08-26
- Source archive SHA-256: `ac2fcf599080b0f3ba2d174c9474db6df1a0e96ff0662580e2da79a122ab78a1`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `keyboard_w.png`, `keyboard_a.png`, `keyboard_s.png`, `keyboard_d.png`, `keyboard_space.png`, `keyboard_escape.png`, `mouse_left.png`, and `mouse_right.png` from `Keyboard & Mouse/Default/`.

## Kenney Particle Pack

- Source: https://kenney.nl/assets/particle-pack
- Downloaded: 2026-08-26
- Source archive SHA-256: `b631d4b07f7002549fdcf155f01141ad482f79f3440e4e301eed49ce5f1d8958`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `fire_01.png`, `flame_02.png`, `magic_01.png`, `muzzle_01.png`, `smoke_01.png`, `spark_01.png`, `star_01.png`, and `trace_01.png` from `PNG (Transparent)/`.

The shared `model.assetpipeline` imports each included GLB through `ModelImporter`. Generated `.glb.d` resources are included so desktop and Web samples can load the imported models without an editor cache.
