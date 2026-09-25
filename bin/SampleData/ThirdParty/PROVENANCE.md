# Third-party sample assets

Only the files listed below are included. The source archives are not included.

Missing core sample resources were restored on 2026-09-24 from the published RedBolt packages in `RedBoltFramework.github.io/samples/resources/`: `RedBolt.CoreData.pak` (SHA-256 `51860989b5582e8c5ad84922e3cae8592b3c7f282cf25ef7a07c7e19b243c110`) and `RedBolt.SampleData.pak` (SHA-256 `74a90db9a81aa5d3a828b99a0cb7bfedcfe567389f89609e9d3a12640c185b1f`). Extracted entries retain their package checksums.

## Kenney Nature Kit

- Source: https://kenney.nl/assets/nature-kit
- Downloaded: 2026-08-26
- Source archive SHA-256: `fa7974a0d342bfe63c38664ba9f8ec1a4aab8ea25f099bdc56870e33588c4d9d`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `grass.glb`, `log.glb`, `rock_smallA.glb`, and `tree_fat.glb` from `Models/GLTF format/`.

## Quaternius Cars Pack

- Source: https://quaternius.com/packs/cars.html
- Downloaded: 2026-08-29
- Selected source file: `Cop.blend` (`bba5fe21ac9a7bc49beac1e0a78bd38abcc03b45aeb5f98f85edbde9734b3290`).
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Included derivative: `PoliceCar.glb` contains the police-car body and one centered wheel mesh. The raycast vehicle places four copies of the wheel at runtime so steering and suspension remain fully animated.

## Quaternius Universal Animation Library

- Source: https://quaternius.com/packs/universalanimationlibrary.html
- Downloaded: 2026-08-26
- Standard source archive SHA-256: `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source file: `UAL1_Standard_RM.glb`.
- Included derivative: `TechnicalDummy.glb` contains the mannequin and a curated technical-demo set: `Idle_Loop`, `Jog_Fwd_Loop`, `Jump_Loop`, `Crouch_Fwd_Loop`, `Crouch_Idle_Loop`, `Interact`, and `Walk_Loop`. Locomotion root tracks are reset in place while the original root-motion tracks are retained for runtime sampling.

## Quaternius Stylized Nature MegaKit

- Source: https://quaternius.com/packs/stylizednaturemegakit.html
- Downloaded: 2026-08-26
- Standard source archive SHA-256: `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected Standard-tier models: `CommonTree_1`, `CommonTree_3`, `TwistedTree_1`, `Pine_2`, `Bush_Common_Flowers`, `Fern_1`, `Flower_3_Group`, `Flower_4_Group`, `Rock_Medium_1` through `Rock_Medium_3`, and `RockPath_Round_Small_1` through `RockPath_Round_Small_3`.
- Included derivative: `StylizedNatureShowcase.glb`. Blender combines only the selected glTF models, deduplicates their shared materials, limits embedded textures to 512 pixels, adds a meadow ground, and adds decimated `CommonTree_1` LODs at distances 45 and 90.

## Quaternius Ultimate Spaceships Pack

- Source: https://quaternius.com/packs/ultimatespaceships.html
- Downloaded: 2026-08-27
- Selected glTF SHA-256 values: `73f187cd1a11e487a1b20405bfa59d3d9f98ef4960ee64e3f062a7373aa095ad` (Bob), `c600b39fd587c323557c682e7aae2e976b62fff2984929163b7ee12a0e4323fd` (Challenger), and `dd77f3f08d17227083b714d80cbc0d0470a6b44480003cca9051a53183571160` (Omen).
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Included derivative: `UltimateSpaceshipsShowcase.glb`. Blender combines the Bob, Challenger, and Omen glTF models, normalizes their scale, and limits each embedded texture to 512 by 512 pixels.

## Kenney Input Prompts

- Source: https://kenney.nl/assets/input-prompts
- Downloaded: 2026-08-26
- Source archive SHA-256: `ac2fcf599080b0f3ba2d174c9474db6df1a0e96ff0662580e2da79a122ab78a1`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `keyboard_w.png`, `keyboard_a.png`, `keyboard_s.png`, `keyboard_d.png`, `keyboard_space.png`, `keyboard_escape.png`, `keyboard_f.png`, `keyboard_g.png`, `keyboard_shift.png`, `keyboard_m.png`, `keyboard_tab.png`, `keyboard_arrow_up.png`, `keyboard_arrow_down.png`, `keyboard_arrows.png`, `keyboard_enter.png`, `mouse_left.png`, `mouse_right.png`, and `mouse_move.png` from `Keyboard & Mouse/Default/`; `generic_button.png` and `generic_joystick.png` from `Generic/Default/`; `touch_swipe_move.png`, `touch_swipe_two_move.png`, and `touch_tap.png` from `Touch/Default/`; and `xbox_button_color_a.png`, `xbox_button_color_b.png`, `xbox_button_color_x.png`, `xbox_button_color_y.png`, `xbox_button_back.png`, `xbox_button_start.png`, `xbox_lb.png`, `xbox_rb.png`, `xbox_stick_l.png`, and `xbox_stick_r.png` from `Xbox Series/Default/`. The pack's `License.txt` is included as `KenneyInputPrompts.LICENSE.txt`.

## Kenney Particle Pack

- Source: https://kenney.nl/assets/particle-pack
- Downloaded: 2026-08-26
- Source archive SHA-256: `b631d4b07f7002549fdcf155f01141ad482f79f3440e4e301eed49ce5f1d8958`
- License: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/)
- Selected source files: `flame_02.png`, `smoke_01.png`, and `star_01.png` from `PNG (Transparent)/`.

The shared `model.assetpipeline` imports each included GLB through `ModelImporter`. Generated `.glb.d` resources are included so desktop and Web samples can load the imported models without an editor cache.
