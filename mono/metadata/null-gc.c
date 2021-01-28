/**
 * \file
 * GC implementation using malloc: will leak everything, just for testing.
 *
 * Copyright 2001-2003 Ximian, Inc (http://www.ximian.com)
 * Copyright 2004-2011 Novell, Inc (http://www.novell.com)
 * Copyright 2011 Xamarin, Inc (http://www.xamarin.com)
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

#include "config.h"

typedef struct _NullGCThreadInfo NullGCThreadInfo;
#undef THREAD_INFO_TYPE
#define THREAD_INFO_TYPE NullGCThreadInfo

#include <glib.h>
#include <mono/metadata/mono-gc.h>
#include <mono/metadata/gc-internals.h>
#include <mono/metadata/runtime.h>
#include <mono/metadata/w32handle.h>
#include <mono/utils/atomic.h>
#include <mono/utils/mono-mmap.h>
#include <mono/utils/mono-threads.h>
#include <mono/utils/mono-counters.h>
#include <mono/metadata/null-gc-handles.h>



struct _NullGCThreadInfo {
	MonoThreadInfo info;

	/*
	 * `skip` is set to TRUE when STW fails to suspend a thread, most probably because
	 * the underlying thread is dead.
	*/
	gboolean skip, suspend_done;
	volatile int in_critical_region;

	/*
	This is set the argument of mono_gc_set_skip_thread.

	A thread that knowingly holds no managed state can call this
	function around blocking loops to reduce the GC burden by not
	been scanned.
	*/
	gboolean gc_disabled;
//
//#ifdef SGEN_POSIX_STW
//	/* This is -1 until the first suspend. */
//	int signal;
//	/* FIXME: kill this, we only use signals on systems that have rt-posix, which doesn't have issues with duplicates. */
//	unsigned int stop_count; /* to catch duplicate signals. */
//#endif
//
//	gpointer runtime_data;
//
//	void* stack_end;
//	void* stack_start;
//	void* stack_start_limit;
//
//	MonoContext ctx;		/* ditto */
};

#ifdef HAVE_NULL_GC

static gboolean gc_inited = FALSE;

struct _GCMemChunk;
typedef struct _GCMemChunk GCMemChunk;

struct _GCMemChunk
{
	GCMemChunk* next;
	char* start_of_memory;
	char* current_memory;
	size_t length;
};

typedef struct GCMemPool
{
	GCMemChunk* chunks;
	size_t page_size;
} GCMemPool;

static mono_mutex_t nullgc_mutex;

static GCMemPool* init_gc_mempool ()
{
	GCMemPool* mp = g_new0 (GCMemPool, 1);
	mp->page_size = mono_pagesize ();

	return mp;
}

static void gc_free_mempool (GCMemPool* mp)
{
	if (!mp)
		return;

	GCMemChunk* chunk = mp->chunks;
	while (chunk) {

		int res = VirtualFree (chunk->start_of_memory, chunk->length, MEM_DECOMMIT);
		g_assert (res);

		GCMemChunk* prev = chunk;
		chunk = chunk->next;
		g_free (prev);
	}
	g_free (mp);
}

void
mono_gc_base_init (void)
{
	if (gc_inited)
		return;

	mono_os_mutex_init (&nullgc_mutex);

	mono_counters_init ();

#ifndef HOST_WIN32
	mono_w32handle_init ();
#endif

	mono_thread_callbacks_init ();
	mono_thread_info_init (sizeof (NullGCThreadInfo));

	mono_thread_info_attach ();

	null_gc_handles_init ();

	gc_inited = TRUE;
}

void
mono_gc_base_cleanup (void)
{
}

void
mono_gc_collect (int generation)
{
}

void
mono_gc_start_incremental_collection()
{
}

int
mono_gc_max_generation (void)
{
	return 0;
}

int
mono_gc_get_generation  (MonoObject *object)
{
	return 0;
}

int
mono_gc_collection_count (int generation)
{
	return 0;
}

void
mono_gc_add_memory_pressure (gint64 value)
{
}

/* maybe track the size, not important, though */
int64_t
mono_gc_get_used_size (void)
{
	return 1024*1024;
}

int64_t
mono_gc_get_heap_size (void)
{
	return 2*1024*1024;
}

int64_t
mono_gc_get_max_time_slice_ns()
{
	return 0;
}

void
mono_gc_set_max_time_slice_ns(int64_t maxTimeSlice)
{
}

