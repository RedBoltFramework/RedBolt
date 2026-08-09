#pragma once

#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0A00
#endif
#include <windows.h>

#if defined(__MINGW32__) && !defined(SetThreadDescription)
#ifdef __cplusplus
extern "C"
#endif
WINBASEAPI HRESULT WINAPI SetThreadDescription(HANDLE hThread, PCWSTR lpThreadDescription);
#endif
