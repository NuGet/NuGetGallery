// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Services.Incidents
{
    public class IncidentApiConfiguration
    {
        /// <summary>
        /// The base <see cref="Uri"/> of the NuGet incident API to access.
        /// </summary>
        public Uri BaseUri { get; set; }

        /// <summary>
        /// The certificate to use to authenticate with the NuGet incident API.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }
    }
}
