// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// SharpDate native runtime.
//
// This is the native half of the "Zero" runtime profile: it is linked together with the object file
// that ILC (the .NET NativeAOT compiler) produces from the game's assemblies and SharpDate.CoreLib.
// It provides object allocation, a non-moving mark-and-sweep garbage collector, GC handles,
// the Playdate entry point, and the small set of helper functions that compiled managed code calls.
//
// The same file is compiled for the Simulator (clang, 64-bit macOS / Linux / Windows host) and for
// the device (arm-none-eabi-gcc, Cortex-M7). Platform-specific stubs live in sdrt_*.S.
//
// Only freestanding headers are used (no C library headers), so the Windows build needs nothing
// but LLVM; the C library functions the runtime and compiled code call (memcpy, sinf, ...) come
// from the platform: newlib on the device, libc on macOS/Linux, ucrtbase.dll on Windows.

#include <stdint.h>
#include <stddef.h>
#include <stdarg.h>

#include "sdrt_pd.h"

#if defined(__arm__)
#define SD_DEVICE 1
#else
#define SD_DEVICE 0
#endif

#if defined(_WIN32)
#define SD_WINDOWS 1
#else
#define SD_WINDOWS 0
#endif

#if !SD_DEVICE && !SD_WINDOWS
// pthread_t is pointer-sized on macOS and Linux; only equality is needed.
typedef uintptr_t sd_thread_t;
extern sd_thread_t pthread_self(void);
#endif

void* memcpy(void* dst, const void* src, size_t n);
void* memset(void* dst, int c, size_t n);

#if SD_WINDOWS
// Exported from the DLL: only the entry point. Everything else is linked statically.
#define SD_EXPORT
#define SD_ENTRY __declspec(dllexport)
// Referenced by MSVC-ABI code that uses floating point; normally defined by the static CRT.
int _fltused = 0;
#else
#define SD_EXPORT __attribute__((visibility("default")))
#define SD_ENTRY SD_EXPORT
#endif
#define SD_NOINLINE __attribute__((noinline))
#define SD_UNUSED __attribute__((unused))

// ---------------------------------------------------------------------------------------------
// Managed entry points (defined in SharpDate.CoreLib / SharpDate via [RuntimeExport] or
// [UnmanagedCallersOnly(EntryPoint = ...)]).
// ---------------------------------------------------------------------------------------------

extern void SharpDate_InitializeModules(void** headers, int count);
extern void SharpDate_ReportUnhandledException(void* exception);
extern int SharpDate_EventHandler(PlaydateAPI* pd, PDSystemEvent event, uint32_t arg);

// Emitted by ILC: the ReadyToRun header describing the compiled module.
extern char __ReadyToRunHeader;

// ---------------------------------------------------------------------------------------------
// Globals
// ---------------------------------------------------------------------------------------------

static PlaydateAPI* sd_pd;
static int sd_initialized;

void sd_fail(const char* fmt, ...);

// ---------------------------------------------------------------------------------------------
// Raw memory (Playdate allocator)
// ---------------------------------------------------------------------------------------------

SD_EXPORT void* sd_alloc(size_t size)
{
	void* p = sd_pd->system->realloc(NULL, size);
	if (p == NULL && size != 0)
		sd_fail("SharpDate: out of native memory (%u bytes)", (unsigned)size);
	return p;
}

SD_EXPORT void* sd_realloc(void* ptr, size_t size)
{
	void* p = sd_pd->system->realloc(ptr, size);
	if (p == NULL && size != 0)
		sd_fail("SharpDate: out of native memory (%u bytes)", (unsigned)size);
	return p;
}

SD_EXPORT void sd_free(void* ptr)
{
	if (ptr != NULL)
		sd_pd->system->realloc(ptr, 0);
}

// ---------------------------------------------------------------------------------------------
// Diagnostics
// ---------------------------------------------------------------------------------------------

SD_EXPORT void sd_log(const char* utf8, int length)
{
	(void)length;
	if (sd_pd != NULL)
		sd_pd->system->logToConsole("%s", utf8);
}

