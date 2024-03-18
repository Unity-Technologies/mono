/**
 * \file
 * exception support for ARM64
 *
 * Copyright 2013 Xamarin Inc
 *
 * Based on exceptions-arm.c:
 *
 * Authors:
 *   Dietmar Maurer (dietmar@ximian.com)
 *   Paolo Molaro (lupus@ximian.com)
 *
 * (C) 2001 Ximian, Inc.
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

#include "mini.h"
#include "mini-runtime.h"
#include "aot-runtime.h"

#include <mono/arch/arm64/arm64-codegen.h>
#include <mono/metadata/abi-details.h>
#include "mono/utils/mono-tls-inline.h"

#ifndef DISABLE_JIT

#ifdef MONO_ARCH_ENABLE_PTRAUTH
	static gboolean enable_ptrauth = TRUE;
#else
	static gboolean enable_ptrauth = FALSE;
#endif

gpointer
mono_arch_get_restore_context (MonoTrampInfo **info, gboolean aot)
{
	guint8 *start, *code;
	MonoJumpInfo *ji = NULL;
	GSList *unwind_ops = NULL;
	int i, ctx_reg, size;
	guint8 *labels [16];

	size = 256;
	code = start = mono_global_codeman_reserve (size);

	MINI_BEGIN_CODEGEN ();

	arm_movx (code, ARMREG_IP0, ARMREG_R0);
	ctx_reg = ARMREG_IP0;

	/* Restore fregs */
	arm_ldrx (code, ARMREG_IP1, ctx_reg, MONO_STRUCT_OFFSET (MonoContext, has_fregs));
	labels [0] = code;
	arm_cbzx (code, ARMREG_IP1, 0);
	for (i = 0; i < 32; ++i)
		arm_ldrfpx (code, i, ctx_reg, MONO_STRUCT_OFFSET (MonoContext, fregs) + (i * sizeof (MonoContextSimdReg)));
	mono_arm_patch (labels [0], code, MONO_R_ARM64_CBZ);
	/* Restore gregs */
	// FIXME: Restore less registers
	// FIXME: fp should be restored later
	code = mono_arm_emit_load_regarray (code, 0xffffffff & ~(1 << ctx_reg) & ~(1 << ARMREG_SP), ctx_reg, MONO_STRUCT_OFFSET (MonoContext, regs));
	/* ip0/ip1 doesn't need to be restored */
	/* ip1 = pc */
	arm_ldrx (code, ARMREG_IP1, ctx_reg, MONO_STRUCT_OFFSET (MonoContext, pc));
	/* ip0 = sp */
	arm_ldrx (code, ARMREG_IP0, ctx_reg, MONO_STRUCT_OFFSET (MonoContext, regs) + (ARMREG_SP * 8));
	/* Restore sp, ctx is no longer valid */
	arm_movspx (code, ARMREG_SP, ARMREG_IP0); 
	/* Branch to pc */
	code = mono_arm_emit_brx (code, ARMREG_IP1);
	/* Not reached */
	arm_brk (code, 0);

	g_assert ((code - start) < size);

	MINI_END_CODEGEN (start, code - start, MONO_PROFILER_CODE_BUFFER_EXCEPTION_HANDLING, NULL);

	if (info)
		*info = mono_tramp_info_create ("restore_context", start, code - start, ji, unwind_ops);

	return MINI_ADDR_TO_FTNPTR (start);
}

gpointer
mono_arch_get_call_filter (MonoTrampInfo **info, gboolean aot)
{
	guint8 *code;
	guint8* start;
	int i, size, offset, gregs_offset, fregs_offset, ctx_offset, num_fregs, frame_size;
	MonoJumpInfo *ji = NULL;
	GSList *unwind_ops = NULL;
	guint8 *labels [16];

	size = 512;
	start = code = mono_global_codeman_reserve (size);

	/* Compute stack frame size and offsets */
	offset = 0;
	/* frame block */
	offset += 2 * 8;
	/* gregs */
	gregs_offset = offset;
	offset += 32 * 8;
	/* fregs */
	num_fregs = 8;
	fregs_offset = offset;
	offset += num_fregs * 8;
	ctx_offset = offset;
	offset += 8;
	frame_size = ALIGN_TO (offset, MONO_ARCH_FRAME_ALIGNMENT);

	/*
	 * We are being called from C code, ctx is in r0, the address to call is in r1.
	 * We need to save state, restore ctx, make the call, then restore the previous state,
	 * returning the value returned by the call.
	 */

	MINI_BEGIN_CODEGEN ();

	/* Setup a frame */
	arm_stpx_pre (code, ARMREG_FP, ARMREG_LR, ARMREG_SP, -frame_size);
	arm_movspx (code, ARMREG_FP, ARMREG_SP);

	/* Save ctx */
	arm_strx (code, ARMREG_R0, ARMREG_FP, ctx_offset);
	/* Save gregs */
	code = mono_arm_emit_store_regarray (code, MONO_ARCH_CALLEE_SAVED_REGS | (1 << ARMREG_FP), ARMREG_FP, gregs_offset);
	/* Save fregs */
	for (i = 0; i < num_fregs; ++i)
		arm_strfpx (code, ARMREG_D8 + i, ARMREG_FP, fregs_offset + (i * 8));

	/* Load regs from ctx */
	code = mono_arm_emit_load_regarray (code, MONO_ARCH_CALLEE_SAVED_REGS, ARMREG_R0, MONO_STRUCT_OFFSET (MonoContext, regs));
	/* Load fregs */
	arm_ldrx (code, ARMREG_IP0, ARMREG_R0, MONO_STRUCT_OFFSET (MonoContext, has_fregs));
	labels [0] = code;
	arm_cbzx (code, ARMREG_IP0, 0);
	for (i = 0; i < num_fregs; ++i)
		arm_ldrfpx (code, ARMREG_D8 + i, ARMREG_R0, MONO_STRUCT_OFFSET (MonoContext, fregs) + ((i + 8) * sizeof (MonoContextSimdReg)));
	mono_arm_patch (labels [0], code, MONO_R_ARM64_CBZ);
	/* Load fp */
	arm_ldrx (code, ARMREG_FP, ARMREG_R0, MONO_STRUCT_OFFSET (MonoContext, regs) + (ARMREG_FP * 8));

	/* Make the call */
	code = mono_arm_emit_blrx (code, ARMREG_R1);
	/* For filters, the result is in R0 */

	/* Restore fp */
	arm_ldrx (code, ARMREG_FP, ARMREG_SP, gregs_offset + (ARMREG_FP * 8));
	/* Load ctx */
	arm_ldrx (code, ARMREG_IP0, ARMREG_FP, ctx_offset);
	/* Save registers back to ctx */
	/* This isn't strictly necessary since we don't allocate variables used in eh clauses to registers */
	code = mono_arm_emit_store_regarray (code, MONO_ARCH_CALLEE_SAVED_REGS, ARMREG_IP0, MONO_STRUCT_OFFSET (MonoContext, regs));

	/* Restore regs */
	code = mono_arm_emit_load_regarray (code, MONO_ARCH_CALLEE_SAVED_REGS, ARMREG_FP, gregs_offset);
	/* Restore fregs */
	for (i = 0; i < num_fregs; ++i)
		arm_ldrfpx (code, ARMREG_D8 + i, ARMREG_FP, fregs_offset + (i * 8));
	/* Destroy frame */
	code = mono_arm_emit_destroy_frame (code, frame_size, (1 << ARMREG_IP0));
	arm_retx (code, ARMREG_LR);

	g_assert ((code - start) < size);

	MINI_END_CODEGEN (start, code - start, MONO_PROFILER_CODE_BUFFER_EXCEPTION_HANDLING, NULL);

	if (info)
		*info = mono_tramp_info_create ("call_filter", start, code - start, ji, unwind_ops);

	return MINI_ADDR_TO_FTNPTR (start);
}

