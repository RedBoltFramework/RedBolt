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

namespace RedBolt.Editor.Project;

using System;

using RedBolt.Core;
using RedBolt.Utility;

/// <summary>Interface of file and folder factory.</summary>
public abstract class ResourceFactory : RedObject
{
    public delegate (bool IsValid, string ExtraLine) FileNameChecker(string filePath, string fileName);

    public ResourceFactory(int group, string title)
    {
        _group = group;
        _title = title;
    }

    public virtual bool IsEnabled(FileSystemEntry parentEntry) => true;

    public abstract void Open(string baseFilePath, string baseResourcePath);

    public abstract void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit);

    public abstract void CommitAndClose();

    public virtual void DiscardAndClose()
    {
    }

    public int Group => _group;

    public string Title => _title;

    /// <summary>Order factories by group and name, usually for menu rendering.</summary>
    public static bool Compare(ResourceFactory lhs, ResourceFactory rhs)
    {
        return lhs._group < rhs._group
            || (lhs._group == rhs._group && string.CompareOrdinal(lhs._title, rhs._title) < 0);
    }

    private readonly int _group;
    private readonly string _title;
}
