// chdman_wrapper.cpp
// MAME chdman wrapper DLL for C# interop
#include "chdman_wrapper.h"
#include <string>
#include <vector>
#include <functional>
#include <unordered_map>

typedef std::unordered_map<std::string, std::string *> parameters_map;

extern void do_create_cd(parameters_map &params);
extern void do_create_dvd(parameters_map &params);
extern void do_extract_cd(parameters_map &params);
extern void do_extract_raw(parameters_map &params);
extern void do_info(parameters_map &params);

ProgressCallback g_progress_cb = nullptr;
LogCallback g_log_cb = nullptr;
volatile bool g_cancel_requested = false;

static parameters_map make_params(std::vector<std::pair<std::string, std::string>> &entries)
{
    parameters_map params;
    for (auto &entry : entries)
        params[entry.first] = &entry.second;
    return params;
}

static int safe_call(std::function<void()> fn, LogCallback log)
{
    try
    {
        fn();
        return 0;
    }
    catch (std::exception &ex)
    {
       if (std::string(ex.what()) == "Cancelled")
            return -1;
        if (log) log(ex.what());
        return 1;
    }
    catch (...)
    {
        if (log) log("Unknown error occurred");
        return 1;
    }
}

CHDMAN_API int chdman_create_cd(
    const char *input,
    const char *output,
    const char *compression,
    ProgressCallback progress,
    LogCallback log)
{
    g_cancel_requested = false;
    g_progress_cb = progress;
    g_log_cb = log;
    std::vector<std::pair<std::string, std::string>> entries = {
        { "input",  std::string(input) },
        { "output", std::string(output) },
        { "force",  std::string("") }
    };
    if (compression && compression[0] != '\0')
        entries.emplace_back("compression", std::string(compression));
    auto params = make_params(entries);
    int result = safe_call([&]() { do_create_cd(params); }, log);
    if (g_cancel_requested) return -1;
    return result;
}

CHDMAN_API int chdman_create_dvd(
    const char *input,
    const char *output,
    const char *compression,
    ProgressCallback progress,
    LogCallback log)
{
    g_cancel_requested = false;
    g_progress_cb = progress;
    g_log_cb = log;
    std::vector<std::pair<std::string, std::string>> entries = {
        { "input",  std::string(input) },
        { "output", std::string(output) },
        { "compression", std::string(compression ? compression : "zlib") },
        { "force",  std::string("") }
    };
    auto params = make_params(entries);
    int result = safe_call([&]() { do_create_dvd(params); }, log);
    if (g_cancel_requested) return -1;
    return result;
}

CHDMAN_API int chdman_extract_cd(
    const char *input,
    const char *output,
    ProgressCallback progress,
    LogCallback log)
{
    g_cancel_requested = false;
    g_progress_cb = progress;
    g_log_cb = log;
    std::vector<std::pair<std::string, std::string>> entries = {
        { "input",  std::string(input) },
        { "output", std::string(output) },
        { "force",  std::string("") }
    };
    auto params = make_params(entries);
    return safe_call([&]() { do_extract_cd(params); }, log);
}

CHDMAN_API int chdman_extract_raw(
    const char *input,
    const char *output,
    ProgressCallback progress,
    LogCallback log)
{
    g_cancel_requested = false;
    g_progress_cb = progress;
    g_log_cb = log;
    std::vector<std::pair<std::string, std::string>> entries = {
        { "input",  std::string(input) },
        { "output", std::string(output) },
        { "force",  std::string("") }
    };
    auto params = make_params(entries);
    return safe_call([&]() { do_extract_raw(params); }, log);
}

CHDMAN_API int chdman_get_info(
    const char *input,
    LogCallback log)
{
    g_log_cb = log;
    std::vector<std::pair<std::string, std::string>> entries = {
        { "input", std::string(input) }
    };
    auto params = make_params(entries);
    return safe_call([&]() { do_info(params); }, log);
}

CHDMAN_API void chdman_cancel()
{
    g_cancel_requested = true;
}