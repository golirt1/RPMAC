// RPMac - libreria del nucleo SMC (reutilizable por la GUI)
// Copyright (C) 2026 golirt1 - GPL-2.0-only
// Derivado de applesmc.c (Boichat, Rydberg) / smcFanControl (Holtmann). Ver LICENSE y NOTICE.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RPMac {

    public struct FanInfo {
        public int Index;
        public double Actual, Min, Max, Target;
        public bool Forced;
    }

    public static class Smc {
        // ---- InpOut32 / InpOutx64 (legacy I/O ports — pre-T2 Macs) ----
        // The 32-bit DLL auto-installs the matching kernel driver (inpoutx64.sys on
        // 64-bit Windows) and exposes the legacy SMC I/O ports 0x300/0x304.
        [DllImport("inpout32.dll")] static extern short Inp32(short port);
        [DllImport("inpout32.dll")] static extern void  Out32(short port, short data);
        [DllImport("inpout32.dll")] public static extern bool IsInpOutDriverOpen();

        // ---- PawnIO (kernel MMIO mapping — T2 Macs 2018-2020) ----
        // On T2 Macs the legacy ports return NaN; the SMC lives behind an MMIO window
        // that only a kernel driver can map. We use PawnIO (https://pawnio.eu): a modern,
        // EV-signed, open-source scriptable kernel driver — Defender doesn't flag it and
        // it isn't on the vulnerable-driver blocklist (unlike WinRing0). Our signed Pawn
        // module "AppleT2Smc" does the SMC handshake in kernel mode; we just send it the
        // key to read/write. inpout32's own MapPhysToLin can't do this (user-mode map,
        // blocked on modern Windows), which is why PawnIO is required for T2.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec,
                                        uint disp, uint flags, IntPtr templ);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr h, uint code, byte[] inBuf, uint inSize,
                                           byte[] outBuf, uint outSize, out uint returned, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);

        const uint GENERIC_RW    = 0xC0000000;   // GENERIC_READ | GENERIC_WRITE
        const uint FILE_SHARE_RW = 0x3;
        const uint OPEN_EXISTING = 0x3;
        const string PAWNIO_DEVICE = @"\\?\GLOBALROOT\Device\PawnIO";
        // IOCTL codes (from LibreHardwareMonitor's PawnIo.cs)
        const uint PIO_DEVICE_TYPE = 41394u << 16;
        const uint PIO_LOAD_BINARY = PIO_DEVICE_TYPE | (0x821u << 2);
        const uint PIO_EXECUTE_FN  = PIO_DEVICE_TYPE | (0x841u << 2);
        const int  PIO_FN_NAME_LEN = 32;
        const string T2_MODULE_FILE = "AppleT2Smc.bin"; // signed PawnIO module, next to the exe

        static bool   useMmio = false;
        static IntPtr pawnHandle = IntPtr.Zero;
        public static bool MmioActive { get { return useMmio; } }

        // Open PawnIO and load our signed T2 SMC module. Returns true on success.
        static bool TryInitMmio() {
            try {
                string dir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string modPath = System.IO.Path.Combine(dir, T2_MODULE_FILE);
                if (!System.IO.File.Exists(modPath)) return false; // module not bundled yet

                IntPtr h = CreateFile(PAWNIO_DEVICE, GENERIC_RW, FILE_SHARE_RW, IntPtr.Zero,
                                      OPEN_EXISTING, 0, IntPtr.Zero);
                if (h == IntPtr.Zero || h == (IntPtr)(-1)) return false; // PawnIO not installed

                byte[] blob = System.IO.File.ReadAllBytes(modPath);
                uint ret;
                if (!DeviceIoControl(h, PIO_LOAD_BINARY, blob, (uint)blob.Length, null, 0, out ret, IntPtr.Zero)) {
                    CloseHandle(h); return false; // module rejected (unsigned / wrong key)
                }
                pawnHandle = h;
                useMmio = true;
                return true;
            } catch { return false; }
        }

        // Call a PawnIO module function. input/output are arrays of 64-bit cells.
        static bool PawnExecute(string fn, long[] input, long[] output) {
            if (pawnHandle == IntPtr.Zero) return false;
            byte[] inBuf = new byte[PIO_FN_NAME_LEN + input.Length * sizeof(long)];
            byte[] nameB = Encoding.ASCII.GetBytes(fn);
            Buffer.BlockCopy(nameB, 0, inBuf, 0, Math.Min(PIO_FN_NAME_LEN - 1, nameB.Length));
            Buffer.BlockCopy(input, 0, inBuf, PIO_FN_NAME_LEN, input.Length * sizeof(long));
            byte[] outBuf = new byte[output.Length * sizeof(long)];
            uint ret;
            if (!DeviceIoControl(pawnHandle, PIO_EXECUTE_FN, inBuf, (uint)inBuf.Length,
                                 outBuf, (uint)outBuf.Length, out ret, IntPtr.Zero))
                return false;
            Buffer.BlockCopy(outBuf, 0, output, 0, Math.Min((int)ret, outBuf.Length));
            return true;
        }

        public static void Cleanup() {
            if (pawnHandle != IntPtr.Zero) {
                try { CloseHandle(pawnHandle); } catch { }
                pawnHandle = IntPtr.Zero; useMmio = false;
            }
        }

        const short DATA = 0x300;
        const short CMD  = 0x304;
        const int AWAITING = 0x01, IB_CLOSED = 0x02, BUSY = 0x04;
        const byte READ_CMD = 0x10, WRITE_CMD = 0x11, GET_KEY_INDEX = 0x12, GET_KEY_TYPE = 0x13;
        const int MIN_WAIT = 8;

        static readonly object gate = new object();

        // ---- I/O port protocol (pre-T2 Macs) ----
        static void Udelay(int us) {
            long ticks = (long)(us * (Stopwatch.Frequency / 1000000.0));
            if (ticks <= 0) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedTicks < ticks) { }
        }
        static int WaitStatus(int val, int mask) {
            int us = MIN_WAIT;
            for (int i = 0; i < 24; i++) {
                if (((Inp32(CMD) & 0xFF) & mask) == val) return 0;
                Udelay(us); if (i > 9) us <<= 1;
            }
            return -1;
        }
        static int SendCommand(byte cmd) { int r = WaitStatus(0, IB_CLOSED); if (r != 0) return r; Out32(CMD, cmd); return 0; }
        static int SendByte(byte b, short port) {
            int r = WaitStatus(0, IB_CLOSED); if (r != 0) return r;
            r = WaitStatus(BUSY, BUSY); if (r != 0) return r;
            Out32(port, b); return 0;
        }
        static int SendArgument(byte[] k) { for (int i = 0; i < 4; i++) if (SendByte(k[i], DATA) != 0) return -1; return 0; }
        static int SmcSane() {
            int r = WaitStatus(0, BUSY); if (r == 0) return r;
            r = SendCommand(READ_CMD); if (r != 0) return r;
            return WaitStatus(0, BUSY);
        }
        static int PortReadSmc(byte cmd, byte[] key, byte[] buf, int len) {
            int r = SmcSane(); if (r != 0) return r;
            if (SendCommand(cmd) != 0 || SendArgument(key) != 0) return -1;
            if (SendByte((byte)len, DATA) != 0) return -1;
            for (int i = 0; i < len; i++) {
                if (WaitStatus(AWAITING | BUSY, AWAITING | BUSY) != 0) return -1;
                buf[i] = (byte)(Inp32(DATA) & 0xFF);
            }
            for (int i = 0; i < 16; i++) { Udelay(MIN_WAIT); if ((Inp32(CMD) & AWAITING) == 0) break; Inp32(DATA); }
            return WaitStatus(0, BUSY);
        }
        static int PortWriteSmc(byte[] key, byte[] buf, int len) {
            int r = SmcSane(); if (r != 0) return r;
            if (SendCommand(WRITE_CMD) != 0 || SendArgument(key) != 0) return -1;
            if (SendByte((byte)len, DATA) != 0) return -1;
            for (int i = 0; i < len; i++) if (SendByte(buf[i], DATA) != 0) return -1;
            return WaitStatus(0, BUSY);
        }

        // ---- T2 SMC via PawnIO module (T2 Macs 2018-2020) ----
        // The AppleT2Smc PawnIO module does the MMIO handshake in kernel mode; here we
        // just marshal the key/data. The key is packed little-endian (key[0] in the low
        // byte) so the module's 32-bit register write lands key[0] at offset 0x78, exactly
        // like *(u32*)key + iowrite32 in the Linux driver. This holds for ASCII keys and
        // for big-endian index arguments (GET_KEY_BY_INDEX) alike.
        static long KeyToLong(byte[] key) {
            uint v = ((uint)key[3] << 24) | ((uint)key[2] << 16) | ((uint)key[1] << 8) | key[0];
            return v;
        }
        static int MmioReadSmc(byte cmd, byte[] key, byte[] buf, int len) {
            if (len < 0 || len > 32) return -1;
            long[] inp = new long[] { cmd, KeyToLong(key), len };
            long[] outp = new long[32];               // module returns one byte per cell
            if (!PawnExecute("ioctl_smc_read", inp, outp)) return -1;
            for (int i = 0; i < len; i++) buf[i] = (byte)(outp[i] & 0xFF);
            return 0;
        }
        static int MmioWriteSmc(byte[] key, byte[] buf, int len) {
            if (len < 0 || len > 32) return -1;
            long[] inp = new long[34];                 // [key, len, b0..b31]
            inp[0] = KeyToLong(key);
            inp[1] = len;
            for (int i = 0; i < len; i++) inp[2 + i] = buf[i] & 0xFF;
            long[] outp = new long[1];                  // module declares out_size 0; dummy avoids 0-byte marshal
            return PawnExecute("ioctl_smc_write", inp, outp) ? 0 : -1;
        }

        // ---- unified dispatch ----
        static int ReadSmc(byte cmd, byte[] key, byte[] buf, int len) {
            return useMmio ? MmioReadSmc(cmd, key, buf, len) : PortReadSmc(cmd, key, buf, len);
        }
        static int WriteSmc(byte[] key, byte[] buf, int len) {
            return useMmio ? MmioWriteSmc(key, buf, len) : PortWriteSmc(key, buf, len);
        }

        // ---- helpers ----
        static byte[] K(string key) { byte[] b = new byte[4]; for (int i = 0; i < 4; i++) b[i] = (i < key.Length) ? (byte)key[i] : (byte)0; return b; }
        static long UintBE(byte[] b, int n) { long v = 0; for (int i = 0; i < n; i++) v = (v << 8) | b[i]; return v; }
        static long IntBE(byte[] b, int n) { long v = UintBE(b, n); long s = 1L << (n * 8 - 1); if ((v & s) != 0) v -= (1L << (n * 8)); return v; }
        static int Hex(char c) { if (c >= '0' && c <= '9') return c - '0'; if (c >= 'a' && c <= 'f') return c - 'a' + 10; if (c >= 'A' && c <= 'F') return c - 'A' + 10; return 0; }

        // El tipo y la longitud de una clave SMC nunca cambian, asi que se cachean:
        // evita un GET_KEY_TYPE por cada lectura (mitad de trafico SMC) -> menos parpadeo
        // y menos "lecturas no plausibles" en SMCs lentos. Acceso serializado por 'gate'.
        struct KeyMeta { public int Len; public string Type; }
        static readonly Dictionary<string, KeyMeta> keyCache = new Dictionary<string, KeyMeta>();

        static bool KeyInfo(string key, out int len, out string type) {
            KeyMeta m;
            if (keyCache.TryGetValue(key, out m)) { len = m.Len; type = m.Type; return true; }
            byte[] info = new byte[6];
            int r = ReadSmc(GET_KEY_TYPE, K(key), info, 6);
            len = info[0]; type = Encoding.ASCII.GetString(info, 1, 4);
            if (r == 0) keyCache[key] = new KeyMeta { Len = len, Type = type }; // solo cachear exitos
            return r == 0;
        }
        static double Decode(string type, byte[] b, int len) {
            string t = type.TrimEnd('\0', ' ');
            if (t == "flt" && len >= 4) { byte[] f = { b[3], b[2], b[1], b[0] }; return BitConverter.ToSingle(f, 0); }
            if (t.StartsWith("fp") && t.Length == 4) return (double)UintBE(b, len) / (1 << Hex(t[3]));
            if (t.StartsWith("sp") && t.Length == 4) return (double)IntBE(b, len) / (1 << Hex(t[3]));
            if (t.StartsWith("ui")) return (double)UintBE(b, len);
            if (t.StartsWith("si")) return (double)IntBE(b, len);
            return (double)UintBE(b, len);
        }
        static byte[] Encode(string type, int len, double val) {
            string t = type.TrimEnd('\0', ' ');
            if (t == "flt" && len >= 4) { byte[] f = BitConverter.GetBytes((float)val); return new byte[] { f[3], f[2], f[1], f[0] }; }
            long raw;
            if (t.StartsWith("fp") && t.Length == 4) raw = (long)Math.Round(val * (1 << Hex(t[3])));
            else if (t.StartsWith("sp") && t.Length == 4) raw = (long)Math.Round(val * (1 << Hex(t[3])));
            else raw = (long)Math.Round(val);
            byte[] o = new byte[len];
            for (int i = len - 1; i >= 0; i--) { o[i] = (byte)(raw & 0xFF); raw >>= 8; }
            return o;
        }
        static double ReadNum(string key) {
            int len; string type;
            if (!KeyInfo(key, out len, out type) || len == 0 || len > 32) return double.NaN;
            byte[] b = new byte[len];
            if (ReadSmc(READ_CMD, K(key), b, len) != 0) return double.NaN;
            return Decode(type, b, len);
        }

        // ---- Seguridad ----
        public static bool WritesAllowed = false;   // por defecto NO se escribe hasta validar
        public static string HardwareName = "";
        public static string SafetyReason = "Not validated yet.";

        // Valida que el hardware sea una Mac antes de permitir escribir. Si algo no
        // cuadra, deja la app en SOLO LECTURA (no escribe nada).
        //
        // La PRUEBA definitiva es el propio SMC: la app le habla por los puertos de E/S
        // 0x300/0x304 con el protocolo propietario de Apple. Un PC genérico no responde
        // a ese protocolo con un conteo de ventiladores coherente (1-8) ni con RPMs
        // plausibles, así que un SMC válido ES hardware Apple. Las cadenas del registro
        // (SystemManufacturer / SystemProductName) son solo una señal secundaria: en las
        // Macs antiguas con Boot Camp en modo BIOS/CSM (Mac Pro 3,1 y similares) vienen
        // vacías o sin "Apple"/"Mac", por eso NO deben bloquear el control por sí solas.
        public static bool Validate() {
            string mfg = "", prod = "";
            try {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS")) {
                    if (k != null) {
                        object m = k.GetValue("SystemManufacturer"); object p = k.GetValue("SystemProductName");
                        mfg = (m == null) ? "" : m.ToString();
                        prod = (p == null) ? "" : p.ToString();
                        // Boot Camp antiguo (2008-2010) a veces omite SystemManufacturer — intentar SystemFamily
                        if (mfg == "") { object f = k.GetValue("SystemFamily"); if (f != null) mfg = f.ToString(); }
                    }
                }
            } catch { }

            HardwareName = (mfg + " " + prod).Trim();

            // Señal secundaria: el registro a veces confirma "Apple"/"Mac", pero no siempre.
            bool registrySaysApple = mfg.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0
                                  || prod.IndexOf("Mac", StringComparison.OrdinalIgnoreCase) >= 0;

            // Prueba primaria y autoritativa: el SMC de Apple debe responder coherentemente.
            lock (gate) {
                double n = ReadNum("FNum");
                if (double.IsNaN(n) || n < 1 || n > 8) {
                    // T2 Macs (2018-2020): the T2 chip intercepts the legacy I/O-port protocol → NaN.
                    // Fall back to the PawnIO kernel module that reaches the SMC over MMIO.
                    if (registrySaysApple && TryInitMmio()) {
                        n = ReadNum("FNum");
                    }
                    if (double.IsNaN(n) || n < 1 || n > 8) {
                        WritesAllowed = false;
                        string t2hint = registrySaysApple && useMmio
                            ? " T2 module loaded but the SMC did not respond — the T2 register layout on this model may differ."
                            : (registrySaysApple ? " Looks like a T2 Mac: install PawnIO (pawnio.eu) and keep AppleT2Smc.bin next to RPMac.exe to enable T2 support." : "");
                        SafetyReason = "SMC did not return a valid fan count (got " + (double.IsNaN(n) ? "NaN" : n.ToString()) + "). Read-only for safety." + t2hint;
                        return false;
                    }
                }
                double ac = ReadNum("F0Ac"), mn = ReadNum("F0Mn"), mx = ReadNum("F0Mx");
                // mn puede ser 0 en algunos modelos (el ventilador puede pararse) — solo rechazar negativo
                if (double.IsNaN(ac) || double.IsNaN(mn) || double.IsNaN(mx) ||
                    mn < 0 || mx <= 0 || mx > 20000 || ac < 0 || ac > 20000) {
                    WritesAllowed = false;
                    SafetyReason = string.Format("Fan readings are not plausible (ac={0:0}, mn={1:0}, mx={2:0}). Read-only for safety.", ac, mn, mx);
                    return false;
                }
            }

            // SMC coherente -> es hardware Apple, aunque el registro no lo confirme.
            WritesAllowed = true;
            string modeStr = useMmio ? " (T2 MMIO mode)" : "";
            SafetyReason = registrySaysApple
                ? "Apple Mac detected, SMC valid" + modeStr + ". Control enabled."
                : "SMC valid (Apple hardware confirmed by the SMC; registry id '" + (HardwareName == "" ? "(empty)" : HardwareName) + "' unrecognized)" + modeStr + ". Control enabled.";
            return true;
        }

        // ---- API publica ----
        public static int FanCount() { lock (gate) { double n = ReadNum("FNum"); return double.IsNaN(n) ? 0 : (int)n; } }

        public static List<FanInfo> GetFans() {
            lock (gate) {
                var list = new List<FanInfo>();
                double dn = ReadNum("FNum"); int n = double.IsNaN(dn) ? 0 : (int)dn;
                long mask = useMmio ? 0 : ReadMaskNoLock();
                for (int i = 0; i < n; i++) {
                    string p = "F" + i;
                    bool forced = useMmio ? (ReadNum(p + "Md") > 0) : ((mask & (1L << i)) != 0);
                    list.Add(new FanInfo {
                        Index = i,
                        Actual = ReadNum(p + "Ac"), Min = ReadNum(p + "Mn"),
                        Max = ReadNum(p + "Mx"), Target = ReadNum(p + "Tg"),
                        Forced = forced
                    });
                }
                return list;
            }
        }

        // Pre-T2: FS! bitmask controls manual mode for all fans
        static long ReadMaskNoLock() {
            int len; string type;
            if (!KeyInfo("FS! ", out len, out type) || len == 0) return 0;
            byte[] b = new byte[len];
            if (ReadSmc(READ_CMD, K("FS! "), b, len) != 0) return 0;
            return UintBE(b, len);
        }
        static void WriteMaskNoLock(long mask) {
            int len; string type;
            if (!KeyInfo("FS! ", out len, out type) || len == 0) return;
            byte[] b = new byte[len];
            for (int i = len - 1; i >= 0; i--) { b[i] = (byte)(mask & 0xFF); mask >>= 8; }
            WriteSmc(K("FS! "), b, len);
        }

        // T2: per-fan boolean F%dMd (0=auto, 1=manual) + float F%dTg for target RPM
        static void T2SetManual(int i, bool manual) {
            string mk = "F" + i + "Md";
            int len; string type;
            if (!KeyInfo(mk, out len, out type) || len == 0) { len = 1; type = "ui8 "; }
            WriteSmc(K(mk), new byte[] { (byte)(manual ? 1 : 0) }, len);
        }

        public static void SetFanAuto(int i) {
            if (!WritesAllowed) return;
            lock (gate) {
                if (useMmio) { T2SetManual(i, false); return; }
                long m = ReadMaskNoLock(); m &= ~(1L << i); WriteMaskNoLock(m);
            }
        }
        public static void SetFanRpm(int i, double rpm) {
            if (!WritesAllowed) return;
            lock (gate) {
                string p = "F" + i;
                double mn = ReadNum(p + "Mn"), mx = ReadNum(p + "Mx");
                if (!double.IsNaN(mn) && rpm < mn) rpm = mn;
                if (!double.IsNaN(mx) && rpm > mx) rpm = mx;
                if (useMmio) {
                    T2SetManual(i, true);
                    int len; string type;
                    if (KeyInfo(p + "Tg", out len, out type)) WriteSmc(K(p + "Tg"), Encode(type, len, rpm), len);
                    return;
                }
                long m = ReadMaskNoLock(); m |= (1L << i); WriteMaskNoLock(m);
                int tlen; string ttype;
                if (KeyInfo(p + "Tg", out tlen, out ttype)) WriteSmc(K(p + "Tg"), Encode(ttype, tlen, rpm), tlen);
            }
        }
        public static void SetFanMax(int i) {
            if (!WritesAllowed) return;
            lock (gate) {
                string p = "F" + i; double mx = ReadNum(p + "Mx");
                if (double.IsNaN(mx)) return;
                if (useMmio) {
                    T2SetManual(i, true);
                    int len; string type;
                    if (KeyInfo(p + "Tg", out len, out type)) WriteSmc(K(p + "Tg"), Encode(type, len, mx), len);
                    return;
                }
                long m = ReadMaskNoLock(); m |= (1L << i); WriteMaskNoLock(m);
                int tlen; string ttype;
                if (KeyInfo(p + "Tg", out tlen, out ttype)) WriteSmc(K(p + "Tg"), Encode(ttype, tlen, mx), tlen);
            }
        }

        // Enumera las claves de sensores de temperatura una sola vez
        public static List<string> EnumTempKeys() {
            lock (gate) {
                var keys = new List<string>();
                byte[] cb = new byte[4];
                if (ReadSmc(READ_CMD, K("#KEY"), cb, 4) != 0) return keys;
                long count = UintBE(cb, 4);
                for (long i = 0; i < count; i++) {
                    byte[] idx = { (byte)((i >> 24) & 0xFF), (byte)((i >> 16) & 0xFF), (byte)((i >> 8) & 0xFF), (byte)(i & 0xFF) };
                    byte[] nm = new byte[4];
                    if (ReadSmc(GET_KEY_INDEX, idx, nm, 4) != 0) continue;
                    string key = Encoding.ASCII.GetString(nm).Replace("\0", "");
                    if (key.Length < 1 || key[0] != 'T') continue;
                    int len; string type;
                    if (!KeyInfo(key, out len, out type)) continue;
                    string t = type.TrimEnd('\0', ' ');
                    if (!(t.StartsWith("sp") || t == "flt")) continue;
                    double v = ReadNum(key);
                    if (double.IsNaN(v) || v < -5 || v > 150) continue;
                    keys.Add(key);
                }
                return keys;
            }
        }
        public static double ReadTemp(string key) { lock (gate) { return ReadNum(key); } }
    }
}
