@echo off
rem Builds SleepGuard.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (echo .NET Framework 4 compiler not found at %CSC% & exit /b 1)
if not exist "%~dp0bin" mkdir "%~dp0bin"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%~dp0bin\SleepGuard.exe" /win32icon:"%~dp0assets\app.ico" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0src\*.cs" || exit /b 1
echo Built %~dp0bin\SleepGuard.exe
