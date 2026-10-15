// Loads a SharpDate game's Simulator library (pdex.dylib/.so/.dll) with a minimal fake PlaydateAPI
// and sends it kEventInit. Used to run tests/RuntimeTests without the Playdate Simulator.
// Exit code: 0 on success, 2 if the game logged a FAIL, 3 if it called pd->system->error,
// 4 on a crash.

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#include <execinfo.h>
#include <signal.h>
#include <unistd.h>
#include <sys/time.h>
#endif

#include "pd_api.h"

static int failures;

static void* mock_realloc(void* p, size_t n)
{
	if (n == 0) { free(p); return NULL; }
	return realloc(p, n);
}

static void mock_log(const char* fmt, ...)
{
	char buffer[4096];
	va_list args;
	va_start(args, fmt);
	vsnprintf(buffer, sizeof(buffer), fmt, args);
	va_end(args);
	if (strncmp(buffer, "FAIL", 4) == 0)
		failures++;
	printf("%s\n", buffer);
	fflush(stdout);
}

static void mock_error(const char* fmt, ...)
{
	va_list args;
	va_start(args, fmt);
	printf("ERROR: ");
	vprintf(fmt, args);
	printf("\n");
	va_end(args);
	fflush(stdout);
	exit(3);
}

#ifdef _WIN32
static unsigned int mock_ms(void)
{
	return (unsigned)GetTickCount64();
}

static unsigned int mock_seconds(unsigned int* ms)
{
	FILETIME ft;
	GetSystemTimeAsFileTime(&ft);
	unsigned long long t = (((unsigned long long)ft.dwHighDateTime << 32) | ft.dwLowDateTime) / 10000; // ms since 1601
	if (ms) *ms = (unsigned)(t % 1000);
	return (unsigned)(t / 1000 - 11644473600ull);
}
#else
static unsigned int mock_ms(void)
{
	struct timeval tv;
	gettimeofday(&tv, NULL);
	return (unsigned)(tv.tv_sec * 1000 + tv.tv_usec / 1000);
}

static unsigned int mock_seconds(unsigned int* ms)
{
	struct timeval tv;
	gettimeofday(&tv, NULL);
	if (ms) *ms = (unsigned)(tv.tv_usec / 1000);
	return (unsigned)tv.tv_sec;
}
#endif

static PDCallbackFunction* update_callback;
static void* update_userdata;

static void mock_set_update(PDCallbackFunction* update, void* userdata)
{
	update_callback = update;
	update_userdata = userdata;
}

static SDFile* mock_open(const char* name, FileOptions mode) { (void)name; (void)mode; return NULL; }
static const char* mock_geterr(void) { return "no file system in mock host"; }

#ifdef _WIN32
static LONG WINAPI crash_handler(EXCEPTION_POINTERS* info)
{
	printf("CRASH: exception 0x%08lx at %p (address %p)\n", info->ExceptionRecord->ExceptionCode,
		info->ExceptionRecord->ExceptionAddress,
		info->ExceptionRecord->NumberParameters > 1 ? (void*)info->ExceptionRecord->ExceptionInformation[1] : NULL);
	fflush(stdout);
	ExitProcess(4);
	return EXCEPTION_EXECUTE_HANDLER;
}

static void install_crash_handler(void)
{
	SetUnhandledExceptionFilter(crash_handler);
}

static void* load_library(const char* path)
{
	HMODULE lib = LoadLibraryA(path);
	if (lib == NULL)
		fprintf(stderr, "LoadLibrary(%s) failed: error %lu\n", path, GetLastError());
	return lib;
}

static void* find_symbol(void* lib, const char* name)
{
	return (void*)GetProcAddress((HMODULE)lib, name);
}
#else
static void crash_handler(int sig, siginfo_t* info, void* context)
{
	(void)context;
	void* frames[32];
	printf("CRASH: signal %d at address %p\n", sig, info->si_addr);
	fflush(stdout);
	int n = backtrace(frames, 32);
	backtrace_symbols_fd(frames, n, STDOUT_FILENO);
	_exit(4);
}

static void install_crash_handler(void)
{
	struct sigaction sa;
	memset(&sa, 0, sizeof(sa));
	sa.sa_sigaction = crash_handler;
	sa.sa_flags = SA_SIGINFO;
	sigaction(SIGSEGV, &sa, NULL);
	sigaction(SIGBUS, &sa, NULL);
	sigaction(SIGILL, &sa, NULL);
}

static void* load_library(const char* path)
{
	void* lib = dlopen(path, RTLD_NOW);
	if (lib == NULL)
		fprintf(stderr, "dlopen: %s\n", dlerror());
	return lib;
}

static void* find_symbol(void* lib, const char* name)
{
	return dlsym(lib, name);
}
#endif

int main(int argc, char** argv)
{
	install_crash_handler();

	if (argc < 2)
	{
		fprintf(stderr, "usage: mockhost <pdex.dylib|pdex.so|pdex.dll> [frames]\n");
		return 1;
	}

	static struct playdate_sys sys;
	sys.realloc = mock_realloc;
	sys.logToConsole = mock_log;
	sys.error = mock_error;
	sys.getCurrentTimeMilliseconds = mock_ms;
	sys.getSecondsSinceEpoch = mock_seconds;
	sys.setUpdateCallback = mock_set_update;

	static struct playdate_file file;
	file.open = mock_open;
	file.geterr = mock_geterr;

	static PlaydateAPI api;
	api.system = &sys;
	api.file = &file;

	void* lib = load_library(argv[1]);
	if (lib == NULL)
		return 1;

	int (*eventHandler)(PlaydateAPI*, PDSystemEvent, uint32_t) = (int (*)(PlaydateAPI*, PDSystemEvent, uint32_t))find_symbol(lib, "eventHandler");
	if (eventHandler == NULL)
	{
		fprintf(stderr, "eventHandler not found\n");
		return 1;
	}

	eventHandler(&api, kEventInit, 0);

	int frames = argc > 2 ? atoi(argv[2]) : 10;
	for (int i = 0; i < frames && update_callback != NULL; i++)
		update_callback(update_userdata);

	return failures > 0 ? 2 : 0;
}
