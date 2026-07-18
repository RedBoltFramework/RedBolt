using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

public static class RbfxUnsupportedApisPass
{
    public static int Apply(BindingCompilation compilation)
    {
        var suppressedCount = 0;
        foreach (var function in compilation.Functions.Where(static function => !function.IsIgnored()))
        {
            if (function.CppQualifiedName == "Urho3D::Graph::LoadJSON")
            {
                // The rbfx SDK header declares Graph::LoadJSON(ea::string_view),
                // but libUrho3D.so does not define/export the symbol. Emitting a
                // thunk compiles but leaves libSharpCpp.Rbfx.Native.so unloadable.
                function.SetIgnoreReason("rbfx SDK declares this member but the linked library does not define/export it");
                suppressedCount++;
            }
        }

        return suppressedCount;
    }
}
