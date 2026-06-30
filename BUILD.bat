@echo off
cd /d %~dp0RomForge
echo Building RomForge Windows...
dotnet publish RomForge.csproj -c Release -r win-x64 --self-contained false -p:PublishSelfContained=false -p:Platform=x64
echo Done!
explorer bin\x64\Release\net8.0-windows\win-x64\publish
pause