static gpointer 
get_throw_trampoline (int size, gboolean corlib, gboolean rethrow, gboolean llvm, gboolean resume_unwind, const char *tramp_name, MonoTrampInfo **info, gboolean aot, gboolean preserve_ips)
{
	guint8 *start, *code;
	MonoJumpInfo *ji = NULL;
	GSList *unwind_ops = NULL;
	int i, offset, gregs_offset, fregs_offset, frame_size, num_fregs;

	code = start = mono_global_codeman_reserve (size);

	/* We are being called by JITted code, the exception object/type token is in R0 */

	/* Compute stack frame size and offsets */
	offset = 0;
	/* frame block */
	offset += 2 * 8;
	/* gregs */
	gregs_offset = offset;
	offset += 32 * 8;
	/* fregs */
	num_fregs = 8;
	fregs_offset = offset;
	offset += num_fregs * 8;
	frame_size = ALIGN_TO (offset, MONO_ARCH_FRAME_ALIGNMENT);

	MINI_BEGIN_CODEGEN ();

	/* Setup a frame */
	arm_stpx_pre (code, ARMREG_FP, ARMREG_LR, ARMREG_SP, -frame_size);
	arm_movspx (code, ARMREG_FP, ARMREG_SP);

	/* Save gregs */
	code = mono_arm_emit_store_regarray (code, 0xffffffff, ARMREG_FP, gregs_offset);
	if (corlib && !llvm)
		/* The real LR is in R1 */
		arm_strx (code, ARMREG_R1, ARMREG_FP, gregs_offset + (ARMREG_LR * 8));
	/* Save fp/sp */
	arm_ldrx (code, ARMREG_IP0, ARMREG_FP, 0);
	arm_strx (code, ARMREG_IP0, ARMREG_FP, gregs_offset + (ARMREG_FP * 8));
	arm_addx_imm (code, ARMREG_IP0, ARMREG_FP, frame_size);
	arm_strx (code, ARMREG_IP0, ARMREG_FP, gregs_offset + (ARMREG_SP * 8));	
	/* Save fregs */
	for (i = 0; i < num_fregs; ++i)
		arm_strfpx (code, ARMREG_D8 + i, ARMREG_FP, fregs_offset + (i * 8));

	/* Call the C trampoline function */
	/* Arg1 =  exception object/type token */
	arm_movx (code, ARMREG_R0, ARMREG_R0);
	/* Arg2 = caller ip */
	if (corlib) {
		if (llvm)
			arm_ldrx (code, ARMREG_R1, ARMREG_FP, gregs_offset + (ARMREG_LR * 8));
		else
			arm_movx (code, ARMREG_R1, ARMREG_R1);
	} else {
		arm_ldrx (code, ARMREG_R1, ARMREG_FP, 8);
	}
	/* Arg 3 = gregs */
	arm_addx_imm (code, ARMREG_R2, ARMREG_FP, gregs_offset);
	/* Arg 4 = fregs */
	arm_addx_imm (code, ARMREG_R3, ARMREG_FP, fregs_offset);
	/* Arg 5 = corlib */
	arm_movzx (code, ARMREG_R4, corlib ? 1 : 0, 0);
	/* Arg 6 = rethrow */
	arm_movzx (code, ARMREG_R5, rethrow ? 1 : 0, 0);
	if (!resume_unwind) {
		/* Arg 7 = preserve_ips */
		arm_movzx (code, ARMREG_R6, preserve_ips ? 1 : 0, 0);
	}

	/* Call the function */
	if (aot) {
		MonoJitICallId icall_id;

		if (resume_unwind)
			icall_id = MONO_JIT_ICALL_mono_arm_resume_unwind;
		else
			icall_id = MONO_JIT_ICALL_mono_arm_throw_exception;

		code = mono_arm_emit_aotconst (&ji, code, start, ARMREG_LR, MONO_PATCH_INFO_JIT_ICALL_ADDR, GUINT_TO_POINTER (icall_id));
	} else {
		gpointer icall_func;

		if (resume_unwind)
			icall_func = (gpointer)mono_arm_resume_unwind;
		else
			icall_func = (gpointer)mono_arm_throw_exception;

		code = mono_arm_emit_imm64 (code, ARMREG_LR, (guint64)icall_func);
	}
	code = mono_arm_emit_blrx (code, ARMREG_LR);
	/* This shouldn't return */
	arm_brk (code, 0x0);

	g_assert ((code - start) < size);

	MINI_END_CODEGEN (start, code - start, MONO_PROFILER_CODE_BUFFER_EXCEPTION_HANDLING, NULL);

	if (info)
		*info = mono_tramp_info_create (tramp_name, start, code - start, ji, unwind_ops);

	return MINI_ADDR_TO_FTNPTR (start);
}

gpointer 
mono_arch_get_throw_exception (MonoTrampInfo **info, gboolean aot)
{
	return get_throw_trampoline (256, FALSE, FALSE, FALSE, FALSE, "throw_exception", info, aot, FALSE);
}

gpointer
mono_arch_get_rethrow_exception (MonoTrampInfo **info, gboolean aot)
{
	return get_throw_trampoline (256, FALSE, TRUE, FALSE, FALSE, "rethrow_exception", info, aot, FALSE);
}

gpointer
mono_arch_get_rethrow_preserve_exception (MonoTrampInfo **info, gboolean aot)
{
	return get_throw_trampoline (256, FALSE, TRUE, FALSE, FALSE, "rethrow_preserve_exception", info, aot, TRUE);
}

gpointer 
mono_arch_get_throw_corlib_exception (MonoTrampInfo **info, gboolean aot)
{
	return get_throw_trampoline (256, TRUE, FALSE, FALSE, FALSE, "throw_corlib_exception", info, aot, FALSE);
}

