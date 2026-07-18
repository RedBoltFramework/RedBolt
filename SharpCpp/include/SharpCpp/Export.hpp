#pragma once

#if defined(_WIN32)
#define SHARPCPP_EXPORT extern "C" __declspec(dllexport)
#else
#define SHARPCPP_EXPORT extern "C" __attribute__((visibility("default")))
#endif