void sd_fail(const char* fmt, ...)
{
	char buffer[256];
	va_list args;
	va_start(args, fmt);
	int n = 0;
	// Minimal formatter (%s, %u, %d, %x, %p) so the runtime does not depend on printf.
	for (const char* f = fmt; *f && n < (int)sizeof(buffer) - 24; f++)
	{
		if (*f != '%')
		{
			buffer[n++] = *f;
			continue;
		}
		f++;
		if (*f == 's')
		{
			const char* s = va_arg(args, const char*);
			if (s == NULL) s = "(null)";
			while (*s && n < (int)sizeof(buffer) - 24) buffer[n++] = *s++;
		}
		else if (*f == 'u' || *f == 'd' || *f == 'x' || *f == 'p')
		{
			uintptr_t v = (*f == 'p') ? (uintptr_t)va_arg(args, void*) : (uintptr_t)va_arg(args, unsigned);
			if (*f == 'd' && (int)v < 0) { buffer[n++] = '-'; v = (uintptr_t)(-(intptr_t)(int)v); }
			unsigned base = (*f == 'x' || *f == 'p') ? 16 : 10;
			char tmp[20];
			int k = 0;
			do { unsigned d = (unsigned)(v % base); tmp[k++] = (char)(d < 10 ? '0' + d : 'a' + d - 10); v /= base; } while (v != 0);
			while (k > 0) buffer[n++] = tmp[--k];
		}
		else
		{
			buffer[n++] = *f;
		}
	}
	buffer[n] = 0;
	va_end(args);

	if (sd_pd != NULL)
		sd_pd->system->error("%s", buffer);
	for (;;) { }
}

// Called by managed Environment.FailFast with a UTF-16 message.
SD_EXPORT void sd_fail_fast(const uint16_t* message, int length)
{
	char buffer[240];
	int n = 0;
	if (message == NULL)
	{
		sd_fail("SharpDate: fatal error");
	}
	for (int i = 0; i < length && n < (int)sizeof(buffer) - 4; i++)
	{
		uint32_t c = message[i];
		if (c < 0x80) buffer[n++] = (char)c;
		else if (c < 0x800) { buffer[n++] = (char)(0xC0 | (c >> 6)); buffer[n++] = (char)(0x80 | (c & 0x3F)); }
		else { buffer[n++] = (char)(0xE0 | (c >> 12)); buffer[n++] = (char)(0x80 | ((c >> 6) & 0x3F)); buffer[n++] = (char)(0x80 | (c & 0x3F)); }
	}
	buffer[n] = 0;
	sd_fail("%s", buffer);
}

// Non-variadic entry points for the variadic Playdate logging functions (called from SharpDate.dll).
SD_EXPORT void SharpDate_LogToConsole(const char* message)
{
	if (sd_pd != NULL)
		sd_pd->system->logToConsole("%s", message != NULL ? message : "");
}

SD_EXPORT void SharpDate_Error(const char* message)
{
	if (sd_pd != NULL)
		sd_pd->system->error("%s", message != NULL ? message : "");
}

SD_EXPORT uint32_t sd_ticks_ms(void)
{
	return sd_pd != NULL ? sd_pd->system->getCurrentTimeMilliseconds() : 0;
}

// ---------------------------------------------------------------------------------------------
// MethodTable access (layout is a contract with ILC; see SharpDate.CoreLib MethodTable.cs)
// ---------------------------------------------------------------------------------------------

typedef struct MethodTable
{
	uint32_t flags;          // low 16 bits: component size when HasComponentSize
	uint32_t baseSize;
	struct MethodTable* relatedType;
	uint16_t numVtableSlots;
	uint16_t numInterfaces;
	uint32_t hashCode;
} MethodTable;

#define MT_HAS_COMPONENT_SIZE 0x80000000u
#define MT_HAS_POINTERS       0x01000000u

static inline uint32_t mt_component_size(const MethodTable* mt)
{
	return (mt->flags & MT_HAS_COMPONENT_SIZE) ? (mt->flags & 0xFFFF) : 0;
}

#define PTR_SIZE ((uintptr_t)sizeof(void*))
#define ALIGN_UP(x, a) (((x) + ((a) - 1)) & ~((uintptr_t)(a) - 1))

// Size of an object as the GC sees it: BaseSize (which includes the object header slot that
// precedes the MethodTable pointer) plus the variable-length payload, pointer aligned.
static inline uintptr_t object_size(void* obj)
{
	MethodTable* mt = *(MethodTable**)obj;
	uintptr_t size = mt->baseSize;
	uint32_t cs = mt_component_size(mt);
	if (cs != 0)
		size += (uintptr_t)(*(uint32_t*)((char*)obj + PTR_SIZE)) * cs;
	return ALIGN_UP(size, PTR_SIZE);
}

// ---------------------------------------------------------------------------------------------
// Garbage-collected heap
//
// Each object is a separate block from the Playdate allocator:
//
//     [padding][GCHeader][ObjHeader slot][MethodTable*][fields ...]
//                                         ^ object reference
//
// All live objects are tracked in sd_objects (unsorted between collections, sorted during one).
// Collection = mark (precise from roots and object fields, conservative from the native stack)
// followed by sweep. Objects never move.
// ---------------------------------------------------------------------------------------------

