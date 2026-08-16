// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using RedBolt.IO;

/// <summary>Exception thrown on I/O error on Archive serialization/deserialization.</summary>
/// <remarks>
/// Try to catch this exception outside of serialization code and don't leak it to user code.
/// Archive is generally not safe to use if ArchiveException has been thrown.
/// </remarks>
public class ArchiveException : InvalidOperationException
{
    public ArchiveException(string message)
        : base(message)
    {
    }
}
