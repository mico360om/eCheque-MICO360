@echo off
REM ============================================================
REM  eCheque MICO360 — one-click push & publish
REM  1) Logs you into GitHub via your web browser (one time)
REM  2) Pushes all commits + the v1.2.0 tag
REM  The tag push triggers GitHub Actions to build the release
REM  with both installers automatically.
REM ============================================================
cd /d "%~dp0"

echo.
echo === Step 1/3: GitHub login (a browser window will open) ===
echo When asked, press Enter to open the browser, then approve.
echo.
gh auth login -h github.com -p https --web
if errorlevel 1 (
  echo.
  echo Login failed or was cancelled. Nothing was pushed.
  pause
  exit /b 1
)
gh auth setup-git -h github.com

echo.
echo === Step 2/3: Pushing commits to GitHub ===
git push origin main
if errorlevel 1 (
  echo Push failed - see the message above.
  pause
  exit /b 1
)

echo.
echo === Step 3/3: Pushing the v1.2.0 release tag ===
git push origin v1.2.0

echo.
echo ============================================================
echo  DONE. GitHub Actions is now building the release.
echo  Installers appear in a few minutes at:
echo  https://github.com/mico360om/eCheque-MICO360/releases
echo ============================================================
pause
