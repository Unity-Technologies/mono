#ifndef __MONO_DEBUGGER_AGENT_H__
#define __MONO_DEBUGGER_AGENT_H__

#include "mini.h"

/* IL offsets used to mark the sequence points belonging to method entry/exit events */
#define METHOD_ENTRY_IL_OFFSET -1
#define METHOD_EXIT_IL_OFFSET 0xffffff
typedef struct _MonoBreakpoint MonoBreakpoint;

void
mono_debugger_agent_parse_options (char *options) MONO_INTERNAL;

void
mono_debugger_agent_init (void) MONO_INTERNAL;

void
mono_debugger_agent_breakpoint_hit (void *sigctx) MONO_INTERNAL;

void
mono_debugger_agent_single_step_event (void *sigctx) MONO_INTERNAL;

void
mono_debugger_agent_free_domain_info (MonoDomain *domain) MONO_INTERNAL;

gboolean mono_debugger_agent_thread_interrupt (void *sigctx, MonoJitInfo *ji) MONO_INTERNAL;

void
mono_debugger_agent_handle_exception (MonoException *ext, MonoContext *throw_ctx, MonoContext *catch_ctx) MONO_INTERNAL;

int
mono_debugger_agent_set_breakpoint(MonoMethod *method, long bpil_offset);

void
mono_debugger_agent_clear_breakpoint(MonoMethod *method, long bpil_offset);

typedef struct UnityStackFrame
{
    MonoMethod* method;
    long il_offset;
} UnityStackFrame;

typedef struct UnityStackFrames
{
    UnityStackFrame* frames;
    int length;
} UnityStackFrames;

typedef enum UnityVariableGroup
{
    Unity_Local = 0,
    Unity_Arg = 1
} UnityVariableGroup;

typedef enum UnityVariableType
{
    Unity_Integer = 0,
    Unity_Float = 1,
    Unity_Double = 2,
    Unity_Pointer = 3,
    Unity_String = 4,
    Unity_Object = 5,
    Unity_Unknown = 6,
    Unity_Array = 7,
    Unity_Valuetype = 8,
    Unity_Referencetype = 9,
    Unity_Reftype = 10,
} UnityVariableType;

typedef struct UnityVariable
{
    UnityVariableGroup group;
    char* name;
    MonoType* type;

    UnityVariableType varType;
    union
    {
        double doubleVar;
        float floatVar;
        long longVar;
        void* pointerVar;
    };
} UnityVariable;

typedef struct UnityVariables
{
    UnityVariable* variables;
    int length;
} UnityVariables;

void mono_unity_get_stack_frames(UnityStackFrames* frames, MonoContext* ctx);

void mono_unity_free_stack_frames(UnityStackFrames* frames);

void mono_unity_get_local_vars(UnityVariables* locals, MonoContext* ctx);

void mono_unity_free_local_vars(UnityVariables* locals);

#endif
