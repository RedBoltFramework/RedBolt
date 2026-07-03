# First Cube Vertical Slice

## Goal

Render a cube in a desktop RedBolt window as quickly as possible while proving the same cross-cutting architecture that the full engine will use.

## Required Systems

| System | Minimum feature set |
|---|---|
| Core | Logging, errors, disposable lifetime helpers, math primitives, subsystem registry, frame events. |
| Engine | Application host, engine parameters, startup/shutdown, fixed frame order, main loop. |
| Input | SDL event polling, keyboard state, mouse state, window close, Escape-to-exit. |
| Graphics | Window-bound device/swapchain, clear, resize, vertex/index buffers, shader/pipeline, draw indexed, present. |
| Resources | Resource roots, resource cache, procedural mesh resource, minimal shader/material resources. |
| Scene | Scene, node transform, camera component, static mesh component, update/render collection. |

## First-Cube Render Path

```text
Program.Main
  ApplicationHost.Run
    Engine.Initialize
      InputSystem creates/polls window
      GraphicsSystem creates Diligent device and swapchain
      ResourceCache registers first-cube resource loaders
      SceneSystem creates scene services
    FirstCubeApp.Start
      create Scene
      create Camera node
      create Cube node with StaticMesh component
      assign MeshResource("Models/ProceduralBox")
      assign MaterialResource("Materials/FirstCube")
    frame loop
      InputSystem.BeginFrame/PollEvents
      Engine sends Update
      Scene updates transforms
      GraphicsSystem renders visible StaticMesh components from active Camera
      swapchain Present
```

## Minimal Public C# API

The first sample should look close to eventual RedBolt application code, even if internals are narrow.

```csharp
public sealed class FirstCubeApplication : Application
{
    protected override void Setup(EngineParameters parameters)
    {
        parameters.WindowTitle = "RedBolt First Cube";
        parameters.Width = 1280;
        parameters.Height = 720;
    }

    protected override void Start()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.Position = new Vector3(0, 2, -6);
        cameraNode.CreateComponent<Camera>();

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.Rotation = Quaternion.Identity;
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.Model = Resources.Get<Model>("Models/ProceduralBox");
        cube.Material = Resources.Get<Material>("Materials/FirstCube");

        Graphics.SetViewport(new Viewport(scene, cameraNode.GetComponent<Camera>()));
    }
}
```

This API is aspirational for the sample. Implementation can be smaller internally, but names and responsibilities should be close enough that future rbfx sample ports are incremental.

The first-cube implementation should also establish the low-GC baseline: no avoidable per-frame allocations in input polling, update dispatch, scene traversal, resource lookup, or draw submission.

## Minimal Core

| Type | First responsibility |
|---|---|
| `RedBoltObject` | Base class if needed for diagnostics/type identity. Avoid heavy inheritance initially. |
| `SubsystemRegistry` | Register and retrieve engine systems by type. |
| `EngineTime` | Delta time and frame number. |
| `RedBoltException` | Runtime error with subsystem context. |
| Logging | C#-native logging abstraction with console output and native log forwarding. Do not directly port rbfx logging internals unless needed for compatibility. |
| Math types | Copy and adapt the existing rbfx C# math types from `/home/rk/Desktop/src/rbfx/rbfx/Source/Urho3D/CSharp/Managed/Math`. |

## Minimal Engine

| Type | First responsibility |
|---|---|
| `Application` | Virtual `Setup`, `Start`, `Update`, `Stop`; owns `Engine`. |
| `Engine` | Initialize systems, run loop, exit flag, frame order. |
| `EngineParameters` | Window title, size, fullscreen flag, resource paths, backend hint. |
| `FrameEvent` | Managed update callback. Can be simple C# event first. |

## Minimal Input

| Type | First responsibility |
|---|---|
| `InputSystem` | Poll native events each frame and expose current state. |
| `Keyboard` | `IsKeyDown(Key.Escape)` and key transitions. |
| `Mouse` | Position and buttons if easy; camera mouse-look can be deferred. |
| `WindowEvent` | Close and resize. |

## Minimal Graphics

