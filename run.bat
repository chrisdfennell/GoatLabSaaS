@echo off
echo Starting GoatLab...
echo.
echo   App:     http://localhost:5051
echo   Swagger: http://localhost:5051/swagger
echo.
cd /d "%~dp0src\GoatLab.Server"
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --urls "http://localhost:5051"
