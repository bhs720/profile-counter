@echo off
rem Applies the local patches in this directory to the mupdf submodule.
rem
rem The submodule is a pristine checkout of an upstream tag, so any edit made
rem inside it is untracked by this repository and is discarded by the next
rem `git submodule update`. Keeping the changes here as patches means they are
rem version controlled, reviewable, and reapplied on every build.
rem
rem Safe to run repeatedly: a patch that is already applied is skipped. If a
rem patch no longer applies (typically after bumping the mupdf tag) the build
rem fails rather than silently producing an unpatched binary.

setlocal enabledelayedexpansion

set "PATCHDIR=%~dp0"
set "SUBMODULE=%~dp0..\mupdf"

where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: git was not found on PATH; it is required to apply the mupdf patches.
    exit /b 1
)

if not exist "%SUBMODULE%\.git" (
    echo ERROR: the mupdf submodule is not checked out at "%SUBMODULE%".
    echo Run: git submodule update --init --recursive
    exit /b 1
)

pushd "%SUBMODULE%" || exit /b 1

rem `exit /b` inside a for loop does not reliably set the batch exit code, so
rem record failures in a flag and report once the loop has finished.
set "PATCHFAILED="

for %%P in ("%PATCHDIR%*.patch") do (
    git apply --reverse --check "%%~fP" >nul 2>&1
    if !errorlevel! equ 0 (
        echo [patch] already applied: %%~nxP
    ) else (
        git apply --check "%%~fP" >nul 2>&1
        if !errorlevel! neq 0 (
            echo ERROR: patch does not apply cleanly: %%~nxP
            echo The mupdf submodule may have moved; refresh the patch against the current tag.
            set "PATCHFAILED=1"
        ) else (
            git apply "%%~fP"
            if !errorlevel! neq 0 (
                echo ERROR: failed to apply patch: %%~nxP
                set "PATCHFAILED=1"
            ) else (
                echo [patch] applied: %%~nxP
            )
        )
    )
)

popd

if defined PATCHFAILED exit /b 1
exit /b 0
