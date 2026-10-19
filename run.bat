@echo off
rem ===================================================================
rem Educational Cabinet Automated Launch Script (Windows)
rem Compliant with GEMINI.md Rule 3: strictly 7-bit ASCII encoding
rem ===================================================================

echo [INFO] Preparing to launch Educational Cabinet...

rem 1. Change directory to web project relative to script
cd /d "%~dp0src\EduCabinet.Web"
if errorlevel 1 (
    echo [ERROR] Failed to navigate to project folder: src\EduCabinet.Web
    goto :error
)

rem 2. Verify .NET SDK/Runtime is installed
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK is not installed or not in PATH.
    echo Please install .NET 8.0 SDK or higher: https://dotnet.microsoft.com
    goto :error
)

rem 3. Check PostgreSQL service
sc query postgresql-x64-18 >nul 2>nul
if errorlevel 1 (
    echo [WARNING] PostgreSQL service postgresql-x64-18 was not detected.
    echo Please make sure PostgreSQL is running on localhost:5432.
) else (
    echo [INFO] PostgreSQL service is detected.
)

rem 4. Open default browser after 3 seconds
echo [INFO] Launching browser at http://localhost:5000 in 3 seconds...
start /b cmd /c "timeout /t 3 /nobreak >nul && start http://localhost:5000"

rem 5. Run ASP.NET Core web server
echo [INFO] Starting Kestrel web server via dotnet run...
dotnet run --urls "http://localhost:5000"
if errorlevel 1 (
    echo [ERROR] Application terminated with an error.
    goto :error
)

goto :eof

:error
echo.
echo [ERROR] Application failed to start.
pause
exit /b 1
