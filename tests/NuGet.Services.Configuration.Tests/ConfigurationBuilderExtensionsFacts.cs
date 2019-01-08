// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationBuilderExtensionsFacts
    {
        public class AddInjectedEnvironmentVariables
        {
            [Fact]
            public void InjectsSecretsIntoEnvironmentVariables()
            {
                Environment.SetEnvironmentVariable("NUGET_TEST_MySecret", "My secret is $$hidden$$.");
                var secretInjector = new Mock<ISecretInjector>();
                secretInjector
                    .Setup(x => x.InjectAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => "My secret is visible.");
                var configuration = new ConfigurationBuilder()
                    .AddInjectedEnvironmentVariables("NUGET_TEST_", secretInjector.Object)
                    .Build();

                var value = configuration.GetValue<string>("MySecret");

                Assert.Equal("My secret is visible.", value);
                secretInjector.Verify(x => x.InjectAsync("My secret is $$hidden$$."), Times.Once);
            }
        }
    }
}
