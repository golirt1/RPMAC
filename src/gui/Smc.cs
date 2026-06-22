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
        [DllImport("inpout32.dll")] static extern short Inp32(short port);
        [DllImport("inpout32.dll")] static extern void  Out32(short port, short data);
        [DllImport("inpout32.dll")] public static extern bool IsInpOutDriverOpen();

        const short DATA = 0x300;
        const short CMD  = 0x304;
        const int AWAITING = 0x01, IB_CLOSED = 0x02, BUSY = 0x04;
        const byte READ_CMD = 0x10, WRITE_CMD = 0x11, GET_KEY_INDEX = 0x12, GET_KEY_TYPE = 0x13;
        const int MIN_WAIT = 8;

        static readonly object gate = new object(); // serializa el acceso al SMC

        // ---- protocolo ----
        static void Udelay(int us) {
            long ticks = (long)(us * (Stopwatch.Frequency / 1000000.0));
            if (ticks <= 0) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedTicks < ticks) { }
        }
        static int InbCmd()  { return Inp32(CMD)  & 0xFF; }
        static int InbData() { return Inp32(DATA) & 0xFF; }
        static int WaitStatus(int val, int mask) {
            int us = MIN_WAIT;
            for (int i = 0; i < 24; i++) {
                if ((InbCmd() & mask) == val) return 0;
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
        static int ReadSmc(byte cmd, byte[] key, byte[] buf, int len) {
            int r = SmcSane(); if (r != 0) return r;
            if (SendCommand(cmd) != 0 || SendArgument(key) != 0) return -1;
            if (SendByte((byte)len, DATA) != 0) return -1;
            for (int i = 0; i < len; i++) {
                if (WaitStatus(AWAITING | BUSY, AWAITING | BUSY) != 0) return -1;
                buf[i] = (byte)InbData();
            }
            for (int i = 0; i < 16; i++) { Udelay(MIN_WAIT); if ((InbCmd() & AWAITING) == 0) break; InbData(); }
            return WaitStatus(0, BUSY);
        }
        static int WriteSmc(byte[] key, byte[] buf, int len) {
            int r = SmcSane(); if (r != 0) return r;
            if (SendCommand(WRITE_CMD) != 0 || SendArgument(key) != 0) return -1;
            if (SendByte((byte)len, DATA) != 0) return -1;
            for (int i = 0; i < len; i++) if (SendByte(buf[i], DATA) != 0) return -1;
            return WaitStatus(0, BUSY);
        }

        // ---- helpers ----
        static byte[] K(string key) { byte[] b = new byte[4]; for (int i = 0; i < 4; i++) b[i] = (i < key.Length) ? (byte)key[i] : (byte)0; return b; }
        static long UintBE(byte[] b, int n) { long v = 0; for (int i = 0; i < n; i++) v = (v << 8) | b[i]; return v; }
        static long IntBE(byte[] b, int n) { long v = UintBE(b, n); long s = 1L << (n * 8 - 1); if ((v & s) != 0) v -= (1L << (n * 8)); return v; }
        static int Hex(char c) { if (c >= '0' && c <= '9') return c - '0'; if (c >= 'a' && c <= 'f') return c - 'a' + 10; if (c >= 'A' && c <= 'F') return c - 'A' + 10; return 0; }

        static bool KeyInfo(string key, out int len, out string type) {
            byte[] info = new byte[6];
            int r = ReadSmc(GET_KEY_TYPE, K(key), info, 6);
            len = info[0]; type = Encoding.ASCII.GetString(info, 1, 4); return r == 0;
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

        // Valida que (1) sea una Mac de Apple y (2) el SMC responda de forma coherente.
        // Si algo no cuadra, deja la app en SOLO LECTURA (no escribe nada).
        public static bool Validate() {
            string mfg = "", prod = "";
            try {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS")) {
                    if (k != null) {
                        object m = k.GetValue("SystemManufacturer"); object p = k.GetValue("SystemProductName");
                        mfg = (m == null) ? "" : m.ToString();
                        prod = (p == null) ? "" : p.ToString();
                    }
                }
            } catch { }
            HardwareName = (mfg + " " + prod).Trim();

            if (mfg.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) < 0) {
                WritesAllowed = false;
                SafetyReason = "Not an Apple Mac (manufacturer: " + (mfg == "" ? "unknown" : mfg) + "). Read-only for safety.";
                return false;
            }
            lock (gate) {
                double n = ReadNum("FNum");
                if (double.IsNaN(n) || n < 1 || n > 8) {
                    WritesAllowed = false; SafetyReason = "SMC did not return a valid fan count. Read-only for safety."; return false;
                }
                double ac = ReadNum("F0Ac"), mn = ReadNum("F0Mn"), mx = ReadNum("F0Mx");
                if (double.IsNaN(ac) || double.IsNaN(mn) || double.IsNaN(mx) ||
                    mn <= 0 || mx <= mn || mx > 20000 || ac < 0 || ac > 20000) {
                    WritesAllowed = false; SafetyReason = "Fan readings are not plausible. Read-only for safety."; return false;
                }
            }
            WritesAllowed = true;
            SafetyReason = "Apple Mac detected, SMC valid. Control enabled.";
            return true;
        }

        // ---- API publica ----
        public static int FanCount() { lock (gate) { double n = ReadNum("FNum"); return double.IsNaN(n) ? 0 : (int)n; } }

        public static List<FanInfo> GetFans() {
            lock (gate) {
                var list = new List<FanInfo>();
                double dn = ReadNum("FNum"); int n = double.IsNaN(dn) ? 0 : (int)dn;
                long mask = ReadMaskNoLock();
                for (int i = 0; i < n; i++) {
                    string p = "F" + i;
                    list.Add(new FanInfo {
                        Index = i,
                        Actual = ReadNum(p + "Ac"), Min = ReadNum(p + "Mn"),
                        Max = ReadNum(p + "Mx"), Target = ReadNum(p + "Tg"),
                        Forced = (mask & (1L << i)) != 0
                    });
                }
                return list;
            }
        }

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

        public static void SetFanAuto(int i) {
            if (!WritesAllowed) return;
            lock (gate) { long m = ReadMaskNoLock(); m &= ~(1L << i); WriteMaskNoLock(m); }
        }
        public static void SetFanRpm(int i, double rpm) {
            if (!WritesAllowed) return;
            lock (gate) {
                string p = "F" + i;
                double mn = ReadNum(p + "Mn"), mx = ReadNum(p + "Mx");   // recortar al rango real del SMC
                if (!double.IsNaN(mn) && rpm < mn) rpm = mn;
                if (!double.IsNaN(mx) && rpm > mx) rpm = mx;
                long m = ReadMaskNoLock(); m |= (1L << i); WriteMaskNoLock(m);
                int len; string type;
                if (KeyInfo(p + "Tg", out len, out type)) WriteSmc(K(p + "Tg"), Encode(type, len, rpm), len);
            }
        }
        public static void SetFanMax(int i) {
            if (!WritesAllowed) return;
            lock (gate) {
                string p = "F" + i; double mx = ReadNum(p + "Mx");
                if (!double.IsNaN(mx)) { long m = ReadMaskNoLock(); m |= (1L << i); WriteMaskNoLock(m);
                    int len; string type; if (KeyInfo(p + "Tg", out len, out type)) WriteSmc(K(p + "Tg"), Encode(type, len, mx), len); }
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