| Type | First responsibility |
|---|---|
| `GraphicsSystem` | Own native window/device/swapchain and per-frame render entrypoint. |
| `GraphicsBackend` | Enum for Auto, Vulkan, OpenGL, D3D11, D3D12 where available. First implementation can support Auto plus one backend, but the object model should stay close to rbfx graphics. |
| `GpuBuffer` | Vertex/index/constant buffer handle. |
| `Shader` | Vertex/pixel shader handle from embedded source or resource text. |
| `PipelineState` | Input layout, shaders, raster/depth state. |
| `Mesh` | CPU vertices/indices and uploaded GPU buffers. |
| `Material` | Shader/pipeline and basic color. |
| `Camera` | View/projection matrix, FOV, near/far. |
| `Viewport` | Scene plus camera binding. |

## Minimal Resources

| Type | First responsibility |
|---|---|
| `ResourceCache` | `Get<T>(string name)`, cache instances, register loaders. |
| `Resource` | Base with name and load state. |
| `ResourceLoader<T>` | Creates resources from file or procedural name. |
| `ProceduralBoxLoader` | Returns cube vertices/indices for `Models/ProceduralBox`. |
| `FirstCubeMaterialLoader` | Returns a basic material for `Materials/FirstCube`. |
| `FileSystem` | Resource root list and file reads. Can be minimal. |

## Minimal Scene

| Type | First responsibility |
|---|---|
| `Scene` | Root node and component query for rendering. |
| `Node` | Name, parent/children, local/world transform, component list. |
| `Component` | Base class with owner node. |
| `Camera` | Scene component used by viewport. |
| `StaticModel` | Scene component with `Model` and `Material`. |
| `Octree` | Can start as a marker or simple renderable list. Real spatial partitioning deferred. |

## Generated Dependency Capabilities Needed

| Capability group | Generated dependency surface needed |
|---|---|
| Window | SDL window creation/destruction, drawable size, title. |
| Events | SDL event polling and close/resize/key/mouse event data. |
| Device | Diligent graphics device and swapchain creation/destruction. |
| Frame | Diligent begin frame, clear color/depth, present. |
| Resources | Diligent buffer, shader, and pipeline creation/update/destruction. |
| Draw | Diligent pipeline/buffer binding, constants, indexed draw. |

These capabilities must come from SharpCpp-generated bindings over dependency headers. Do not introduce RedBolt-owned native facade headers or forwarding functions to provide them.

## Deferred From First Cube

| Deferred item | Replacement in first cube |
|---|---|
| rbfx `.mdl` parser | Procedural cube loader. |
| rbfx XML materials | Hardcoded first-cube material loader. |
| Shader translation | Embedded backend shader source or one backend-specific shader. |
| Texture loading | Solid color material. |
| Lighting/shadows | Unlit cube or one simple directional light in shader constants. |
| Free-fly camera | Static camera first; keyboard rotation/movement can follow. |
| Octree culling | Linear renderable list. |
| Multithreaded work queue | Single-threaded frame loop. |
| Hot reload | Restart app after changes. |
| ECS or advanced component metadata | Simple node/component classes. |
| rbfx serialization internals | C#-native serialization solution later. |

## First-Cube Implementation Order

| Step | Output |
|---|---|
| 1 | Root CMake configures and locates rbfx source root plus SharpCpp symlink. |
| 2 | Native SDL+Diligent dependency batch compiles and passes a C++ smoke executable. |
| 3 | RedBolt generator emits SharpCpp bindings from dependency headers and managed smoke calls generated APIs. |
| 4 | Managed `Engine` and `Application` own lifecycle and input polling. |
| 5 | Managed `GraphicsSystem` uploads hardcoded cube buffers and draws. |
| 6 | Move cube data behind `ResourceCache` and scene `StaticModel`. |
| 7 | Add camera matrices and resize handling. |
| 8 | Add sample target and first-cube verification instructions. |

## Validation Checklist

| Check | Expected result |
|---|---|
| Native smoke | C++ dependency smoke validates linked SDL and Diligent headers/libs. |
| Binding smoke | C# can call generated dependency bindings; no hand-authored binding wrappers are used. |
| Managed frame loop | Escape and window close exit without native leaks. |
| Resource smoke | `ResourceCache.Get<Model>("Models/ProceduralBox")` returns cached resource. |
| Scene smoke | Cube renderable is discovered through scene/component path. |
| Resize smoke | Resize updates swapchain and projection. |
| Shutdown smoke | Disposing application releases graphics resources before device/window. |
| Allocation smoke | After warmup, first-cube frame loop should not allocate in normal input/update/render frames except diagnostics or one-time resource creation. |
