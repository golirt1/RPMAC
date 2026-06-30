# T2 SMC debugging notes

This branch collects everything needed to debug T2 fan control on Windows. The
relevant code is already on `main`:

- **PawnIO module (source):** [`src/pawnio/AppleT2Smc.p`](src/pawnio/AppleT2Smc.p)
- **App-side loader / SMC layer:** [`src/gui/Smc.cs`](src/gui/Smc.cs) — see `TryInitMmio` / `TryLoadBlob`

## What it does

On T2 Macs the chip intercepts the legacy SMC I/O ports (0x300/0x304), so the SMC
is only reachable through an MMIO window at physical **0xFE0B0000**. The PawnIO
module maps that window (`io_space_map`) and runs the SMC request/response
handshake in kernel mode. Register layout + protocol come from the Linux T2
applesmc driver (MCMrARM/mbp2018-etc, `applesmc_t2_kmod.c`):

```
DATA      0x0000   (byte-addressed data buffer)
KEY_NAME  0x0078   (dword write, 4-byte key)
DATALEN   0x007D
SMC_ID    0x007E   (dword write, always 0)
CMD       0x007F   (dword write; byte read = error)
STATUS    0x4005   (byte; bit 0x20 = done)
```

## The LoadBinary blob format (this is the err 87 cause)

PawnIO's `IOCTL_PIO_LOAD_BINARY` does **not** take a raw `.amx`. From the driver
(`vm_load_binary_internal` in PawnIO/src/vm.cpp) the buffer must be:

```
[4 bytes: sig_len (little-endian)] [sig_len bytes: signature] [AMX]
```

```c
if (size < 4) return STATUS_INVALID_PARAMETER;
const auto sig_len = *(PULONG)buffer;
if (sig_len > (size - 4)) return STATUS_INVALID_PARAMETER;   // <-- err 87 (ERROR_INVALID_PARAMETER)
const auto mem = (uint8_t*)buffer + 4 + sig_len;
auto status = check_signature(mem, len, (uint8_t*)buffer + 4, sig_len);
#ifdef PAWNIO_UNRESTRICTED
  status = STATUS_SUCCESS;   // unrestricted ignores the signature result
#endif
if (NT_SUCCESS(status)) { /* load */ }
```

So **err 87 = STATUS_INVALID_PARAMETER** can come from:
1. `sig_len > size - 4` → the buffer is a raw `.amx` (its first 4 header bytes get
   read as a huge sig_len). The app guards against this by trying the file as-is,
   then wrapped as `[00 00 00 00][amx]` (sig_len = 0).
2. `check_signature(...)` returning INVALID_PARAMETER on a **signed** driver when
   the signature is empty/invalid. On the **unrestricted** driver this path is
   overwritten with `STATUS_SUCCESS`, so it cannot produce err 87.

**If you still get err 87 with `AppleT2Smc.bin` = `[00 00 00 00] + amx` (sig_len 0),
that strongly implies the loaded driver is verifying the signature — i.e. the
*signed* edition is active, not the unrestricted one.**

## How to confirm which driver is active

The unrestricted driver prints the signature result via `DbgPrint`:

```
[PawnIO] Signature check result: <status>
```

Run **DebugView** (Sysinternals) as admin with "Capture Kernel" enabled, then load
the module. If you see that line, the unrestricted edition is active and the
signature is being ignored. If you don't, the signed edition is what's loaded.

Self-signing won't help here: the signed driver only trusts namazso's key, so a
self-signed blob is still rejected. The two real options are (a) genuinely running
the unrestricted edition, or (b) getting the module signed upstream.

## Building the module

The `.amx` is produced by the PawnIO.Modules CI (`pawncc -iinclude -C64 -;+ -(+ -p`).
Source + an open PR live at: https://github.com/golirt1/PawnIO.Modules (branch
`add-apple-t2-smc`). The app loads `AppleT2Smc.bin` from next to `RPMac.exe`; for an
unsigned test build that file is just `[00 00 00 00]` followed by the compiled `.amx`.