MonoBoolean 
mono_gc_is_incremental()
{
    return FALSE;
}

void
mono_gc_set_incremental(MonoBoolean value)
{
}

gboolean
mono_gc_is_gc_thread (void)
{
	return TRUE;
}

int
mono_gc_walk_heap (int flags, MonoGCReferences callback, void *data)
{
	return 1;
}

gboolean
mono_object_is_alive (MonoObject* o)
{
	return TRUE;
}

int
mono_gc_register_root (char *start, size_t size, void *descr, MonoGCRootSource source, void *key, const char *msg)
{
	return TRUE;
}

int
mono_gc_register_root_wbarrier (char *start, size_t size, MonoGCDescriptor descr, MonoGCRootSource source, void *key, const char *msg)
{
	return TRUE;
}


void
mono_gc_deregister_root (char* addr)
{
}

void*
mono_gc_make_descr_for_string (gsize *bitmap, int numbits)
{
	return NULL;
}

void*
mono_gc_make_descr_for_object (gsize *bitmap, int numbits, size_t obj_size)
{
	return NULL;
}

void*
mono_gc_make_descr_for_array (int vector, gsize *elem_bitmap, int numbits, size_t elem_size)
{
	return NULL;
}

void*
mono_gc_make_descr_from_bitmap (gsize *bitmap, int numbits)
{
	return NULL;
}

void*
mono_gc_make_vector_descr (void)
{
	return NULL;
}

void*
mono_gc_make_root_descr_all_refs (int numbits)
{
	return NULL;
}

#define ALIGN_TO(val,align) ((((guint64)val) + ((align) - 1)) & ~((align) - 1))

