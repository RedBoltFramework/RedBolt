// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Resource;

using RedBolt.Core;
using RedBolt.IO;

/// <summary>
/// Optional resource request processor.
/// Can deny requests, re-route resource file names, or perform other processing per request.
/// </summary>
public abstract class ResourceRouter : RedObject
{
    /// <summary>
    /// Construct.
    /// </summary>
    public ResourceRouter()
    {
    }

    /// <summary>
    /// Process the resource request and optionally modify the resource identifier.
    /// Empty identifier means the resource is not found or not allowed.
    /// </summary>
    public abstract void Route(ref FileIdentifier name);
}