GSList*
mono_arm_get_exception_trampolines (gboolean aot)
{
	MonoTrampInfo *info;
	GSList *tramps = NULL;

	// FIXME Macroize.

	/* LLVM uses the normal trampolines, but with a different name */
	get_throw_trampoline (256, TRUE, FALSE, FALSE, FALSE, "llvm_throw_corlib_exception_trampoline", &info, aot, FALSE);
	info->jit_icall_info = &mono_get_jit_icall_info ()->mono_llvm_throw_corlib_exception_trampoline;
	tramps = g_slist_prepend (tramps, info);

	get_throw_trampoline (256, TRUE, FALSE, TRUE, FALSE, "llvm_throw_corlib_exception_abs_trampoline", &info, aot, FALSE);
	info->jit_icall_info = &mono_get_jit_icall_info ()->mono_llvm_throw_corlib_exception_abs_trampoline;
	tramps = g_slist_prepend (tramps, info);

	get_throw_trampoline (256, FALSE, FALSE, FALSE, TRUE, "llvm_resume_unwind_trampoline", &info, aot, FALSE);
	info->jit_icall_info = &mono_get_jit_icall_info ()->mono_llvm_resume_unwind_trampoline;
	tramps = g_slist_prepend (tramps, info);

	return tramps;
}

#else

GSList*
mono_arm_get_exception_trampolines (gboolean aot)
{
	g_assert_not_reached ();
	return NULL;
}

#endif /* DISABLE_JIT */

void
mono_arch_exceptions_init (void)
{
	gpointer tramp;
	GSList *tramps, *l;

	if (mono_aot_only) {
		tramp = mono_aot_get_trampoline ("llvm_throw_corlib_exception_trampoline");
		mono_register_jit_icall_info (&mono_get_jit_icall_info ()->mono_llvm_throw_corlib_exception_trampoline, tramp, "llvm_throw_corlib_exception_trampoline", NULL, TRUE, NULL);

		tramp = mono_aot_get_trampoline ("llvm_throw_corlib_exception_abs_trampoline");
		mono_register_jit_icall_info (&mono_get_jit_icall_info ()->mono_llvm_throw_corlib_exception_abs_trampoline, tramp, "llvm_throw_corlib_exception_abs_trampoline", NULL, TRUE, NULL);

		tramp = mono_aot_get_trampoline ("llvm_resume_unwind_trampoline");
		mono_register_jit_icall_info (&mono_get_jit_icall_info ()->mono_llvm_resume_unwind_trampoline, tramp, "llvm_resume_unwind_trampoline", NULL, TRUE, NULL);
	} else {
		tramps = mono_arm_get_exception_trampolines (FALSE);
		for (l = tramps; l; l = l->next) {
			MonoTrampInfo *info = (MonoTrampInfo*)l->data;
			mono_register_jit_icall_info (info->jit_icall_info, info->code, g_strdup (info->name), NULL, TRUE, NULL);
			mono_tramp_info_register (info, NULL);
		}
		g_slist_free (tramps);
	}
}

/*
 * mono_arm_throw_exception:
 *
 *   This function is called by the exception trampolines.
 * FP_REGS points to the 8 callee saved fp regs.
 */
void
mono_arm_throw_exception (gpointer arg, host_mgreg_t pc, host_mgreg_t *int_regs, gdouble *fp_regs, gboolean corlib, gboolean rethrow, gboolean preserve_ips)
{
	ERROR_DECL (error);
	MonoContext ctx;
	MonoObject *exc = NULL;
	guint32 ex_token_index, ex_token;

	if (!corlib)
		exc = (MonoObject*)arg;
	else {
		ex_token_index = (guint64)arg;
		ex_token = MONO_TOKEN_TYPE_DEF | ex_token_index;
		exc = (MonoObject*)mono_exception_from_token (mono_defaults.corlib, ex_token);
	}

	/* Adjust pc so it points into the call instruction */
	pc -= 4;

	/* Initialize a ctx based on the arguments */
	memset (&ctx, 0, sizeof (MonoContext));
	memcpy (&(ctx.regs [0]), int_regs, sizeof (host_mgreg_t) * 32);
	for (int i = 0; i < 8; i++)
		*((gdouble*)&ctx.fregs [ARMREG_D8 + i]) = fp_regs [i];
	ctx.has_fregs = 1;
	ctx.pc = pc;

	if (mono_object_isinst_checked (exc, mono_defaults.exception_class, error)) {
		MonoException *mono_ex = (MonoException*)exc;
		if (!rethrow && !mono_ex->caught_in_unmanaged) {
			mono_ex->stack_trace = NULL;
			mono_ex->trace_ips = NULL;
		} else if (preserve_ips) {
			mono_ex->caught_in_unmanaged = TRUE;
		}
	}
	mono_error_assert_ok (error);

	mono_handle_exception (&ctx, exc);

	mono_restore_context (&ctx);
}

void
mono_arm_resume_unwind (gpointer arg, host_mgreg_t pc, host_mgreg_t *int_regs, gdouble *fp_regs, gboolean corlib, gboolean rethrow)
{
	MonoContext ctx;

	/* Adjust pc so it points into the call instruction */
	pc -= 4;

	/* Initialize a ctx based on the arguments */
	memset (&ctx, 0, sizeof (MonoContext));
	memcpy (&(ctx.regs [0]), int_regs, sizeof (host_mgreg_t) * 32);
	for (int i = 0; i < 8; i++)
		*((gdouble*)&ctx.fregs [ARMREG_D8 + i]) = fp_regs [i];
	ctx.has_fregs = 1;
	ctx.pc = pc;

	mono_resume_unwind (&ctx);
}

/* 
 * mono_arch_unwind_frame:
 *
 * See exceptions-amd64.c for docs;
 */
gboolean
mono_arch_unwind_frame (MonoDomain *domain, MonoJitTlsData *jit_tls, 
							 MonoJitInfo *ji, MonoContext *ctx, 
							 MonoContext *new_ctx, MonoLMF **lmf,
							 host_mgreg_t **save_locations,
							 StackFrameInfo *frame)
{
	memset (frame, 0, sizeof (StackFrameInfo));
	frame->ji = ji;

	*new_ctx = *ctx;

	if (ji != NULL) {
		host_mgreg_t regs [MONO_MAX_IREGS + 8 + 1];
		guint8 *cfa;
		guint32 unwind_info_len;
		guint8 *unwind_info;

		if (ji->is_trampoline)
			frame->type = FRAME_TYPE_TRAMPOLINE;
		else
			frame->type = FRAME_TYPE_MANAGED;

		unwind_info = mono_jinfo_get_unwind_info (ji, &unwind_info_len);

		memcpy (regs, &new_ctx->regs, sizeof (host_mgreg_t) * 32);
		/* v8..v15 are callee saved */
		for (int i = 0; i < 8; i++)
			(regs + MONO_MAX_IREGS) [i] = *((host_mgreg_t*)&new_ctx->fregs [8 + i]);

		gpointer ip = MINI_FTNPTR_TO_ADDR (MONO_CONTEXT_GET_IP (ctx));
		gboolean success = mono_unwind_frame (unwind_info, unwind_info_len, (guint8*)ji->code_start,
						   (guint8*)ji->code_start + ji->code_size,
						   (guint8*)ip, NULL, regs, MONO_MAX_IREGS + 8,
						   save_locations, MONO_MAX_IREGS, (guint8**)&cfa);

		if (!success)
			return FALSE;

		memcpy (&new_ctx->regs, regs, sizeof (host_mgreg_t) * 32);
		for (int i = 0; i < 8; i++)
			*((host_mgreg_t*)&new_ctx->fregs [8 + i]) = (regs + MONO_MAX_IREGS) [i];

		new_ctx->pc = regs [ARMREG_LR];
		new_ctx->regs [ARMREG_SP] = (host_mgreg_t)(gsize)cfa;

		if (*lmf && (*lmf)->gregs [MONO_ARCH_LMF_REG_SP] && (MONO_CONTEXT_GET_SP (ctx) >= (gpointer)(*lmf)->gregs [MONO_ARCH_LMF_REG_SP])) {
			/* remove any unused lmf */
			*lmf = (MonoLMF*)(((gsize)(*lmf)->previous_lmf) & ~3);
		}

		/* we substract 1, so that the IP points into the call instruction */
		new_ctx->pc--;

		return TRUE;
	} else if (*lmf) {
		g_assert ((((guint64)(*lmf)->previous_lmf) & 2) == 0);

		frame->type = FRAME_TYPE_MANAGED_TO_NATIVE;

		ji = mini_jit_info_table_find (domain, (gpointer)(*lmf)->pc, NULL);
		if (!ji)
			return FALSE;

		g_assert (MONO_ARCH_LMF_REGS == ((0x3ff << 19) | (1 << ARMREG_FP) | (1 << ARMREG_SP)));
		memcpy (&new_ctx->regs [ARMREG_R19], &(*lmf)->gregs [0], sizeof (host_mgreg_t) * 10);
		new_ctx->regs [ARMREG_FP] = (*lmf)->gregs [MONO_ARCH_LMF_REG_FP];
		new_ctx->regs [ARMREG_SP] = (*lmf)->gregs [MONO_ARCH_LMF_REG_SP];
		new_ctx->pc = (*lmf)->pc;

		/* we substract 1, so that the IP points into the call instruction */
		new_ctx->pc--;

		*lmf = (MonoLMF*)(((gsize)(*lmf)->previous_lmf) & ~3);

		return TRUE;
	}

	return FALSE;
}

