// SharpDate runtime stubs for the Playdate device (ARM Cortex-M7, Thumb-2, hard-float ABI).
// These helpers have register contracts with JIT-generated code and so cannot be written in C.

    .syntax unified
    .thumb
    .text

    .macro FUNC name
    .globl \name
    .type \name, %function
    .thumb_func
    .p2align 2
\name:
    .endm

// void RhpAssignRef(Object** dst /* r0 */, Object* ref /* r1 */)
// The JIT assumes only r0, r3, r12 and lr are trashed. Non-generational GC: plain store.
FUNC RhpAssignRef
FUNC RhpCheckedAssignRef
    str     r1, [r0]
    bx      lr

// Copies one reference from [r1] to [r0] and advances both by 4. May trash r2, r3, r12, lr.
FUNC RhpByRefAssignRef
    ldr     r2, [r1], #4
    str     r2, [r0], #4
    bx      lr

// Interface dispatch. Compiled code puts the dispatch cell address in r12 and calls through the
// cell's first pointer, which points here. Resolve the target for the receiver (r0 = this) and
// jump to it with argument registers (r0-r3, s0-s15) preserved.
FUNC RhpInitialDynamicInterfaceDispatch
FUNC RhpInitialInterfaceDispatch
FUNC RhpInterfaceDispatchSlow
    push    {r0-r4, lr}                 // r4 keeps the stack 8-byte aligned
    vpush   {d0-d7}
    mov     r1, r12
    bl      SharpDate_ResolveInterfaceDispatch
    mov     r12, r0
    vpop    {d0-d7}
    pop     {r0-r4, lr}
    bx      r12

    .section .note.GNU-stack,"",%progbits
