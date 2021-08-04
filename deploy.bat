set custdir=publishcustdir
set appdir=CentralMaster
echo "Cleaning publish directory %appdir%\%custdir%\"
del /F /Q %appdir%\%custdir%\
echo "Publishing to directory %appdir%\%custdir%\"
dotnet publish -c release -f net5.0 -r linux-arm  -o %appdir%\%custdir%\ --self-contained=true
copy %appdir%\VersionInfo.txt %appdir%\%custdir%\



rem dotnet publish -c release -f net5.0 -r linux-arm --self-contained=true
rem copy CentralMaster\VersionInfo.txt CentralMaster\bin\Release\net5.0\linux-arm\publish\