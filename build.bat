@echo off
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
  exit /b 1
)

if not exist "build" mkdir "build"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Generate-Icon.ps1
if errorlevel 1 exit /b 1
echo Building PC-Cleanup.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.ServiceProcess.dll /win32manifest:build\app.manifest /win32icon:assets\app-icon.ico /out:build\PC-Cleanup.exe src\Properties\AssemblyInfo.cs src\Program.cs src\Models\CleanupModels.cs src\Services\FileSystemService.cs src\Services\RecycleBinService.cs src\Controls\FancyProgressBar.cs src\UI\MainForm.cs src\UI\MainForm.Operations.cs src\UI\MainForm.Workers.cs

if errorlevel 1 (
  echo [ERROR] Build failed.
  exit /b 1
)

echo [OK] build\PC-Cleanup.exe was built successfully.
endlocal

