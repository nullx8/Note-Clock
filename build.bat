@echo off
rem Builds NoteClock.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%~dp0NoteClock.exe" /r:System.Web.Extensions.dll "%~dp0NoteClock.cs"
