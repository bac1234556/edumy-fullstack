@echo off
echo ==============================================
echo       EDUMY FULLSTACK LOCAL START SCRIPT
echo ==============================================
echo.
echo Starting Backend (.NET)...
start cmd /k "cd /d %~dp0Backend && dotnet run"

echo Starting ML Service (Python FastAPI)...
start cmd /k "cd /d %~dp0Edumy-ML-Service && .\venv\Scripts\activate && uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload"

echo Starting Frontend (React/Vite)...
start cmd /k "cd /d %~dp0Frontend && npm run dev"

echo.
echo All services have been launched in separate windows!
echo - Frontend: http://localhost:5173
echo - Backend:  http://localhost:5152
echo - ML API:   http://localhost:8000
echo.
pause
