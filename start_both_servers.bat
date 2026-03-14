@echo off
:: Start the web server
start "PR3 Web Server" cmd /k "cd PlatformRacing3.Web && dotnet run --no-build"

:: Start the game server
start "PR3 Web Server" cmd /k "cd PlatformRacing3.Server && dotnet run --no-build"
