# Feature Parity Roadmap

## rbfx Feature Surface

rbfx describes itself as a moderately lightweight, modular 3D game engine/framework for Windows, Linux, macOS, Android, iOS, Web, and UWP/Xbox, with optional C# bindings. RedBolt should target the same feature set over time, but the order should be driven by vertical slices.

Public RedBolt APIs should be near-1:1 with rbfx where possible and where it makes sense. The expected exceptions are subsystems where .NET has better native solutions or where the rbfx internals would create unnecessary friction in C#, especially logging and serialization. Graphics should be ported especially closely because it is central to runtime behavior and sample/resource compatibility.

## Platform And Graphics Parity

| rbfx platform/API | RedBolt priority | Notes |
|---|---|---|
| Windows + D3D11 | High after Linux/desktop bootstrap | Important mainstream target. |
| Windows + D3D12 | Medium | Defer until D3D11 path is stable. |
| Linux + Vulkan | High | Likely primary development target. |
| Linux + OpenGL | Medium | Useful fallback. |
| macOS + Metal/Diligent | Medium | Requires macOS build lane. |
| macOS/iOS + Vulkan via MoltenVK | Low initially | Defer until mobile/apple work starts. |
| Android + Vulkan/GLES | Low initially | Requires mobile lifecycle and packaging. |
| iOS + GLES/Metal/MoltenVK | Low initially | Requires mobile lifecycle and app packaging. |
| Web + WebGL2 | Low initially | Requires Emscripten and .NET/WebAssembly strategy. |
| UWP/Xbox | Low initially | Requires Windows-specific packaging and constraints. |

## Engine Subsystem Parity

| rbfx subsystem | RedBolt status target | Packaging | First-cube need |
|---|---|---|---|
| Core | Phase 1 | Main `RedBolt` library | Required. |
| Engine | Phase 1 | Main `RedBolt` library | Required. |
| Graphics | Phase 1 | Main `RedBolt` library | Required. Graphics is not optional. |
| Input | Phase 1 | Main `RedBolt` library | Required. |
| Resource | Phase 1 | Main `RedBolt` library | Required in minimal form. |
| Scene | Phase 1 | Main `RedBolt` library | Required. |
| RenderPipeline | Phase 1 minimal, Phase 2 full | Main `RedBolt` library | First cube can use a simple forward path. |
| Shader | Phase 1 minimal, Phase 2 full | Main `RedBolt` library | First cube can use embedded/minimal shaders. |
| UI | Deferred | Optional module | Not required. |
| SystemUI/ImGui | Deferred | Optional module or editor tooling | Editor/tools later. |
| RmlUI | Deferred | `RedBolt.RmlUi` optional module | Application UI later. |
| Audio | Deferred | Main library or optional module, decide when implemented | Independent after first cube. |
| Physics | Deferred | Optional module if kept optional | Bullet after graphics/scene. |
| Physics2D | Deferred | Optional module if kept optional | Box2D after physics module pattern exists. |
| Urho2D | Deferred | Main library or optional module, decide when implemented | Needs 2D renderer and resources. |
| Navigation | Deferred | Optional module if kept optional | Recast/Detour after scene/physics. |
| Network/Replica | Deferred | Optional module if kept optional | Requires C# serialization policy, threading, transport. |
| Particles | Deferred | Main library or optional module, decide when implemented | Needs graphics resource breadth. |
| Actions | Deferred | Main `RedBolt` library if pure managed | Pure managed tweening can land later. |
| Glow/lightmapping | Deferred | Optional tools/rendering module | Requires advanced rendering and tooling. |
| IK | Deferred | Optional module if kept optional | Animation stack first. |
| XR | Deferred | Optional module | Platform-specific graphics lifecycle. |
| Plugins | Deferred | Optional module/system | Requires ABI and assembly loading policy. |
| FileWatcher | Deferred | Main library or optional tooling, decide when implemented | Resource hot reload after stable loaders. |
| Threading/WorkQueue | Deferred minimal | Main `RedBolt` library | First cube can be single-threaded. |
| Profiling | Deferred | Optional module/tooling | Use simple logs first. |
| Packaging | Deferred | Tools module | Needed after resource formats stabilize. |

