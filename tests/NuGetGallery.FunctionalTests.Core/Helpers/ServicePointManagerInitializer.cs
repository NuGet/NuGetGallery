// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NuGetGallery.FunctionalTests
{
    public class ServicePointManagerInitializer
    {
        public static void InitializeServerCertificateValidationCallback()
        {
            // suppress SSL validation for *.cloudapp.net
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
        }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            var httpWebRequest = sender as HttpWebRequest;
            if (httpWebRequest != null 
                && httpWebRequest.RequestUri.Host.EndsWith(".cloudapp.net", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}