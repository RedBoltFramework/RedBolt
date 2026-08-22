// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;
using RedBolt.Core;
using RedBolt.Scene;

namespace DemoApplication
{
    [Reflected(ObjectCategory.Category_Logic)]
    class ScriptRotateObject : LogicComponent
    {
        public ScriptRotateObject()
        {
            UpdateEventMask = UpdateEvent.UseUpdate;
        }

        public override void Update(float timeStep)
        {
            var d = new Quaternion(10 * timeStep, 20 * timeStep, 30 * timeStep);
            Node.Rotate(d);
        }
    }
}
