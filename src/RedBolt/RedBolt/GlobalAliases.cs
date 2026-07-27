global using Vector2f = RedBolt.Math.BaseRealVector2<float>;
global using Vector2d = RedBolt.Math.BaseRealVector2<double>;
global using Vector2i = RedBolt.Math.BaseIntegerVector2<int>;
global using Vector3f = RedBolt.Math.BaseRealVector3<float>;
global using Vector3d = RedBolt.Math.BaseRealVector3<double>;
global using Vector3i = RedBolt.Math.BaseIntegerVector3<int>;
global using Vector4f = RedBolt.Math.Vector4f;
global using FloatRange = RedBolt.Math.NumericRange<float>;
global using UintRange = RedBolt.Math.NumericRange<uint>;
global using IntVector2 = RedBolt.Math.BaseIntegerVector2<int>;
global using IntVector3 = RedBolt.Math.BaseIntegerVector3<int>;
global using TextureFormat = RedBolt.Bindings.Diligent.TextureFormat;
/// <summary>
/// Helper for pipeline state creation.
/// Keep entire string because it should be used only on startup.
/// </summary>
global using NamedSamplerStateDesc = (string Name, RedBolt.RenderAPI.SamplerStateDesc Desc);
