#pragma once

#include "SampleMarshallers.h"

namespace Test
{
struct Vector2
{
    int x;
    int y;
};

struct Object
{
    int GetId() const;
};

struct StringHash
{
    unsigned Value() const;
};

struct Variant
{
    int GetType() const;
};

using VariantMap = ea::hash_map<StringHash, Variant, int, int, int>;

struct CallbackOwner
{
    using HandlerFunction = ea::function<void(Object* receiver, StringHash eventType, VariantMap& eventData)>;

    CallbackOwner(Object* receiver, HandlerFunction handler);
};

void ForEach(std::function<void(int, float)> callback);
int Transform(std::function<int(int)> callback);
void SetCallback(std::function<void()> callback);
void SetDefaultCallback(std::function<unsigned()> callback = nullptr);
void UseVectorCallback(std::function<void(const Vector2&)> callback);
void UseGeneratedDelegate([[sharpcpp::delegate]] std::function<void(int)> callback);
void UseNamedDelegate([[sharpcpp::delegate("ProgressCallback")]] std::function<void(int)> callback);
void UseInvalidDelegate([[sharpcpp::delegate("not valid")]] std::function<void(int)> callback);
void UseInvalidExternalDelegate([[sharpcpp::delegate("global::")]] std::function<void(int)> callback);
void UseDelegateOnNonCallback([[sharpcpp::delegate]] int value);
}
