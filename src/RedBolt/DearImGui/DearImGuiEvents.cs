using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt;

public static class DearImGuiEvents
{
    public struct EndRenderingDearImGuiEvent
    {
    }

    public struct ConsoleClosedEvent
    {
    }

    public struct AttributeInspectorMenuEvent
    {
        public Serializable? Serializable; // Serializable pointer
        public AttributeInfo? AttributeInfo; // AttributeInfo pointer
    }

    public struct AttributeInspectorValueModifiedEvent
    {
        public Serializable? Serializable; // Serializable pointer
        public AttributeInfo? AttributeInfo; // AttributeInfo pointer
        public Variant OldValue; // Variant
        public Variant NewValue; // Variant
        public uint Reason; // unsigned
    }

    public struct AttributeInspectorAttributeEvent
    {
        public Serializable? Serializable; // Serializable pointer
        public AttributeInfo? AttributeInfo; // AttributeInfo pointer
        public Color Color; // Color
        public bool Hidden; // Boolean
        public string Tooltip; // String
        public int ValueKind; // int
    }

    public struct GizmoNodeModifiedEvent
    {
        public Node? Node; // Node pointer
        public Matrix3x4 OldTransform; // Matrix3x4
        public Matrix3x4 NewTransform; // Matrix3x4
    }

    public struct GizmoSelectionChangedEvent
    {
    }
}
