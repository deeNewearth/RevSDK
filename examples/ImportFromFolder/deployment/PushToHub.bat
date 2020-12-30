@echo off
xcopy /Y /F Dockerfile ..\bin\Release
set /p revversion=<version.txt

if "!revversion!"=="" (
    set /P revversion=1
) else (
    set /A revversion=revversion+1
)

echo building baradiyah/importfromfolder:1.0.%revversion%
docker build -t baradiyah/importfromfolder:1.0.%revversion% ..\bin\Release

echo ready to publish baradiyah/importfromfolder:1.0.%revversion%, Press CTRL-C to exit or any key to continue

pause
docker push baradiyah/importfromfolder:1.0.%revversion%

echo all done
pause 