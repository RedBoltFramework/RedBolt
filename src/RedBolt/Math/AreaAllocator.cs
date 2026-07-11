using System.Collections.Generic;

namespace RedBolt;

/// <summary>Rectangular area allocator.</summary>
public class AreaAllocator
{
    /// <summary>Default construct with empty size.</summary>
    public AreaAllocator()
    {
        Reset(0, 0);
    }

    /// <summary>Construct with given width and height.</summary>
    public AreaAllocator(int width, int height, bool fastMode = true)
    {
        Reset(width, height, width, height, fastMode);
    }

    /// <summary>Construct with given width and height, and set the maximum it allows to grow.</summary>
    public AreaAllocator(int width, int height, int maxWidth, int maxHeight, bool fastMode = true)
    {
        Reset(width, height, maxWidth, maxHeight, fastMode);
    }

    /// <summary>Reset to given width and height and remove all previous allocations.</summary>
    public void Reset(int width, int height, int maxWidth = 0, int maxHeight = 0, bool fastMode = true)
    {
        _doubleWidth = true;
        _size = new Vector2i(width, height);
        _maxSize = new Vector2i(maxWidth, maxHeight);
        _fastMode = fastMode;

        _freeAreas.Clear();
        var initialArea = new IntRect(0, 0, width, height);
        _freeAreas.Add(initialArea);
    }

    /// <summary>Try to allocate an area. Return true if successful, with x &amp; y coordinates filled.</summary>
    public bool Allocate(int width, int height, ref int x, ref int y)
    {
        if (width < 0)
            width = 0;
        if (height < 0)
            height = 0;

        var bestIndex = -1;
        var bestFreeArea = int.MaxValue;

        for (;;)
        {
            bestIndex = -1;
            bestFreeArea = int.MaxValue;
            for (var i = 0; i < _freeAreas.Count; ++i)
            {
                var freeWidth = _freeAreas[i].Width;
                var freeHeight = _freeAreas[i].Height;

                if (freeWidth >= width && freeHeight >= height)
                {
                    // Calculate rank for free area. Lower is better
                    var freeArea = freeWidth * freeHeight;

                    if (freeArea < bestFreeArea)
                    {
                        bestIndex = i;
                        bestFreeArea = freeArea;
                    }
                }
            }

            if (bestIndex == -1)
            {
                if (_doubleWidth && _size.X < _maxSize.X)
                {
                    var oldWidth = _size.X;
                    _size.X <<= 1;
                    // If no allocations yet, simply expand the single free area
                    var first = _freeAreas[0];
                    if (_freeAreas.Count == 1 && first.Left == 0 && first.Top == 0 && first.Right == oldWidth &&
                        first.Bottom == _size.Y)
                        first.Right = _size.X;
                    else
                    {
                        var newArea = new IntRect(oldWidth, 0, _size.X, _size.Y);
                        _freeAreas.Add(newArea);
                    }

                    _freeAreas[0] = first;
                }
                else if (!_doubleWidth && _size.Y < _maxSize.Y)
                {
                    var oldHeight = _size.Y;
                    _size.Y <<= 1;
                    // If no allocations yet, simply expand the single free area
                    var first = _freeAreas[0];
                    if (_freeAreas.Count == 1 && first.Left == 0 && first.Top == 0 && first.Right == _size.X &&
                        first.Bottom == oldHeight)
                        first.Bottom = _size.Y;
                    else
                    {
                        var newArea = new IntRect(0, oldHeight, _size.X, _size.Y);
                        _freeAreas.Add(newArea);
                    }

                    _freeAreas[0] = first;
                }
                else
                    return false;

                _doubleWidth = !_doubleWidth;
            }
            else
                break;
        }

        var best = _freeAreas[bestIndex];
        var reserved = new IntRect(best.Left, best.Top, best.Left + width, best.Top + height);
        x = best.Left;
        y = best.Top;

        if (_fastMode)
        {
            // Reserve the area by splitting up the remaining free area
            best.Left = reserved.Right;
            if (best.Height > 2 * height || height >= _size.Y / 2)
            {
                var splitArea = new IntRect(reserved.Left, reserved.Bottom, best.Right, best.Bottom);
                best.Bottom = reserved.Bottom;
                _freeAreas.Add(splitArea);
            }

            _freeAreas[bestIndex] = best;
        }
        else
        {
            // Remove the reserved area from all free areas
            for (var i = 0; i < _freeAreas.Count;)
            {
                if (SplitRect(i, reserved))
                    _freeAreas.RemoveAt(i);
                else
                    ++i;
            }

            Cleanup();
        }

        return true;
    }

