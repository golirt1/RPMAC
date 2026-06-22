// Fan Control Open - nucleo universal SMC
// Copyright (C) 2026
// Licensed under GPL-2.0-only. Derived from the Linux kernel driver applesmc.c
//   Copyright (C) 2007 Nicolas Boichat, (C) 2010 Henrik Rydberg
//   Fan control based on smcFanControl, (C) 2006 Hendrik Holtmann
// See LICENSE and NOTICE.
//
// Universal: descubre tipo/longitud de cada clave (fpe2, flt, ui*, sp*, si*),
// enumera todos los ventiladores y lee sensores de temperatura.
//
// Uso:  smccore.exe [list | temps | max | auto | set <rpm> | key <CLAVE>]

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

class SmcCore {
    [DllImport("inpout32.dll")] static extern short Inp32(short port);
    [DllImport("inpout32.dll")] static extern void  Out32(short port, short data);
    [DllImport("inpout32.dll")] static extern bool  IsInpOutDriverOpen();

    // --- Puertos y constantes (de applesmc.c) ---
    const short DATA = 0x300;
    const short CMD  = 0x304;
    const int AWAITING  = 0x01; // SMC_STATUS_AWAITING_DATA
    const int IB_CLOSED = 0x02; // SMC_STATUS_IB_CLOSED
    const int BUSY      = 0x04; // SMC_STATUS_BUSY
    const byte READ_CMD       = 0x10;
    const byte WRITE_CMD      = 0x11;
    const byte GET_KEY_INDEX  = 0x12;
    const byte GET_KEY_TYPE   = 0x13;
    const int MIN_WAIT = 8;

    static StringBuilder log = new StringBuilder();
    static void P(string s) { log.AppendLine(s); Console.WriteLine(s); }

