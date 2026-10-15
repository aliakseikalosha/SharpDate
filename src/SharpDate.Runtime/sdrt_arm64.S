// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// SharpDate runtime stubs for arm64 hosts (Playdate Simulator on Apple Silicon and arm64 Linux).
// These helpers have register contracts with JIT-generated code and so cannot be written in C.

#if defined(__APPLE__)
#define SYM(name) _##name
#else
#define SYM(name) name
#endif

#define GLOBAL(name) .globl SYM(name)

    .text

// x14 = destination, x15 = reference; x14 is incremented by 8. Trashes only x12/x17 (unused here).
// The SharpDate GC is non-generational, so the barrier is a plain store.
    .p2align 2
GLOBAL(RhpAssignRefArm64)
GLOBAL(RhpCheckedAssignRefArm64)
SYM(RhpAssignRefArm64):
SYM(RhpCheckedAssignRefArm64):
    str     x15, [x14], #8
    ret

// x13 = source, x14 = destination; both incremented by 8. May trash x15, x12, x17.
    .p2align 2
GLOBAL(RhpByRefAssignRefArm64)
SYM(RhpByRefAssignRefArm64):
    ldr     x15, [x13], #8
    str     x15, [x14], #8
    ret

// C-convention variants (x0 = destination, x1 = reference).
    .p2align 2
GLOBAL(RhpAssignRef)
GLOBAL(RhpCheckedAssignRef)
SYM(RhpAssignRef):
SYM(RhpCheckedAssignRef):
    str     x1, [x0]
    ret

// Interface dispatch: x11 = dispatch cell, x0 = this. Resolve the target and jump to it with the
// argument registers (x0-x8, q0-q7) preserved.
    .p2align 2
GLOBAL(RhpInitialDynamicInterfaceDispatch)
GLOBAL(RhpInitialInterfaceDispatch)
GLOBAL(RhpInterfaceDispatchSlow)
SYM(RhpInitialDynamicInterfaceDispatch):
SYM(RhpInitialInterfaceDispatch):
SYM(RhpInterfaceDispatchSlow):
    stp     x29, x30, [sp, #-224]!
    mov     x29, sp
    stp     x0, x1, [sp, #16]
    stp     x2, x3, [sp, #32]
    stp     x4, x5, [sp, #48]
    stp     x6, x7, [sp, #64]
    str     x8, [sp, #80]
    stp     q0, q1, [sp, #96]
    stp     q2, q3, [sp, #128]
    stp     q4, q5, [sp, #160]
    stp     q6, q7, [sp, #192]

    mov     x1, x11
    bl      SYM(SharpDate_ResolveInterfaceDispatch)
    mov     x16, x0

    ldp     x0, x1, [sp, #16]
    ldp     x2, x3, [sp, #32]
    ldp     x4, x5, [sp, #48]
    ldp     x6, x7, [sp, #64]
    ldr     x8, [sp, #80]
    ldp     q0, q1, [sp, #96]
    ldp     q2, q3, [sp, #128]
    ldp     q4, q5, [sp, #160]
    ldp     q6, q7, [sp, #192]
    ldp     x29, x30, [sp], #224
    br      x16

#if !defined(__APPLE__) && !defined(_WIN32)
    .section .note.GNU-stack,"",%progbits
#endif
