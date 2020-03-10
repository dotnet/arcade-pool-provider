set WORKSPACEPATH=%1
REM Deleting %WORKSPACEPATH% in case it happens to be a file
del /Q %WORKSPACEPATH%

REM Use a guaranteed new empty directory to be able to clear out the folder
REM in the case of a previous build hitting MAX_PATH (260 chars)
SET EMPTYDIR=%TEMP%\%RANDOM%%RANDOM%
MD %EMPTYDIR%

REM Kill known processes that might leave handles open
taskkill -f -im msbuild.exe
taskkill -f -im dotnet.exe
taskkill -f -im vbcscompiler.exe
taskkill -f -im devenv.exe
robocopy /mir %EMPTYDIR% %WORKSPACEPATH%
rmdir /S /Q %WORKSPACEPATH%

REM Sometimes killing the processes above fails; there could also be surprise new processes.
REM As the disk here is big and the consequences of a dirty workspace are bad, let's make a new one.
REM We'll always try to delete the existing ones in ascending order so if it's possible, we'll eventually clean up.
IF NOT EXIST "%WORKSPACEPATH%" goto :CREATE_WORKSPACE
set /a suffix=0
:while
set /a suffix+=1
set candidateworkspacepath=%WORKSPACEPATH%.%suffix%
echo Trying %candidateworkspacepath%...
robocopy /mir %EMPTYDIR% %candidateworkspacepath%
rmdir /S /Q %candidateworkspacepath%
if exist "%candidateworkspacepath%" goto :while
set WORKSPACEPATH=%candidateworkspacepath%

:CREATE_WORKSPACE
rd /s /q %EMPTYDIR%
mkdir %WORKSPACEPATH%
xcopy /Y /S /I %HELIX_CORRELATION_PAYLOAD%\* %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.agent %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.credentials %WORKSPACEPATH%
call %WORKSPACEPATH%\run.cmd

set LASTEXITCODE=%errorlevel%

REM Preserve the diag logs for future use
IF EXIST "%WORKSPACEPATH%\_diag" (xcopy /s /y "%WORKSPACEPATH%\_diag\*" "%HELIX_WORKITEM_UPLOAD_ROOT%" )

if not "%LASTEXITCODE%" == "0" (
    echo "Unexpected error returned from agent: %LASTEXITCODE%"
    exit /b 1
) else (
    echo "Agent disconnected successfully, exiting"
    exit /b 0
)

rmdir /S /Q %WORKSPACEPATH%
echo "Requesting reboot to kill all processes"
%HELIX_PYTHONPATH% -c "from helix.workitemutil import request_reboot; request_reboot('Optional reason string')"