typedef struct GCHeader
{
	void* block;
	uint32_t size;   // object_size() at allocation (bytes from ObjHeader slot to end)
	uint32_t mark;
} GCHeader;

static inline GCHeader* header_of(void* obj)
{
	return (GCHeader*)((char*)obj - PTR_SIZE - sizeof(GCHeader));
}

static void** sd_objects;
static size_t sd_object_count;
static size_t sd_object_capacity;

static size_t sd_live_bytes;
static size_t sd_bytes_since_gc;
static size_t sd_gc_threshold = 256 * 1024;
static int sd_gc_count;
static int sd_in_gc;

// Explicit roots: GC static bases (registered at startup by the managed module initializer).
static void** sd_roots;
static size_t sd_root_count;
static size_t sd_root_capacity;

// Native stack range for conservative scanning (stack grows down on all supported targets).
static uintptr_t sd_stack_top;
static int sd_managed_depth;
#if SD_WINDOWS
static uintptr_t sd_main_thread;

// The current thread's id from the TEB (ClientId.UniqueThread), without importing kernel32.
static uintptr_t current_thread_id(void)
{
	uintptr_t id;
#if defined(__x86_64__)
	__asm__ volatile("movq %%gs:0x48, %0" : "=r"(id));
#else
	__asm__ volatile("ldr %0, [x18, #0x48]" : "=r"(id));
#endif
	return id;
}
#elif !SD_DEVICE
static sd_thread_t sd_main_thread;
#endif

static int on_main_context(void)
{
#if SD_DEVICE
	uint32_t ipsr;
	__asm__ volatile("mrs %0, ipsr" : "=r"(ipsr));
	return ipsr == 0; // not inside an interrupt handler (audio callbacks)
#elif SD_WINDOWS
	return current_thread_id() == sd_main_thread;
#else
	return pthread_self() == sd_main_thread;
#endif
}

static void track_object(void* obj)
{
	if (sd_object_count == sd_object_capacity)
	{
		size_t newCapacity = sd_object_capacity == 0 ? 1024 : sd_object_capacity * 2;
		sd_objects = (void**)sd_realloc(sd_objects, newCapacity * sizeof(void*));
		sd_object_capacity = newCapacity;
	}
	sd_objects[sd_object_count++] = obj;
}

SD_EXPORT void sd_gc_collect(void);

static void* gc_alloc(MethodTable* mt, uintptr_t size, int align8, int misalign)
{
	if (!on_main_context())
		sd_fail("SharpDate: managed allocation from an audio callback is not allowed");

	if (sd_bytes_since_gc >= sd_gc_threshold && !sd_in_gc)
		sd_gc_collect();

	// Reserve room for the header, the object header slot and up to 8 bytes of alignment slack.
	uintptr_t total = size + sizeof(GCHeader) + 8;
	void* block = sd_pd->system->realloc(NULL, total);
	if (block == NULL)
	{
		sd_gc_collect();
		block = sd_pd->system->realloc(NULL, total);
		if (block == NULL)
			sd_fail("SharpDate: out of memory allocating %u bytes (live %u)", (unsigned)size, (unsigned)sd_live_bytes);
	}

	// size includes the ObjHeader slot; the object reference points just past it.
	uintptr_t obj = (uintptr_t)block + sizeof(GCHeader) + PTR_SIZE;
	if (align8 || misalign)
	{
		uintptr_t want = misalign ? 4 : 0;
		while ((obj & 7) != want)
			obj += 4;
	}
	else
	{
		obj = ALIGN_UP(obj, PTR_SIZE);
	}

	memset((void*)(obj - PTR_SIZE), 0, size);
	*(MethodTable**)obj = mt;

	GCHeader* h = header_of((void*)obj);
	h->block = block;
	h->size = (uint32_t)size;
	h->mark = 0;

	track_object((void*)obj);
	sd_live_bytes += size;
	sd_bytes_since_gc += size;
	return (void*)obj;
}

static inline int requires_align8(MethodTable* mt)
{
	return sizeof(void*) == 4 && !(mt->flags & MT_HAS_COMPONENT_SIZE) && (mt->flags & 0x1000);
}

SD_EXPORT void* RhpNewFast(MethodTable* mt)
{
	return gc_alloc(mt, ALIGN_UP(mt->baseSize, PTR_SIZE), requires_align8(mt), 0);
}

