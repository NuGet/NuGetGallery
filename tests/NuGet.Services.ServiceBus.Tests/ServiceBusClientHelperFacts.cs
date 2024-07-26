// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.ServiceBus.Tests
{
    public class ServiceBusClientHelperFacts
    {
        public class TheGetServiceBusClientMethod
        {
            [Fact]
            public void HandlesOldUriFormat()
            {
                var connectionString = "sb://mytestnamespace.servicebus.windows.net/";

                var client = ServiceBusClientHelper.GetServiceBusClient(connectionString, managedIdentityClientId: null);

                Assert.Equal("mytestnamespace.servicebus.windows.net", client.FullyQualifiedNamespace);
            }
        }
    }
}
