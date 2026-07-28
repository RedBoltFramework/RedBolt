//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using RedBolt.Scene;

namespace RedBolt.Navigation;

public static class NavigationEvents
{
    /// <summary>Complete rebuild of navigation mesh.</summary>
    public struct NavigationMeshRebuiltEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>NavigationMesh pointer.</summary>
        public NavigationMesh Mesh;
    }

    /// <summary>Mesh tile is added to navigation mesh.</summary>
    public struct NavigationTileAddedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>NavigationMesh pointer.</summary>
        public NavigationMesh Mesh;
        /// <summary>IntVector2.</summary>
        public IntVector2 Tile;
    }

    /// <summary>Mesh tile is removed from navigation mesh.</summary>
    public struct NavigationTileRemovedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>NavigationMesh pointer.</summary>
        public NavigationMesh Mesh;
        /// <summary>IntVector2.</summary>
        public IntVector2 Tile;
    }

    /// <summary>All mesh tiles are removed from navigation mesh.</summary>
    public struct NavigationAllTilesRemovedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>NavigationMesh pointer.</summary>
        public NavigationMesh Mesh;
    }

    /// <summary>Crowd agent formation.</summary>
    public struct CrowdAgentFormationEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Unsigned.</summary>
        public int Index;
        /// <summary>Unsigned.</summary>
        public int Size;
        /// <summary>Vector3 [in/out].</summary>
        public Vector3f Position;
    }

    /// <summary>Crowd agent formation specific to a node.</summary>
    public struct CrowdAgentNodeFormationEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Unsigned.</summary>
        public int Index;
        /// <summary>Unsigned.</summary>
        public int Size;
        /// <summary>Vector3 [in/out].</summary>
        public Vector3f Position;
    }

    /// <summary>Crowd agent has been repositioned.</summary>
    public struct CrowdAgentRepositionEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Bool.</summary>
        public bool Arrived;
        /// <summary>Float.</summary>
        public float TimeStep;
    }

    /// <summary>Crowd agent has been repositioned, specific to a node.</summary>
    public struct CrowdAgentNodeRepositionEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Bool.</summary>
        public bool Arrived;
        /// <summary>Float.</summary>
        public float TimeStep;
    }

    /// <summary>Crowd agent's internal state has become invalidated. This is a special case of CrowdAgentStateChanged event.</summary>
    public struct CrowdAgentFailureEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Int.</summary>
        public CrowdAgentState CrowdAgentState;
        /// <summary>Int.</summary>
        public CrowdAgentTargetState CrowdTargetState;
    }

    /// <summary>Crowd agent's internal state has become invalidated. This is a special case of CrowdAgentStateChanged event.</summary>
    public struct CrowdAgentNodeFailureEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Int.</summary>
        public CrowdAgentState CrowdAgentState;
        /// <summary>Int.</summary>
        public CrowdAgentTargetState CrowdTargetState;
    }

    /// <summary>Crowd agent's state has been changed.</summary>
    public struct CrowdAgentStateChangedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Int.</summary>
        public CrowdAgentState CrowdAgentState;
        /// <summary>Int.</summary>
        public CrowdAgentTargetState CrowdTargetState;
    }

    /// <summary>Crowd agent's state has been changed.</summary>
    public struct CrowdAgentNodeStateChangedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>CrowdAgent pointer.</summary>
        public CrowdAgent CrowdAgent;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Vector3.</summary>
        public Vector3f Velocity;
        /// <summary>Int.</summary>
        public CrowdAgentState CrowdAgentState;
        /// <summary>Int.</summary>
        public CrowdAgentTargetState CrowdTargetState;
    }

    /// <summary>Addition of obstacle to dynamic navigation mesh.</summary>
    public struct NavigationObstacleAddedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Obstacle pointer.</summary>
        public Obstacle Obstacle;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Float.</summary>
        public float Radius;
        /// <summary>Float.</summary>
        public float Height;
    }

    /// <summary>Removal of obstacle from dynamic navigation mesh.</summary>
    public struct NavigationObstacleRemovedEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Obstacle pointer.</summary>
        public Obstacle Obstacle;
        /// <summary>Vector3.</summary>
        public Vector3f Position;
        /// <summary>Float.</summary>
        public float Radius;
        /// <summary>Float.</summary>
        public float Height;
    }
}