SD_EXPORT void* RhpNewFinalizable(MethodTable* mt) { return RhpNewFast(mt); }
SD_EXPORT void* RhpNewFastAlign8(MethodTable* mt) { return gc_alloc(mt, ALIGN_UP(mt->baseSize, PTR_SIZE), 1, 0); }
SD_EXPORT void* RhpNewFinalizableAlign8(MethodTable* mt) { return RhpNewFastAlign8(mt); }
SD_EXPORT void* RhpNewFastMisalign(MethodTable* mt) { return gc_alloc(mt, ALIGN_UP(mt->baseSize, PTR_SIZE), 0, 1); }

static void* new_array(MethodTable* mt, intptr_t length, int align8)
{
	if (length < 0)
		sd_fail("SharpDate: negative array size");
	uint64_t size = (uint64_t)mt->baseSize + (uint64_t)length * mt_component_size(mt);
	if (size > 0x7FFFFFFF)
		sd_fail("SharpDate: array too large");
	void* obj = gc_alloc(mt, ALIGN_UP((uintptr_t)size, PTR_SIZE), align8, 0);
	*(uint32_t*)((char*)obj + PTR_SIZE) = (uint32_t)length;
	return obj;
}

SD_EXPORT void* RhpNewArrayFast(MethodTable* mt, intptr_t length) { return new_array(mt, length, 0); }
SD_EXPORT void* RhpNewPtrArrayFast(MethodTable* mt, intptr_t length) { return new_array(mt, length, 0); }
SD_EXPORT void* RhpNewArrayFastAlign8(MethodTable* mt, intptr_t length) { return new_array(mt, length, 1); }
SD_EXPORT void* RhpNewArray(MethodTable* mt, intptr_t length) { return new_array(mt, length, 0); }
SD_EXPORT void* RhNewString(MethodTable* mt, intptr_t length) { return new_array(mt, length, 0); }

SD_EXPORT void sd_gc_add_root(void* obj)
{
	if (sd_root_count == sd_root_capacity)
	{
		size_t newCapacity = sd_root_capacity == 0 ? 64 : sd_root_capacity * 2;
		sd_roots = (void**)sd_realloc(sd_roots, newCapacity * sizeof(void*));
		sd_root_capacity = newCapacity;
	}
	sd_roots[sd_root_count++] = obj;
}

SD_EXPORT size_t sd_gc_total_bytes(void) { return sd_live_bytes; }
SD_EXPORT int sd_gc_collection_count(void) { return sd_gc_count; }

// ---- GC handles ----

typedef struct GCHandleSlot
{
	void* target;
	intptr_t type; // 0 weak, 1 weak-track-resurrection, 2 normal, 3 pinned; -1 free
} GCHandleSlot;

static GCHandleSlot* sd_handles;
static size_t sd_handle_count;
static size_t sd_handle_capacity;
static intptr_t sd_handle_free_list = -1;

// Handles are returned as (index + 1) * 2 so they are never zero and never look like object pointers.
SD_EXPORT intptr_t sd_handle_alloc(void* obj, intptr_t type)
{
	size_t index;
	if (sd_handle_free_list >= 0)
	{
		index = (size_t)sd_handle_free_list;
		sd_handle_free_list = (intptr_t)sd_handles[index].target;
	}
	else
	{
		if (sd_handle_count == sd_handle_capacity)
		{
			size_t newCapacity = sd_handle_capacity == 0 ? 64 : sd_handle_capacity * 2;
			sd_handles = (GCHandleSlot*)sd_realloc(sd_handles, newCapacity * sizeof(GCHandleSlot));
			sd_handle_capacity = newCapacity;
		}
		index = sd_handle_count++;
	}
	sd_handles[index].target = obj;
	sd_handles[index].type = type;
	return (intptr_t)(index + 1) * 2;
}

static GCHandleSlot* handle_slot(intptr_t handle)
{
	size_t index = (size_t)(handle / 2) - 1;
	if (handle <= 0 || (handle & 1) || index >= sd_handle_count || sd_handles[index].type < 0)
		sd_fail("SharpDate: invalid GCHandle");
	return &sd_handles[index];
}

SD_EXPORT void sd_handle_free(intptr_t handle)
{
	GCHandleSlot* slot = handle_slot(handle);
	size_t index = (size_t)(slot - sd_handles);
	slot->type = -1;
	slot->target = (void*)sd_handle_free_list;
	sd_handle_free_list = (intptr_t)index;
}

SD_EXPORT void* sd_handle_get(intptr_t handle) { return handle_slot(handle)->target; }
SD_EXPORT void sd_handle_set(intptr_t handle, void* obj) { handle_slot(handle)->target = obj; }

// ---- Marking ----

