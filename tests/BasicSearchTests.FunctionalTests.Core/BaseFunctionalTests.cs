// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using System.Net;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class BaseFunctionalTests : IDisposable
    {
        protected HttpClient Client;
        protected RetryHandler RetryHandler;

        public BaseFunctionalTests()
            : this(EnvironmentSettings.SearchServiceBaseUrl)
        {
        }

        public BaseFunctionalTests(string baseUrl)
        {
            // Arrange
            IgnoreCertificateValidationErrors();
            RetryHandler = new RetryHandler(new HttpClientHandler());
            Client = new HttpClient(RetryHandler) { BaseAddress = new Uri(baseUrl) };
        }

        private static void IgnoreCertificateValidationErrors()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }

        public void Dispose()
        {
            Client.Dispose();
            RetryHandler.Dispose();
        }
    }
}