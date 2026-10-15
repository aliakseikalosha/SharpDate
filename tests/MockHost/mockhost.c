// Loads a SharpDate game's Simulator library (pdex.dylib/.so) with a minimal fake PlaydateAPI and
// sends it kEventInit. Used to run tests/RuntimeTests without the Playdate Simulator.
// Exit code: 0 on success, 2 if the game logged a FAIL, 3 if it called pd->system->error.

#include <dlfcn.h>
#include <execinfo.h>
#include <signal.h>
#include <unistd.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/time.h>

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

static PDCallbackFunction* update_callback;
static void* update_userdata;

static void mock_set_update(PDCallbackFunction* update, void* userdata)
{
	update_callback = update;
	update_userdata = userdata;
}

static SDFile* mock_open(const char* name, FileOptions mode) { (void)name; (void)mode; return NULL; }
static const char* mock_geterr(void) { return "no file system in mock host"; }

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

int main(int argc, char** argv)
{
	struct sigaction sa;
	memset(&sa, 0, sizeof(sa));
	sa.sa_sigaction = crash_handler;
	sa.sa_flags = SA_SIGINFO;
	sigaction(SIGSEGV, &sa, NULL);
	sigaction(SIGBUS, &sa, NULL);
	sigaction(SIGILL, &sa, NULL);

	if (argc < 2)
	{
		fprintf(stderr, "usage: mockhost <pdex.dylib> [frames]\n");
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

	void* lib = dlopen(argv[1], RTLD_NOW);
	if (lib == NULL)
	{
		fprintf(stderr, "dlopen: %s\n", dlerror());
		return 1;
	}

	int (*eventHandler)(PlaydateAPI*, PDSystemEvent, uint32_t) = dlsym(lib, "eventHandler");
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