/*
 * handle_exception:
 *
 *   Called by resuming from a signal handler.
 */
static void
handle_signal_exception (gpointer obj)
{
	MonoJitTlsData *jit_tls = mono_tls_get_jit_tls ();
	MonoContext ctx;

	memcpy (&ctx, &jit_tls->ex_ctx, sizeof (MonoContext));

	mono_handle_exception (&ctx, (MonoObject*)obj);

	mono_restore_context (&ctx);
}

/*
 * This is the function called from the signal handler
 */
gboolean
mono_arch_handle_exception (void *ctx, gpointer obj)
{
#if defined(MONO_CROSS_COMPILE)

	g_assert_not_reached();

#elif defined(MONO_ARCH_USE_SIGACTION)

	MonoJitTlsData *jit_tls;
	void *sigctx = ctx;

	/*
	 * Resume into the normal stack and handle the exception there.
	 */
	jit_tls = mono_tls_get_jit_tls ();

	/* Pass the ctx parameter in TLS */
	mono_sigctx_to_monoctx (sigctx, &jit_tls->ex_ctx);
	/* The others in registers */
	UCONTEXT_REG_R0 (sigctx) = (gsize)obj;

	gpointer addr = (gpointer)handle_signal_exception;
	UCONTEXT_REG_SET_PC (sigctx, addr);
	host_mgreg_t sp = UCONTEXT_REG_SP (sigctx) - MONO_ARCH_REDZONE_SIZE;
	UCONTEXT_REG_SET_SP (sigctx, sp);
#else
	MonoContext mctx;

	mono_sigctx_to_monoctx (ctx, &mctx);

	mono_handle_exception (&mctx, obj);

	mono_monoctx_to_sigctx (&mctx, ctx);

#endif
	return TRUE;
}

gpointer
mono_arch_ip_from_context (void *sigctx)
{
#ifdef MONO_CROSS_COMPILE
	g_assert_not_reached ();
	return NULL;
#else
	return (gpointer)UCONTEXT_REG_PC (sigctx);
#endif
}

void
mono_arch_setup_async_callback (MonoContext *ctx, void (*async_cb)(void *fun), gpointer user_data)
{
	host_mgreg_t sp = (host_mgreg_t)MONO_CONTEXT_GET_SP (ctx);

	// FIXME:
	g_assert (!user_data);

	/* Allocate a stack frame */
	sp -= 32;
	MONO_CONTEXT_SET_SP (ctx, sp);

	mono_arch_setup_resume_sighandler_ctx (ctx, (gpointer)async_cb);
}

/*
 * mono_arch_setup_resume_sighandler_ctx:
 *
 *   Setup CTX so execution continues at FUNC.
 */
void
mono_arch_setup_resume_sighandler_ctx (MonoContext *ctx, gpointer func)
{
	MONO_CONTEXT_SET_IP (ctx,func);
}

void
mono_arch_undo_ip_adjustment (MonoContext *ctx)
{
	gpointer pc = (gpointer)ctx->pc;
	pc = (gpointer)((guint64)MINI_FTNPTR_TO_ADDR (pc) + 1);
	ctx->pc = (host_mgreg_t)MINI_ADDR_TO_FTNPTR (pc);
}

void
mono_arch_do_ip_adjustment (MonoContext *ctx)
{
	gpointer pc = (gpointer)ctx->pc;
	pc = (gpointer)((guint64)MINI_FTNPTR_TO_ADDR (pc) - 1);
	ctx->pc = (host_mgreg_t)MINI_ADDR_TO_FTNPTR (pc);
}


#ifdef MONO_ARCH_HAVE_UNWIND_TABLE

static gboolean 
mono_arch_unwind_add_assert (guint32 unwind_code_size, guint32 max_offset, guint32 unwind_codes_buffer_size, guint32 offset) {

	if (offset > max_offset) {
		g_debug("Unwind code max offset exceeded %d > %d", offset, max_offset);
		return FALSE;
	}

	if (unwind_codes_buffer_size + unwind_code_size > MONO_ARM64_MAX_UNWIND_CODE_SIZE) {
		g_debug("Larger allocation needed for the unwind information.");
		return FALSE;
	}

	return TRUE;
}

static void
mono_arch_unwind_add_nop (guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert(1, 0, *unwind_code_size, 0))
		return;

	// nop: 11100011: no unwind operation is required.
	unwind_codes[(*unwind_code_size)++] = 0b11100011;
}

static void
mono_arch_unwind_add_nops (guint8* unwind_codes, guint32* unwind_code_size, int nop_count) {
	while (nop_count--)
		mono_arch_unwind_add_nop (unwind_codes, unwind_code_size);
}

static void
mono_arch_unwind_add_set_fp (guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert(1, 0, *unwind_code_size, 0))
		return;

	// set_fp: 11100001: set up x29 with mov x29,sp
	unwind_codes[(*unwind_code_size)++] = 0b11100001;
}

static void
mono_arch_unwind_add_pac_sign_lr (guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert(1, 0, *unwind_code_size, 0))
		return;

	// pac_sign_lr: 11111100: sign the return address in lr with pacibsp
	unwind_codes[(*unwind_code_size)++] = 0b11111100;
}

