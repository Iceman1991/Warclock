@echo off
rem Baut dist\Warclock-Setup.exe: App ohne .NET-Voraussetzung (self-contained) + Inno Setup. Installiert nichts.
cd /d "%~dp0.."
if exist tmp\sc rmdir /s /q tmp\sc
dotnet publish src\DcsCampaign -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o tmp\sc || exit /b 1
rem Splash Damage (fremdes Skript, ohne Lizenz, nicht im Repo): nur lokaler Build; "build.cmd public" (Release) laesst es weg
if /i not "%~1"=="public" if exist "%LOCALAPPDATA%\DCS-Kampagne\scripts\Splash_Damage*.lua" (mkdir tmp\sc\scripts & copy /y "%LOCALAPPDATA%\DCS-Kampagne\scripts\Splash_Damage*.lua" tmp\sc\scripts\ >nul)
tmp\sc\DcsCampaign.exe --test || (echo Self-test failed & exit /b 1)
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" /Q installer\DcsCampaign.iss || exit /b 1
echo Done: dist\Warclock-Setup.exe
