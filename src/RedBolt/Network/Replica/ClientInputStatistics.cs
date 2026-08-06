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

/// <summary>Utility to evaluate client input quality and preferred input buffering.</summary>
public class ClientInputStatistics
{
    public ClientInputStatistics(uint windowSize, uint maxInputLoss)
    {
        _windowSize = windowSize;
        _maxInputLoss = unchecked((int)maxInputLoss);
    }

    /// <summary>Notify that the input was received for given frame.</summary>
    public void OnInputReceived(NetworkFrame frame)
    {
        if (!_latestInputFrame.HasValue)
        {
            _latestInputFrame = frame;
            return;
        }

        var delta = unchecked((int)(frame - _latestInputFrame.Value));
        _latestInputFrame = frame;
        if (delta <= 0)
            return;

        var numLostFrames = System.Math.Min(delta, _maxInputLoss) - 1;
        for (var i = 0; i <= numLostFrames; ++i)
        {
            _numLostFrames.Enqueue((uint)i);
            if (_numLostFrames.Count > _windowSize)
                _numLostFrames.Dequeue();
        }

        UpdateHistogram();
        _bufferSize = GetMaxRepeatedLoss();
    }

    public uint RecommendedBufferSize => _bufferSize;

    private void UpdateHistogram()
    {
        _histogram.Clear();
        foreach (var numLost in _numLostFrames)
        {
            while (_histogram.Count <= numLost)
                _histogram.Add(0);
            ++_histogram[(int)numLost];
        }
    }

    private uint GetMaxRepeatedLoss()
    {
        for (var i = _histogram.Count - 1; i >= 0; --i)
        {
            if (_histogram[i] >= 2)
                return (uint)i;
        }
        return 0;
    }

    private readonly int _maxInputLoss;
    private readonly uint _windowSize;

    private NetworkFrame? _latestInputFrame;
    private readonly Queue<uint> _numLostFrames = new();

    private readonly List<uint> _histogram = [];
    private uint _bufferSize;
}