## Component And Tool Parity

| rbfx component | RedBolt priority | Notes |
|---|---|---|
| Player | Medium | Needed after sample/application model stabilizes. |
| Editor | Low initially | Requires UI, C#-native serialization, asset DB, scene inspector. |
| Samples | Continuous | Each subsystem should land with a sample. |
| Tests | High | Managed unit tests from the beginning. |
| Tools | Medium | Resource packaging/import tools after resource model. |
| Extras | Low | Defer until core parity is credible. |

## Dependency Parity Map

| Dependency | rbfx use | RedBolt plan |
|---|---|---|
| SDL | Window/input/platform | Required first. Generate bindings from SDL headers with SharpCpp. |
| Diligent | Graphics backend abstraction | Required first. Generate bindings from Diligent headers with SharpCpp. |
| EASTL | C++ containers | Native-only unless dependency APIs require it. Prefer C# collections in RedBolt. |
| LZ4 | Compression | Resource/package phase. |
| PugiXml | XML resources | Resource format phase. |
| rapidjson | JSON resources/config | Resource format phase. |
| STB | Images | Texture loading phase. |
| FreeType | Fonts/text | UI/text phase. |
| fmt/spdlog | Native logs | Optional native diagnostics. C# logging is primary. |
| glslang/SPIRV-Cross/SPIRV-Tools | Shader translation/optimization | Shader pipeline phase. |
| Bullet/StanHull | 3D physics | Physics phase. |
| Box2D | 2D physics | 2D phase. |
| Recast/Detour | Navigation | Navigation phase. |
| RmlUi | HTML-like UI | UI phase. |
| ImGui/ImGuizmo | Tools/editor UI | Editor/tooling phase. |
| libdatachannel/OpenSSL/Civetweb | Networking/WebRTC/http | Network phase. |
| tinygltf | glTF import | Asset import phase. |
| xatlas/embree | Lightmapping/GI tooling | Advanced rendering/tools phase. |
| Tracy/capstone/zstd/ppqsort | Profiling tooling | Profiling phase. |
| OpenXRSDK | XR | XR phase. |

## Resource Compatibility Roadmap

| Resource type | First support | Later support |
|---|---|---|
| Mesh/model | Procedural cube resource | rbfx `.mdl`, glTF import. |
| Material | Minimal unlit/lit material object | rbfx XML material files and techniques. |
| Shader | Embedded backend-specific shader source | Universal GLSL translation and SPIR-V pipeline. |
| Texture | Solid color or generated texture | STB image loading, compressed texture formats, WebP. |
| Scene | Runtime-created scene | XML/JSON/prefab scene loading. |
| Font/UI | None | FreeType, UI skins, RmlUI documents. |
| Package | Loose files only | `.pak` packaging and mount order. |

## Suggested Parity Milestones After First Cube

| Milestone | Result |
|---|---|
| Asset cube | Same sample loads `Models/Box.mdl`, material, and shader resources from rbfx-style data paths. |
| Textured cube | Texture loading and material binding work. |
| Sample camera | Free-fly camera and input mapping mirror rbfx sample behavior. |
| Basic UI | On-screen text or debug overlay. |
| Physics cube | Bullet rigid body and collision shape integrated with scene components. |
| Scene serialization | Load/save a minimal scene with nodes/components/resources using a C#-native serialization solution, not a direct port of rbfx serialization internals. |
| Multi-backend graphics | At least Vulkan and D3D11 or OpenGL from same graphics abstraction. |
| Tooling loop | Resource compiler/package target and sample launcher. |
| Editor foundation | ImGui/RmlUI shell, scene hierarchy, inspector, resource browser. |
