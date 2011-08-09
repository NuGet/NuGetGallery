<%@ Page Language="C#" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title>NuGet Private Repository</title>
    <style>
        body {font-family: Calibri;}
    </style>
</head>
<body>
    <div>
        <h2>You are running NuGet.Server v<%= typeof(NuGet.Server.DataServices.Package).Assembly.GetName().Version %></h2>
        <p>
            Click <a href="<%= VirtualPathUtility.ToAbsolute("~/nuget/Packages") %>">here</a> to view your packages.
        </p>
        <fieldset style="width:600px">
            <legend><strong>Repository URL</strong></legend>
            In the package manager settings, add the following URL to the list of 
            Package Sources:
            <blockquote>
                <strong><%= Helpers.GetRepositoryUrl(Request.Url, Request.ApplicationPath) %></strong>
            </blockquote>
        </fieldset>
        <p style="font-size:1.1em">
            To add packages to the feed put package files (.nupkg files) in a folder named "Packages" under the site root (i.e. ~/Packages).
        </p>
    </div>
</body>
</html>