static void** sd_mark_stack;
static size_t sd_mark_count;
static size_t sd_mark_capacity;

// Frozen (compile-time preallocated) objects are not on the GC heap but may reference heap objects
// (e.g. a preinitialized static array that the game later fills in). They are traced once per GC.
static void** sd_frozen_seen;
static size_t sd_frozen_seen_capacity;
static size_t sd_frozen_seen_count;

static int cmp_ptr(const void* a, const void* b)
{
	uintptr_t x = *(const uintptr_t*)a, y = *(const uintptr_t*)b;
	return x < y ? -1 : (x > y ? 1 : 0);
}

static void sort_objects(void)
{
	// Heap sort: no recursion and no dependency on qsort.
	void** a = sd_objects;
	size_t n = sd_object_count;
	if (n < 2)
		return;
	for (size_t start = n / 2; start-- > 0;)
	{
		size_t root = start;
		for (;;)
		{
			size_t child = root * 2 + 1;
			if (child >= n) break;
			if (child + 1 < n && cmp_ptr(&a[child], &a[child + 1]) < 0) child++;
			if (cmp_ptr(&a[root], &a[child]) >= 0) break;
			void* t = a[root]; a[root] = a[child]; a[child] = t;
			root = child;
		}
	}
	for (size_t end = n - 1; end > 0; end--)
	{
		void* t = a[0]; a[0] = a[end]; a[end] = t;
		size_t root = 0;
		for (;;)
		{
			size_t child = root * 2 + 1;
			if (child >= end) break;
			if (child + 1 < end && cmp_ptr(&a[child], &a[child + 1]) < 0) child++;
			if (cmp_ptr(&a[root], &a[child]) >= 0) break;
			void* t2 = a[root]; a[root] = a[child]; a[child] = t2;
			root = child;
		}
	}
}

// Finds the heap object containing address p (objects sorted). Returns NULL if none.
static void* find_object_containing(uintptr_t p)
{
	size_t lo = 0, hi = sd_object_count;
	while (lo < hi)
	{
		size_t mid = (lo + hi) / 2;
		if ((uintptr_t)sd_objects[mid] <= p) lo = mid + 1;
		else hi = mid;
	}
	if (lo == 0)
		return NULL;
	void* obj = sd_objects[lo - 1];
	uintptr_t start = (uintptr_t)obj;
	// The end pointer of an object is accepted too (loops may keep a one-past-the-end pointer).
	if (p <= start - PTR_SIZE + header_of(obj)->size)
		return obj;
	return NULL;
}

static int is_heap_object(void* p)
{
	size_t lo = 0, hi = sd_object_count;
	while (lo < hi)
	{
		size_t mid = (lo + hi) / 2;
		uintptr_t v = (uintptr_t)sd_objects[mid];
		if (v == (uintptr_t)p) return 1;
		if (v < (uintptr_t)p) lo = mid + 1;
		else hi = mid;
	}
	return 0;
}

static void push_mark(void* obj)
{
	if (sd_mark_count == sd_mark_capacity)
	{
		size_t newCapacity = sd_mark_capacity == 0 ? 256 : sd_mark_capacity * 2;
		sd_mark_stack = (void**)sd_realloc(sd_mark_stack, newCapacity * sizeof(void*));
		sd_mark_capacity = newCapacity;
	}
	sd_mark_stack[sd_mark_count++] = obj;
}

static int frozen_seen_add(void* obj)
{
	if (sd_frozen_seen_count * 2 >= sd_frozen_seen_capacity)
	{
		size_t oldCapacity = sd_frozen_seen_capacity;
		void** old = sd_frozen_seen;
		sd_frozen_seen_capacity = oldCapacity == 0 ? 64 : oldCapacity * 2;
		sd_frozen_seen = (void**)sd_alloc(sd_frozen_seen_capacity * sizeof(void*));
		memset(sd_frozen_seen, 0, sd_frozen_seen_capacity * sizeof(void*));
		sd_frozen_seen_count = 0;
		for (size_t i = 0; i < oldCapacity; i++)
			if (old[i] != NULL)
				frozen_seen_add(old[i]);
		sd_free(old);
	}
	size_t mask = sd_frozen_seen_capacity - 1;
	size_t i = ((uintptr_t)obj >> 3) & mask;
	while (sd_frozen_seen[i] != NULL)
	{
		if (sd_frozen_seen[i] == obj)
			return 0;
		i = (i + 1) & mask;
	}
	sd_frozen_seen[i] = obj;
	sd_frozen_seen_count++;
	return 1;
}