static void
mono_arch_unwind_add_end (guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert (1, 0, *unwind_code_size, 0))
		return;

	// end: 11100100: end of unwind code. Implies ret in epilog.
	unwind_codes[(*unwind_code_size)++] = 0b11100100;
}

static void
mono_arch_unwind_add_end_c (guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert(1, 0, *unwind_code_size, 0))
		return;

	// end_c: 11100101: end of unwind code in current chained scope.
	unwind_codes[(*unwind_code_size)++] = 0b11100101;
}

#define WIN_ARM64_SAVE_REG_MAX_OFFSET 504
#define WIN_ARM64_SAVE_REGP_MAX_OFFSET 512

static void
mono_arch_unwind_add_save_fplr (guint8* unwind_codes, guint32* unwind_code_size, gint32 offset) {
	if (!mono_arch_unwind_add_assert(1, WIN_ARM64_SAVE_REG_MAX_OFFSET, *unwind_code_size, offset))
		return;

	// save_fplr: 01zzzzzz: save <x29,lr> pair at [sp+#Z*8], offset <= 504.
	unwind_codes[(*unwind_code_size)++] = 0b01000000 | offset / 8;
}

static void
mono_arch_unwind_add_save_fplr_x (guint8* unwind_codes, guint32* unwind_code_size, gint32 offset) {
	if (!mono_arch_unwind_add_assert(1, WIN_ARM64_SAVE_REGP_MAX_OFFSET, *unwind_code_size, offset))
		return;

	// save_fplr_x: 10zzzzzz: save <x29, lr> pair at[sp - (#Z + 1) * 8]!, pre - indexed offset >= -512
	unwind_codes[(*unwind_code_size)++] = 0b10000000 | (offset-8) / 8;
}

static void
mono_arch_unwind_add_save_reg (guint8* unwind_codes, guint32* unwind_code_size, guint32 reg, guint32 offset, gboolean reverse) {
	if (!mono_arch_unwind_add_assert(2, WIN_ARM64_SAVE_REG_MAX_OFFSET, *unwind_code_size, offset))
		return;

	guint8 reg_offset = reg - ARMREG_R19;
	int order = reverse ? 1 : 0;

	// save_reg: 110100xx'xxzzzzzz: save reg x(19+#X) at [sp+#Z*8], offset <= 504
	unwind_codes[*unwind_code_size + order] = 0b11010000 | reg_offset / 4;
	unwind_codes[*unwind_code_size + 1 - order] = (reg_offset & 0x3) << 6 | (offset / 8);

	*(unwind_code_size) += 2;
}

static void
mono_arch_unwind_add_save_regp (guint8* unwind_codes, guint32* unwind_code_size, guint32 reg, guint32 offset, gboolean reverse) {
	if (!mono_arch_unwind_add_assert(2, WIN_ARM64_SAVE_REG_MAX_OFFSET, *unwind_code_size, offset))
		return;

	guint8 reg_offset = reg - ARMREG_R19;
	int order = reverse ? 1 : 0;

	// save_regp: 110010xx'xxzzzzzz: save x(19+#X) pair at [sp+#Z*8], offset <= 504
	unwind_codes[*unwind_code_size + order] = 0b11001000 | reg_offset / 4;
	unwind_codes[*unwind_code_size + 1 - order] = (reg_offset & 0x3) << 6 | (offset / 8);
	*(unwind_code_size) += 2;
}

static void 
mono_arch_unwind_add_save_next(guint8* unwind_codes, guint32* unwind_code_size) {
	if (!mono_arch_unwind_add_assert(1, 0, *unwind_code_size, 0))
		return;

	// save_next: 11100110: save next non-volatile Int or FP register pair.
	unwind_codes[(*unwind_code_size)++] = 0b11100110;
}

static void
mono_arch_unwind_add_alloc_s (guint8* unwind_codes, guint32* unwind_code_size, guint32 size) {
	if (!mono_arch_unwind_add_assert(1, 511, *unwind_code_size, size))
		return;

	g_assert(size % MONO_ARCH_FRAME_ALIGNMENT == 0);

	// alloc_s: 000xxxxx: allocate small stack with size < 512 (2^5 * 16).
	unwind_codes[(*unwind_code_size)++] = 0b00000000 | (size / MONO_ARCH_FRAME_ALIGNMENT);
}

static void
mono_arch_unwind_add_alloc_m (guint8* unwind_codes, guint32* unwind_code_size, guint32 size, gboolean reverse) {

	if (!mono_arch_unwind_add_assert(2, 0x7FFF, *unwind_code_size, size))
		return;

	g_assert(size % MONO_ARCH_FRAME_ALIGNMENT == 0);

	int order = reverse ? 1 : 0;
	size = size / MONO_ARCH_FRAME_ALIGNMENT; // Size is multiples of 16

	// alloc_m: 11000xxx'xxxxxxxx: allocate large stack with size < 32K (2^11 * 16).
	unwind_codes[*unwind_code_size + order] = 0b11000000 | (size >> 8);
	unwind_codes[*unwind_code_size + 1 - order] = size & 0xFF;

	*(unwind_code_size) += 2;
}

static void
mono_arch_unwind_add_alloc_l (guint8* unwind_codes, guint32* unwind_code_size, guint32 size, gboolean reverse) {
	if (!mono_arch_unwind_add_assert(4, 0x0FFFFFFF, *unwind_code_size, size))
		return;

	g_assert(size % MONO_ARCH_FRAME_ALIGNMENT == 0);

	int order = reverse ? 1 : 0;
	size = size / MONO_ARCH_FRAME_ALIGNMENT; // Size is multiples of 16

	// alloc_l: 11100000'xxxxxxxx'xxxxxxxx'xxxxxxxx: allocate large stack with size < 256M (2^24 * 16)
	unwind_codes[*unwind_code_size + order * 3] = 0b11100000;
	unwind_codes[*unwind_code_size + 1 + order] = size >> 16;
	unwind_codes[*unwind_code_size + 2 - order] = (size >> 8) & 0xFF;
	unwind_codes[*unwind_code_size + 3 - order * 3] = size & 0xFF;

	(*unwind_code_size) += 4;
}

static void
mono_arch_unwind_add_alloc_x (guint8* unwind_codes, guint32* unwind_code_size, guint32 size, gboolean reverse) {

	if (size < 512)
		mono_arch_unwind_add_alloc_s (unwind_codes, unwind_code_size, size);
	else if (size < 0x8000)
		mono_arch_unwind_add_alloc_m (unwind_codes, unwind_code_size, size, reverse);
	else
		mono_arch_unwind_add_alloc_l (unwind_codes, unwind_code_size, size, reverse);
}

