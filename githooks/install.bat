@echo off
REM Install Neuro-Engine git hooks (Windows)
REM Usage: githooks\install.bat

echo Installing Neuro-Engine git hooks...
echo.

REM Get the repository root
cd /d "%~dp0\.."

REM Set the hooks path
git config core.hooksPath githooks

echo Git hooks path set to: githooks/
echo.

echo Installed hooks:
echo   - pre-commit  : Blocks commits with anti-patterns in engine code
echo   - pre-push    : Checks for layer review before pushing
echo   - commit-msg  : Suggests layer tags for commit messages
echo.
echo Protected paths:
echo   - Packages/com.neuroengine.core/Runtime/Core/
echo   - Packages/com.neuroengine.core/Runtime/Services/
echo   - Packages/com.neuroengine.core/Editor/
echo   - Packages/com.neuroengine.core/Tests/
echo.
echo To bypass hooks (not recommended):
echo   git commit --no-verify
echo   git push --no-verify
echo.
echo Done!
pause
