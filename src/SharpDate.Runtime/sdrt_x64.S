// SharpDate runtime stubs for x86-64 hosts: System V (Playdate Simulator on macOS and Linux) and
// Windows x64. These helpers have register contracts with JIT-generated code and so cannot be
// written in C.

#if defined(__APPLE__)
#define SYM(name) _##name
#else
#define SYM(name) name
#endif

#define GLOBAL(name) .globl SYM(name)

    .intel_syntax noprefix
    .text

// void RhpAssignRef(Object** dst, Object* ref): the first two argument registers
// (rdi, rsi on System V; rcx, rdx on Windows).
// The SharpDate GC is non-generational, so the write barrier is a plain store.
    .p2align 4
GLOBAL(RhpAssignRef)
GLOBAL(RhpCheckedAssignRef)
SYM(RhpAssignRef):
SYM(RhpCheckedAssignRef):
#if defined(_WIN32)
    mov     qword ptr [rcx], rdx
#else
    mov     qword ptr [rdi], rsi
#endif
    ret

// Copies one reference from [rsi] to [rdi] and advances both by 8 (rdi/rsi on Windows too).
// May only trash rax and rcx.
    .p2align 4
GLOBAL(RhpByRefAssignRef)
SYM(RhpByRefAssignRef):
    mov     rcx, qword ptr [rsi]
    mov     qword ptr [rdi], rcx
    add     rdi, 8
    add     rsi, 8
    ret

// Interface dispatch. Compiled code loads the address of the dispatch cell into r11 and calls
// through the cell's first pointer, which points here. We resolve the target for the receiver's
// type (the first argument: rdi, or rcx on Windows) and tail-jump to it with all argument
// registers preserved.
    .p2align 4
GLOBAL(RhpInitialDynamicInterfaceDispatch)
GLOBAL(RhpInitialInterfaceDispatch)
GLOBAL(RhpInterfaceDispatchSlow)
SYM(RhpInitialDynamicInterfaceDispatch):
SYM(RhpInitialInterfaceDispatch):
SYM(RhpInterfaceDispatchSlow):
#if defined(_WIN32)
    push    rbp
    mov     rbp, rsp
    sub     rsp, 128                    // 32 shadow + 4 GP + 4 XMM argument registers
    mov     [rsp + 32], rcx
    mov     [rsp + 40], rdx
    mov     [rsp + 48], r8
    mov     [rsp + 56], r9
    movdqu  [rsp + 64], xmm0
    movdqu  [rsp + 80], xmm1
    movdqu  [rsp + 96], xmm2
    movdqu  [rsp + 112], xmm3

    // rcx = this (already), rdx = cell
    mov     rdx, r11
    call    SYM(SharpDate_ResolveInterfaceDispatch)
    mov     r11, rax

    mov     rcx, [rsp + 32]
    mov     rdx, [rsp + 40]
    mov     r8,  [rsp + 48]
    mov     r9,  [rsp + 56]
    movdqu  xmm0, [rsp + 64]
    movdqu  xmm1, [rsp + 80]
    movdqu  xmm2, [rsp + 96]
    movdqu  xmm3, [rsp + 112]
    leave
    jmp     r11
#else
    push    rbp
    mov     rbp, rsp
    sub     rsp, 176                    // 6 GP + 8 XMM argument registers, keeps 16-byte alignment
    mov     [rsp + 0], rdi
    mov     [rsp + 8], rsi
    mov     [rsp + 16], rdx
    mov     [rsp + 24], rcx
    mov     [rsp + 32], r8
    mov     [rsp + 40], r9
    movdqu  [rsp + 48], xmm0
    movdqu  [rsp + 64], xmm1
    movdqu  [rsp + 80], xmm2
    movdqu  [rsp + 96], xmm3
    movdqu  [rsp + 112], xmm4
    movdqu  [rsp + 128], xmm5
    movdqu  [rsp + 144], xmm6
    movdqu  [rsp + 160], xmm7

    // rdi = this (already), rsi = cell
    mov     rsi, r11
    call    SYM(SharpDate_ResolveInterfaceDispatch)
    mov     r11, rax

    mov     rdi, [rsp + 0]
    mov     rsi, [rsp + 8]
    mov     rdx, [rsp + 16]
    mov     rcx, [rsp + 24]
    mov     r8,  [rsp + 32]
    mov     r9,  [rsp + 40]
    movdqu  xmm0, [rsp + 48]
    movdqu  xmm1, [rsp + 64]
    movdqu  xmm2, [rsp + 80]
    movdqu  xmm3, [rsp + 96]
    movdqu  xmm4, [rsp + 112]
    movdqu  xmm5, [rsp + 128]
    movdqu  xmm6, [rsp + 144]
    movdqu  xmm7, [rsp + 160]
    leave
    jmp     r11
#endif

#if !defined(__APPLE__) && !defined(_WIN32)
    .section .note.GNU-stack,"",@progbits
#endif
