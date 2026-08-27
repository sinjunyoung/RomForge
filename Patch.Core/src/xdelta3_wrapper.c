#ifdef _WIN32
#define _CRT_SECURE_NO_WARNINGS
#endif

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#define XD3_ENCODER         1
#define SECONDARY_LZMA      1
#define SECONDARY_DJW       1
#define XD3_USE_LARGEFILE64 1
#define XD3_STDIO           1
#define SIZEOF_SIZE_T       8
#define SIZEOF_USIZE_T      8
#define SIZEOF_XOFF_T       8

#include "xdelta3.h"

#ifdef _WIN32
#include <windows.h>
#include <io.h>
#define DLL_EXPORT __declspec(dllexport)
#define fseek64 _fseeki64
#define ftell64 _ftelli64
#else
#include <sys/mman.h>
#define DLL_EXPORT __attribute__((visibility("default")))
#define fseek64 fseeko
#define ftell64 ftello
#endif

static char last_error_msg[512] = { 0 };

DLL_EXPORT const char* xd3_get_last_error(void) {
    return last_error_msg;
}

static void save_error(xd3_stream* stream, int code) {
    if (stream && stream->msg)
        snprintf(last_error_msg, sizeof(last_error_msg),
            "%s (code: %d)", stream->msg, code);
    else
        snprintf(last_error_msg, sizeof(last_error_msg),
            "unknown error (code: %d)", code);
}

typedef struct {
    uint8_t* buf;
    xoff_t   size;
    FILE*    file;
#ifdef _WIN32
    HANDLE   hMap;
#endif
} src_ctx;

static int my_getblk(xd3_stream* stream, xd3_source* source, xoff_t blkno)
{
    src_ctx* ctx = (src_ctx*)source->ioh;
    source->curblk = ctx->buf;
    source->curblkno = 0;
    source->onblk = (usize_t)ctx->size;
    return 0;
}

static src_ctx* src_ctx_new_from_path(const void* source_path, int is_wide, xd3_source* source, xd3_stream* stream)
{
    src_ctx* ctx = (src_ctx*)calloc(1, sizeof(src_ctx));
    if (!ctx) return NULL;

#ifdef _WIN32
    ctx->file = is_wide ? _wfopen((const wchar_t*)source_path, L"rb")
                         : fopen((const char*)source_path, "rb");
#else
    ctx->file = fopen((const char*)source_path, "rb");
#endif
    if (!ctx->file) { free(ctx); return NULL; }

    fseek64(ctx->file, 0, SEEK_END);
    ctx->size = (xoff_t)ftell64(ctx->file);
    fseek64(ctx->file, 0, SEEK_SET);

    if (ctx->size == 0) {
        ctx->buf = NULL;
    }
    else {
#ifdef _WIN32
        HANDLE hFile = (HANDLE)_get_osfhandle(_fileno(ctx->file));
        ctx->hMap = CreateFileMapping(hFile, NULL, PAGE_READONLY, 0, 0, NULL);
        if (!ctx->hMap) { fclose(ctx->file); free(ctx); return NULL; }
        ctx->buf = (uint8_t*)MapViewOfFile(ctx->hMap, FILE_MAP_READ, 0, 0, 0);
        if (!ctx->buf) { CloseHandle(ctx->hMap); fclose(ctx->file); free(ctx); return NULL; }
#else
        ctx->buf = (uint8_t*)mmap(NULL, ctx->size, PROT_READ, MAP_PRIVATE, fileno(ctx->file), 0);
        if (ctx->buf == MAP_FAILED) { fclose(ctx->file); free(ctx); return NULL; }
#endif
    }

    source->blksize = (usize_t)ctx->size;
    source->ioh = ctx;
    source->curblk = ctx->buf;
    source->curblkno = 0;
    source->onblk = (usize_t)ctx->size;
    source->max_winsize = (usize_t)ctx->size;

    xd3_set_source_and_size(stream, source, ctx->size);
    return ctx;
}

static void src_ctx_free(src_ctx* ctx) {
    if (!ctx) return;
    if (ctx->buf) {
#ifdef _WIN32
        UnmapViewOfFile(ctx->buf);
        if (ctx->hMap) CloseHandle(ctx->hMap);
#else
        munmap(ctx->buf, ctx->size);
#endif
    }
    if (ctx->file) fclose(ctx->file);
    free(ctx);
}

/* ---------------- streaming decode API ---------------- */

typedef enum {
    XD3S_OK = 0,          /* data was written to out_buf; check *written / *has_more */
    XD3S_NEED_INPUT = 1,  /* caller must call xd3_stream_feed() again before reading more */
    XD3S_FINISHED = 2,    /* decode complete, no more output will ever be produced */
    XD3S_ERROR = -1
} xd3_stream_status;

