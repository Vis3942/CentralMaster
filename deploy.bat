dotnet publish -c release -f net5.0 -r linux-arm --self-contained=true
copy CentralMaster\VersionInfo.txt CentralMaster\bin\Release\net5.0\linux-arm\publish\