static int
mono_arch_unwind_add_reg_offset (MonoUnwindOp* unwind_op_data, GSList* next, gint stack_offset, guint8 *unwind_codes, guint32 *unwind_code_size, MonoUnwindOp** last_saved_regp, gboolean processing_prolog_codes) {

	if (unwind_op_data->reg == ARMREG_SP) {
		g_debug("I don't believe there is a way to encode this in the unwind info");
		return 0;
	}

	if (unwind_op_data->reg == ARMREG_FP || unwind_op_data->reg == ARMREG_LR) {
		// FP and LR are handled seperately, see mono_arch_uwwind_add_frame_setup
		return 0;
	}

	if (unwind_op_data->reg < ARMREG_R19 && unwind_op_data->reg > ARMREG_R28) {
		// Only the presreved registers have unwind codes
		// We shouldn't hit here, but emit a nop if we do
		g_debug("Non preserved register stored in unwind info %d", unwind_op_data->reg);
		mono_arch_unwind_add_nop(unwind_codes, unwind_code_size);
		return 1;
	}

	MonoUnwindOp* next_unwind_op_data = NULL;
	guint offset = stack_offset + unwind_op_data->val;

	if (next && next->data)
		next_unwind_op_data = (MonoUnwindOp*)next->data;

	gint next_reg_dir = processing_prolog_codes ? 1 : -1;
	guint8 op = processing_prolog_codes ? DW_CFA_offset : DW_CFA_mono_restore_offset_win64_arm64;

	// Mono emits paired reg loads/stores (stp/ldp) for adjacent regs, but still emits seperate MonoUnwindOp foreach
	// Detect those cases and emit a single unwind code so to match the emitted assembly
	// The 256 value is from mini-arm64.c::emit_store_regset_cfa, when we exeed that mono won't emit regp stores/loads
	if (offset < 256 && next_unwind_op_data && next_unwind_op_data->op == op && next_unwind_op_data->reg == unwind_op_data->reg + next_reg_dir && next_unwind_op_data->val == unwind_op_data->val + next_reg_dir * sizeof(host_mgreg_t)) {

		if (*last_saved_regp && (*last_saved_regp)->reg == unwind_op_data->reg - 2*next_reg_dir && unwind_op_data->val == (*last_saved_regp)->val + 2*next_reg_dir*sizeof(host_mgreg_t))
			mono_arch_unwind_add_save_next (unwind_codes, unwind_code_size);
		else 
			mono_arch_unwind_add_save_regp (unwind_codes, unwind_code_size, unwind_op_data->reg, offset, processing_prolog_codes);

		*last_saved_regp = unwind_op_data;
		return 2;
	}

	*last_saved_regp = NULL;

	// The Windows ARM64 unwind codes are setup assuming that the preserved registers are stored at the bottom of the frame, but Mono places them at the top.
	// So we end up with offsets we can't encode, so we'll just emit nops for those (we emit nops to so we correctly encode the size of the prolog)
	// Saving the preserved regs allows unwinding in the prolog/epilog which insn't needed for correct stack traces, so there doesn't seem to be a critical issue
	if (arm_is_strx_imm(offset)) {
		if (offset < WIN_ARM64_SAVE_REG_MAX_OFFSET)
			mono_arch_unwind_add_save_reg (unwind_codes, unwind_code_size, unwind_op_data->reg, offset, processing_prolog_codes);
		else
			mono_arch_unwind_add_nop (unwind_codes, unwind_code_size); // There is no way to encode an offset this large
	}
	else {
		// Mono emits this an a mov xip0, #offset; str rn,[sp+xip0]
		// There doesn't appear to be any way to encode this in the unwind codes, so just emit nops
		mono_arch_unwind_add_nop (unwind_codes, unwind_code_size);
		mono_arch_unwind_add_nop (unwind_codes, unwind_code_size);
	}

	return 1;
}

static void
mono_arch_unwind_add_emit_subx_sp_imm (gint offset, guint8* unwind_codes, guint32* unwind_code_size, gboolean reverse) {

	/* Encodes emit_subx_sp_imm:
		mov         xip0, offet & 0xFFFF
		movk        xip0, #4, lsl offset >> 16 // if offset > 0xFFFF
		mov         xip1, sp
		add         xip1, xip1, xip0
		mov	    sp, xip1
	*/
	mono_arch_unwind_add_nops(unwind_codes, unwind_code_size, offset > 0xFFFF ? 4 : 3);
	mono_arch_unwind_add_alloc_x (unwind_codes, unwind_code_size, offset, reverse);
}

static void 
mono_arch_uwwind_add_frame_setup(guint cfa_offset, guint8* unwind_codes, guint32* unwind_code_size, gboolean reverse) {

	// Frame Setup
	// This matches the logic in mini-arm64.c::mono_arch_emit_prolog
	if (arm_is_ldpx_imm(-cfa_offset)) {
		mono_arch_unwind_add_save_fplr_x(unwind_codes, unwind_code_size, cfa_offset);
	}
	else {
		mono_arch_unwind_add_emit_subx_sp_imm(cfa_offset, unwind_codes, unwind_code_size, reverse);
		mono_arch_unwind_add_save_fplr(unwind_codes, unwind_code_size, 0);
	}
}

typedef struct _InitilizedWindowInfoResult {
	gint32 saved_int_regs;
	guint32 cfa_offset;
	gboolean can_use_packed_format;
	gboolean needs_seperate_epilog;
	gboolean has_restore_reg_ops;
} InitilizedWindowInfoResult;

static InitilizedWindowInfoResult
initialize_unwind_info_prolog (GSList* unwind_ops, guint8 *unwind_codes, guint32 *unwind_code_size) {

	InitilizedWindowInfoResult res = { 0 };
	res.needs_seperate_epilog = FALSE;
	res.can_use_packed_format = TRUE;
	res.has_restore_reg_ops = FALSE;

	if (enable_ptrauth)
		mono_arch_unwind_add_pac_sign_lr (unwind_codes, unwind_code_size);

	gint32 last_saved_in_order_reg = -1;
	guint32 last_saved_reg_offset = -1;
	guint cfa_offset = -1;
	
	MonoUnwindOp* unwind_op_data;
	MonoUnwindOp* last_saved_regp = NULL;;

	// Replay collected unwind info and setup Windows format.
	for (GSList* l = unwind_ops; l; l = l->next) {
		unwind_op_data = (MonoUnwindOp*)l->data;

		switch (unwind_op_data->op) {
		case DW_CFA_mono_prolog_nop_win64_arm64:
			res.needs_seperate_epilog = TRUE;
			mono_arch_unwind_add_nops (unwind_codes, unwind_code_size, unwind_op_data->val);
			break;
		case DW_CFA_mono_epilog_nop_win64_arm64:
			res.needs_seperate_epilog = TRUE;
			break;
		case DW_CFA_mono_restore_offset_win64_arm64: 
			res.needs_seperate_epilog = TRUE;
			res.has_restore_reg_ops = TRUE;
			break;
		case DW_CFA_def_cfa_offset:
			cfa_offset = unwind_op_data->val;
			break;
		case DW_CFA_offset: {

			// DW_CFA_def_cfa_offset must have been emitted before any DW_CFA_offset
			// We expect the stack frame to be setup before saving any regs
			g_assert(cfa_offset >= 0);

			guint offset = cfa_offset + unwind_op_data->val;
			int reg_count;

			if (unwind_op_data->reg == ARMREG_FP)
			{
				mono_arch_uwwind_add_frame_setup(cfa_offset, unwind_codes, unwind_code_size, TRUE);
				mono_arch_unwind_add_set_fp(unwind_codes, unwind_code_size);

				break;
			}

			reg_count = mono_arch_unwind_add_reg_offset(unwind_op_data, l->next, cfa_offset, unwind_codes, unwind_code_size, &last_saved_regp, TRUE);

			if (reg_count > 0) {
				if (last_saved_in_order_reg == -1)
					res.can_use_packed_format = res.can_use_packed_format && (unwind_op_data->reg == ARMREG_R19);
				else if (unwind_op_data->reg != last_saved_in_order_reg + 1 || offset != last_saved_reg_offset + sizeof(host_mgreg_t))
					res.can_use_packed_format = FALSE;

				res.saved_int_regs += reg_count;
				last_saved_reg_offset = offset + (reg_count - 1) * sizeof(host_mgreg_t);
				last_saved_in_order_reg = unwind_op_data->reg + (reg_count - 1);
				while (--reg_count > 0)
					l = l->next;
			}

			break;
		}
		}
	}

	res.cfa_offset = cfa_offset > 0 ? cfa_offset : 0;
	return res;
}

