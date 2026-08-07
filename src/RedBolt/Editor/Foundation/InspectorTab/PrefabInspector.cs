// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

file static class PrefabInspectorHelpers
{
    public static float CalculateCameraDistance(BoundingBox bbox, Camera camera, float margin)
    {
        var objectRadius = bbox.Size.Length * 0.5f;
        var verticalFov = camera.GetFov();
        var horizontalFov = 2.0f * MathDefs.Atan(MathDefs.Tan(verticalFov * 0.5f) * camera.GetAspectRatio());
        var minFov = MathF.Min(verticalFov, horizontalFov);
        var distance = objectRadius / MathDefs.Tan(minFov * 0.5f) * (1.0f + margin);
        return distance;
    }

    public static Vector3f CalculateOptimalCameraDirection(BoundingBox bbox, float topDominanceMargin)
    {
        // Calculate visible area for each primary viewing direction
        var size = bbox.Size;
        var areaYZ = size.Y * size.Z; // Looking along X axis
        var areaXZ = size.X * size.Z; // Looking along Y axis (top/bottom)
        var areaXY = size.X * size.Y; // Looking along Z axis

        // Heuristic:
        // - Prefer top-down if object is very thin vertically vs its widest horizontal dimension
        // - Otherwise, pick the side (RIGHT or FORWARD) that shows the largest visible area
        // - If top area clearly dominates best side by margin, also pick top
        var bestSideArea = MathF.Max(areaYZ, areaXY);

        if (areaXZ >= bestSideArea * topDominanceMargin)
            return Vector3f.Up;      // Very thin or clearly best from top: look from top
        else if (areaYZ >= areaXY)
            return Vector3f.Right;   // Look from the right (Y-Z plane is larger)
        else
            return Vector3f.Forward; // Look from the front (X-Y plane is larger)
    }
}

// TODO: port Foundation_PrefabInspector when editor plugins are ported.
// void Foundation_PrefabInspector(Context* context, InspectorTab* inspectorTab)
// {
//     inspectorTab->RegisterAddon<PrefabInspector>(inspectorTab->GetProject());
// }

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class PrefabInspector : InspectorWithPreview
{
    public PrefabInspector(Project project)
        : base(project)
    {
    }

    protected override StringHash GetResourceType() => typeof(PrefabResource).GetTypeId();

    protected override ResourceInspectorWidget? MakeInspectorWidget(IReadOnlyList<Resource> resources)
    {
        return new ModelInspectorWidget(resources);
    }

    protected override BaseWidget? MakePreviewWidget(Resource resource)
    {
        var sceneWidget = new SceneWidget();
        var scene = sceneWidget.CreateDefaultScene();
        var prefabNode = scene.InstantiatePrefab((PrefabResource)resource)!;
        prefabNode.SetName("Prefab");
        prefabNode.SetScale(Vector3f.One); // Normalize scale because prefab will be rescaled anyway

        // Calculate total bounding box of the prefab
        var drawables = prefabNode.FindComponents<Drawable>(
            ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived);
        var bbox = new BoundingBox();
        foreach (var drawable in drawables)
        {
            drawable.Update(new FrameInfo()); // Ensure that bounding boxes are up to date
            bbox.Merge(drawable.GetWorldBoundingBox());
        }

        // Scale up small objects
        var radius = bbox.Size.Length * 0.5f;
        const float minRadius = 10.0f;
        if (radius > 0.0f && radius < minRadius)
        {
            var scale = minRadius / radius;
            prefabNode.SetScale(scale);
            bbox.Transform(Matrix3x4.FromScale(scale));
        }

        // Prefab bbox center is at origin
        prefabNode.SetPosition(-bbox.Center);
        bbox.Transform(Matrix3x4.FromTranslation(-bbox.Center));

        // Add a slight upward/sideways angle for side/front views to see the object better
        var cameraDirection = PrefabInspectorHelpers.CalculateOptimalCameraDirection(bbox, 10.0f);
        if (cameraDirection != Vector3f.Up)
        {
            const float elevationAngle = 20.0f;
            const float verticalRotationAngle = -30.0f;
            var horizontalAxis = cameraDirection.CrossProduct(Vector3f.Up);
            cameraDirection = new Quaternion(elevationAngle, horizontalAxis) * cameraDirection;
            cameraDirection = new Quaternion(verticalRotationAngle, Vector3f.Up) * cameraDirection;
        }

        // Position camera to see the prefab.
        var cameraNode = sceneWidget.GetCamera()!.Node;
        var camera = sceneWidget.GetCamera()!;
        var distance = PrefabInspectorHelpers.CalculateCameraDistance(bbox, camera, +0.1f);
        cameraNode.SetPosition(cameraDirection * distance);
        cameraNode.LookAt(Vector3f.Zero);

        // Apply limits to the MoveAndOrbitComponent
        var minDistance = PrefabInspectorHelpers.CalculateCameraDistance(bbox, camera, -0.9f);
        var maxDistance = PrefabInspectorHelpers.CalculateCameraDistance(bbox, camera, +0.9f);
        var moveAndOrbit = cameraNode.GetOrCreateComponent<MoveAndOrbitComponent>();
        moveAndOrbit.SetDistanceLimits(minDistance, maxDistance);

        return sceneWidget;
    }
}
