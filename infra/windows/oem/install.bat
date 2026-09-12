@echo off
echo ====================================================
echo Provisioning Heimdall Windows 10 LTSC Test Node
echo ====================================================

if not exist "C:\OEM" mkdir "C:\OEM"
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\OEM\setup.ps1" >> "C:\OEM\provision.log" 2>&1
