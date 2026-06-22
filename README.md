# RPMac

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

RPMac is a free and open-source fan-control utility for **Intel-based Macs running Windows via Boot Camp**. It talks directly to the Mac's **SMC (System Management Controller)** to monitor fan speeds and temperatures, and lets you set each fan to **Automatic**, **Maximum**, or a **custom RPM**.

Designed as a lightweight, modern alternative to paid tools, RPMac includes **hardware safety checks**: it stays read-only on non-Apple hardware and never disables the SMC's built-in thermal protection.

## Features
- Real-time fan RPM and temperature monitoring
- Per-fan control: Auto / Max / custom RPM
- Curated, friendly temperature sensors (plus a raw view of every key)
- Start with Windows (one toggle)
- Safety first — read-only unless it confirms a genuine Apple Mac with a valid SMC; clamps RPM to the SMC's own min/max
- Lightweight modern dark UI — nothing extra to install
- Free and open source (GPL-2.0)

## Compatibility
| Hardware | Status |
|---|---|
| Intel Macs (up to 2017) on Boot Camp | Should work (tested on MacPro6,1) |
| Intel Macs with T2 (2018-2020) | Likely; `flt` format not yet verified |
| Apple Silicon (M1+) | Not possible (no Boot Camp) |
| Non-Apple PCs | Read-only (writes are blocked) |

### Tested hardware
So far RPMac has been verified on a **single machine**:

- **Mac Pro (Late 2013)** — model identifier `MacPro6,1`
- Intel Xeon CPU, dual AMD FirePro GPUs, single centrifugal system fan
- Running Windows via Boot Camp
- SMC fan/sensor values in `fpe2` format, I/O base `0x300`

On this machine, reading sensors and controlling the fan (Auto / Max / custom RPM) work correctly.

### Other Intel Macs (untested, but expected to work)
RPMac has **not** been tested on any other Mac model yet. That said, it is built on the **standard Apple SMC interface that is common to virtually all Intel Macs**, and the core auto-detects the number of fans and each key's data format. So it *should* work on most Intel Macs in Boot Camp, with these caveats:

- **Fan control** is the most portable part (it uses standard keys), so it has the highest chance of working everywhere.
- **Temperature sensor names vary by model**, so on other Macs some labeled sensors may be missing or wrong (use "Show all sensors (raw)" to see everything).
- **T2 Macs (2018-2020)** use the `flt` value format, which is implemented but **not yet verified**.
- If the SMC does not respond with plausible values, RPMac **automatically stays read-only** and writes nothing.

Reports (working or not) from other Intel Macs are very welcome — please open an issue.

## How it works
RPMac uses the public Apple SMC protocol (the same one documented in the Linux `applesmc` driver) over the SMC's I/O ports (`DATA = 0x300`, `CMD = 0x304`), via the InpOut32 ring-0 I/O bridge. It reads and writes standard SMC keys (`FNum`, `F<n>Tg`, `FS! `, temperature `T...` keys) and auto-detects each key's data type (`fpe2`, `flt`, `ui*`, `sp*`, ...).

## Requirements
- An Intel Mac running Windows (Boot Camp)
- Administrator rights (required for hardware I/O)

## Build
Compiles with the .NET Framework C# compiler already present on Windows — no Visual Studio needed:
```
csc /noconfig /target:winexe /platform:x86 /win32manifest:src\gui\app.manifest /out:build\RPMac.exe ^
    /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll ^
    /reference:WPF\WindowsBase.dll /reference:WPF\PresentationCore.dll /reference:WPF\PresentationFramework.dll ^
    src\gui\Smc.cs src\gui\App.cs
```
`inpout32.dll` must sit next to `RPMac.exe`.

## License
**GPL-2.0-only.** See `LICENSE`.

RPMac's SMC code is derived from the Linux `applesmc` driver and smcFanControl — see `NOTICE` for credits. InpOut32 is bundled under the MIT license.

## Credits
- `applesmc.c` (Linux): Nicolas Boichat, Henrik Rydberg
- smcFanControl: Hendrik Holtmann
- InpOut32: Phillip Gibbons

## Disclaimer — no warranty

RPMac is provided **"AS IS", without warranty of any kind**, express or implied, including but not limited to the warranties of merchantability and fitness for a particular purpose, as stated in the GNU GPL-2.0.

- **There is no guarantee that it will work on your Mac.** It has only been tested on one model (see above).
- **You use this software entirely at your own risk.**
- The author and contributors are **not responsible or liable for any damage** of any kind — including but not limited to overheating, throttling, hardware failure, data loss, or any other problem — arising from the use or misuse of this software.

Notes on safety:
- Running the fan at maximum is safe for the hardware itself, but **constant maximum speed increases noise, dust buildup and bearing wear** over time.
- RPMac **never disables the SMC's built-in hardware thermal protection** — the SMC can still override the fans and throttle or shut down the machine to prevent overheating.
- Setting a fan too low under heavy load could let the machine run hot; RPMac clamps manual values to the SMC's own minimum/maximum, but use manual mode with care.