    /// <summary>Return the current width.</summary>
    public int Width => _size.X;

    /// <summary>Return the current height.</summary>
    public int Height => _size.Y;

    /// <summary>Return whether uses fast mode. Fast mode uses a simpler allocation scheme which may waste free space, but is OK for eg. fonts.</summary>
    public bool FastMode => _fastMode;

    /// <summary>Remove space from a free rectangle. Return true if the original rectangle should be erased from the free list. Not called in fast mode.</summary>
    private bool SplitRect(int freeAreaIndex, IntRect reserve)
    {
        // Make a copy, as the vector will be modified
        var original = _freeAreas[freeAreaIndex];

        if (reserve.Right > original.Left && reserve.Left < original.Right && reserve.Bottom > original.Top &&
            reserve.Top < original.Bottom)
        {
            // Check for splitting from the right
            if (reserve.Right < original.Right)
            {
                var newRect = original;
                newRect.Left = reserve.Right;
                _freeAreas.Add(newRect);
            }
            // Check for splitting from the left
            if (reserve.Left > original.Left)
            {
                var newRect = original;
                newRect.Right = reserve.Left;
                _freeAreas.Add(newRect);
            }
            // Check for splitting from the bottom
            if (reserve.Bottom < original.Bottom)
            {
                var newRect = original;
                newRect.Top = reserve.Bottom;
                _freeAreas.Add(newRect);
            }
            // Check for splitting from the top
            if (reserve.Top > original.Top)
            {
                var newRect = original;
                newRect.Bottom = reserve.Top;
                _freeAreas.Add(newRect);
            }

            return true;
        }

        return false;
    }

    /// <summary>Clean up redundant free space. Not called in fast mode.</summary>
    private void Cleanup()
    {
        // Remove rects which are contained within another rect
        for (var i = 0; i < _freeAreas.Count;)
        {
            var erased = false;
            for (var j = i + 1; j < _freeAreas.Count;)
            {
                if (_freeAreas[i].Left >= _freeAreas[j].Left &&
                    _freeAreas[i].Top >= _freeAreas[j].Top &&
                    _freeAreas[i].Right <= _freeAreas[j].Right &&
                    _freeAreas[i].Bottom <= _freeAreas[j].Bottom)
                {
                    _freeAreas.RemoveAt(i);
                    erased = true;
                    break;
                }
                if (_freeAreas[j].Left >= _freeAreas[i].Left &&
                    _freeAreas[j].Top >= _freeAreas[i].Top &&
                    _freeAreas[j].Right <= _freeAreas[i].Right &&
                    _freeAreas[j].Bottom <= _freeAreas[i].Bottom)
                    _freeAreas.RemoveAt(j);
                else
                    ++j;
            }

            if (!erased)
                ++i;
        }
    }

    /// <summary>Free rectangles.</summary>
    private readonly List<IntRect> _freeAreas = [];

    /// <summary>Current size.</summary>
    private Vector2i _size;

    /// <summary>Maximum size it allows to grow. It is zero when it is not allowed to grow.</summary>
    private Vector2i _maxSize;

    /// <summary>The dimension use for next growth. Used internally.</summary>
    private bool _doubleWidth = true;

    /// <summary>Fast mode flag.</summary>
    private bool _fastMode = true;
}
