// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using System.Buffers.Binary;
using SystemMath = global::System.Math;

using RedBolt.Core;
using Timer = global::RedBolt.Core.Timer;
using RedBolt.Math;

/// <summary>Occlusion hierarchy depth value.</summary>
public struct DepthValue
{
    /// <summary>Minimum value.</summary>
    public int Min;
    /// <summary>Maximum value.</summary>
    public int Max;
}

/// <summary>Per-thread occlusion buffer data.</summary>
public struct OcclusionBufferData
{
    /// <summary>Full buffer data with safety padding.</summary>
    public int[]? DataWithSafety;
    /// <summary>Buffer data offset.</summary>
    public int DataOffset;
    /// <summary>Use flag.</summary>
    public bool Used;
}

/// <summary>Stored occlusion render job.</summary>
public struct OcclusionBatch
{
    /// <summary>Model matrix.</summary>
    public Matrix3x4 Model;
    /// <summary>Vertex data.</summary>
    public byte[]? VertexData;
    /// <summary>Vertex size in bytes.</summary>
    public uint VertexSize;
    /// <summary>Index data. Null if using non-indexed geometry.</summary>
    public byte[]? IndexData;
    /// <summary>Index size in bytes.</summary>
    public uint IndexSize;
    /// <summary>Draw start. First index for indexed geometry, otherwise first vertex.</summary>
    public uint DrawStart;
    /// <summary>Index or vertex count.</summary>
    public uint DrawCount;
}

/// <summary>Software renderer for occlusion.</summary>
[Reflected]
public sealed partial class OcclusionBuffer : RedObject
{
    private const int OcclusionMinSize = 8;
    private const int OcclusionDefaultMaxTriangles = 5000;
    private const float OcclusionRelativeBias = 0.00001f;
    private const int OcclusionFixedBias = 16;
    private const float OcclusionXScale = 65536.0f;
    private const float OcclusionZScale = 16777216.0f;

    private readonly List<OcclusionBufferData> _buffers = [];
    private readonly List<DepthValue[]> _mipBuffers = [];
    private readonly List<OcclusionBatch> _batches = [];
    private int _width;
    private int _height;
    private uint _numTriangles;
    private uint _maxTriangles = OcclusionDefaultMaxTriangles;
    private CullMode _cullMode = CullMode.CounterClockwise;
    private bool _depthHierarchyDirty = true;
    private bool _reverseCulling;
    private Matrix3x4 _view = Matrix3x4.Identity;
    private Matrix4 _projection = Matrix4.Identity;
    private Matrix4 _viewProj = Matrix4.Identity;
    private readonly Timer _useTimer = new();
    private float _nearClip;
    private float _farClip;
    private float _scaleX;
    private float _scaleY;
    private float _offsetX;
    private float _offsetY;
    private float _projOffsetScaleX;
    private float _projOffsetScaleY;

