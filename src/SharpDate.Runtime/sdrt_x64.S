// SharpDate runtime stubs for x86-64 System V hosts (Playdate Simulator on macOS and Linux).
// These helpers have register contracts with JIT-generated code and so cannot be written in C.

#if defined(__APPLE__)
#define SYM(name) _##name
#else
#define SYM(name) name
#endif

#define GLOBAL(name) .globl SYM(name)

    .intel_syntax noprefix
    .text

// void RhpAssignRef(Object** dst /* rdi */, Object* ref /* rsi */)
// The SharpDate GC is non-generational, so the write barrier is a plain store.
    .p2align 4
GLOBAL(RhpAssignRef)
GLOBAL(RhpCheckedAssignRef)
SYM(RhpAssignRef):
SYM(RhpCheckedAssignRef):
    mov     qword ptr [rdi], rsi
    ret

// Copies one reference from [rsi] to [rdi] and advances both by 8. May only trash rax and rcx.
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
// type (rdi = this) and tail-jump to it with all argument registers preserved.
    .p2align 4
GLOBAL(RhpInitialDynamicInterfaceDispatch)
GLOBAL(RhpInitialInterfaceDispatch)
GLOBAL(RhpInterfaceDispatchSlow)
SYM(RhpInitialDynamicInterfaceDispatch):
SYM(RhpInitialInterfaceDispatch):
SYM(RhpInterfaceDispatchSlow):
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

#if !defined(__APPLE__)
    .section .note.GNU-stack,"",@progbits
#endif
