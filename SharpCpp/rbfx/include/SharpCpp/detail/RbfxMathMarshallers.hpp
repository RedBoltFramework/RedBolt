#pragma once

#include <Urho3D/Math/Vector2.h>
#include <Urho3D/Math/Vector3.h>
#include <Urho3D/Math/Vector4.h>
#include <Urho3D/Math/Quaternion.h>
#include <Urho3D/Math/StringHash.h>
#include <Urho3D/Math/Color.h>
#include <Urho3D/Math/Matrix2.h>
#include <Urho3D/Math/Matrix3.h>
#include <Urho3D/Math/Matrix3x4.h>
#include <Urho3D/Math/Matrix4.h>
#include <Urho3D/Math/BoundingBox.h>
#include <Urho3D/Math/Plane.h>
#include <Urho3D/Math/Ray.h>
#include <Urho3D/Math/Sphere.h>
#include <Urho3D/Math/Rect.h>
#include <Urho3D/Math/Transform.h>

namespace SharpCpp
{

// ─── Vector2 ─────────────────────────────────────────────────────────

struct Vector2Abi { float X; float Y; };
inline Urho3D::Vector2 ToCpp(const Vector2Abi& v) { return {v.X, v.Y}; }
inline Vector2Abi ToAbi(const Urho3D::Vector2& v) { return {v.x_, v.y_}; }

// ─── Vector3 ─────────────────────────────────────────────────────────

struct Vector3Abi { float X; float Y; float Z; };
inline Urho3D::Vector3 ToCpp(const Vector3Abi& v) { return {v.X, v.Y, v.Z}; }
inline Vector3Abi ToAbi(const Urho3D::Vector3& v) { return {v.x_, v.y_, v.z_}; }

// ─── Vector4 ─────────────────────────────────────────────────────────

struct Vector4Abi { float X; float Y; float Z; float W; };
inline Urho3D::Vector4 ToCpp(const Vector4Abi& v) { return {v.X, v.Y, v.Z, v.W}; }
inline Vector4Abi ToAbi(const Urho3D::Vector4& v) { return {v.x_, v.y_, v.z_, v.w_}; }

// ─── IntVector2 ──────────────────────────────────────────────────────

struct IntVector2Abi { int X; int Y; };
inline Urho3D::IntVector2 ToCpp(const IntVector2Abi& v) { return {v.X, v.Y}; }
inline IntVector2Abi ToAbi(const Urho3D::IntVector2& v) { return {v.x_, v.y_}; }

// ─── IntVector3 ──────────────────────────────────────────────────────

struct IntVector3Abi { int X; int Y; int Z; };
inline Urho3D::IntVector3 ToCpp(const IntVector3Abi& v) { return {v.X, v.Y, v.Z}; }
inline IntVector3Abi ToAbi(const Urho3D::IntVector3& v) { return {v.x_, v.y_, v.z_}; }

// ─── DoubleVector2 ───────────────────────────────────────────────────

struct DoubleVector2Abi { double X; double Y; };
inline Urho3D::DoubleVector2 ToCpp(const DoubleVector2Abi& v) { return {v.X, v.Y}; }
inline DoubleVector2Abi ToAbi(const Urho3D::DoubleVector2& v) { return {v.x_, v.y_}; }

// ─── DoubleVector3 ───────────────────────────────────────────────────

struct DoubleVector3Abi { double X; double Y; double Z; };
inline Urho3D::DoubleVector3 ToCpp(const DoubleVector3Abi& v) { return {v.X, v.Y, v.Z}; }
inline DoubleVector3Abi ToAbi(const Urho3D::DoubleVector3& v) { return {v.x_, v.y_, v.z_}; }

// ─── Quaternion ──────────────────────────────────────────────────────

struct QuaternionAbi { float W; float X; float Y; float Z; };
inline Urho3D::Quaternion ToCpp(const QuaternionAbi& v) { return {v.W, v.X, v.Y, v.Z}; }
inline QuaternionAbi ToAbi(const Urho3D::Quaternion& v) { return {v.w_, v.x_, v.y_, v.z_}; }

// ─── Color ───────────────────────────────────────────────────────────

struct ColorAbi { float R; float G; float B; float A; };
inline Urho3D::Color ToCpp(const ColorAbi& v) { return {v.R, v.G, v.B, v.A}; }
inline ColorAbi ToAbi(const Urho3D::Color& v) { return {v.r_, v.g_, v.b_, v.a_}; }

// ─── StringHash ──────────────────────────────────────────────────────

// StringHash is a simple unsigned-int wrapper — no ABI struct needed.
using StringHashAbi = unsigned int;
inline Urho3D::StringHash ToCpp(StringHashAbi v) { return Urho3D::StringHash(v); }
inline StringHashAbi ToAbi(const Urho3D::StringHash& v) { return v.Value(); }

} // namespace SharpCpp
