using System.Threading;

namespace RedBolt;

public static class Profiler
{
    public const uint ColorEvents = 0xb26d19;
    public const uint ColorResources = 0x006b82;

    /// <summary>
    /// Set profiler-visible name for the current thread.
    /// </summary>
    // C++: void SetProfilerThreadName(const char* name)
    // C++: {
    // C++: #if URHO3D_PROFILING
    // C++:     tracy::SetThreadName(name);
    // C++: #endif
    // C++: }
    public static void SetProfilerThreadName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var currentThread = Thread.CurrentThread;
        if (currentThread.Name is null)
            currentThread.Name = name;
    }

    // TODO: port - C++ macros:
    // #define URHO3D_PROFILE_FUNCTION()                   ZoneScopedN(__FUNCTION__)
    // #define URHO3D_PROFILE_C(name, color)               ZoneScopedNC(name, color)
    // #define URHO3D_PROFILE(name)                        ZoneScopedN(name)
    // #define URHO3D_PROFILE_THREAD(name)                 Urho3D::SetProfilerThreadName(name)
    // #define URHO3D_PROFILE_VALUE(name, value)           TracyPlot(name, value)
    // #define URHO3D_PROFILE_FRAME()                      FrameMark
    // #define URHO3D_PROFILE_MESSAGE(txt, len)            TracyMessage(txt, len)
    // #define URHO3D_PROFILE_ZONENAME(txt, len)           ZoneName(txt, len)
    // #if URHO3D_PROFILING
    // #   define URHO3D_PROFILE_SRC_LOCATION(title)       [] () -> const tracy::SourceLocationData* { static const tracy::SourceLocationData srcloc { nullptr, title, __FILE__, __LINE__, 0 }; return &srcloc; }()
    // #   define URHO3D_PROFILE_MUTEX(name)               ProfiledMutex name{URHO3D_PROFILE_SRC_LOCATION_DATA(#name)}
    // #else
    // #   define URHO3D_PROFILE_SRC_LOCATION_DATA(title)
    // #   define URHO3D_PROFILE_MUTEX(name)               Mutex name{}
    // #endif
}