static void mark_reference(void* ref)
{
	if (ref == NULL)
		return;
	if (is_heap_object(ref))
	{
		GCHeader* h = header_of(ref);
		if (!h->mark)
		{
			h->mark = 1;
			push_mark(ref);
		}
	}
	else
	{
		// A frozen object: trace it once if it can contain references.
		MethodTable* mt = *(MethodTable**)ref;
		if ((mt->flags & MT_HAS_POINTERS) && frozen_seen_add(ref))
			push_mark(ref);
	}
}

// Visits every reference field of obj using the GC descriptor stored in front of its MethodTable.
static void scan_object(void* obj)
{
	MethodTable* mt = *(MethodTable**)obj;
	if (!(mt->flags & MT_HAS_POINTERS))
		return;

	char* o = (char*)obj;
	uintptr_t size = object_size(obj);
	intptr_t numSeries = *((intptr_t*)mt - 1);
	// Series descriptors (seriessize, startoffset) are stored below numSeries, highest first.
	uintptr_t* highest = (uintptr_t*)mt - 1 - 2;

	if (numSeries >= 0)
	{
		uintptr_t* cur = highest;
		uintptr_t* last = (uintptr_t*)((char*)mt - (PTR_SIZE + (uintptr_t)numSeries * 2 * PTR_SIZE));
		while (cur >= last)
		{
			void** parm = (void**)(o + cur[1]);
			void** stop = (void**)((char*)parm + (intptr_t)cur[0] + size);
			for (; parm < stop; parm++)
				mark_reference(*parm);
			cur -= 2;
		}
	}
	else
	{
		// Repeating pattern (arrays of structs containing references): val_serie items of
		// (nptrs, skip) half-words, stored at highest->seriessize going downward.
		void** parm = (void**)(o + highest[1]);
		char* end = o + size - PTR_SIZE;
		while ((char*)parm < end)
		{
			for (intptr_t i = 0; i > numSeries; i--)
			{
#if UINTPTR_MAX == 0xFFFFFFFFu
				uint16_t* item = (uint16_t*)&highest[0] + 2 * i;
#else
				uint32_t* item = (uint32_t*)&highest[0] + 2 * i;
#endif
				uintptr_t nptrs = item[0];
				uintptr_t skip = item[1];
				void** stop = parm + nptrs;
				do
				{
					mark_reference(*parm);
					parm++;
				} while (parm < stop);
				parm = (void**)((char*)stop + skip);
			}
		}
	}
}

static void drain_mark_stack(void)
{
	while (sd_mark_count > 0)
	{
		void* obj = sd_mark_stack[--sd_mark_count];
		scan_object(obj);
	}
}

static void mark_conservative(uintptr_t low, uintptr_t high)
{
	low = ALIGN_UP(low, sizeof(void*));
	for (uintptr_t p = low; p + sizeof(void*) <= high; p += sizeof(void*))
	{
		uintptr_t v = *(uintptr_t*)p;
		void* obj = find_object_containing(v);
		if (obj != NULL)
		{
			GCHeader* h = header_of(obj);
			if (!h->mark)
			{
				h->mark = 1;
				push_mark(obj);
			}
		}
	}
}

SD_NOINLINE static void mark_stack_and_registers(void)
{
	// Force callee-saved registers into this frame so the stack scan sees them.
#if defined(__GNUC__)
	__builtin_unwind_init();
#endif
	volatile uintptr_t marker = 0;
	uintptr_t sp = (uintptr_t)&marker;
	if (sd_stack_top > sp)
		mark_conservative(sp, sd_stack_top);
	drain_mark_stack();
}

SD_EXPORT void sd_gc_collect(void)
{
	if (sd_in_gc || !on_main_context() || sd_stack_top == 0)
		return;
	sd_in_gc = 1;

	sort_objects();
	sd_frozen_seen_count = 0;
	if (sd_frozen_seen != NULL)
		memset(sd_frozen_seen, 0, sd_frozen_seen_capacity * sizeof(void*));

	// Roots: static bases, strong handles, the native stack.
	for (size_t i = 0; i < sd_root_count; i++)
		mark_reference(sd_roots[i]);
	for (size_t i = 0; i < sd_handle_count; i++)
		if (sd_handles[i].type >= 2)
			mark_reference(sd_handles[i].target);
	drain_mark_stack();
	mark_stack_and_registers();

	// Weak handles lose their targets if nothing else keeps them alive.
	for (size_t i = 0; i < sd_handle_count; i++)
	{
		GCHandleSlot* s = &sd_handles[i];
		if (s->type == 0 || s->type == 1)
		{
			if (s->target != NULL && is_heap_object(s->target) && !header_of(s->target)->mark)
				s->target = NULL;
		}
	}

	// Sweep.
	size_t kept = 0;
	for (size_t i = 0; i < sd_object_count; i++)
	{
		void* obj = sd_objects[i];
		GCHeader* h = header_of(obj);
		if (h->mark)
		{
			h->mark = 0;
			sd_objects[kept++] = obj;
		}
		else
		{
			sd_live_bytes -= h->size;
			sd_pd->system->realloc(h->block, 0);
		}
	}
	sd_object_count = kept;

	sd_bytes_since_gc = 0;
	sd_gc_threshold = sd_live_bytes < 256 * 1024 ? 256 * 1024 : sd_live_bytes;
	sd_gc_count++;
	sd_in_gc = 0;
}

