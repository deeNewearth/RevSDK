@echo off
xcopy /Y /F Dockerfile ..\bin\Release\publish
set /p revversion=<version.txt

echo building labizbille/importfromfolder:%revversion%
docker build -t labizbille/importfromfolder:%revversion% -t labizbille/importfromfolder:currentBuild ..\bin\Release\publish

echo creating portbale image %CD%/../bin/importfromfolder_%revversion%.tar
docker save --output %CD%/../bin/importfromfolder_%revversion%.tar labizbille/importfromfolder:%revversion% 


echo all done
pause 