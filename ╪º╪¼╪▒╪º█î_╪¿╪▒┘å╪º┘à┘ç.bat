@echo off
chcp 65001 >nul
cd /d "%~dp0MaintenanceManagement"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK نصب نیست. .NET 8 SDK را نصب کنید.
  pause
  exit /b 1
)
echo در حال بازیابی بسته‌ها...
dotnet restore
if errorlevel 1 (
  echo خطا در restore. اتصال اینترنت و نصب .NET 8 SDK را بررسی کنید.
  pause
  exit /b 1
)
echo در حال اجرا...
dotnet run
if errorlevel 1 (
  echo اجرای برنامه با خطا مواجه شد.
  pause
)
