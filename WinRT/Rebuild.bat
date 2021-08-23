@echo off

setlocal


if "%1" == "32" (
    set BITNESS=32
    set FXDIR=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319
    set CSC_PLATFORM_ARG=/platform:x86

) else if "%1" == "64" (
    set BITNESS=64
    set FXDIR=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319
    set CSC_PLATFORM_ARG=/platform:x64

) else (
    echo.
    echo Usage: Rebuild.bat ^(32^|64^)
    echo.
    exit /b 1
)


if /I not "%CD%\" == "%~dp0" (
    echo.
    echo Error: This script requires the current directory to be the same directory that holds the script.
    echo.
    echo Required: "%~dp0"
    echo Observed: "%CD%"
    echo.
    exit /b 1
)


set BINROOT=Bin\%BITNESS%
call :MakeOrCleanDir %BINROOT%


set CSC=%FXDIR%\csc.exe
set CSC_SWITCHES=/nologo /debug+ /optimize+ %CSC_PLATFORM_ARG%
set FAILED=


call :RunCsc %CSC_SWITCHES% /target:exe ^
    /r:%FXDIR%\System.Runtime.InteropServices.WindowsRuntime.dll ^
    /r:%FXDIR%\System.Runtime.dll ^
    /r:%SystemRoot%\system32\WinMetadata\Windows.Foundation.winmd ^
    /r:%SystemRoot%\system32\WinMetadata\Windows.Networking.winmd ^
    /out:%BINROOT%\WinRtEvent.exe ^
    WinRtEvent.cs


if defined FAILED (
    echo.
    echo Error: One or more csc.exe operations failed.
    echo.
    exit /b 1
)

echo.
echo Success: The rebuilt binaries are available in the %BINROOT% directory.
echo.
echo To attempt the repro, run the following command:
echo.
echo     %BINROOT%\WinRtEvent.exe
echo.

exit /b 0



:MakeOrCleanDir

mkdir %1 >nul 2>nul
del /q %1\*.dll >nul 2>nul
del /q %1\*.exe >nul 2>nul
del /q %1\*.pdb >nul 2>nul
exit /b



rem
rem %* - Arguments to pass to csc.exe.
rem

:RunCsc

    if defined FAILED (
        exit /b
    )

    @echo on
    call %CSC% %*
    @echo off

    if not "%ERRORLEVEL%" == "0" (
        set FAILED=1
        exit /b
    )

    exit /b



