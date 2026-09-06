// pawntest - minimal PawnIO probe. Opens the device, prints the driver version,
// then tries IOCTL_PIO_LOAD_BINARY on each file given and prints the raw result.
// No guessing: just the numbers the driver returns.
using System;
using System.IO;
using System.Runtime.InteropServices;

class PawnTest {
    const string DEV = @"\?\GLOBALROOT\Device\PawnIO";
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

    static IntPtr Open() {
        IntPtr h = CreateFile(DEV, GENERIC_RW, SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == IntPtr.Zero || h == (IntPtr)(-1)) {
            Console.WriteLine("  CreateFile FAILED, Win32 err " + Marshal.GetLastWin32Error() + "  (PawnIO not reachable)");
            return IntPtr.Zero;
        }
        return h;
    }

    static void Main(string[] args) {
        Console.WriteLine("pawntest - PawnIO probe");
        Console.WriteLine("device: " + DEV);

        IntPtr h = Open();
        if (h == IntPtr.Zero) return;
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
