@echo off
REM pawntest - minimal PawnIO probe. Build with the .NET Framework 4 compiler.
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" -nologo -target:exe -platform:x64 -out:pawntest.exe pawntest.cs
