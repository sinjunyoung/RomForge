#pragma once

#ifdef _WIN32
#define CHDMAN_API extern "C" __declspec(dllexport)
#else
#define CHDMAN_API extern "C"
#endif

typedef void (*ProgressCallback)(int percent);
typedef void (*LogCallback)(const char *message);

CHDMAN_API int chdman_create_cd(
    const char *input,
    const char *output,
    const char *compression,
    ProgressCallback progress,
    LogCallback log);

CHDMAN_API int chdman_create_dvd(
    const char *input,
    const char *output,
    const char *compression,
    ProgressCallback progress,
    LogCallback log);

CHDMAN_API int chdman_extract_cd(
    const char *input,
    const char *output,
    ProgressCallback progress,
    LogCallback log);

CHDMAN_API int chdman_extract_raw(
    const char *input,
    const char *output,
    ProgressCallback progress,
    LogCallback log);

CHDMAN_API int chdman_get_info(
    const char *input,
    LogCallback log);

CHDMAN_API void chdman_cancel();