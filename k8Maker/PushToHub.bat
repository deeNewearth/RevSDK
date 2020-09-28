@echo off
setlocal EnableDelayedExpansion



set /P revversion=<versionNo.txt

if "!revversion!"=="" (
    set /P revversion=1
) else (
    set /A revversion=revversion+1
)

echo building labizbille/makek8:1.0.%revversion%
echo %revversion% > versionNo.txt
docker build -t labizbille/makek8:latest -t labizbille/makek8:1.0.%revversion%  .

docker push labizbille/makek8:1.0.%revversion%
docker push labizbille/makek8:latest

echo all done
pause 

