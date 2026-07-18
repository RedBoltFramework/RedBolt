using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class BindingCompilationPolymorphismPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records)
        {
            record.IsPolymorphic = false;
        }

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var record in compilation.Records)
            {
                if (record.IsIgnored())
                {
                    continue;
                }

                var isPolymorphic = record.Members
                    .OfType<FunctionBinding>()
                    .Where(static function => !function.IsIgnored())
                    .Any(static function => function.IsVirtual);

                if (!isPolymorphic &&
                    record.EnumerateAllBaseRecords(compilation).Any(static baseRecord => !baseRecord.IsIgnored() && baseRecord.IsPolymorphic))
                {
                    isPolymorphic = true;
                }

                if (record.IsPolymorphic == isPolymorphic)
                {
                    continue;
                }

                record.IsPolymorphic = isPolymorphic;
                changed = true;
            }
        }
    }
}