typedef struct {
    xd3_stream stream;
    xd3_config config;
    xd3_source source;
    src_ctx* src;

    const uint8_t* pending_ptr;   /* unconsumed decoded bytes still owned by xd3, not yet copied to caller */
    usize_t        pending_len;

    int finished;   /* true once xd3_decode_input has returned a terminal (non-XD3_OUTPUT/INPUT) status */
    int errored;
} xd3_decode_handle;

static xd3_decode_handle* xd3_stream_open_decode_impl(const void* source_path, int is_wide)
{
    xd3_decode_handle* h = (xd3_decode_handle*)calloc(1, sizeof(xd3_decode_handle));
    if (!h) return NULL;

    last_error_msg[0] = 0;

    xd3_init_config(&h->config, 0);
    h->config.winsize = (1 << 26);
    h->config.getblk = my_getblk;

    int ret = xd3_config_stream(&h->stream, &h->config);
    if (ret != 0) {
        save_error(&h->stream, ret);
        free(h);
        return NULL;
    }

    h->src = src_ctx_new_from_path(source_path, is_wide, &h->source, &h->stream);
    if (!h->src) {
        snprintf(last_error_msg, sizeof(last_error_msg), "failed to open/map source file");
        xd3_free_stream(&h->stream);
        free(h);
        return NULL;
    }

    return h;
}

#ifdef _WIN32
DLL_EXPORT xd3_decode_handle* xd3_stream_open_decode_w(const wchar_t* source_path) {
    return xd3_stream_open_decode_impl(source_path, 1);
}
#else
DLL_EXPORT xd3_decode_handle* xd3_stream_open_decode(const char* source_path) {
    return xd3_stream_open_decode_impl(source_path, 0);
}
#endif

/* Feed one chunk of PATCH bytes. Call xd3_stream_read_output() repeatedly after this
 * until it returns XD3S_NEED_INPUT or XD3S_FINISHED before feeding the next chunk.
 * Set is_last_chunk=1 on the final call (even if len==0) so xdelta3 knows no more
 * patch data is coming and can flush its final window. */
DLL_EXPORT int xd3_stream_feed(xd3_decode_handle* h, const uint8_t* patch_chunk, size_t len, int is_last_chunk)
{
    if (!h || h->errored) return XD3S_ERROR;

    xd3_avail_input(&h->stream, patch_chunk, (usize_t)len);

    if (is_last_chunk)
        xd3_set_flags(&h->stream, XD3_FLUSH | h->stream.flags);

    return 0;
}

/* Drains decoded output into out_buf (capacity out_buf_capacity bytes).
 * *written receives how many bytes were copied this call (may be 0).
 * Return value:
 *   XD3S_OK          - out_buf now holds *written bytes; call again for more
 *                       (there may or may not be more without a fresh feed())
 *   XD3S_NEED_INPUT   - all currently available output has been drained;
 *                       call xd3_stream_feed() with the next patch chunk
 *   XD3S_FINISHED     - decode is complete; *written may still be >0 on this call
 *   XD3S_ERROR        - see xd3_get_last_error()
 */
DLL_EXPORT int xd3_stream_read_output(xd3_decode_handle* h, uint8_t* out_buf, size_t out_buf_capacity, size_t* written)
{
    if (!h || h->errored) return XD3S_ERROR;

    *written = 0;

    for (;;) {
        if (h->pending_len > 0) {
            size_t take = h->pending_len < out_buf_capacity ? h->pending_len : out_buf_capacity;

            if (take > 0) {
                memcpy(out_buf, h->pending_ptr, take);
                h->pending_ptr += take;
                h->pending_len -= take;
                out_buf += take;
                out_buf_capacity -= take;
                *written += take;
            }

            if (h->pending_len > 0) {
                /* caller's buffer is full; more pending data remains for next call */
                return XD3S_OK;
            }

            /* fully drained this xd3 output chunk */
            xd3_consume_output(&h->stream);
        }

        if (h->finished)
            return XD3S_FINISHED;

        if (out_buf_capacity == 0)
            return XD3S_OK;

        int ret = xd3_decode_input(&h->stream);

        switch (ret) {
        case XD3_INPUT:
            return XD3S_NEED_INPUT;

        case XD3_OUTPUT:
            h->pending_ptr = h->stream.next_out;
            h->pending_len = h->stream.avail_out;
            continue;

        case XD3_GETSRCBLK:
            ret = my_getblk(&h->stream, h->stream.src, h->stream.src->getblkno);
            if (ret != 0) { save_error(&h->stream, ret); h->errored = 1; return XD3S_ERROR; }
            continue;

        case XD3_GOTHEADER:
        case XD3_WINSTART:
        case XD3_WINFINISH:
            continue;

        case 0: /* stream complete */
            h->finished = 1;
            continue;

        default:
            save_error(&h->stream, ret);
            h->errored = 1;
            return XD3S_ERROR;
        }
    }
}

DLL_EXPORT void xd3_stream_close(xd3_decode_handle* h)
{
    if (!h) return;

    xd3_free_stream(&h->stream);
    src_ctx_free(h->src);
    free(h);
}