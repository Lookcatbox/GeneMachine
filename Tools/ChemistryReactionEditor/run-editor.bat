@echo off
setlocal
cd /d "%~dp0..\.."
dotnet run --project "Tools\ChemistryReactionEditor\ChemistryReactionEditor.csproj"
pause

