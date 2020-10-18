FROM mcr.microsoft.com/dotnet/framework/aspnet:4.7.2

RUN Invoke-WebRequest 'http://download.microsoft.com/download/6/7/D/67D80164-7DD0-48AF-86E3-DE7A182D6815/rewrite_2.0_rtw_x64.msi' -OutFile c:\inetpub\rewrite.msi; \
    powershell.exe -Command Start-Process c:\inetpub\rewrite.msi -ArgumentList "/qn" -Wait; \
    Remove-Item c:\inetpub\rewrite.msi

COPY ./api /inetpub/wwwroot/api/
COPY ./App_Code /inetpub/wwwroot/App_Code/
COPY ./App_Data /inetpub/wwwroot/App_Data/
COPY ./bin /inetpub/wwwroot/bin/
COPY ./Branding /inetpub/wwwroot/Branding/
COPY ./Content /inetpub/wwwroot/Content/
COPY ./Public /inetpub/wwwroot/Public/
COPY ./Scripts /inetpub/wwwroot/Scripts/
COPY ./Views /inetpub/wwwroot/Views/
COPY ./Web.config /inetpub/wwwroot/

RUN Get-ChildItem c:\inetpub\wwwroot -Recurse

RUN Get-Acl c:\inetpub\wwwroot\App_Data | Set-Acl c:\inetpub\wwwroot\App_Data; \
    $acl = Get-Acl c:\inetpub\wwwroot\App_Data; \
    $inh = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit; \
    $fullc = New-Object System.Security.AccessControl.FileSystemAccessRule('BUILTIN\IIS_IUSRS', 'FullControl', $inh, 'None', 'Allow'); \
    $acl.SetAccessRule($fullc); \
    Set-Acl c:\inetpub\wwwroot\App_Data $acl;
