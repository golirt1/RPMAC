using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Control de ventilador SMC para Mac en Boot Camp (Windows).
// Protocolo tomado fielmente de applesmc.c (kernel Linux). Herramienta propia.
// Uso:  smcfan.exe max   |   smcfan.exe auto   |   smcfan.exe read
class SmcFan {
    [DllImport("inpout32.dll")] static extern short Inp32(short port);
    [DllImport("inpout32.dll")] static extern void  Out32(short port, short data);
    [DllImport("inpout32.dll")] static extern bool  IsInpOutDriverOpen();

    const short DATA = 0x300;
    const short CMD  = 0x304;
    const int AWAITING  = 0x01;
    const int IB_CLOSED = 0x02;
    const int BUSY      = 0x04;
    const byte READ_CMD  = 0x10;
    const byte WRITE_CMD = 0x11;
    const int MIN_WAIT  = 8;

    static StringBuilder log = new StringBuilder();

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
    static int SendArgument(string key) {
        for (int i = 0; i < 4; i++) if (SendByte((byte)key[i], DATA) != 0) return -1;
        return 0;
    }
    static int SmcSane() {
        int ret = WaitStatus(0, BUSY); if (ret == 0) return ret;
        ret = SendCommand(READ_CMD); if (ret != 0) return ret;
        return WaitStatus(0, BUSY);
    }
    static int ReadSmc(string key, byte[] buf, int len) {
        int ret = SmcSane(); if (ret != 0) return ret;
        if (SendCommand(READ_CMD) != 0 || SendArgument(key) != 0) return -1;
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
    static int WriteSmc(string key, byte[] buf, int len) {
        int ret = SmcSane(); if (ret != 0) return ret;
        if (SendCommand(WRITE_CMD) != 0 || SendArgument(key) != 0) return -1;
        if (SendByte((byte)len, DATA) != 0) return -1;
        for (int i = 0; i < len; i++) if (SendByte(buf[i], DATA) != 0) return -1;
        return WaitStatus(0, BUSY);
    }

    static int Rpm(byte[] b) { return ((b[0] << 8) | b[1]) >> 2; }

    static void SetForced(bool on) {
        byte[] fs = new byte[2];
        ReadSmc("FS! ", fs, 2);                 // mascara actual de modo forzado
        int mask = (fs[0] << 8) | fs[1];
        if (on) mask |= 0x01; else mask &= ~0x01; // bit 0 = ventilador 0
        byte[] nv = { (byte)((mask >> 8) & 0xFF), (byte)(mask & 0xFF) };
        int r = WriteSmc("FS! ", nv, 2);
        log.AppendLine("FS!  -> " + (on ? "forzado" : "auto") + "  ret=" + r);
    }

    static void Main(string[] args) {
        string mode = args.Length > 0 ? args[0].ToLower() : "read";
        try {
            log.AppendLine("[" + DateTime.Now + "] modo=" + mode + "  driver=" + IsInpOutDriverOpen());
            byte[] mx = new byte[2]; ReadSmc("F0Mx", mx, 2);
            byte[] ac = new byte[2]; ReadSmc("F0Ac", ac, 2);
            log.AppendLine("Antes: actual=" + Rpm(ac) + " RPM, max=" + Rpm(mx) + " RPM");

            if (mode == "max") {
                SetForced(true);
                int r = WriteSmc("F0Tg", mx, 2);   // objetivo = maximo
                log.AppendLine("F0Tg -> " + Rpm(mx) + " RPM  ret=" + r);
            } else if (mode == "auto") {
                SetForced(false);                  // devolver a control automatico
            }
        } catch (Exception ex) {
            log.AppendLine("EXCEPTION: " + ex);
        }
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        File.AppendAllText(Path.Combine(dir, "smcfan.log"), log.ToString());
        Console.Write(log.ToString());
    }
}
