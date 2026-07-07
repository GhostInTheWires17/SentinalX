@echo off
echo =================================================
echo SentinelX Build System (.NET Framework 4.8)
echo =================================================

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo [-] Error: csc.exe not found at %CSC_PATH%
    echo [-] Please verify your Windows .NET Framework 4.0/4.5/4.8 installation.
    exit /b 1
)

echo [*] Compiling SentinelX using csc.exe...
echo [*] Compiler: %CSC_PATH%
echo [*] References: System.Management.dll, System.ServiceProcess.dll
echo [*] Target: SentinelX.exe
echo.

"%CSC_PATH%" /target:exe /out:SentinelX.exe /r:System.Management.dll,System.ServiceProcess.dll /recurse:src\*.cs /nologo /optimize

if %ERRORLEVEL% equ 0 (
    echo.
    echo [+] Build successful! Generated: SentinelX.exe
    echo [+] Run: SentinelX.exe --duration 5 --output report.json
    exit /b 0
) else (
    echo.
    echo [-] Build failed! Check compiler errors above.
    exit /b %ERRORLEVEL%
)
