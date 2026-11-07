@echo off
title Aarohi Developer Login Setup
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0EnableDeveloperLogin.ps1"
exit /b %errorlevel%