    // ---------------- protocolo de bajo nivel ----------------
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
            Udelay(us);
            if (i > 9) us <<= 1;
        }
        return -1;
    }
    static int SendCommand(byte cmd) {
        int ret = WaitStatus(0, IB_CLOSED); if (ret != 0) return ret;
        Out32(CMD, cmd); return 0;
    }
    static int SendByte(byte b, short port) {
        int ret = WaitStatus(0, IB_CLOSED); if (ret != 0) return ret;
        ret = WaitStatus(BUSY, BUSY); if (ret != 0) return ret;
        Out32(port, b); return 0;
    }
    static int SendArgument(byte[] key4) {
        for (int i = 0; i < 4; i++) if (SendByte(key4[i], DATA) != 0) return -1;
        return 0;
    }
    static int SmcSane() {
        int ret = WaitStatus(0, BUSY); if (ret == 0) return ret;
        ret = SendCommand(READ_CMD); if (ret != 0) return ret;
        return WaitStatus(0, BUSY);
    }
    static int ReadSmc(byte cmd, byte[] key4, byte[] buf, int len) {
        int ret = SmcSane(); if (ret != 0) return ret;
        if (SendCommand(cmd) != 0 || SendArgument(key4) != 0) return -1;
        if (SendByte((byte)len, DATA) != 0) return -1;
        for (int i = 0; i < len; i++) {
            if (WaitStatus(AWAITING | BUSY, AWAITING | BUSY) != 0) return -1;
            buf[i] = (byte)InbData();
        }
        for (int i = 0; i < 16; i++) {
            Udelay(MIN_WAIT);
            if ((InbCmd() & AWAITING) == 0) break;
            InbData();
        }
        return WaitStatus(0, BUSY);
    }
    static int WriteSmc(byte[] key4, byte[] buf, int len) {
        int ret = SmcSane(); if (ret != 0) return ret;
        if (SendCommand(WRITE_CMD) != 0 || SendArgument(key4) != 0) return -1;
        if (SendByte((byte)len, DATA) != 0) return -1;
        for (int i = 0; i < len; i++) if (SendByte(buf[i], DATA) != 0) return -1;
        return WaitStatus(0, BUSY);
    }

    // ---------------- helpers de clave / tipo ----------------
    static byte[] K(string key) {
        byte[] b = new byte[4];
        for (int i = 0; i < 4; i++) b[i] = (i < key.Length) ? (byte)key[i] : (byte)0;
        return b;
    }
    static bool KeyInfo(string key, out int len, out string type) {
        byte[] info = new byte[6];
        int r = ReadSmc(GET_KEY_TYPE, K(key), info, 6);
        len = info[0];
        type = Encoding.ASCII.GetString(info, 1, 4);
        return r == 0;
    }
    static long KeyCount() {
        byte[] b = new byte[4];
        if (ReadSmc(READ_CMD, K("#KEY"), b, 4) != 0) return 0;
        return UintBE(b, 4);
    }
    static string KeyByIndex(long i) {
        byte[] idx = { (byte)((i >> 24) & 0xFF), (byte)((i >> 16) & 0xFF), (byte)((i >> 8) & 0xFF), (byte)(i & 0xFF) };
        byte[] name = new byte[4];
        if (ReadSmc(GET_KEY_INDEX, idx, name, 4) != 0) return null;
        return Encoding.ASCII.GetString(name).Replace("\0", "");
    }

    static long UintBE(byte[] b, int len) { long v = 0; for (int i = 0; i < len; i++) v = (v << 8) | b[i]; return v; }
    static long IntBE(byte[] b, int len) {
        long v = UintBE(b, len); long sign = 1L << (len * 8 - 1);
        if ((v & sign) != 0) v -= (1L << (len * 8)); return v;
    }
    static int Hex(char c) {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return 0;
    }

    // Decodifica bytes segun el tipo SMC -> double
    static double Decode(string type, byte[] b, int len) {
        string t = type.TrimEnd('\0', ' ');
        if (t == "flt" && len >= 4) {           // float IEEE (Macs con T2); orden por validar
            byte[] f = { b[3], b[2], b[1], b[0] };
            return BitConverter.ToSingle(f, 0);
        }
        if (t.StartsWith("fp") && t.Length == 4) { int frac = Hex(t[3]); return (double)UintBE(b, len) / (1 << frac); }
        if (t.StartsWith("sp") && t.Length == 4) { int frac = Hex(t[3]); return (double)IntBE(b, len) / (1 << frac); }
        if (t.StartsWith("ui")) return (double)UintBE(b, len);
        if (t.StartsWith("si")) return (double)IntBE(b, len);
        return (double)UintBE(b, len);
    }
    // Codifica un valor segun el tipo SMC -> bytes
    static byte[] Encode(string type, int len, double val) {
        string t = type.TrimEnd('\0', ' ');
        if (t == "flt" && len >= 4) {
            byte[] f = BitConverter.GetBytes((float)val);
            return new byte[] { f[3], f[2], f[1], f[0] };
        }
        long raw;
        if (t.StartsWith("fp") && t.Length == 4) raw = (long)Math.Round(val * (1 << Hex(t[3])));
        else if (t.StartsWith("sp") && t.Length == 4) raw = (long)Math.Round(val * (1 << Hex(t[3])));
        else raw = (long)Math.Round(val);
        byte[] outb = new byte[len];
        for (int i = len - 1; i >= 0; i--) { outb[i] = (byte)(raw & 0xFF); raw >>= 8; }
        return outb;
    }

    // Lee una clave numerica -> double (o NaN si falla)
    static double ReadNum(string key) {
        int len; string type;
        if (!KeyInfo(key, out len, out type) || len == 0 || len > 32) return double.NaN;
        byte[] b = new byte[len];
        if (ReadSmc(READ_CMD, K(key), b, len) != 0) return double.NaN;
        return Decode(type, b, len);
    }

    static int FanCount() {
        double n = ReadNum("FNum");
        return double.IsNaN(n) ? 0 : (int)n;
    }

    // ---------------- acciones ----------------
    static void ListFans() {
        int n = FanCount();
        P("Ventiladores (FNum): " + n);
        for (int i = 0; i < n; i++) {
            string p = "F" + i;
            P(string.Format("  Fan {0}:  actual={1,5:0} RPM   min={2,5:0}   max={3,5:0}   objetivo={4,5:0}",
                i, ReadNum(p + "Ac"), ReadNum(p + "Mn"), ReadNum(p + "Mx"), ReadNum(p + "Tg")));
        }
    }

    static void ListTemps() {
        long count = KeyCount();
        P("Total de claves SMC: " + count);
        P("Sensores de temperatura (claves T..., valores plausibles 0-150 C):");
        int shown = 0;
        for (long i = 0; i < count; i++) {
            string key = KeyByIndex(i);
            if (key == null || key.Length < 1 || key[0] != 'T') continue;
            int len; string type;
            if (!KeyInfo(key, out len, out type)) continue;
            string t = type.TrimEnd('\0', ' ');
            if (!(t.StartsWith("sp") || t == "flt")) continue;
            double v = ReadNum(key);
            if (double.IsNaN(v) || v < -5 || v > 150) continue;
            P(string.Format("  {0}  ({1})  = {2,6:0.0} C", key, t, v));
            shown++;
        }
        if (shown == 0) P("  (no se encontraron sensores con valores plausibles)");
    }

    static void SetForcedAll(bool on) {
        int len; string type;
        if (!KeyInfo("FS! ", out len, out type) || len == 0) { P("No se pudo leer FS!"); return; }
        byte[] cur = new byte[len];
        ReadSmc(READ_CMD, K("FS! "), cur, len);
        long mask = UintBE(cur, len);
        int n = FanCount();
        if (on) { for (int i = 0; i < n; i++) mask |= (1L << i); }
        else mask = 0;
        byte[] nv = new byte[len];
        for (int i = len - 1; i >= 0; i--) { nv[i] = (byte)(mask & 0xFF); mask >>= 8; }
        int r = WriteSmc(K("FS! "), nv, len);
        P("FS!  -> " + (on ? "forzado" : "auto") + "  (mask escrita)  ret=" + r);
    }

    static void SetAll(double rpm, bool toMax) {
        int n = FanCount();
        SetForcedAll(true);
        for (int i = 0; i < n; i++) {
            string p = "F" + i;
            int len; string type;
            if (!KeyInfo(p + "Tg", out len, out type)) continue;
            double target = toMax ? ReadNum(p + "Mx") : rpm;
            if (double.IsNaN(target)) continue;
            byte[] b = Encode(type, len, target);
            int r = WriteSmc(K(p + "Tg"), b, len);
            P(string.Format("  Fan {0}: objetivo -> {1:0} RPM ({2})  ret={3}", i, target, type.TrimEnd('\0', ' '), r));
        }
    }

    static void DumpKey(string key) {
        int len; string type;
        if (!KeyInfo(key, out len, out type)) { P(key + ": no existe / sin info"); return; }
        byte[] b = new byte[len];
        int r = ReadSmc(READ_CMD, K(key), b, len);
        P(string.Format("{0}: tipo='{1}' len={2} bytes=[{3}] valor={4} ret={5}",
            key, type, len, BitConverter.ToString(b), Decode(type, b, len), r));
    }

    static void Main(string[] args) {
        string mode = args.Length > 0 ? args[0].ToLower() : "list";
        try {
            P("=== Fan Control Open - nucleo universal ===");
            P("Driver InpOut abierto: " + IsInpOutDriverOpen());
            P("");
            switch (mode) {
                case "list":  ListFans(); break;
                case "temps": ListFans(); P(""); ListTemps(); break;
                case "max":   P("Forzando TODOS los ventiladores al MAXIMO..."); SetAll(0, true); break;
                case "auto":  P("Devolviendo ventiladores a AUTOMATICO..."); SetForcedAll(false); break;
                case "set":
                    if (args.Length > 1) { double rpm = double.Parse(args[1]); P("Fijando " + rpm + " RPM..."); SetAll(rpm, false); }
                    else P("Uso: smccore.exe set <rpm>");
                    break;
                case "key":
                    if (args.Length > 1) DumpKey(args[1]);
                    else P("Uso: smccore.exe key <CLAVE>");
                    break;
                default: P("Modo desconocido: " + mode); break;
            }
        } catch (Exception ex) {
            P("EXCEPTION: " + ex.Message);
        }
        try {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            File.AppendAllText(Path.Combine(dir, "smccore.log"), log.ToString() + Environment.NewLine);
        } catch { }
    }
}
