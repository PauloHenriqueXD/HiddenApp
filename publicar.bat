@echo off
rem Gera o HiddenApp.exe portatil (unico arquivo, ja com o .NET incluso) na pasta "publish".
cd /d "%~dp0"
dotnet publish HiddenApp.csproj -p:PublishProfile=Portatil
if errorlevel 1 (
  echo.
  echo Falha ao gerar o executavel.
  pause
  exit /b 1
)
echo.
echo Pronto! Executavel gerado em: %~dp0publish\HiddenApp.exe
explorer "%~dp0publish"
pause
