@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install .NET 10 SDK on the build computer.
  pause
  exit /b 1
)
dotnet build Agent\DomainConsole.Agent.csproj -c Release
if errorlevel 1 goto failed
dotnet publish Console\DomainConsole.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Publish
if errorlevel 1 goto failed
if not exist Publish\Agent mkdir Publish\Agent
copy /y Agent\bin\Release\net462\DomainConsole.Agent.exe Publish\Agent\
if errorlevel 1 goto failed
echo Built: Publish\DomainConsole.exe
pause
exit /b 0
:failed
echo Build failed. Save the console output.
pause
exit /b 1