static void
initialize_unwind_info_epilog (GSList* unwind_ops, gint cfa_offset, guint8 *unwind_codes, guint32 *unwind_code_size, gboolean has_restore_reg_ops) {
	
	MonoUnwindOp* unwind_op_data;
	MonoUnwindOp* last_saved_regp = NULL;

	guint32 dwa_cfa_offset_start = *unwind_code_size;

	// Replay collected unwind info and setup Windows format.
	for (GSList* l = unwind_ops; l; l = l->next) {
		unwind_op_data = (MonoUnwindOp*)l->data;

		switch (unwind_op_data->op) {
		case DW_CFA_mono_epilog_nop_win64_arm64:
			mono_arch_unwind_add_nops (unwind_codes, unwind_code_size, unwind_op_data->val);
			break;
		case DW_CFA_offset:
		case DW_CFA_mono_restore_offset_win64_arm64: {

			// If the method has restore codes process them
			// otherwise process the 
			if (has_restore_reg_ops && unwind_op_data->op == DW_CFA_offset)
				break;

			int reg_count;
			reg_count = mono_arch_unwind_add_reg_offset(unwind_op_data, l->next, cfa_offset, unwind_codes, unwind_code_size, &last_saved_regp, !has_restore_reg_ops);
			while (--reg_count > 0)
				l = l->next;

			break;
		}
		}

	}

	if (!has_restore_reg_ops)
	{
		guint32 dwa_cfa_offset_end = *unwind_code_size;

		// We processed DW_CFA_offsets, which are in the oposize order we need them, reverse them now
		for (int i = 0; i < (dwa_cfa_offset_end - dwa_cfa_offset_start) / 2; i++)
		{
			guint8 swap = unwind_codes[dwa_cfa_offset_start + i];
			unwind_codes[dwa_cfa_offset_start + i] = unwind_codes[dwa_cfa_offset_end - i - 1];
			unwind_codes[dwa_cfa_offset_end - i - 1] = swap;
		}
	}

	mono_arch_unwind_add_set_fp (unwind_codes, unwind_code_size);
	mono_arch_uwwind_add_frame_setup(cfa_offset, unwind_codes, unwind_code_size, FALSE);
	if (enable_ptrauth)
		mono_arch_unwind_add_pac_sign_lr (unwind_codes, unwind_code_size);
}

static guint
mono_arch_unwinddata_get_size (UnwindData* unwinddata) {

	if (unwinddata->pdata.Flag == PdataRefToFullXdata)
		return sizeof(UnwindData) - sizeof(unwinddata->unwind_codes) + ((unwinddata->xdata.CodeWords) * sizeof(guint32));
	return sizeof(RUNTIME_FUNCTION);
}

static guint
mono_arch_unwindinfo_get_size (UnwindInfo* unwindinfo) {

	guint size = 0;
	for (guint i = 0; i < unwindinfo->count; i++)
		size += mono_arch_unwinddata_get_size(&unwindinfo->unwind_data[i]);
	return size;
}

PUnwindInfo
mono_arch_unwindinfo_init_method_unwind_info_ex (GSList* unwind_ops, guint epilog_start, guint epilog_end, guint code_len) {

	guint32 code_word_size = 0, epliog_unwind_codes_start = 0;
	InitilizedWindowInfoResult res;
	guint8 unwind_codes[MONO_ARM64_MAX_UNWIND_CODE_SIZE];
	UnwindData* unwinddata = NULL;
	guint numSectionsNeeded = 1;

	if (unwind_ops == NULL)
		return 0;

	res = initialize_unwind_info_prolog (unwind_ops, unwind_codes, &code_word_size);

	if (res.can_use_packed_format && !res.needs_seperate_epilog && code_len < 0x2000 && res.cfa_offset < 0x2000 && epilog_end == code_len) {

		unwinddata = (PUnwindData)g_new0(RUNTIME_FUNCTION, 1);
		// We can use the packed format
		unwinddata->pdata.Flag = PdataPackedUnwindFunction;
		unwinddata->pdata.FunctionLength = code_len / MONO_ARM64_UNWIND_WORD_SIZE;
		unwinddata->pdata.FrameSize = res.cfa_offset / MONO_ARCH_FRAME_ALIGNMENT;
		unwinddata->pdata.CR = enable_ptrauth ? PdataCrChainedWithPac : PdataCrChained;
		unwinddata->pdata.RegI = res.saved_int_regs;
		unwinddata->pdata.RegF = 0; // No floating point registers saved
		unwinddata->pdata.H = 0;    // No home area
	}
	else {
		PUnwindData current;
		guint8* prolog_unwind_codes;
		guint prolog_unwind_codes_len;
		guint segment_start = 0;

		numSectionsNeeded = (code_len / MONO_ARM64_UNWIND_LARGE_FUNCTION_SIZE) + 1;
		// We can't split the epilog - create a sepreate section
		if (epilog_start / MONO_ARM64_UNWIND_LARGE_FUNCTION_SIZE != epilog_end / MONO_ARM64_UNWIND_LARGE_FUNCTION_SIZE)
			numSectionsNeeded++;

		unwinddata = (PUnwindData)g_new0(UnwindData, numSectionsNeeded);
		current = unwinddata;

		// Reverse the codewords - they need to be in reverse order
		for (int i = 0; i < code_word_size; i++)
			current->unwind_codes[i] = unwind_codes[code_word_size - i - 1];

		mono_arch_unwind_add_end (current->unwind_codes, &code_word_size);
		// Count of 32 bit words

		prolog_unwind_codes = current->unwind_codes;
		prolog_unwind_codes_len = code_word_size;

		while (segment_start < code_len) {

			guint segment_len = min(code_len - segment_start, MONO_ARM64_UNWIND_LARGE_FUNCTION_SIZE);

			current->pdata.Flag = PdataRefToFullXdata;
			current->xdata.FunctionLength = segment_len / MONO_ARM64_UNWIND_WORD_SIZE;
			current->xdata.Version = 0;
			current->xdata.ExceptionDataPresent = 0;			// No exception handle data
			current->xdata.EpilogInHeader = 1;				// Only one epilog in the header
			current->xdata.EpilogCount = 0;					// When EpilogInHeader is true, EpilogCount is an offset into unwind codes

			if (current != unwinddata) {
				code_word_size = 0;
				mono_arch_unwind_add_end_c (current->unwind_codes, &code_word_size);
				memcpy(&current->unwind_codes[code_word_size], prolog_unwind_codes, prolog_unwind_codes_len);
				code_word_size += prolog_unwind_codes_len;
			}

			current->xdata.CodeWords = ALIGN_TO(code_word_size, MONO_ARM64_UNWIND_WORD_SIZE) / MONO_ARM64_UNWIND_WORD_SIZE;

			segment_start += segment_len;
			current++;
		}

		if (res.needs_seperate_epilog) {
			current--;

			current->xdata.EpilogCount = code_word_size;	// When EpilogInHeader is true, EpilogCount is an offset into unwind codes
			initialize_unwind_info_epilog (unwind_ops, res.cfa_offset, current->unwind_codes, &code_word_size, res.has_restore_reg_ops);
			mono_arch_unwind_add_end (current->unwind_codes, &code_word_size);

			// Count of 32 bit words
			unwinddata->xdata.CodeWords = ALIGN_TO(code_word_size, MONO_ARM64_UNWIND_WORD_SIZE) / MONO_ARM64_UNWIND_WORD_SIZE;
		}
	}

	PUnwindInfo unwindinfo = (PUnwindInfo)g_new0(UnwindInfo, 1);
	unwindinfo->unwind_data = unwinddata;
	unwindinfo->count = numSectionsNeeded;

	return unwindinfo;
}

