// pawntest - minimal PawnIO probe. Opens the device, prints the driver version,
// then tries IOCTL_PIO_LOAD_BINARY on each file given and prints the raw result.
// No guessing: just the numbers the driver returns.
using System;
using System.IO;
using System.Runtime.InteropServices;

class PawnTest {
    // Try the NT path first (exists in every PawnIO), then the DOS symlink (2.0.x and 2.2.0 only).
    static readonly string[] DEVS = { @"\\?\GLOBALROOT\Device\PawnIO", @"\\.\PawnIO" };
    const uint DEVTYPE = 41394u << 16;
    const uint IOCTL_LOAD    = DEVTYPE | (0x821u << 2);
    const uint IOCTL_VERSION = DEVTYPE | (0x861u << 2);
    const uint GENERIC_RW = 0x80000000u | 0x40000000u;
    const uint SHARE_RW = 1 | 2;
    const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFile(string n, uint a, uint s, IntPtr sa, uint d, uint f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, uint c, byte[] i, uint il, byte[] o, uint ol, out uint r, IntPtr ov);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static string opened = "";
    static IntPtr Open() {
        int lastErr = 0;
        foreach (string dev in DEVS) {
            IntPtr h = CreateFile(dev, GENERIC_RW, SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h != IntPtr.Zero && h != (IntPtr)(-1)) { opened = dev; return h; }
            lastErr = Marshal.GetLastWin32Error();
        }
        Console.WriteLine("  CreateFile FAILED on both paths, last Win32 err " + lastErr + "  (PawnIO not reachable / not installed)");
        return IntPtr.Zero;
    }

    static void Main(string[] args) {
        Console.WriteLine("pawntest - PawnIO probe");

        IntPtr h = Open();
        if (h == IntPtr.Zero) return;
        Console.WriteLine("device: " + opened);
        byte[] ver = new byte[4]; uint ret;
        if (DeviceIoControl(h, IOCTL_VERSION, null, 0, ver, 4, out ret, IntPtr.Zero)) {
            uint v = BitConverter.ToUInt32(ver, 0);
            Console.WriteLine("driver version: " + (v >> 16) + "." + ((v >> 8) & 0xFF) + "." + (v & 0xFF) + "  (raw 0x" + v.ToString("X8") + ")");
        } else Console.WriteLine("version ioctl FAILED, Win32 err " + Marshal.GetLastWin32Error());
        CloseHandle(h);

        if (args.Length == 0) { Console.WriteLine("usage: pawntest <module.bin> [more.bin ...]"); return; }

        foreach (string f in args) {
            Console.WriteLine();
            Console.WriteLine("== " + Path.GetFileName(f) + " ==");
            if (!File.Exists(f)) { Console.WriteLine("  file not found"); continue; }
            byte[] blob = File.ReadAllBytes(f);
            uint sigLen = blob.Length >= 4 ? BitConverter.ToUInt32(blob, 0) : 0xFFFFFFFF;
            Console.WriteLine("  size " + blob.Length + " bytes, header sig_len = " + sigLen
                + (sigLen == 0 ? "  (unsigned/unrestricted-only blob)" : sigLen == 512 ? "  (signed module)" : ""));
            h = Open(); if (h == IntPtr.Zero) continue;
            if (DeviceIoControl(h, IOCTL_LOAD, blob, (uint)blob.Length, null, 0, out ret, IntPtr.Zero))
                Console.WriteLine("  LoadBinary: SUCCESS (module loaded and its main() returned OK)");
            else {
                int e = Marshal.GetLastWin32Error();
                Console.WriteLine("  LoadBinary: FAILED, Win32 err " + e + Hint(e));
            }
            CloseHandle(h);
        }
    }

    static string Hint(int e) {
        switch (e) {
            case 87:   return "  = STATUS_INVALID_PARAMETER";
            case 50:   return "  = STATUS_NOT_SUPPORTED (module ran, its own hardware check declined)";
            case 1:    return "  = STATUS_UNSUCCESSFUL (AMX failed to load: bad file or unresolved native)";
            case 1450: return "  = STATUS_INSUFFICIENT_RESOURCES";
            default:   return "";
        }
    }
}
