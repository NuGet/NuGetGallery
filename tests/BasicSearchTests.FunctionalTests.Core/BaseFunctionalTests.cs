// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using System.Net;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class BaseFunctionalTests
    {
        protected HttpClient Client;
        protected RetryHandler RetryHandler;

        public BaseFunctionalTests()
        {
            // Arrange
            IgnoreCertificateValidationErrors();
            RetryHandler = new RetryHandler(new HttpClientHandler());
            Client = new HttpClient(RetryHandler) { BaseAddress = new Uri(EnvironmentSettings.SearchServiceBaseUrl) };
        }

        private static void IgnoreCertificateValidationErrors()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
    }
}