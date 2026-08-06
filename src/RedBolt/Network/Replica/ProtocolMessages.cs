//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.IO;

public struct MsgConfigure
{
    public MsgConfigure()
    {
        Settings = new VariantMap();
    }

    public readonly void Save(VectorBuffer destination)
    {
        destination.WriteUInt(Magic);
        destination.WriteVariantMap(Settings);
    }

    public void Load(MemoryBuffer source)
    {
        Magic = source.ReadUInt();
        Settings = source.ReadVariantMap();
    }

    public override readonly string ToString() => $"{{magic={Magic}, settings...}}";

    public uint Magic;
    public VariantMap Settings;
}

public struct MsgSynchronized
{
    public readonly void Save(VectorBuffer destination)
    {
        destination.WriteUInt(Magic);
    }

    public void Load(MemoryBuffer source)
    {
        Magic = source.ReadUInt();
    }

    public override readonly string ToString() => $"{{magic={Magic}}}";

    public uint Magic;
}

public struct MsgSceneClock
{
    public readonly void Save(VectorBuffer destination)
    {
        destination.WriteInt64((long)LatestFrame);
        destination.WriteUInt(LatestFrameTime);
        destination.WriteVLE(InputDelay);
    }

    public void Load(MemoryBuffer source)
    {
        LatestFrame = (NetworkFrame)source.ReadInt64();
        LatestFrameTime = source.ReadUInt();
        InputDelay = source.ReadVLE();
    }

    public override readonly string ToString() =>
        $"{{latestFrame={LatestFrame} at {LatestFrameTime}, inputDelay={InputDelay}}}";

    public NetworkFrame LatestFrame;
    public uint LatestFrameTime;
    public uint InputDelay;
}
