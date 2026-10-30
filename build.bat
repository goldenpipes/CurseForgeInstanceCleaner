@echo off
rem Builds CurseForgeInstanceCleaner.exe with the C# compiler that comes with Windows (.NET Framework 4).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo The .NET Framework 4 C# compiler was not found.
    exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
    /out:"%~dp0CurseForgeInstanceCleaner.exe" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
    "%~dp0src\*.cs"
if errorlevel 1 exit /b 1
echo Built CurseForgeInstanceCleaner.exe
