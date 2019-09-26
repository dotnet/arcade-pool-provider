set WORKSPACEPATH=%1
REM Deleting %WORKSPACEPATH% in case it happens to be a file
del /Q %WORKSPACEPATH%
REM Kill known processes that might leave handles open
taskkill -f -im msbuild.exe
taskkill -f -im dotnet.exe
taskkill -f -im vbcscompiler.exe
taskkill -f -im devenv.exe
rmdir /S /Q %WORKSPACEPATH%

REM Sometimes killing the processes above fails; there could also be surprise new processes.
REM As the disk here is big and the consequences of a dirty workspace are bad, let's make a new one.
REM We'll always try to delete the existing ones in ascending order so if it's possible, we'll eventually clean up.
IF NOT EXIST "%WORKSPACEPATH%" goto :CREATE_WORKSPACE
set reboot_required=true
set /a suffix=0
:while
set /a suffix+=1
set candidateworkspacepath=%WORKSPACEPATH%.%suffix%
echo Trying %candidateworkspacepath%...
rmdir /S /Q %candidateworkspacepath%
if exist "%candidateworkspacepath%" goto :while
set WORKSPACEPATH=%candidateworkspacepath%

:CREATE_WORKSPACE
mkdir %WORKSPACEPATH%
xcopy /Y /S /I %HELIX_CORRELATION_PAYLOAD%\* %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.agent %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.credentials %WORKSPACEPATH%
call %WORKSPACEPATH%\run.cmd

set LASTEXITCODE=%errorlevel%

REM At this point the agent job is done.  If we had a non-delete-able workspace,
REM we'll want to reboot to kill any mystery processes left running which caused this.
if "%reboot_required%"=="true" (call %HELIX_PYTHONPATH% -c "from helix.platformutil import reboot_machine; reboot_machine()" )

REM The above reboot prevents uploading logs; if we got past it, preserve the diag logs for future use.
IF EXIST "%WORKSPACEPATH%\_diag" (xcopy /s /y "%WORKSPACEPATH%\_diag\*" "%HELIX_WORKITEM_UPLOAD_ROOT%" )

if not "%LASTEXITCODE%" == "0" (
    echo "Unexpected error returned from agent: %LASTEXITCODE%"
    exit /b 1
) else (
    echo "Agent disconnected successfully, exiting"
    exit /b 0
)
