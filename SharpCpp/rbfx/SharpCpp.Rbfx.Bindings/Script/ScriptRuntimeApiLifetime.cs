using Urho3D.Core;

namespace Urho3D.Script;

public abstract partial class ScriptRuntimeApi
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Context.ClearRuntimeApiIfCurrent(this);
        }

        base.Dispose(disposing);
    }
}
