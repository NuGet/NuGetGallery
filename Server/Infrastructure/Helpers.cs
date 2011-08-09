using System;

public static class Helpers {
    public static string GetRepositoryUrl(Uri currentUrl, string applicationPath) {
        var uriBuilder = new UriBuilder(currentUrl);

        string repositoryUrl = uriBuilder.Scheme + "://" + uriBuilder.Host;
        if (uriBuilder.Port != 80) {
            repositoryUrl += ":" + uriBuilder.Port;
        }
        repositoryUrl += applicationPath;

        // ApplicationPath for Virtual Apps don't end with /
        if (!repositoryUrl.EndsWith("/")) {
            repositoryUrl += "/";
        }
        repositoryUrl += "nuget";

        return repositoryUrl;
    }
}
