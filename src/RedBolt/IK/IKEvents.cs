// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using RedBolt.Scene;

namespace RedBolt.IK;

public static class IKEvents
{
    /// <summary>IKSolver at the sender Node is about to start solving.</summary>
    public struct IKPreSolveEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>IKSolver pointer.</summary>
        public IKSolver IKSolver;
    }

    /// <summary>IKSolver at the sender Node has finished solving.</summary>
    public struct IKPostSolveEvent
    {
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>IKSolver pointer.</summary>
        public IKSolver IKSolver;
    }
}
