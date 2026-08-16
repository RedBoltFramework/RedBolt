// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;

internal readonly record struct FixedWidthAliasProjection(
    BindingPrimitiveKind PrimitiveKind,
    string ManagedTypeName);

internal static class FixedWidthAliasNormalizer
{
    public static void Normalize(
        BindingCompilation compilation,
        IReadOnlyDictionary<string, FixedWidthAliasProjection> projections)
    {
        for (var index = 0; index < compilation.TypeAliases.Count; ++index)
        {
            var alias = compilation.TypeAliases[index];
            if (!TryGetProjection(alias.CppQualifiedName, projections, out var projection))
                continue;

            compilation.TypeAliases[index] = alias with
            {
                TargetType = ProjectType(alias.TargetType, projection)
            };
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = NormalizeType(function.ReturnType, function.CppReturnTypeName, projections);
            foreach (var parameter in function.Parameters)
                parameter.Type = NormalizeType(parameter.Type, parameter.CppTypeName, projections);
            NormalizeGenericInstantiations(function.SupportedGenericInstantiations, projections);
        }

        foreach (var record in compilation.Records)
        {
            foreach (var field in record.Members.OfType<FieldBinding>())
                field.Type = NormalizeType(field.Type, field.CppTypeName, projections);
            NormalizeGenericInstantiations(record.SupportedGenericInstantiations, projections);
        }

        compilation.InvalidateCaches();
    }

    public static BindingType ProjectType(BindingType type, FixedWidthAliasProjection projection)
    {
        var projectedType = BindingType.CreateConfigured(
            type.CppTypeName,
            projection.ManagedTypeName,
            type.MarshallerTypeName,
            type.ManagedMarshaller,
            projection.PrimitiveKind,
            type.GenericArguments,
            type.Marshaller.CppIsReference);
        projectedType = PreserveTypeMetadata(type, projectedType);
        if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
            projectedType = projectedType.WithFixedArray(type.FixedArrayElementType, type.FixedArrayLength.Value);
        if (type.DynamicArrayElementType is not null)
            projectedType = projectedType.WithDynamicArray(type.DynamicArrayElementType);
        return projectedType;
    }

    private static void NormalizeGenericInstantiations(
        IList<RecordGenericInstantiationBinding> instantiations,
        IReadOnlyDictionary<string, FixedWidthAliasProjection> projections)
    {
        foreach (var instantiation in instantiations)
        {
            for (var index = 0; index < instantiation.TypeArguments.Count; ++index)
                instantiation.UpdateTypeArgument(index, NormalizeType(instantiation.TypeArguments[index], projections));
        }
    }

    private static BindingType NormalizeType(
        BindingType type,
        string cppTypeName,
        IReadOnlyDictionary<string, FixedWidthAliasProjection> projections)
    {
        type = NormalizeType(type, projections);
        return TryGetProjection(cppTypeName, projections, out var projection)
            ? ProjectType(type, projection)
            : type;
    }

    private static BindingType NormalizeType(
        BindingType type,
        IReadOnlyDictionary<string, FixedWidthAliasProjection> projections)
    {
        var genericArguments = type.IsCallback
            ? type.GenericArguments
            : type.GenericArguments.Select(argument => NormalizeType(argument, projections)).ToArray();
        if (!type.IsCallback && !type.GenericArguments.SequenceEqual(genericArguments))
            type = type.WithGenericArguments(genericArguments);

        if (type.IsCallback)
        {
            var callbackReturnType = type.CallbackReturnType is null
                ? null
                : NormalizeType(type.CallbackReturnType, projections);
            var callbackParameterTypes = type.CallbackParameterTypes
                .Select(parameter => NormalizeType(parameter, projections))
                .ToArray();
            if (callbackReturnType is not null &&
                (!Equals(type.CallbackReturnType, callbackReturnType) ||
                 !type.CallbackParameterTypes.SequenceEqual(callbackParameterTypes)))
            {
                type = PreserveTypeMetadata(
                    type,
                    type.WithCallbackSignature(callbackReturnType, callbackParameterTypes));
            }
        }

        if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
        {
            var elementType = NormalizeType(type.FixedArrayElementType, projections);
            if (!Equals(type.FixedArrayElementType, elementType))
                type = type.WithFixedArray(elementType, type.FixedArrayLength.Value);
        }

        if (type.DynamicArrayElementType is not null)
        {
            var elementType = NormalizeType(type.DynamicArrayElementType, projections);
            if (!Equals(type.DynamicArrayElementType, elementType))
                type = type.WithDynamicArray(elementType);
        }

        return TryGetProjection(type.AliasCppQualifiedName, projections, out var projection)
            ? ProjectType(type, projection)
            : type;
    }

    private static BindingType PreserveTypeMetadata(BindingType source, BindingType target)
    {
        target = target
            .WithCppIsRecordValue(source.Marshaller.CppIsRecordValue)
            .WithCppPointerIsRecordValueProjection(source.Marshaller.CppPointerIsRecordValueProjection)
            .WithCppIsReference(source.Marshaller.CppIsReference)
            .WithManagedTypeFormatOverride(source.ManagedTypeFormatOverride);
        return source.AliasCppQualifiedName is not null && source.AliasNativeName is not null
            ? target.WithAliasSurface(source.AliasCppQualifiedName, source.AliasNativeName)
            : target;
    }

    private static bool TryGetProjection(
        string? cppQualifiedName,
        IReadOnlyDictionary<string, FixedWidthAliasProjection> projections,
        out FixedWidthAliasProjection projection)
    {
        if (cppQualifiedName is not null && projections.TryGetValue(cppQualifiedName, out projection))
            return true;

        projection = default;
        return false;
    }
}
