@echo off
setlocal

echo === 설계도서 검토 툴 실행파일(exe) 빌드 ===
echo.

python -m pip install -e ".[gui]"
if errorlevel 1 goto :error

python -m PyInstaller --noconfirm --onefile --windowed ^
  --name "설계도서검토툴" ^
  --paths src ^
  --add-data "src\design_review_tool\config;design_review_tool\config" ^
  src\design_review_tool\gui\app.py
if errorlevel 1 goto :error

echo.
echo 빌드 완료: dist\설계도서검토툴.exe
pause
exit /b 0

:error
echo.
echo 빌드 중 오류가 발생했습니다. 위 로그를 확인해주세요.
pause
exit /b 1