guint
mono_arch_unwindinfo_init_method_unwind_info (gpointer cfg) {

	MonoCompile* current_cfg = (MonoCompile*)cfg;

	UnwindInfo* unwindinfo = mono_arch_unwindinfo_init_method_unwind_info_ex (current_cfg->unwind_ops, current_cfg->epilog_begin, current_cfg->epilog_end, current_cfg->code_len);

	current_cfg->arch.unwindinfo = unwindinfo;

	if (unwindinfo == NULL)
		return 0;

	return mono_arch_unwindinfo_get_size(unwindinfo);
}

void
mono_arch_unwindinfo_install_tramp_unwind_info (GSList* unwind_ops, gpointer code, guint code_size) {

	if (unwind_ops == NULL)
		return;

	UnwindInfo* unwindinfo = mono_arch_unwindinfo_init_method_unwind_info_ex (unwind_ops, 0, code_size, code_size);
	if (unwindinfo != NULL)
	{
		// We only allocate enough data for the compressed unwind info for trampolines -- see MONO_TRAMPOLINE_UNWINDINFO_SIZE
		g_assert(MONO_TRAMPOLINE_UNWINDINFO_SIZE == mono_arch_unwindinfo_get_size (unwindinfo));

		if (MONO_TRAMPOLINE_UNWINDINFO_SIZE == mono_arch_unwindinfo_get_size (unwindinfo))
			mono_arch_unwindinfo_install_method_unwind_info (&unwindinfo, code, code_size);
	}
}

static PUnwindData mono_arch_unwind_info_from_code (gpointer code, guint code_size) {

	guint64 targetlocation;

	targetlocation = (guint64) & (((guchar*)code)[code_size]);
	g_assert(targetlocation % MONO_ARM64_UNWIND_WORD_SIZE == 0);
	return (PUnwindData)targetlocation;
}

void
mono_arch_unwindinfo_install_method_unwind_info (PUnwindInfo* monoui, gpointer code, guint code_size) {

	PUnwindInfo unwindinfo;
	PUnwindData targetinfo;
	guchar codecount;
	guint64 targetlocation;
	gpointer code_segment = code;

	if (!*monoui)
		return;

	unwindinfo = *monoui;

	targetlocation = (guint64) & (((guchar*)code)[code_size]);
	g_assert(targetlocation % MONO_ARM64_UNWIND_WORD_SIZE == 0);
	targetinfo = mono_arch_unwind_info_from_code (code, code_size);

	for (guint i = 0; i < unwindinfo->count; i++) {
		guint segment_length;
		PUnwindData unwinddata = &unwindinfo->unwind_data[i];
		guint32 unwind_data_size = mono_arch_unwinddata_get_size (unwinddata);
		memcpy(targetinfo, unwinddata, unwind_data_size);

		if (unwinddata->pdata.Flag == PdataRefToFullXdata)
			segment_length = unwinddata->xdata.FunctionLength * MONO_ARM64_UNWIND_WORD_SIZE;
		else
			segment_length = unwinddata->pdata.FunctionLength * MONO_ARM64_UNWIND_WORD_SIZE;

		mono_arch_unwindinfo_insert_rt_func_in_table (code_segment, segment_length, targetinfo);
		code_segment = (gchar*)code_segment + segment_length;
		targetinfo = (PUnwindData)((gchar*)targetinfo +  unwind_data_size);
	}

	g_free(unwindinfo->unwind_data);
	g_free(unwindinfo);
	*monoui = NULL;
}

RUNTIME_FUNCTION
mono_arch_unwindinfo_init (gpointer code, gsize code_offset, gsize code_size, gsize begin_range, gsize end_range, const gpointer unwinddata) {

	RUNTIME_FUNCTION new_rt_func_data;

	PUnwindData targetinfo = (PUnwindData)unwinddata;

	new_rt_func_data.BeginAddress = code_offset;
	if (targetinfo->pdata.Flag == PdataRefToFullXdata)
	{
		// Update the pdata to point to the RVA for the xdata
		new_rt_func_data.UnwindData = code_offset + ((guint64)targetinfo - (guint64)code) + sizeof(RUNTIME_FUNCTION);
	}
	else
	{
		// Unwind data is the MONO_ARM64_UNWIND_WORD_SIZE stored in UnwindData
		new_rt_func_data.UnwindData = targetinfo->pdata.UnwindData;
	}

	return new_rt_func_data;
}

guint32
mono_arch_unwindinfo_get_end_address (gpointer rvaRoot, PRUNTIME_FUNCTION func) {

	if (func->Flag == PdataRefToFullXdata) {
		PUnwindData unwindInfo = (PUnwindData)func;
		IMAGE_ARM64_RUNTIME_FUNCTION_ENTRY_XDATA* xData = (IMAGE_ARM64_RUNTIME_FUNCTION_ENTRY_XDATA*)((guint8*)mono_arch_unwindinfo_init + func->UnwindData + sizeof(guint32));
		return func->BeginAddress + xData->FunctionLength * MONO_ARM64_UNWIND_WORD_SIZE;
	}

	return func->BeginAddress + func->FunctionLength * MONO_ARM64_UNWIND_WORD_SIZE;
}

gboolean
mono_arch_unwindinfo_emit_epilog_codes (guint64 saved_regset, guint64 restore_regset) {
	return saved_regset != restore_regset;
}

#endif /* MONO_ARCH_HAVE_UNWIND_TABLE*/