void*
mono_gc_alloc_fixed (size_t size, void *descr, MonoGCRootSource source, void *key, const char *msg)
{
	//return g_malloc0 (size);
	size += sizeof(size_t);
	size = ALIGN_TO (size, mono_pagesize ());
	char* ret = VirtualAlloc (0, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	*(size_t*)ret = size;
	ret += sizeof (size_t);
	return ret;
}

void
mono_gc_free_fixed (void* addr)
{
	char* start = (char*)addr - sizeof (size_t);
	size_t length = *(size_t*)start;
	int res = VirtualFree (start, length, MEM_DECOMMIT);
	g_assert (res);
}

/* only one can be enabled at a time */
#define GUARD_PRELUDE 0
#define GUARD_POSTLUDE 0

static void*
gc_mempool_alloc (MonoDomain* domain, size_t size)
{
	void* ret = NULL;
	mono_os_mutex_lock (&nullgc_mutex);
	GCMemPool* mp = domain->gc_mp;
	if (!mp)
		mp = domain->gc_mp = init_gc_mempool ();

	// keep 16 byte alignment
	size = ALIGN_TO (size, 16);

#if GUARD_PRELUDE
	GCMemChunk* chunk = g_new0 (GCMemChunk, 1);;
	size_t chunk_size = MAX ((mp->page_size), ALIGN_TO (size, mp->page_size));
	chunk_size += mp->page_size;

	chunk->start_of_memory = chunk->current_memory = (char*)VirtualAlloc (0, chunk_size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	chunk->current_memory += mp->page_size;

	DWORD old;
	BOOL res = VirtualProtect (chunk->start_of_memory, mp->page_size, PAGE_NOACCESS, &old);
	g_assert (res);

	ret = chunk->current_memory;


	chunk->length = chunk_size;

	chunk->next = mp->chunks;
	mp->chunks = chunk;
#elif GUARD_POSTLUDE
	GCMemChunk* chunk = g_new0 (GCMemChunk, 1);
	size_t chunk_size = MAX ((mp->page_size), ALIGN_TO (size, mp->page_size));
	chunk_size += mp->page_size;

	chunk->start_of_memory = chunk->current_memory = (char*)VirtualAlloc (0, chunk_size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	/* offset to abut last page */
	chunk->current_memory += (chunk_size - mp->page_size - size);

	DWORD old;
	BOOL res = VirtualProtect (chunk->start_of_memory + chunk_size - mp->page_size, mp->page_size, PAGE_NOACCESS, &old);
	g_assert (res);

	ret = chunk->current_memory;


	chunk->length = chunk_size;

	chunk->next = mp->chunks;
	mp->chunks = chunk;
#else

	GCMemChunk* chunk = mp->chunks;
	if (!chunk || ((chunk->current_memory + size) > (chunk->start_of_memory + chunk->length)))
	{
		chunk = g_new0 (GCMemChunk, 1);
		size_t chunk_size = MAX ((4 * mp->page_size), ALIGN_TO (size, mp->page_size));
		chunk->start_of_memory = chunk->current_memory = (char*)VirtualAlloc (0, chunk_size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
		chunk->length = chunk_size;

		chunk->next = mp->chunks;
		mp->chunks = chunk;
	}

	ret = chunk->current_memory;
	chunk->current_memory += size;

	g_assert (chunk->current_memory <= (chunk->start_of_memory + chunk->length));
#endif

	mono_os_mutex_unlock (&nullgc_mutex);

	return ret;
}

void *
mono_gc_alloc_obj (MonoVTable *vtable, size_t size)
{
	MonoObject *obj = gc_mempool_alloc (vtable->domain, size);

	obj->vtable = vtable;

	return obj;
}

void *
mono_gc_alloc_vector (MonoVTable *vtable, size_t size, uintptr_t max_length)
{
	MonoArray *obj = gc_mempool_alloc (vtable->domain, size);

	obj->obj.vtable = vtable;
	obj->max_length = max_length;

	return obj;
}

void *
mono_gc_alloc_array (MonoVTable *vtable, size_t size, uintptr_t max_length, uintptr_t bounds_size)
{
	MonoArray *obj = gc_mempool_alloc (vtable->domain, size);

	obj->obj.vtable = vtable;
	obj->max_length = max_length;

	if (bounds_size)
		obj->bounds = (MonoArrayBounds *) ((char *) obj + size - bounds_size);

	return obj;
}

void *
mono_gc_alloc_string (MonoVTable *vtable, size_t size, gint32 len)
{
	MonoString *obj = gc_mempool_alloc (vtable->domain, size);

	obj->object.vtable = vtable;
	obj->length = len;
	obj->chars [len] = 0;

	return obj;
}

void*
mono_gc_alloc_mature (MonoVTable *vtable, size_t size)
{
	return mono_gc_alloc_obj (vtable, size);
}

void*
mono_gc_alloc_pinned_obj (MonoVTable *vtable, size_t size)
{
	return mono_gc_alloc_obj (vtable, size);
}

void
mono_gc_wbarrier_set_field (MonoObject *obj, gpointer field_ptr, MonoObject* value)
{
	*(void**)field_ptr = value;
}

void
mono_gc_wbarrier_set_arrayref (MonoArray *arr, gpointer slot_ptr, MonoObject* value)
{
	*(void**)slot_ptr = value;
}

void
mono_gc_wbarrier_arrayref_copy (gpointer dest_ptr, gpointer src_ptr, int count)
{
	mono_gc_memmove_aligned (dest_ptr, src_ptr, count * sizeof (gpointer));
}

void
mono_gc_wbarrier_generic_store (gpointer ptr, MonoObject* value)
{
	*(void**)ptr = value;
}

void
mono_gc_wbarrier_generic_store_atomic (gpointer ptr, MonoObject *value)
{
	mono_atomic_store_ptr (ptr, value);
}

void
mono_gc_wbarrier_generic_nostore (gpointer ptr)
{
}

void
mono_gc_wbarrier_value_copy (gpointer dest, gpointer src, int count, MonoClass *klass)
{
	mono_gc_memmove_atomic (dest, src, count * mono_class_value_size (klass, NULL));
}

void
mono_gc_wbarrier_object_copy (MonoObject* obj, MonoObject *src)
{
	/* do not copy the sync state */
	mono_gc_memmove_aligned ((char*)obj + sizeof (MonoObject), (char*)src + sizeof (MonoObject),
			mono_object_class (obj)->instance_size - sizeof (MonoObject));
}

gboolean
mono_gc_is_critical_method (MonoMethod *method)
{
	return FALSE;
}

gpointer
mono_gc_thread_attach (NullGCThreadInfo* info)
{
	info->info.handle_stack = mono_handle_stack_alloc ();
	return info;
}

void
mono_gc_thread_detach (NullGCThreadInfo*p)
{
}

void
mono_gc_thread_detach_with_lock (NullGCThreadInfo*p)
{
	mono_handle_stack_free (p->info.handle_stack);
}

gboolean
mono_gc_thread_in_critical_region (NullGCThreadInfo*info)
{
	return FALSE;
}

int
mono_gc_get_aligned_size_for_allocator (int size)
{
	return size;
}

MonoMethod*
mono_gc_get_managed_allocator (MonoClass *klass, gboolean for_box, gboolean known_instance_size)
{
	return NULL;
}

MonoMethod*
mono_gc_get_managed_array_allocator (MonoClass *klass)
{
	return NULL;
}

MonoMethod*
mono_gc_get_managed_allocator_by_type (int atype, ManagedAllocatorVariant variant)
{
	return NULL;
}

guint32
mono_gc_get_managed_allocator_types (void)
{
	return 0;
}

const char *
mono_gc_get_gc_name (void)
{
	return "null";
}

void
mono_gc_clear_domain (MonoDomain *domain)
{
	gc_free_mempool (domain->gc_mp);
}

void
mono_gc_suspend_finalizers (void)
{
}

int
mono_gc_get_suspend_signal (void)
{
	return -1;
}

int
mono_gc_get_restart_signal (void)
{
	return -1;
}

MonoMethod*
mono_gc_get_specific_write_barrier (gboolean is_concurrent)
{
	g_assert_not_reached ();
	return NULL;
}

MonoMethod*
mono_gc_get_write_barrier (void)
{
	g_assert_not_reached ();
	return NULL;
}

void*
mono_gc_invoke_with_gc_lock (MonoGCLockedCallbackFunc func, void *data)
{
	return func (data);
}

char*
mono_gc_get_description (void)
{
	return g_strdup (DEFAULT_GC_NAME);
}

void
mono_gc_set_desktop_mode (void)
{
}

gboolean
mono_gc_is_moving (void)
{
	return FALSE;
}

gboolean
mono_gc_needs_write_barriers(void)
{
	return FALSE;
}

gboolean
mono_gc_is_disabled (void)
{
	return FALSE;
}

void
mono_gc_wbarrier_range_copy (gpointer _dest, gpointer _src, int size)
{
	g_assert_not_reached ();
}

void*
mono_gc_get_range_copy_func (void)
{
	return &mono_gc_wbarrier_range_copy;
}

guint8*
mono_gc_get_card_table (int *shift_bits, gpointer *card_mask)
{
	g_assert_not_reached ();
	return NULL;
}

gboolean
mono_gc_card_table_nursery_check (void)
{
	g_assert_not_reached ();
	return TRUE;
}

void*
mono_gc_get_nursery (int *shift_bits, size_t *size)
{
	return NULL;
}

gboolean
mono_gc_precise_stack_mark_enabled (void)
{
	return FALSE;
}

FILE *
mono_gc_get_logfile (void)
{
	return NULL;
}

void
mono_gc_conservatively_scan_area (void *start, void *end)
{
	g_assert_not_reached ();
}

void *
mono_gc_scan_object (void *obj, void *gc_data)
{
	g_assert_not_reached ();
	return NULL;
}

gsize*
mono_gc_get_bitmap_for_descr (void *descr, int *numbits)
{
	g_assert_not_reached ();
	return NULL;
}

void
mono_gc_set_gc_callbacks (MonoGCCallbacks *callbacks)
{
}

void
mono_gc_set_stack_end (void *stack_end)
{
}

int
mono_gc_get_los_limit (void)
{
	return G_MAXINT;
}

gboolean
mono_gc_user_markers_supported (void)
{
	return FALSE;
}

void *
mono_gc_make_root_descr_user (MonoGCRootMarkFunc marker)
{
	g_assert_not_reached ();
	return NULL;
}

#ifndef HOST_WIN32
int
mono_gc_pthread_create (pthread_t *new_thread, const pthread_attr_t *attr, void *(*start_routine)(void *), void *arg)
{
	return pthread_create (new_thread, attr, start_routine, arg);
}
#endif

void mono_gc_set_skip_thread (gboolean value)
{
}

#ifdef HOST_WIN32
BOOL APIENTRY mono_gc_dllmain (HMODULE module_handle, DWORD reason, LPVOID reserved)
{
	return TRUE;
}
#endif

MonoVTable *
mono_gc_get_vtable (MonoObject *obj)
{
	// No pointer tagging.
	return obj->vtable;
}

guint
mono_gc_get_vtable_bits (MonoClass *klass)
{
	return 0;
}

void
mono_gc_register_altstack (gpointer stack, gint32 stack_size, gpointer altstack, gint32 altstack_size)
{
}

gboolean
mono_gc_is_null (void)
{
	return TRUE;
}

int
mono_gc_invoke_finalizers (void)
{
	return 0;
}

MonoBoolean
mono_gc_pending_finalizers (void)
{
	return FALSE;
}

void
mono_gc_register_obj_with_weak_fields (void* obj)
{
	g_error ("Weak fields not supported by null gc");
}

static gboolean
nullgc_is_thread_in_current_stw (NullGCThreadInfo* info, int* reason)
{
	/*
	A thread explicitly asked to be skiped because it holds no managed state.
	This is used by TP and finalizer threads.
	FIXME Use an atomic variable for this to avoid everyone taking the GC LOCK.
	*/
	if (info->gc_disabled) {
		if (reason)
			*reason = 1;
		return FALSE;
	}

	/*
	We have detected that this thread is failing/dying, ignore it.
	FIXME: can't we merge this with thread_is_dying?
	*/
	if (info->skip) {
		if (reason)
			*reason = 2;
		return FALSE;
	}

	/*
	Suspending the current thread will deadlock us, bad idea.
	*/
	if (info == mono_thread_info_current ()) {
		if (reason)
			*reason = 3;
		return FALSE;
	}

	/*
	We can't suspend the workers that will do all the heavy lifting.
	FIXME Use some state bit in SgenThreadInfo for this.
	*/
	//if (sgen_thread_pool_is_thread_pool_thread (mono_thread_info_get_tid (info))) {
	//	if (reason)
	//		*reason = 4;
	//	return FALSE;
	//}

	/*
	The thread has signaled that it started to detach, ignore it.
	FIXME: can't we merge this with skip
	*/
	if (!mono_thread_info_is_live (info)) {
		if (reason)
			*reason = 5;
		return FALSE;
	}

	return TRUE;
}


#define THREADS_STW_DEBUG(...)

void
nullgc_unified_suspend_stop_world (void)
{
	int sleep_duration = -1;

	mono_threads_begin_global_suspend ();
	//	THREADS_STW_DEBUG ("[GC-STW-BEGIN][%p] *** BEGIN SUSPEND *** \n", mono_thread_info_get_tid (mono_thread_info_current ()));

	FOREACH_THREAD (info) {
		info->skip = FALSE;
		info->suspend_done = FALSE;

		int reason;
		if (!nullgc_is_thread_in_current_stw (info, &reason)) {
			THREADS_STW_DEBUG ("[GC-STW-BEGIN-SUSPEND] IGNORE thread %p skip %s reason %d\n", mono_thread_info_get_tid (info), info->skip ? "true" : "false", reason);
			continue;
		}

		info->skip = !mono_thread_info_begin_suspend (info);

		THREADS_STW_DEBUG ("[GC-STW-BEGIN-SUSPEND] SUSPEND thread %p skip %s\n", mono_thread_info_get_tid (info), info->client_info.skip ? "true" : "false");
	} FOREACH_THREAD_END

		mono_thread_info_current ()->suspend_done = TRUE;
		mono_threads_wait_pending_operations ();

	for (;;) {
		gint restart_counter = 0;

		FOREACH_THREAD (info) {
			gint suspend_count;

			int reason = 0;
			if (info->suspend_done || !nullgc_is_thread_in_current_stw (info, &reason)) {
				THREADS_STW_DEBUG ("[GC-STW-RESTART] IGNORE RESUME thread %p not been processed done %d current %d reason %d\n", mono_thread_info_get_tid (info), info->client_info.suspend_done, !sgen_is_thread_in_current_stw (info, NULL), reason);
				continue;
			}

			/*
			All threads that reach here are pristine suspended. This means the following:

			- We haven't accepted the previous suspend as good.
			- We haven't gave up on it for this STW (it's either bad or asked not to)
			*/
			if (!mono_thread_info_in_critical_location (info)) {
				info->suspend_done = TRUE;

				THREADS_STW_DEBUG ("[GC-STW-RESTART] DONE thread %p deemed fully suspended\n", mono_thread_info_get_tid (info));
				continue;
			}

			suspend_count = mono_thread_info_suspend_count (info);
			if (!(suspend_count == 1))
				g_error ("[%p] suspend_count = %d, but should be 1", mono_thread_info_get_tid (info), suspend_count);

			info->skip = !mono_thread_info_begin_resume (info);
			if (!info->skip)
				restart_counter += 1;

			THREADS_STW_DEBUG ("[GC-STW-RESTART] RESTART thread %p skip %s\n", mono_thread_info_get_tid (info), info->client_info.skip ? "true" : "false");
		} FOREACH_THREAD_END

			mono_threads_wait_pending_operations ();

		if (restart_counter == 0)
			break;

		if (sleep_duration < 0) {
			mono_thread_info_yield ();
			sleep_duration = 0;
		}
		else {
			g_usleep (sleep_duration);
			sleep_duration += 10;
		}

		FOREACH_THREAD (info) {
			int reason = 0;
			if (info->suspend_done || !nullgc_is_thread_in_current_stw (info, &reason)) {
				THREADS_STW_DEBUG ("[GC-STW-RESTART] IGNORE SUSPEND thread %p not been processed done %d current %d reason %d\n", mono_thread_info_get_tid (info), info->client_info.suspend_done, !sgen_is_thread_in_current_stw (info, NULL), reason);
				continue;
			}

			if (!mono_thread_info_is_running (info)) {
				THREADS_STW_DEBUG ("[GC-STW-RESTART] IGNORE SUSPEND thread %p not running\n", mono_thread_info_get_tid (info));
				continue;
			}

			/*info->client_info.skip = !*/mono_thread_info_begin_suspend (info);

			//			THREADS_STW_DEBUG ("[GC-STW-RESTART] SUSPEND thread %p skip %s\n", mono_thread_info_get_tid (info), info->client_info.skip ? "true" : "false");
		} FOREACH_THREAD_END

			mono_threads_wait_pending_operations ();
	}

	FOREACH_THREAD (info) {
		gpointer stopped_ip;

		int reason = 0;
		if (!nullgc_is_thread_in_current_stw (info, &reason)) {
			//g_assert (!info->client_info.suspend_done || info == mono_thread_info_current ());

			THREADS_STW_DEBUG ("[GC-STW-SUSPEND-END] thread %p is NOT suspended, reason %d\n", mono_thread_info_get_tid (info), reason);
			continue;
		}

		g_assert (info->suspend_done);

		//info->ctx = mono_thread_info_get_suspend_state (info)->ctx;

		/* Once we remove the old suspend code, we should move sgen to directly access the state in MonoThread */
		//info->client_info.stack_start = (gpointer)((char*)MONO_CONTEXT_GET_SP (&info->client_info.ctx) - REDZONE_SIZE);

		//if (info->client_info.stack_start < info->client_info.info.stack_start_limit
		//	|| info->client_info.stack_start >= info->client_info.info.stack_end) {
		//	/*
		//	 * Thread context is in unhandled state, most likely because it is
		//	 * dying. We don't scan it.
		//	 * FIXME We should probably rework and check the valid flag instead.
		//	 */
		//	info->client_info.stack_start = NULL;
		//}

		//stopped_ip = (gpointer)(MONO_CONTEXT_GET_IP (&info->client_info.ctx));

		//binary_protocol_thread_suspend ((gpointer)mono_thread_info_get_tid (info), stopped_ip);

		//THREADS_STW_DEBUG ("[GC-STW-SUSPEND-END] thread %p is suspended, stopped_ip = %p, stack = %p -> %p\n",
		//	mono_thread_info_get_tid (info), stopped_ip, info->stack_start, info->stack_start ? info->info.stack_end : NULL);
	} FOREACH_THREAD_END
}

void
nullgc_unified_suspend_restart_world (void)
{
	THREADS_STW_DEBUG ("[GC-STW-END] *** BEGIN RESUME ***\n");
	FOREACH_THREAD (info) {
		int reason = 0;
		if (nullgc_is_thread_in_current_stw (info, &reason)) {
			g_assert (mono_thread_info_begin_resume (info));
			THREADS_STW_DEBUG ("[GC-STW-RESUME-WORLD] RESUME thread %p\n", mono_thread_info_get_tid (info));

			//binary_protocol_thread_restart ((gpointer)mono_thread_info_get_tid (info));
		}
		else {
			THREADS_STW_DEBUG ("[GC-STW-RESUME-WORLD] IGNORE thread %p, reason %d\n", mono_thread_info_get_tid (info), reason);
		}
	} FOREACH_THREAD_END

		mono_threads_wait_pending_operations ();
	mono_threads_end_global_suspend ();
}


#else

MONO_EMPTY_SOURCE_FILE (null_gc);
#endif /* HAVE_NULL_GC */
