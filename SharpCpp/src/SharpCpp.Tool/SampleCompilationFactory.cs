using SharpCpp.Model.Ast;

namespace SharpCpp.Tool;

internal static class SampleCompilationFactory
{
    public static BindingCompilation CreateMetadataSample()
    {
        var compilation = new BindingCompilation(
            projectName: "SharpCpp.Sample",
            managedNamespace: "SharpCpp.Sample",
            managedClassName: "SampleBindings",
            nativeLibraryName: "SharpCpp.Sample.Native",
            headerFiles: new[]
            {
                "include/Sample.hpp"
            });

        var sampleContext = new RecordBinding(
            symbolId: "record:SampleContext",
            nativeName: "SampleContext",
            managedName: "SampleContext",
            marshallerTypeName: "SampleContextMarshaller");

        var addNumbers = new FunctionBinding(
            symbolId: "function:add_numbers",
            nativeName: "add_numbers",
            managedName: "AddNumbers",
            returnType: BindingType.Int32,
            entryPointName: "sharpcpp_sample_add_numbers");
        addNumbers.Parameters.Add(new ParameterBinding("parameter:add_numbers:lhs", "lhs", "lhs", BindingType.Int32));
        addNumbers.Parameters.Add(new ParameterBinding("parameter:add_numbers:rhs", "rhs", "rhs", BindingType.Int32));

        var createContext = new FunctionBinding(
            symbolId: "function:create_context",
            nativeName: "create_context",
            managedName: "CreateContext",
            returnType: sampleContext.HandleType,
            entryPointName: "sharpcpp_sample_create_context");

        compilation.Members.Add(sampleContext);
        compilation.Members.Add(addNumbers);
        compilation.Members.Add(createContext);
        return compilation;
    }
}
