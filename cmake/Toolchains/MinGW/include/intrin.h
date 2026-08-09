#pragma once

#if defined(__clang__)
#include <x86intrin.h>
#else
#include_next <intrin.h>
#endif
