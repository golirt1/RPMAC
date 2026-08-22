## RPMac v1.6.1

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### Fixed in 1.6.1
- **Curve points can no longer cross each other.** Dragging a point past its neighbour left the curve going *down* as the temperature went *up* (e.g. `38°C → 860 RPM` sitting next to `40°C → 790 RPM`), which is never what you want from a fan and looked broken. Points now keep their order, stay at least 3°C apart, and can't be dragged below the point on their left or above the one on their right. Curves saved with a dip are straightened out when they're loaded.

### Download
Download `RPMac-v1.6.1-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.6.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's new in 1.6.0
- **Curves with as many points as you want.** A curve is no longer a single straight ramp: **double-click the graph to add a point**, drag it anywhere, and **right-click a point to remove it**. That's what it takes to say "stay silent up to 55°, then ramp hard" — one straight line can't. Changes to an active curve apply immediately, without pressing Apply again.
- **Smooth fan changes** (on by default). Temperatures wobble by a degree constantly, and a curve that follows every wobble makes the fan audibly hunt up and down. RPMac now ignores tiny changes, rises quickly when something really heats up, and eases back down slowly. Turn it off in Settings if you want the raw curve.
- **Emergency cooling.** Set a temperature — if *any* sensor reaches it, every fan goes to maximum until the machine cools down, whatever mode each fan is in. A safety net so you can run a quiet curve without worrying.
- **Copy a curve to all fans.** On Macs with several fans, set one up and hand the same shape to the rest — the RPM values are rescaled to each fan's own range.
- **Record to a CSV file.** Append every reading to `history.csv` so you can look back at what ran hot during a game or a long render.

Existing curves are read as before and converted automatically; nothing is lost when you update.

### Download
Download `RPMac-v1.6.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.5.1

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### Fixed in 1.5.1
- **The minimize and close buttons work again.** In 1.5.0's new title bar, pressing either button started a window drag instead, which swallowed the click — so the buttons appeared dead and the only ways out were the tray icon or Alt+F4. They now respond normally (close still hides RPMac to the tray, as it always has; quit from the tray menu). Dragging the window by the title bar is unaffected.

### Download
Download `RPMac-v1.5.1-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.5.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

A full redesign of the interface, and the curve editor RPMac always should have had.

### What's new in 1.5.0
- **Graphical curve editor.** The temperature curve is now a real chart you edit by **dragging its points**, instead of four sliders you had to imagine the result of. A **live dot** rides the curve showing exactly where the fan is running right now, and the axes label themselves in °C or °F.
- **5-minute history graph.** A live chart of the hottest sensor and the fan speed, so you can actually see what your Mac is doing over time.
- **Redesigned interface.** RPMac now has its own window chrome with a live temperature readout, an icon sidebar (**Fans · Sensors · Presets · Settings**) instead of one endless scrolling page, and a proper app icon. Sensors are grouped by CPU / GPU / system with their raw SMC keys, and values turn amber and then red as they get hot.
- **Fan names from the SMC.** If your Mac reports a name for a fan (e.g. *EXHAUST*, *MAIN*), RPMac shows it, along with the fan's RPM range and a marker for the target speed on the speed bar.
- Live temperatures stay visible next to the fan controls while you tune them.

### Download
Download `RPMac-v1.5.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

> **If Windows blocks it:** RPMac isn't code-signed, so Windows doesn't recognise it yet. On **SmartScreen** choose *More info → Run anyway*; if **Smart App Control** blocks it the app just won't start, so either turn Smart App Control off or build RPMac yourself from source. This is normal for any unsigned utility that talks to hardware — the full source is in the repo.

---

## RPMac v1.4.1

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### Fixed in 1.4.1
- **Themes now switch correctly again.** Selecting a theme (Dark / Light / Nature / Japan) only changed the background — the accent color, controls and the highlighted button were left on the previous theme, so switching looked half-applied. The whole UI now recolors instantly and completely, and the selected theme is highlighted properly.
- **Sliders, the scrollbar and the selected buttons now follow the theme too.** They were hardcoded to the dark palette, so they looked out of place in the light themes (an invisible slider handle on a white background, dark tracks, low-contrast button text). They now match every theme.

### New in 1.4.1
- **Curve on the hottest sensor.** The per-fan temperature curve has a new sensor option, **"Highest temp (any sensor)"**, which drives the fan from whichever sensor is currently hottest instead of a single fixed one — so the fan ramps up when *either* the CPU or the GPU gets hot. Useful on iMacs where the GPU is often the hottest part but the CPU can spike on its own. Requested by @Bibihi98.
- **iMac17,1 confirmed working.** The iMac (Retina 5K, 27-inch, Late 2015) is now a verified model — fan control and temperature sensors both work. Thanks to @Bibihi98 for the report.

