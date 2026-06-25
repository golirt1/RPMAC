## RPMac v1.0.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

A free, open-source, lightweight alternative for controlling Mac fans from Windows (Boot Camp).

### Download
Download `RPMac-v1.0.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe` and `inpout32.dll` together in the same folder.

### Requirements
- An Intel Mac running Windows (Boot Camp)
- Administrator rights

### Features
- Real-time fan RPM and temperature monitoring
- Per-fan control: Auto / Max / custom RPM
- Curated temperature sensors (plus a raw view of every key)
- Remembers your last setting and re-applies it on launch
- Start with Windows + start minimized to the system tray
- Modern dark UI, nothing extra to install
- Safety first: stays read-only on non-Apple hardware and never disables the SMC's thermal protection

### Antivirus note
RPMac bundles **InpOut32**, a low-level I/O driver used to talk to the Mac's SMC. Some antivirus products flag this kind of driver as "potentially unwanted" because it grants hardware access. This is **normal for fan-control utilities** — the full source is in this repository. Allow it if your AV blocks it.

### Compatibility
- **Verified on:** Mac Pro (Late 2013, `MacPro6,1`).
- **Intel Macs up to ~2017 (pre-T2):** should work — same SMC interface — but untested on each model.
- **Intel Macs with T2 (2018-2020):** not verified; the `flt` data format is implemented but unconfirmed.
- **Apple Silicon (M1+):** not supported (no Boot Camp).

It has only been tested on one machine, so **reports from other Intel Macs are very welcome** — see the README's "Help us test it" section.

License: **GPL-2.0-only**. Provided as is, without warranty — use at your own risk.
