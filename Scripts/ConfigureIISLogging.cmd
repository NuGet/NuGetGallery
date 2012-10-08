@ECHO OFF

echo Configuring IIS logging

rem The set of fields that will be logged by IIS
SET logfields=Date,Time,UserName,ServerIP,Method,UriStem,UriQuery,HttpStatus,Win32Status,TimeTaken,ServerPort,UserAgent,Referer,HttpSubStatus

rem The appcmd executable
SET appcmd=%windir%\system32\inetsrv\appcmd

rem Retrieve all the sites, /xml flag allows output to be piped to next command
SET getallsites=%appcmd% list sites /xml

rem Set the logging fields for each site in the xml input (triggered by /in flag)
SET setlogging=%appcmd%  set site /in /logFile.logExtFileFlags:%logfields%

rem Look for the string that indicates logging fields were set
SET checkforsuccess=find "SITE object"
 
:configlogstart
%getallsites% | %setlogging% | %checkforsuccess%
IF NOT ERRORLEVEL 1 goto configlogdone
 
echo No site found, waiting 10 secs before retry... >> output.log
TIMEOUT 10 > nul
goto configlogstart
 
:configlogdone
echo Done configuring IIS logging >> output.log