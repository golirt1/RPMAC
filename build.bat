@echo off
setlocal
REM ============================================================
REM  RPMac build script
REM  Compiles the WPF GUI with the .NET Framework C# compiler
REM  that ships with Windows. No Visual Studio required.
REM ============================================================

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo ERROR: .NET Framework 4 C# compiler ^(csc.exe^) not found.
  echo Make sure .NET Framework 4.x is installed ^(it is on every Windows 10/11^).
  exit /b 1
)

set "ROOT=%~dp0"
if not exist "%ROOT%build" mkdir "%ROOT%build"

echo Compiling RPMac...
"%FW%\csc.exe" /noconfig /nologo /target:winexe /platform:x86 ^
  /win32manifest:"%ROOT%src\gui\app.manifest" ^
  /out:"%ROOT%build\RPMac.exe" ^
  /reference:"%FW%\System.dll" ^
  /reference:"%FW%\System.Core.dll" ^
  /reference:"%FW%\System.Xaml.dll" ^
  /reference:"%FW%\WPF\WindowsBase.dll" ^
  /reference:"%FW%\WPF\PresentationCore.dll" ^
  /reference:"%FW%\WPF\PresentationFramework.dll" ^
  /reference:"%FW%\System.Windows.Forms.dll" ^
  /reference:"%FW%\System.Drawing.dll" ^
  "%ROOT%src\gui\Smc.cs" "%ROOT%src\gui\App.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

echo Compiling smccore (command-line / scripting tool)...
"%FW%\csc.exe" /nologo /target:exe /platform:x86 ^
  /out:"%ROOT%build\smccore.exe" ^
  "%ROOT%src\smccore.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED (smccore)
  exit /b 1
)

REM Copy inpout32.dll. If RPMac is running it holds the DLL open; that's fine,
REM the existing copy is identical, so don't fail the build over it.
copy /y "%ROOT%third_party\InpOut32\inpout32.dll" "%ROOT%build\inpout32.dll" >nul 2>&1
if not exist "%ROOT%build\inpout32.dll" (
  echo BUILD FAILED: inpout32.dll missing
  exit /b 1
)

echo.
echo BUILD OK  -^>  build\RPMac.exe  +  build\smccore.exe
echo Run as administrator. Keep inpout32.dll next to them.
endlocal
exit /b 0
