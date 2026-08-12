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

namespace RedBolt.PhysicsJolt;

using RedBolt.Scene;

public static class PhysicsEvents
{
    public struct PhysicsPreUpdateEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
    }

    public struct PhysicsPostUpdateEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
        public float Overtime;
    }

    public struct PhysicsPreStepEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
        public long? NetworkFrame;
    }

    public struct PhysicsPostStepEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
    }

    public struct PhysicsCollisionStartEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
        public IReadOnlyList<PhysicsContact> Contacts;
    }

    public struct PhysicsCollisionEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
        public IReadOnlyList<PhysicsContact> Contacts;
    }

    public struct PhysicsCollisionEndEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
    }

    public struct NodeCollisionStartEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
        public IReadOnlyList<PhysicsContact> Contacts;
    }

    public struct NodeCollisionEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
        public IReadOnlyList<PhysicsContact> Contacts;
    }

    public struct NodeCollisionEndEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
    }
}
