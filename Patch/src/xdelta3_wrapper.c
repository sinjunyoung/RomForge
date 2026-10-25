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
#define DLL_EXPORT __declspec(dllexport)
#else
#define DLL_EXPORT __attribute__((visibility("default")))
#endif

static char last_error_msg[512] = { 0 };
static int last_error_code = 0;

DLL_EXPORT const char* xd3_get_last_error(void) {
    return last_error_msg;
}

DLL_EXPORT int xd3_get_last_error_code(void) {
    return last_error_code;
}

static void save_error(xd3_stream* stream, int code) {
    last_error_code = code;

    if (stream && stream->msg)
        snprintf(last_error_msg, sizeof(last_error_msg),
            "%s (code: %d)", stream->msg, code);
    else
        snprintf(last_error_msg, sizeof(last_error_msg),
            "unknown error (code: %d)", code);
}

typedef struct {
    const uint8_t* buf;
    xoff_t         size;
} src_ctx;

static int my_getblk(xd3_stream* stream, xd3_source* source, xoff_t blkno)
{
    src_ctx* ctx = (src_ctx*)source->ioh;
    source->curblk = ctx->buf;
    source->curblkno = 0;
    source->onblk = (usize_t)ctx->size;
    return 0;
}

static src_ctx* src_ctx_new_from_buffer(const uint8_t* buf, xoff_t size, xd3_source* source, xd3_stream* stream)
{
    src_ctx* ctx = (src_ctx*)calloc(1, sizeof(src_ctx));
    if (!ctx) return NULL;

    usize_t blksize = size > 0 ? (usize_t)size : 1;

    ctx->buf = buf;
    ctx->size = size;

    source->blksize = blksize;
    source->ioh = ctx;
    source->curblk = ctx->buf;
    source->curblkno = 0;
    source->onblk = (usize_t)size;
    source->max_winsize = blksize;

    xd3_set_source_and_size(stream, source, size);
    return ctx;
}

static void src_ctx_free(src_ctx* ctx) {
    free(ctx);
}

typedef enum {
    XD3S_OK = 0,
    XD3S_NEED_INPUT = 1,
    XD3S_FINISHED = 2,
    XD3S_ERROR = -1
} xd3_stream_status;

typedef struct {
    xd3_stream stream;
    xd3_config config;
    xd3_source source;
    src_ctx* src;

    const uint8_t* pending_ptr;
    usize_t        pending_len;

    int finished;
    int errored;
} xd3_decode_handle;

DLL_EXPORT xd3_decode_handle* xd3_stream_open_decode_buf(const uint8_t* source_buf, xoff_t source_size)
{
    xd3_decode_handle* h = (xd3_decode_handle*)calloc(1, sizeof(xd3_decode_handle));
    if (!h) return NULL;

    last_error_msg[0] = 0;
    last_error_code = 0;

    xd3_init_config(&h->config, 0);
    h->config.winsize = (1 << 26);
    h->config.getblk = my_getblk;

    int ret = xd3_config_stream(&h->stream, &h->config);
    if (ret != 0) {
        save_error(&h->stream, ret);
        free(h);
        return NULL;
    }

    h->src = src_ctx_new_from_buffer(source_buf, source_size, &h->source, &h->stream);
    if (!h->src) {
        snprintf(last_error_msg, sizeof(last_error_msg), "failed to bind source buffer");
        xd3_free_stream(&h->stream);
        free(h);
        return NULL;
    }

    return h;
}

DLL_EXPORT int xd3_stream_feed(xd3_decode_handle* h, const uint8_t* patch_chunk, size_t len, int is_last_chunk)
{
    if (!h || h->errored) return XD3S_ERROR;

    xd3_avail_input(&h->stream, patch_chunk, (usize_t)len);

    if (is_last_chunk)
        xd3_set_flags(&h->stream, XD3_FLUSH | h->stream.flags);

    return 0;
}

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

            if (h->pending_len > 0)
                return XD3S_OK;

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

        case 0:
            h->finished = 1;
            continue;

        default:
            save_error(&h->stream, ret);
            h->errored = 1;
            return XD3S_ERROR;
        }
    }
}

DLL_EXPORT int xd3_stream_finish(xd3_decode_handle* h)
{
    if (!h || h->errored) return XD3S_ERROR;

    if (h->pending_len > 0) {
        last_error_code = 0;
        snprintf(last_error_msg, sizeof(last_error_msg), "decoded output was not fully drained");
        h->errored = 1;
        return XD3S_ERROR;
    }

    int ret = xd3_close_stream(&h->stream);
    if (ret != 0) {
        save_error(&h->stream, ret);
        h->errored = 1;
        return XD3S_ERROR;
    }

    return 0;
}

DLL_EXPORT void xd3_stream_close(xd3_decode_handle* h)
{
    if (!h) return;

    xd3_free_stream(&h->stream);
    src_ctx_free(h->src);
    free(h);
}