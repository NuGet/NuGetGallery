// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Stats.ParseAzureCdnLogs
{
    internal class NuGetClientResolver
    {
        private static readonly NuGetClientInfo _unknownClient = new NuGetClientInfo("Other");
        private static readonly IDictionary<string, NuGetClientInfo> _knownClients;

        static NuGetClientResolver()
        {
            _knownClients = new Dictionary<string, NuGetClientInfo>();

            // NuGet Clients
            RegisterNuGetClients();

            // Ecosystem Partners
            RegisterEcosystemPartners();

            // Register Browsers
            RegisterBrowsers();
        }

        private static void RegisterNuGetClients()
        {
            // VS NuGet 2.8+ User Agent Strings
            _knownClients.Add("NuGet VS PowerShell Console/", new NuGetClientInfo("NuGet VS PowerShell Console", "NuGet"));
            _knownClients.Add("NuGet VS Packages Dialog - Solution/", new NuGetClientInfo("NuGet VS Packages Dialog - Solution", "NuGet"));
            _knownClients.Add("NuGet VS Packages Dialog/", new NuGetClientInfo("NuGet VS Packages Dialog", "NuGet"));

            // VS NuGet (pre-2.8) User Agent Strings
            _knownClients.Add("NuGet Add Package Dialog/", new NuGetClientInfo("NuGet Add Package Dialog", "NuGet"));
            _knownClients.Add("NuGet Command Line/", new NuGetClientInfo("NuGet Command Line", "NuGet"));
            _knownClients.Add("NuGet Package Manager Console/", new NuGetClientInfo("NuGet Package Manager Console", "NuGet"));
            _knownClients.Add("NuGet Visual Studio Extension/", new NuGetClientInfo("NuGet Visual Studio Extension", "NuGet"));
            _knownClients.Add("Package-Installer/", new NuGetClientInfo("Package-Installer", "NuGet"));

            // WebMatrix includes its own core version number as part of the client name, before the slash
            // Therefore we don't include the slash in the match
            _knownClients.Add("WebMatrix", new NuGetClientInfo("WebMatrix", "WebMatrix"));
        }

        private static void RegisterEcosystemPartners()
        {
            // Refer to npecodeplex.com
            _knownClients.Add("NuGet Package Explorer Metro/", new NuGetClientInfo("NuGet Package Explorer Metro", "NuGet Package Explorer"));
            _knownClients.Add("NuGet Package Explorer/", new NuGetClientInfo("NuGet Package Explorer", "NuGet Package Explorer"));

            // Refer to www.jetbrains.com for details
            // TeamCity uses a space to separate the client from the version instead of slash
            _knownClients.Add("JetBrains TeamCity ", new NuGetClientInfo("JetBrains TeamCity"));

            // Refer to www.sonatype.com for details
            // Make sure to use the slash here because there are "Nexus" phones that match otherwise
            _knownClients.Add("Nexus/", new NuGetClientInfo("Sonatype Nexus"));

            // Refer to www.jfrog.com for details
            _knownClients.Add("Artifactory/", new NuGetClientInfo("JFrog Artifactory"));

            // Refer to www.myget.org
            // MyGet doesn't send a version, so be sure to omit the slash
            _knownClients.Add("MyGet", new NuGetClientInfo("MyGet"));

            // Refer to www.inedo.com for details
            _knownClients.Add("ProGet/", new NuGetClientInfo("Inedo ProGet"));

            // Refer to http://fsprojects.github.io/Paket
            // Paket 0.x doesn't send a version, so be sure to omit the slash
            _knownClients.Add("Paket", new NuGetClientInfo("Paket"));

            // Refer to www.xamarin.com
            _knownClients.Add("Xamarin Studio/", new NuGetClientInfo("Xamarin Studio"));
        }

        private static void RegisterBrowsers()
        {
            _knownClients.Add("Mozilla", NuGetClientInfo.Browser());
            _knownClients.Add("Opera", NuGetClientInfo.Browser());
        }

        public static NuGetClientInfo FromUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return _unknownClient;
            }

            foreach (var knownClient in _knownClients)
            {
                if (userAgent.IndexOf(knownClient.Key, StringComparison.Ordinal) == 0)
                {
                    return knownClient.Value;
                }
            }

            return _unknownClient;
        }
    }
}