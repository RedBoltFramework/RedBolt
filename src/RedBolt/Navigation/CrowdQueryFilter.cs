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

namespace RedBolt.Navigation;

using DotRecast.Core.Numerics;
using DotRecast.Detour;

internal sealed class CrowdQueryFilter : IDtQueryFilter
{
    private readonly float[] _areaCosts = new float[DtDetour.DT_MAX_AREAS];
    private int _includeFlags = ushort.MaxValue;
    private int _excludeFlags;

    public CrowdQueryFilter()
    {
        Array.Fill(_areaCosts, 1.0f);
    }

    public bool PassFilter(long reference, DtMeshTile tile, DtPoly poly)
    {
        return (poly.flags & _includeFlags) != 0 && (poly.flags & _excludeFlags) == 0;
    }

    public float GetCost(RcVec3f pa, RcVec3f pb, long previousReference, DtMeshTile previousTile,
        DtPoly previousPoly, long currentReference, DtMeshTile currentTile, DtPoly currentPoly,
        long nextReference, DtMeshTile nextTile, DtPoly nextPoly)
    {
        return RcVec3f.Distance(pa, pb) * _areaCosts[currentPoly.GetArea()];
    }

    public int GetIncludeFlags() => _includeFlags;

    public void SetIncludeFlags(int flags) => _includeFlags = flags;

    public int GetExcludeFlags() => _excludeFlags;

    public void SetExcludeFlags(int flags) => _excludeFlags = flags;

    public float GetAreaCost(int areaId) => _areaCosts[areaId];

    public void SetAreaCost(int areaId, float cost) => _areaCosts[areaId] = cost;
}
