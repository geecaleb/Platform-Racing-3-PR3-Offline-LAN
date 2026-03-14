@echo off
setlocal enabledelayedexpansion

:: Get new IP address from user
set /p NEW_IP="Enter the new IP address: "

:: Paths to settings files
set "SETTINGS_FILE=settings.json"
set "BIN_SETTINGS_FILE=bin\Debug\net8.0\settings.json"

echo Updating IP addresses in settings files...

:: Function to update settings.json
call :UpdateSettings "%SETTINGS_FILE%"
if exist "%BIN_SETTINGS_FILE%" call :UpdateSettings "%BIN_SETTINGS_FILE%"

echo.
echo IP addresses updated successfully to %NEW_IP%.
echo Please restart the servers for changes to take effect.
echo.
pause
exit /b 0

:UpdateSettings
if not exist "%~1" (
    echo File not found: %~1
    exit /b 1
)

echo Processing %~1

:: Create a temporary file
set "TEMP_FILE=%~1.tmp"

:: Read the file line by line and replace IP addresses
(
for /f "tokens=*" %%a in ('type "%~1"') do (
    set "line=%%a"
    echo !line! | findstr /C:"database_host" >nul
    if !errorlevel! equ 0 (
        echo   "database_host": "%NEW_IP%",
    ) else (
        echo !line! | findstr /C:"redis_host" >nul
        if !errorlevel! equ 0 (
            echo   "redis_host": "%NEW_IP%",
        ) else (
            echo !line! | findstr /C:"bind_ip" >nul
            if !errorlevel! equ 0 (
                echo   "bind_ip": "%NEW_IP%",
            ) else (
                echo !line!
            )
        )
    )
)
) > "%TEMP_FILE%"

:: Replace the original file with the temporary file
move /y "%TEMP_FILE%" "%~1" >nul
echo Updated %~1
exit /b 0 