    /// <summary>Construct.</summary>
    public OcclusionBuffer()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _batches.Clear();
        _mipBuffers.Clear();
        _buffers.Clear();
        base.Dispose();
    }


    /// <summary>Set occlusion buffer size and whether to reserve multiple buffers for threading optimization.</summary>
    public bool SetSize(int width, int height, bool threaded)
    {
        if ((height & 1u) != 0)
            ++height;
        if (width == _width && height == _height)
            return true;
        if (width <= 0 || height <= 0)
            return false;
        if (!MathDefs.IsPowerOfTwo((uint)width))
            return false;

        _width = width;
        _height = height;
        var workQueue = GetSubsystem<WorkQueue>();
        var numThreadBuffers = threaded ? workQueue!.GetNumProcessingThreads() : 1;
        _buffers.Clear();
        for (var i = 0; i < numThreadBuffers; ++i)
        {
            _buffers.Add(new OcclusionBufferData
            {
                DataWithSafety = new int[checked(width * (height + 2) + 2)],
                DataOffset = width + 1
            });
        }

        _mipBuffers.Clear();
        for (;;)
        {
            width = (width + 1) / 2;
            height = (height + 1) / 2;
            _mipBuffers.Add(new DepthValue[checked(width * height)]);
            if (width <= OcclusionMinSize && height <= OcclusionMinSize)
                break;
        }
        CalculateViewport();
        return true;
    }

    /// <summary>Set camera view to render from.</summary>
    public void SetView(Camera? camera)
    {
        if (camera is null)
            return;
        _view = camera.GetView();
        _projection = camera.GetProjection();
        _viewProj = _projection * _view;
        _nearClip = camera.GetNearClip();
        _farClip = camera.GetFarClip();
        _reverseCulling = camera.GetReverseCulling();
        CalculateViewport();
    }

    public void SetMaxTriangles(uint triangles) => _maxTriangles = triangles;

    public void SetCullMode(CullMode mode)
    {
        if (_reverseCulling)
        {
            if (mode == CullMode.Clockwise) mode = CullMode.CounterClockwise;
            else if (mode == CullMode.CounterClockwise) mode = CullMode.Clockwise;
        }
        _cullMode = mode;
    }

    public void Reset() { _numTriangles = 0; _batches.Clear(); }

    public void Clear()
    {
        Reset();
        ClearBuffer(0);
        for (var i = 1; i < _buffers.Count; ++i)
        {
            var buffer = _buffers[i];
            buffer.Used = false;
            _buffers[i] = buffer;
        }
        _depthHierarchyDirty = true;
    }

    public bool AddTriangles(in Matrix3x4 model, byte[]? vertexData, uint vertexSize, uint vertexStart, uint vertexCount)
    {
        _batches.Add(new OcclusionBatch { Model = model, VertexData = vertexData, VertexSize = vertexSize,
            DrawStart = vertexStart, DrawCount = vertexCount });
        _numTriangles += vertexCount / 3;
        return _numTriangles <= _maxTriangles;
    }

    public bool AddTriangles(in Matrix3x4 model, byte[]? vertexData, uint vertexSize, byte[]? indexData, uint indexSize,
        uint indexStart, uint indexCount)
    {
        _batches.Add(new OcclusionBatch { Model = model, VertexData = vertexData, VertexSize = vertexSize,
            IndexData = indexData, IndexSize = indexSize, DrawStart = indexStart, DrawCount = indexCount });
        _numTriangles += indexCount / 3;
        return _numTriangles <= _maxTriangles;
    }

    public void DrawTriangles()
    {
        if (_buffers.Count == 1)
        {
            foreach (var batch in _batches)
                DrawBatch(batch, 0);
            _depthHierarchyDirty = true;
        }
        else if (_buffers.Count > 1)
        {
            WorkQueue.ForEachParallel(GetSubsystem<WorkQueue>()!, _batches,
                (_, batch) => DrawBatch(batch, WorkQueue.GetThreadIndex()));
            MergeBuffers();
            _depthHierarchyDirty = true;
        }
        _batches.Clear();
    }

    public void BuildDepthHierarchy()
    {
        if (_buffers.Count == 0 || !_depthHierarchyDirty)
            return;
        var width = (_width + 1) / 2;
        var height = (_height + 1) / 2;
        if (_mipBuffers.Count != 0)
        {
            for (var y = 0; y < height; ++y)
            for (var x = 0; x < width; ++x)
            {
                var sx = x * 2;
                var sy = y * 2;
                var a = Pixel(sx, sy); var b = Pixel(sx + 1, sy);
                var c = sy + 1 < _height ? Pixel(sx, sy + 1) : a;
                var d = sy + 1 < _height ? Pixel(sx + 1, sy + 1) : b;
                _mipBuffers[0][y * width + x] = new DepthValue
                    { Min = SystemMath.Min(SystemMath.Min(a, b), SystemMath.Min(c, d)), Max = SystemMath.Max(SystemMath.Max(a, b), SystemMath.Max(c, d)) };
            }
        }
        for (var level = 1; level < _mipBuffers.Count; ++level)
        {
            var prevWidth = width; var prevHeight = height;
            width = (width + 1) / 2; height = (height + 1) / 2;
            for (var y = 0; y < height; ++y)
            for (var x = 0; x < width; ++x)
            {
                var i0 = y * 2 * prevWidth + x * 2;
                var a = _mipBuffers[level - 1][i0];
                var b = _mipBuffers[level - 1][i0 + 1];
                var c = y * 2 + 1 < prevHeight ? _mipBuffers[level - 1][i0 + prevWidth] : a;
                var d = y * 2 + 1 < prevHeight ? _mipBuffers[level - 1][i0 + prevWidth + 1] : b;
                _mipBuffers[level][y * width + x] = new DepthValue
                    { Min = SystemMath.Min(SystemMath.Min(a.Min, b.Min), SystemMath.Min(c.Min, d.Min)), Max = SystemMath.Max(SystemMath.Max(a.Max, b.Max), SystemMath.Max(c.Max, d.Max)) };
            }
        }
        _depthHierarchyDirty = false;
    }

    public void ResetUseTimer() => _useTimer.Reset();
    /// <summary>Return highest level depth values.</summary>
    public ArraySegment<int>? GetBuffer() => _buffers.Count != 0
        ? new ArraySegment<int>(_buffers[0].DataWithSafety!, _buffers[0].DataOffset, _width * _height)
        : (ArraySegment<int>?)null;
    public ref readonly Matrix3x4 GetView() => ref _view;
    public ref readonly Matrix4 GetProjection() => ref _projection;
    public int GetWidth() => _width;
    public int GetHeight() => _height;
    public uint GetNumTriangles() => _numTriangles;
    public uint GetMaxTriangles() => _maxTriangles;
    public CullMode GetCullMode() => _cullMode;
    public bool IsThreaded() => _buffers.Count > 1;
    public uint GetUseTimer() => _useTimer.GetMSec(false);

    public bool IsVisible(in BoundingBox box)
    {
        if (_buffers.Count == 0) return true;
        Span<Vector3f> corners = stackalloc Vector3f[8] { box.Min,
            new(box.Max.X, box.Min.Y, box.Min.Z), new(box.Min.X, box.Max.Y, box.Min.Z),
            new(box.Max.X, box.Max.Y, box.Min.Z), new(box.Min.X, box.Min.Y, box.Max.Z),
            new(box.Max.X, box.Min.Y, box.Max.Z), new(box.Min.X, box.Max.Y, box.Max.Z), box.Max };
        var first = ModelTransform(_viewProj, corners[0]); first.Z -= OcclusionRelativeBias;
        if (first.Z <= 0) return true;
        var p = ViewportTransform(first);
        var minX=p.X; var maxX=p.X; var minY=p.Y; var maxY=p.Y; var minZ=p.Z;
        for (var i=1;i<8;++i) { var v=ModelTransform(_viewProj,corners[i]); v.Z-=OcclusionRelativeBias;
            if(v.Z<=0)return true; p=ViewportTransform(v); minX=SystemMath.Min(minX,p.X); maxX=SystemMath.Max(maxX,p.X);
            minY=SystemMath.Min(minY,p.Y); maxY=SystemMath.Max(maxY,p.Y); minZ=SystemMath.Min(minZ,p.Z); }
        var left=(int)(minX-1.5f); var top=(int)(minY-1.5f); var right=MathDefs.RoundToInt(maxX); var bottom=MathDefs.RoundToInt(maxY);
        if(right<0||bottom<0||left>=_width||top>=_height)return true;
        left=SystemMath.Max(left,0); top=SystemMath.Max(top,0); right=SystemMath.Min(right,_width-1); bottom=SystemMath.Min(bottom,_height-1);
        var z=MathDefs.RoundToInt(minZ)-OcclusionFixedBias;
        if (!_depthHierarchyDirty)
        {
            for (var level = _mipBuffers.Count - 1; level >= 0; --level)
            {
                var shift = level + 1;
                var width = _width >> shift;
                var mipLeft = left >> shift;
                var mipRight = right >> shift;
                var allOccluded = true;
                for (var y = top >> shift; y <= bottom >> shift; ++y)
                {
                    for (var x = mipLeft; x <= mipRight; ++x)
                    {
                        var depth = _mipBuffers[level][y * width + x];
                        if (z <= depth.Min)
                            return true;
                        if (z <= depth.Max)
                            allOccluded = false;
                    }
                }
                if (allOccluded)
                    return false;
            }
        }
        for(var y=top;y<=bottom;++y) for(var x=left;x<=right;++x) if(z<=Pixel(x,y)) return true;
        return false;
    }

    public void DrawBatch(in OcclusionBatch batch, int threadIndex)
    {
        if (threadIndex > 0 && !_buffers[threadIndex].Used)
        {
            ClearBuffer(threadIndex);
            var buffer = _buffers[threadIndex];
            buffer.Used = true;
            _buffers[threadIndex] = buffer;
        }
        var transform=_viewProj*batch.Model;
        Span<Vector4f> vertices=stackalloc Vector4f[192];
        for(var i=0u;i+2<batch.DrawCount;i+=3)
        {
            uint a,b,c;
            if(batch.IndexData is null){a=batch.DrawStart+i;b=a+1;c=a+2;}
            else {a=ReadIndex(batch,i);b=ReadIndex(batch,i+1);c=ReadIndex(batch,i+2);}
            vertices[0]=ModelTransform(transform,ReadPosition(batch,a)); vertices[1]=ModelTransform(transform,ReadPosition(batch,b));
            vertices[2]=ModelTransform(transform,ReadPosition(batch,c)); DrawTriangle(vertices,threadIndex);
        }
    }

    private int Pixel(int x,int y)=>_buffers[0].DataWithSafety![_buffers[0].DataOffset+y*_width+x];
    private static Vector3f ReadPosition(OcclusionBatch b,uint i){var o=checked((int)(i*b.VertexSize));return new(
        BinaryPrimitives.ReadSingleLittleEndian(b.VertexData!.AsSpan(o,4)),BinaryPrimitives.ReadSingleLittleEndian(b.VertexData.AsSpan(o+4,4)),
        BinaryPrimitives.ReadSingleLittleEndian(b.VertexData.AsSpan(o+8,4)));}
    private static uint ReadIndex(OcclusionBatch b,uint i){var o=checked((int)((b.DrawStart+i)*b.IndexSize));return b.IndexSize==2?
        BinaryPrimitives.ReadUInt16LittleEndian(b.IndexData!.AsSpan(o,2)):BinaryPrimitives.ReadUInt32LittleEndian(b.IndexData!.AsSpan(o,4));}
    private static Vector4f ModelTransform(Matrix4 t,Vector3f v)=>new(t.M00*v.X+t.M01*v.Y+t.M02*v.Z+t.M03,
        t.M10*v.X+t.M11*v.Y+t.M12*v.Z+t.M13,t.M20*v.X+t.M21*v.Y+t.M22*v.Z+t.M23,t.M30*v.X+t.M31*v.Y+t.M32*v.Z+t.M33);
    private Vector3f ViewportTransform(Vector4f v){var w=1f/v.W;return new(w*v.X*_scaleX+_offsetX,w*v.Y*_scaleY+_offsetY,w*v.Z*OcclusionZScale);}
    private static Vector4f ClipEdge(Vector4f a,Vector4f b,float d0,float d1)=>a+(d0/(d0-d1))*(b-a);
    private static float SignedArea(Vector3f a,Vector3f b,Vector3f c)=>(a.X-b.X)*(c.Y-b.Y)-(a.Y-b.Y)*(c.X-b.X);
    private void CalculateViewport(){_scaleX=.5f*_width;_scaleY=-.5f*_height;_offsetX=.5f*_width+.5f;_offsetY=.5f*_height+.5f;
        _projOffsetScaleX=_projection.M00*_scaleX;_projOffsetScaleY=_projection.M11*_scaleY;}

    private void DrawTriangle(Span<Vector4f> v,int threadIndex)
    {
        uint clip=0,and=0;
        for(var i=0;i<3;++i){uint m=0;if(v[i].X>v[i].W)m|=1;if(v[i].X< -v[i].W)m|=2;if(v[i].Y>v[i].W)m|=4;
            if(v[i].Y< -v[i].W)m|=8;if(v[i].Z>v[i].W)m|=16;if(v[i].Z<0)m|=32;clip|=m;and=i==0?m:and&m;}
        if(and!=0)return;
        Span<bool> triangles=stackalloc bool[64];triangles[0]=true;var count=1u;
        if(clip!=0){if((clip&1)!=0)ClipVertices(new(-1,0,0,1),v,triangles,ref count);if((clip&2)!=0)ClipVertices(new(1,0,0,1),v,triangles,ref count);
            if((clip&4)!=0)ClipVertices(new(0,-1,0,1),v,triangles,ref count);if((clip&8)!=0)ClipVertices(new(0,1,0,1),v,triangles,ref count);
            if((clip&16)!=0)ClipVertices(new(0,0,-1,1),v,triangles,ref count);if((clip&32)!=0)ClipVertices(new(0,0,1,0),v,triangles,ref count);}
        var drawn=false;
        Span<Vector3f> p=stackalloc Vector3f[3];
        for(var i=0u;i<count;++i)if(triangles[(int)i]){var n=(int)i*3;
            p[0]=ViewportTransform(v[n]);p[1]=ViewportTransform(v[n+1]);p[2]=ViewportTransform(v[n+2]);var cw=SignedArea(p[0],p[1],p[2])<0;
            if(_cullMode==CullMode.None||(_cullMode==CullMode.CounterClockwise&&cw)||(_cullMode==CullMode.Clockwise&&!cw)){DrawTriangle2D(p,cw,threadIndex);drawn=true;}}
        if(drawn)++_numTriangles;
    }

    private static void ClipVertices(Vector4f plane,Span<Vector4f> v,Span<bool> triangles,ref uint count)
    {
        var original=count;for(var i=0u;i<original;++i)if(triangles[(int)i]){var n=(int)i*3;var d0=plane.DotProduct(v[n]);var d1=plane.DotProduct(v[n+1]);var d2=plane.DotProduct(v[n+2]);
            if(d0<0&&d1<0&&d2<0){triangles[(int)i]=false;continue;}if(d0<0&&d1<0){v[n]=ClipEdge(v[n],v[n+2],d0,d2);v[n+1]=ClipEdge(v[n+1],v[n+2],d1,d2);}
            else if(d0<0&&d2<0){v[n]=ClipEdge(v[n],v[n+1],d0,d1);v[n+2]=ClipEdge(v[n+2],v[n+1],d2,d1);}else if(d1<0&&d2<0){v[n+1]=ClipEdge(v[n+1],v[n],d1,d0);v[n+2]=ClipEdge(v[n+2],v[n],d2,d0);}
            else if(d0<0){var q=(int)count*3;triangles[(int)count++]=true;v[q]=ClipEdge(v[n],v[n+2],d0,d2);v[q+1]=v[n]=ClipEdge(v[n],v[n+1],d0,d1);v[q+2]=v[n+2];}
            else if(d1<0){var q=(int)count*3;triangles[(int)count++]=true;v[q+1]=ClipEdge(v[n+1],v[n],d1,d0);v[q+2]=v[n+1]=ClipEdge(v[n+1],v[n+2],d1,d2);v[q]=v[n];}
            else if(d2<0){var q=(int)count*3;triangles[(int)count++]=true;v[q+2]=ClipEdge(v[n+2],v[n+1],d2,d1);v[q]=v[n+2]=ClipEdge(v[n+2],v[n],d2,d0);v[q+1]=v[n+1];}}
    }

    private void DrawTriangle2D(Span<Vector3f> v,bool clockwise,int threadIndex)
    {
        int top,middle,bottom;bool middleIsRight;
        if(v[0].Y<v[1].Y){if(v[2].Y<v[0].Y){top=2;middle=0;bottom=1;middleIsRight=true;}
            else{top=0;if(v[1].Y<v[2].Y){middle=1;bottom=2;middleIsRight=true;}else{middle=2;bottom=1;middleIsRight=false;}}}
        else{if(v[2].Y<v[1].Y){top=2;middle=1;bottom=0;middleIsRight=false;}
            else{top=1;if(v[0].Y<v[2].Y){middle=0;bottom=2;middleIsRight=false;}else{middle=2;bottom=0;middleIsRight=true;}}}
        var topY=(int)v[top].Y;var middleY=(int)v[middle].Y;var bottomY=(int)v[bottom].Y;if(topY==bottomY)return;
        if(!clockwise)middleIsRight=!middleIsRight;
        var g=new Gradients(v);var longEdge=new Edge(g,v[top],v[bottom],topY);
        if(topY!=middleY){var shortEdge=new Edge(g,v[top],v[middle],topY);RasterHalf(ref longEdge,ref shortEdge,topY,middleY,middleIsRight,g,threadIndex);}
        if(middleY!=bottomY){var shortEdge=new Edge(g,v[middle],v[bottom],middleY);RasterHalf(ref longEdge,ref shortEdge,middleY,bottomY,middleIsRight,g,threadIndex);}
    }

    private void RasterHalf(ref Edge longEdge,ref Edge shortEdge,int startY,int endY,bool middleIsRight,Gradients g,int threadIndex)
    {
        var data=_buffers[threadIndex];for(var y=startY;y<endY;++y){var left=middleIsRight?longEdge:shortEdge;var right=middleIsRight?shortEdge:longEdge;
            var z=left.InvZ;for(var x=left.X>>16;x<(right.X>>16);++x){var p=data.DataOffset+y*_width+x;if(z<data.DataWithSafety![p])data.DataWithSafety[p]=z;z+=g.DInvZdXInt;}
            longEdge.Step();shortEdge.Step();}
    }

    private void MergeBuffers(){for(var i=1;i<_buffers.Count;++i)if(_buffers[i].Used)for(var p=0;p<_width*_height;++p){var s=_buffers[i].DataWithSafety![_buffers[i].DataOffset+p];var d=_buffers[0].DataOffset+p;if(s<_buffers[0].DataWithSafety![d])_buffers[0].DataWithSafety![d]=s;}}
    private void ClearBuffer(int threadIndex){if(threadIndex>=_buffers.Count)return;Array.Fill(_buffers[threadIndex].DataWithSafety!,(int)OcclusionZScale,_buffers[threadIndex].DataOffset,_width*_height);}

    private readonly struct Gradients
    {
        public readonly int DInvZdXInt;public readonly float DInvZdX;public readonly float DInvZdY;
        public Gradients(Span<Vector3f> v){var dx=1f/(((v[1].X-v[2].X)*(v[0].Y-v[2].Y))-((v[0].X-v[2].X)*(v[1].Y-v[2].Y)));
            DInvZdX=dx*(((v[1].Z-v[2].Z)*(v[0].Y-v[2].Y))-((v[0].Z-v[2].Z)*(v[1].Y-v[2].Y)));
            DInvZdY=-dx*(((v[1].Z-v[2].Z)*(v[0].X-v[2].X))-((v[0].Z-v[2].Z)*(v[1].X-v[2].X)));DInvZdXInt=(int)DInvZdX;}
    }
    private struct Edge
    {
        public int X,XStep,InvZ,InvZStep;
        public Edge(Gradients g,Vector3f top,Vector3f bottom,int topY){var h=bottom.Y-top.Y;var slope=h!=0?(bottom.X-top.X)/h:0;var yp=topY+1-top.Y;var xp=slope*yp;
            X=MathDefs.RoundToInt((xp+top.X)*OcclusionXScale);XStep=MathDefs.RoundToInt(slope*OcclusionXScale);InvZ=MathDefs.RoundToInt(top.Z+xp*g.DInvZdX+yp*g.DInvZdY);InvZStep=MathDefs.RoundToInt(slope*g.DInvZdX+g.DInvZdY);}
        public void Step(){X+=XStep;InvZ+=InvZStep;}
    }
}
