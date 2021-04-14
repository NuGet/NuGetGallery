// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

        public class AddInjectedInMemoryCollection
        {
            [Fact]
            public void InjectsSecretsIntoInMemoryCollection()
            {
                var dict = new Dictionary<string, string>();
                dict.Add("Key1", "Some $$hidden$$ secret");

                var secretInjector = new Mock<ISecretInjector>();
                secretInjector
                    .Setup(x => x.InjectAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => "Some unhidden secret");

                var configuration = new ConfigurationBuilder()
                    .AddInjectedInMemoryCollection(dict, secretInjector.Object)
                    .Build();

                var value = configuration.GetValue<string>("Key1");
                Assert.Equal("Some unhidden secret", value);
                secretInjector.Verify(x => x.InjectAsync("Some $$hidden$$ secret"), Times.Once);
            }
        }
    }
}
