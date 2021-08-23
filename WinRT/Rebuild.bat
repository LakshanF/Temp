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



C:\Users\lakshanf\temp>call C:\Windows\Microsoft.NET\FrameworkARM64\v4.0.30319\csc.exe /nologo /debug+ /optimize+ /platform:x64 /target:exe     /r:C:\Windows\Microsoft.NET\FrameworkARM64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll     /r:C:\Windows\Microsoft.NET\FrameworkARM64\v4.0.30319\System.Runtime.dll     /r:C:\Windows\system32\WinMetadata\Windows.Foundation.winmd     /r:C:\Windows\system32\WinMetadata\Windows.Networking.winmd     /out:Bin\64\WinRtEvent.exe     WinRtEvent.cs
warning CS1607: Assembly generation -- Referenced assembly 'Accessibility.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'Microsoft.CSharp.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Configuration.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Configuration.Install.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Core.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Data.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Data.DataSetExtensions.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Data.Linq.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Data.OracleClient.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Deployment.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Design.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.DirectoryServices.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Drawing.Design.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Drawing.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.EnterpriseServices.dll' targets a different processorwarning CS1607: Assembly generation -- Referenced assembly 'System.Management.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Messaging.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Runtime.Remoting.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Runtime.Serialization.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Runtime.Serialization.Formatters.Soap.dll' targets a
        different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Security.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.ServiceModel.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.ServiceModel.Web.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.ServiceProcess.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Transactions.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.Extensions.Design.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.Extensions.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.Mobile.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.RegularExpressions.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Web.Services.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Windows.Forms.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Workflow.Activities.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Workflow.ComponentModel.dll' targets a different
        processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Workflow.Runtime.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Xml.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'System.Xml.Linq.dll' targets a different processor
warning CS1607: Assembly generation -- Referenced assembly 'mscorlib.dll' targets a different processor

Success: The rebuilt binaries are available in the Bin\64 directory.

To attempt the repro, run the following command:

    Bin\64\WinRtEvent.exe


C:\Users\lakshanf\temp>Bin\64\WinRtEvent.exe
Running add/remove/add sequence in entrypoint domain...
Adding handlers in remote domains...
TRACE: Attaching handler in domain `RemoteDomain_0'.
TRACE: Attaching handler in domain `RemoteDomain_1'.
TRACE: Attaching handler in domain `RemoteDomain_2'.
TRACE: Attaching handler in domain `RemoteDomain_3'.
Unloading all remote domains...
During sleep, attach a debugger and generate NetworkStatusChanged events to observe their effect on the neutered CCWs.
    NetworkStatusChanged events can be triggered, e.g., by using the control panel to toggle the network
    adapter on and off.
In the debugger, 'sxe rtt' can be used to break just after a neutered event handler invocation fails.
Remote domains have been unloaded, blocking forever...
TRACE: Processing NetworkStatusChanged event in the entrypoint domain...
TRACE: Processing NetworkStatusChanged event in the entrypoint domain...
TRACE: Processing NetworkStatusChanged event in the entrypoint domain...
TRACE: Processing NetworkStatusChanged event in the entrypoint domain...
