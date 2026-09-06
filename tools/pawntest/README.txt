pawntest - a 30-second PawnIO probe
===================================

This does one thing: opens PawnIO, prints the driver version, then asks it to
load each module file you pass in and prints the raw result. No guessing.

Run from an ADMINISTRATOR command prompt, in this folder:

    pawntest.exe Echo.bin AppleT2Smc.bin

Echo.bin is the official, SIGNED "hello world" module from the PawnIO.Modules
0.2.11 release (sig_len = 512). AppleT2Smc.bin is ours, UNSIGNED (sig_len = 0),
which only the unrestricted edition accepts.

How to read the result
----------------------
  Echo.bin  SUCCESS   +  AppleT2Smc.bin  err 87
      -> the driver that is loaded ENFORCES signatures. That is the official
         edition, whatever the installer said. The unrestricted build never
         returns 87 for an unsigned blob - it skips the check entirely.

  Echo.bin  SUCCESS   +  AppleT2Smc.bin  SUCCESS
      -> both load. The module works; the remaining problem is in RPMac.

  Echo.bin  err 87    (too)
      -> the driver rejects even a signed official module: wrong driver
         version or a buffer/IOCTL mismatch. Please paste the full output.

  Echo.bin  SUCCESS   +  AppleT2Smc.bin  err 50
      -> our module loaded, ran, and declined this machine on purpose
         (STATUS_NOT_SUPPORTED). DebugView lines starting "AppleT2Smc:" say why.

Please paste the complete output (it is only a few lines).

Optional: DebugView with "Capture Kernel" AND "Enable Verbose Kernel Output".
The unrestricted PawnIO prints "[PawnIO] Signature check result: ..." on every
load attempt; without Verbose Kernel Output Windows filters that line out, so
its absence proves nothing unless Verbose is on.
