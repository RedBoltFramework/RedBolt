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
using System.Collections.Generic;

using RedBolt.IO;
using RedBolt.Resource;

/// <summary>Project configuration flags.</summary>
[Flags]
public enum ProjectFlag
{
    None = 0,
    /// <summary>Project should not modify any files unless explicitly requested by the user.</summary>
    ReadOnly = 1 << 0,
    /// <summary>Project should not spawn any additional processes unless explicitly requested by the user.</summary>
    SingleProcess = 1 << 1,
}

/// <summary>Result of the graceful project close.</summary>
public enum CloseProjectResult
{
    Undefined,
    Closed,
    Canceled
}

/// <summary>File type analysis context.</summary>
public class AnalyzeFileContext : IDisposable
{
    public AbstractFile? BinaryFile;
    public XMLFile? XmlFile;
    public JSONFile? JsonFile;

    public bool HasXMLRoot(string root)
    {
        if (XmlFile is null)
            return false;

        using var xmlRoot = XmlFile.GetRoot();
        return string.Equals(xmlRoot.Name, root, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasXMLRoot(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (HasXMLRoot(root))
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        JsonFile?.Dispose();
        JsonFile = null;
        XmlFile?.Dispose();
        XmlFile = null;
        BinaryFile?.Dispose();
        BinaryFile = null;
        GC.SuppressFinalize(this);
    }
}