// ---------------------------------------------------------------------------------------------
// Transitions between native and managed code. The GC itself never needs threads to stop,
// so these only track the extent of the stack that may hold managed references.
// ---------------------------------------------------------------------------------------------

static inline void enter_managed(uintptr_t frame)
{
	if (!on_main_context())
		return;
	if (sd_managed_depth++ == 0)
	{
		// Leave room for the managed frame that contains `frame`.
		uintptr_t top = frame + 1024;
		if (top > sd_stack_top)
			sd_stack_top = top;
	}
}

static inline void leave_managed(void)
{
	if (!on_main_context())
		return;
	sd_managed_depth--;
}

SD_EXPORT void RhpReversePInvoke(void* frame) { enter_managed((uintptr_t)frame); }
SD_EXPORT void RhpReversePInvokeReturn(void* frame) { (void)frame; leave_managed(); }
SD_EXPORT void RhpPInvoke(void* frame) { (void)frame; }
SD_EXPORT void RhpPInvokeReturn(void* frame) { (void)frame; }
SD_EXPORT void RhpGcPoll(void) { }

// ---------------------------------------------------------------------------------------------
// Exceptions: SharpDate does not unwind. A throw reports the exception and stops the game.
// ---------------------------------------------------------------------------------------------

SD_EXPORT void RhpThrowEx(void* exception)
{
	SharpDate_ReportUnhandledException(exception);
	sd_fail("SharpDate: unhandled exception");
}

SD_EXPORT void RhpRethrow(void) { sd_fail("SharpDate: unhandled exception (rethrow)"); }
SD_EXPORT void RhpFallbackFailFast(void) { sd_fail("SharpDate: fatal runtime error"); }
SD_EXPORT void RhDebugBreak(void) { sd_fail("SharpDate: Debugger.Break"); }

// Stack-buffer-overrun protection cookie checked by JIT-generated code.
SD_EXPORT uintptr_t __security_cookie = (uintptr_t)0x2B992DDFA232ull;

#if SD_DEVICE
// ILC emits ARM EHABI unwind tables; SharpDate never unwinds, so the personality routines are inert.
__attribute__((weak)) void __aeabi_unwind_cpp_pr0(void) { }
__attribute__((weak)) void __aeabi_unwind_cpp_pr1(void) { }
__attribute__((weak)) void __aeabi_unwind_cpp_pr2(void) { }
#endif

// ---------------------------------------------------------------------------------------------
// Arithmetic helpers called by compiled code (32-bit ARM lacks single-instruction forms).
// Compiled by gcc these use libgcc (soft-float for doubles on the single-precision FPU).
// ---------------------------------------------------------------------------------------------

SD_EXPORT int32_t sd_idiv(int32_t a, int32_t b) { return a / b; }
SD_EXPORT int32_t sd_imod(int32_t a, int32_t b) { return a % b; }
SD_EXPORT uint32_t sd_uidiv(uint32_t a, uint32_t b) { return a / b; }
SD_EXPORT uint32_t sd_uimod(uint32_t a, uint32_t b) { return a % b; }
SD_EXPORT int64_t sd_ldiv(int64_t a, int64_t b) { return a / b; }
SD_EXPORT int64_t sd_lmod(int64_t a, int64_t b) { return a % b; }
SD_EXPORT uint64_t sd_uldiv(uint64_t a, uint64_t b) { return a / b; }
SD_EXPORT uint64_t sd_ulmod(uint64_t a, uint64_t b) { return a % b; }

