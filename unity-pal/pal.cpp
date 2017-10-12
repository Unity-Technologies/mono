#include <config.h>

#include <os/c-api/il2cpp-config-platforms.h>

#include <os/c-api/SystemCertificates.cpp>

#if IL2CPP_TARGET_WINDOWS_DESKTOP
#include <os/Win32/SystemCertificates.cpp>
#elif IL2CPP_TARGET_OSX
#include <os/OSX/SystemCertificates.cpp>
#elif IL2CPP_TARGET_LINUX
#include <os/Posix/SystemCertificates.cpp>
#elif IL2CPP_PLATFORM_SUPPORTS_SYSTEM_CERTIFICATES
#error please include platform implementation
#else
#include <os/Generic/SystemCertificates.cpp>
#endif