### Download
Download `RPMac-v1.4.1-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.4.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's new in 1.4.0
- **Temperature in the system tray.** The tray icon can now show a live temperature — the highest sensor, or a specific one — as seven-segment digits, instead of (or as well as) the app icon. Pick it in **Settings → "Show in tray"** (App Icon / None / Highest Temp / a specific sensor). Contributed by VladislavEkimtcov.
- **Fixed: RPMac now launches at startup on battery.** The "Start with Windows" task was created with Windows' default *"start only if on AC power"* condition, so on laptops it wouldn't launch at logon while on battery. The task now allows running on battery. Reported by hooshmandd700.
- **Fixed a tray-icon handle leak** that could make the app run out of Windows handles and crash after a few hours in temperature-tray mode. Contributed by VladislavEkimtcov.

### Download
Download `RPMac-v1.4.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.3.2

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### Fixed in 1.3.2
- **Hardened startup so it can't silently die.** Some machines reported RPMac flashing and closing on the next launch when a saved `config.txt` was present (with nothing written to the error log). The startup path is now fully isolated step-by-step, and **corrupted-state / native exceptions** (e.g. from the low-level I/O driver while re-applying the saved config) are now caught and logged instead of killing the process. Any startup problem is written to `%APPDATA%\RPMac\error.log`. If it still happens, that log now pinpoints the exact cause.
- Fixed the manual build command in the README — it was missing `System.Windows.Forms` and `System.Drawing` (needed by the tray icon) — and documented `build.bat`. Thanks to @I-Love-Potatoes for both findings.

### Download
Download `RPMac-v1.3.2-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `RPMac.exe.config`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.3.1

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### Fixed in 1.3.1
- **Fixed RPMac becoming unreachable after a restart** — the window would flash and close while the process kept running (fans still spinning), with no way to get the GUI back. RPMac is now **single-instance**: launching it again brings the already-running window to the front instead of starting a hidden duplicate that fights over the SMC. Also added a tray fallback (it minimizes instead of vanishing if the tray icon can't be created) and writes any startup error to `%APPDATA%\RPMac\error.log`. Thanks to the reporter for the detailed video.

### Download
Download `RPMac-v1.3.1-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.3.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's new in 1.3.0
- **Presets (profiles).** Save your current fan setup as a named profile — e.g. "Silent", "Gaming", "Max" — and switch between them with one click. Each profile stores every fan's mode (Auto/Max/Manual/Curve) and its parameters. Apply them from the app or straight from the **tray icon** (right-click → Presets), without opening the window. The active profile is highlighted, and each shows a summary of what it does.
- Overlay now re-asserts always-on-top each refresh, so it stays above games running in **borderless / windowed** mode (exclusive-fullscreen still can't be drawn over by any window overlay).

### Download
Download `RPMac-v1.3.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.2.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's new in 1.2.0
- **Per-fan temperature curve.** Each fan now has a **Curve** mode: pick a temperature sensor, set a min and max temperature and a min and max RPM, and the fan ramps linearly between them — below the min temp it runs at min RPM, above the max temp at max RPM. Works independently per fan, so multi-fan Macs can curve each one separately.
- **Command-line tool (`smccore.exe`)** for scripting and automation, now with **per-fan** control:
  - `smccore.exe list | temps` — read fans / sensors
  - `smccore.exe auto [fan] | max [fan] | set [fan] <rpm>` — control all fans, or one fan by number
  - RPM values are clamped to each fan's own min/max; read-only on non-Apple hardware.
- Fixed the build script and bundled `smccore.exe` alongside `RPMac.exe`.

### Download
Download `RPMac-v1.2.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe`, `smccore.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.1.1

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's fixed in 1.1.1
- **Corrected CPU sensor labeling.** `TC0P` was labeled "CPU", but it's a socket-**proximity** sensor that reads hotter than the actual core — on some Macs it can show ~105°C while the real die temp is lower. It's now labeled **"CPU (proximity)"**, and **`TC0D` "CPU (die)"** is surfaced as the true per-core reading. Reported on a dual-CPU Mac Pro.

### Download
Download `RPMac-v1.1.1-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe` and `inpout32.dll` together in the same folder.

---

## RPMac v1.1.0

**The other app capable of controlling fans on Intel Macs in Windows — for free.**

### What's new in 1.1.0
- **On-screen overlay (FRAPS-style):** always-on-top, click-through, pinned to the top-right corner. Choose **vertical or horizontal (compact)** layout and **select which fans/sensors** to show.
- **Themes:** Dark / Light / Nature / Japan, applied instantly (the Windows title bar follows the theme too).
- **Temperatures in °C or °F** with a single toggle.
- **Re-applies your fan settings after sleep/resume** (the SMC drops forced mode on resume, so fans no longer silently revert to auto).
- Performance: SMC key info is cached to roughly halve SMC traffic per refresh.

### Download
Download `RPMac-v1.1.0-windows.zip` below, unzip it, and run **`RPMac.exe` as administrator**.
Keep `RPMac.exe` and `inpout32.dll` together in the same folder.

---

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
