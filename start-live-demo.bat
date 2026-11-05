@echo off
title VitalQ Live Production Tunnel
echo ========================================================
echo         VitalQ - Outpatient Clinic Live Launcher
echo ========================================================
echo.
echo 1. Starting Backend Web API (.NET 8) on http://localhost:5089 ...
start "VitalQ API (Port 5089)" cmd /k "dotnet run --project api/VitalQ.API/VitalQ.API.csproj"

echo 2. Starting Frontend Web App (Angular 19 Production) on http://localhost:4200 ...
start "VitalQ Web (Port 4200)" cmd /k "cd web && npx ng serve -c production --disable-host-check"

echo.
echo Waiting 10 seconds for servers to initialize...
timeout /t 10 /nobreak > nul

echo 3. Launching Cloudflare Public HTTPS Tunnel...
echo --------------------------------------------------------
echo YOUR LIVE PUBLIC URL WILL APPEAR BELOW (look for https://*.trycloudflare.com):
echo --------------------------------------------------------
tools\cloudflared.exe tunnel --url http://localhost:4200
pause
