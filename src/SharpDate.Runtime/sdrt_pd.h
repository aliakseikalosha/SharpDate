// SPDX-License-Identifier: 0BSD
// This file restates interface facts from the Playdate SDK headers (names, values, layouts);
// those remain Panic's. SharpDate's own contributions to it are 0BSD. See LICENSING.md.

// The few parts of the Playdate C API that the SharpDate runtime uses, declared here so the runtime
// compiles without the SDK headers (which need a C library: stdlib.h, stdio.h, math.h...). That
// lets the Windows Simulator build use a bare LLVM install. Managed code uses the full generated
// bindings (src/SharpDate/Native/PlaydateApi.g.cs).
//
// The layout follows pd_api.h / pd_api_sys.h; only leading members are declared, and the
// Playdate API only ever appends to these structs.

#ifndef SHARPDATE_PD_H
#define SHARPDATE_PD_H

#include <stddef.h>
#include <stdint.h>

typedef enum
{
	kEventInit,
	kEventInitLua,
	kEventLock,
	kEventUnlock,
	kEventPause,
	kEventResume,
	kEventTerminate,
	kEventKeyPressed,
	kEventKeyReleased,
	kEventLowPower,
	kEventMirrorStarted,
	kEventMirrorEnded
} PDSystemEvent;

struct playdate_sys
{
	void* (*realloc)(void* ptr, size_t size);
	int (*formatString)(char** ret, const char* fmt, ...);
	void (*logToConsole)(const char* fmt, ...);
	void (*error)(const char* fmt, ...);
	int (*getLanguage)(void);   // PDLanguage; not used by the runtime
	unsigned int (*getCurrentTimeMilliseconds)(void);
	// ... (not used by the runtime)
};

typedef struct PlaydateAPI
{
	const struct playdate_sys* system;
	// ... (not used by the runtime)
} PlaydateAPI;

#endif
