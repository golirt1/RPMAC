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
  /win32icon:"%ROOT%resources\RPMac.ico" ^
  /out:"%ROOT%build\RPMac.exe" ^
  /reference:"%FW%\System.dll" ^
  /reference:"%FW%\System.Core.dll" ^
  /reference:"%FW%\System.Xaml.dll" ^
  /reference:"%FW%\WPF\WindowsBase.dll" ^
  /reference:"%FW%\WPF\PresentationCore.dll" ^
  /reference:"%FW%\WPF\PresentationFramework.dll" ^
  /reference:"%FW%\System.Windows.Forms.dll" ^
  /reference:"%FW%\System.Drawing.dll" ^
  "%ROOT%src\gui\Smc.cs" "%ROOT%src\gui\I18n.cs" "%ROOT%src\gui\App.cs"

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
  echo BUILD FAILED ^(smccore^)
  exit /b 1
)

echo Copying resources...

REM App config (enables catching corrupted-state exceptions so startup crashes are logged).
copy /y "%ROOT%src\gui\RPMac.exe.config" "%ROOT%build\RPMac.exe.config" >nul 2>&1

REM Copy inpout32.dll. If RPMac is running it holds the DLL open; that's fine,
REM the existing copy is identical, so don't fail the build over it.
copy /y "%ROOT%third_party\InpOut32\bin\Win32\inpout32.dll" "%ROOT%build\inpout32.dll" >nul 2>&1
if not exist "%ROOT%build\inpout32.dll" (
  echo BUILD FAILED: inpout32.dll missing
  exit /b 1
)

REM Copy the a prebuilt copy of the PawnIO driver
copy /y "%ROOT%dist\AppleT2Smc.bin" "%ROOT%build\AppleT2Smc.bin" >nul 2>&1
if not exist "%ROOT%build\AppleT2Smc.bin" (
  echo BUILD FAILED: AppleT2Smc.bin missing
  exit /b 1
)

REM The official PawnIO installer (namazso, redistributed unmodified). On a T2 Mac without
REM PawnIO, RPMac offers to run it. It isn't kept in git: fetch the pinned version once and
REM make sure it is exactly that file. Keep the version and hash in sync with App.cs and the .iss.
set "PAWNIO_URL=https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe"
set "PAWNIO_SHA256=1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032"
set "PAWNIO_FILE=%ROOT%dist\PawnIO_setup.exe"
call :pawnio_ok
if errorlevel 1 (
  REM Missing or not the pinned file: download it. It lands in a .part first, so a broken
  REM download never replaces anything, and a correct file placed by hand is never deleted.
  echo Downloading PawnIO_setup.exe 2.2.0...
  curl -fsSL -o "%PAWNIO_FILE%.part" "%PAWNIO_URL%" && move /y "%PAWNIO_FILE%.part" "%PAWNIO_FILE%" >nul
  if exist "%PAWNIO_FILE%.part" del /f /q "%PAWNIO_FILE%.part" >nul 2>&1
  call :pawnio_ok
)
if errorlevel 1 (
  echo BUILD FAILED: dist\PawnIO_setup.exe is missing or is not the official PawnIO 2.2.0 installer.
  echo Download it from %PAWNIO_URL%
  echo into dist\ and run build.bat again.
  exit /b 1
)
copy /y "%PAWNIO_FILE%" "%ROOT%build\PawnIO_setup.exe" >nul 2>&1
if not exist "%ROOT%build\PawnIO_setup.exe" (
  echo BUILD FAILED: PawnIO_setup.exe missing
  exit /b 1
)

echo.
echo BUILD OK  -^>  build\RPMac.exe  +  build\smccore.exe
echo Run as administrator. Keep inpout32.dll next to them.
endlocal
exit /b 0

REM Exit code 0 when dist\PawnIO_setup.exe is exactly the pinned file. certutil prints the hash
REM on its second line; older Windows puts spaces between the bytes, so they are removed.
:pawnio_ok
set "GOT="
if not exist "%PAWNIO_FILE%" exit /b 1
for /f "skip=1 delims=" %%H in ('certutil -hashfile "%PAWNIO_FILE%" SHA256 2^>nul') do if not defined GOT set "GOT=%%H"
if not defined GOT exit /b 1
set "GOT=%GOT: =%"
if /i "%GOT%"=="%PAWNIO_SHA256%" exit /b 0
exit /b 1
