global using Vector2f = RedBolt.BaseRealVector2<float>;
global using Vector2d = RedBolt.BaseRealVector2<double>;
global using Vector2i = RedBolt.BaseIntegerVector2<int>;
global using Vector3f = RedBolt.BaseRealVector3<float>;
global using Vector3d = RedBolt.BaseRealVector3<double>;
global using Vector3i = RedBolt.BaseIntegerVector3<int>;
global using Vector4f = RedBolt.Vector4f;
global using FloatRange = RedBolt.NumericRange<float>;
global using UintRange = RedBolt.NumericRange<uint>;
global using IntVector2 = RedBolt.BaseIntegerVector2<int>;
global using IntVector3 = RedBolt.BaseIntegerVector3<int>;
global using TextureFormat = RedBolt.Bindings.Diligent.TextureFormat;
/// <summary>
/// Helper for pipeline state creation.
/// Keep entire string because it should be used only on startup.
/// </summary>
global using NamedSamplerStateDesc = (string Name, RedBolt.SamplerStateDesc Desc);
