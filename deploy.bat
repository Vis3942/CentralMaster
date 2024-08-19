set custdir=publish_release
set appdir=CentralMaster
set publishdir=%appdir%\bin\Release\net6.0\linux-arm\publish\
echo "Cleaning publish directory %appdir%\%custdir%\"
del /F /Q %appdir%\%custdir%\
del /F /Q %publishdir%

timeout /T 5 > NUL
echo "Publishing to directory %publishdir%"
dotnet publish -c release -f net6.0 -r linux-arm  -o %publishdir% --self-contained=true
copy %appdir%\VersionInfo.txt %publishdir%

echo "Generating zip file for deployment"
tar -a -c -f %appdir%\%custdir%\%appdir%.zip -C %publishdir% *.*


rem dotnet publish -c release -f net5.0 -r linux-arm --self-contained=true
rem copy CentralMaster\VersionInfo.txt CentralMaster\bin\Release\net6.0\linux-arm\publish\