SD_EXPORT int64_t RhpLMul(int64_t a, int64_t b) { return a * b; }
// 64-bit shifts by a variable count (C# masks the count to 0..63).
SD_EXPORT int64_t RhpLLsh(int64_t v, int32_t s) { return (int64_t)((uint64_t)v << (s & 63)); }
SD_EXPORT int64_t RhpLRsh(int64_t v, int32_t s) { return v >> (s & 63); }
SD_EXPORT uint64_t RhpLRsz(uint64_t v, int32_t s) { return v >> (s & 63); }
SD_EXPORT float RhpLng2Flt(int64_t v) { return (float)v; }
SD_EXPORT float RhpULng2Flt(uint64_t v) { return (float)v; }
SD_EXPORT double RhpLng2Dbl(int64_t v) { return (double)v; }
SD_EXPORT double RhpULng2Dbl(uint64_t v) { return (double)v; }
SD_EXPORT int64_t RhpDbl2Lng(double v) { return (int64_t)v; }
SD_EXPORT uint64_t RhpDbl2ULng(double v) { return v <= 0 ? 0 : (uint64_t)v; }
SD_EXPORT int32_t RhpDbl2Int(double v) { return (int32_t)v; }
SD_EXPORT uint32_t RhpDbl2UInt(double v) { return v <= 0 ? 0 : (uint32_t)v; }
SD_EXPORT float sd_double_to_float(double v) { return (float)v; }

// Formats a double with up to `decimals` fractional digits (trailing zeros trimmed).
SD_EXPORT int sd_format_double(double value, int decimals, char* buffer, int bufferLength)
{
	int n = 0;
	if (value != value) { memcpy(buffer, "NaN", 3); return 3; }
	if (value < 0) { buffer[n++] = '-'; value = -value; }
	if (value > 1.8e19) { memcpy(buffer + n, "Inf", 3); return n + 3; }
	double scale = 1;
	for (int i = 0; i < decimals; i++) scale *= 10;
	uint64_t ip = (uint64_t)value;
	uint64_t frac = (uint64_t)((value - (double)ip) * scale + 0.5);
	if ((double)frac >= scale) { frac -= (uint64_t)scale; ip++; }
	char tmp[24];
	int k = 0;
	do { tmp[k++] = (char)('0' + (int)(ip % 10)); ip /= 10; } while (ip != 0 && k < 24);
	while (k > 0 && n < bufferLength) buffer[n++] = tmp[--k];
	int d = decimals;
	while (d > 0 && frac % 10 == 0) { frac /= 10; d--; }
	if (d > 0 && n + d + 1 < bufferLength)
	{
		buffer[n++] = '.';
		for (int i = d - 1; i >= 0; i--) { buffer[n + i] = (char)('0' + (int)(frac % 10)); frac /= 10; }
		n += d;
	}
	return n;
}

SD_EXPORT float sd_parse_float(const char* s, int length)
{
	int i = 0;
	while (i < length && (s[i] == ' ' || s[i] == '\t')) i++;
	float sign = 1;
	if (i < length && (s[i] == '-' || s[i] == '+')) { if (s[i] == '-') sign = -1; i++; }
	float v = 0;
	while (i < length && s[i] >= '0' && s[i] <= '9') v = v * 10 + (float)(s[i++] - '0');
	if (i < length && s[i] == '.')
	{
		i++;
		float f = 0.1f;
		while (i < length && s[i] >= '0' && s[i] <= '9') { v += (float)(s[i++] - '0') * f; f *= 0.1f; }
	}
	if (i < length && (s[i] == 'e' || s[i] == 'E'))
	{
		i++;
		int esign = 1, e = 0;
		if (i < length && (s[i] == '-' || s[i] == '+')) { if (s[i] == '-') esign = -1; i++; }
		while (i < length && s[i] >= '0' && s[i] <= '9') e = e * 10 + (s[i++] - '0');
		while (e-- > 0) v = esign > 0 ? v * 10 : v / 10;
	}
	return sign * v;
}

// ---------------------------------------------------------------------------------------------
// Playdate entry point
// ---------------------------------------------------------------------------------------------

static void runtime_initialize(PlaydateAPI* pd)
{
	sd_pd = pd;
#if SD_WINDOWS
	sd_main_thread = current_thread_id();
#elif !SD_DEVICE
	sd_main_thread = pthread_self();
#endif
	void* headers[1] = { &__ReadyToRunHeader };
	SharpDate_InitializeModules(headers, 1);
	sd_initialized = 1;
}

SD_ENTRY int eventHandler(PlaydateAPI* pd, PDSystemEvent event, uint32_t arg)
{
	volatile uintptr_t frame = 0;
	uintptr_t top = (uintptr_t)&frame + 256;
	if (top > sd_stack_top)
		sd_stack_top = top;

	if (!sd_initialized)
		runtime_initialize(pd);

	return SharpDate_EventHandler(pd, event, arg);
}
