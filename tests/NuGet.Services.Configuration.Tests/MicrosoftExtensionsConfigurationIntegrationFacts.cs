// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    /// <summary>
    /// Checks some assumptions about implementation of Microsoft.Extensions.Configuration, Microsoft.Extensions.Options libraries and their integration
    /// with our KeyVault secret injecting code.
    /// </summary>
    public class MicrosoftExtensionsConfigurationIntegrationFacts
    {
        [Fact]
        public void ConfigurationRootDoesNotStoreData()
        {
            const string propertyValue = "Value";
            var configurationRoot = CreateConfigurationRoot(propertyValue, out Mock<Microsoft.Extensions.Configuration.IConfigurationProvider> mock);

            string value = propertyValue;
            mock.Verify(cfg => cfg.TryGet(nameof(TestConfiguration.Property), out value), Times.Never);

            var configuredValue = configurationRoot[nameof(TestConfiguration.Property)];
            Assert.Equal(propertyValue, configuredValue);
            mock.Verify(cfg => cfg.TryGet(nameof(TestConfiguration.Property), out value), Times.Once);

            configuredValue = configurationRoot[nameof(TestConfiguration.Property)];
            Assert.Equal(propertyValue, configuredValue);
            mock.Verify(cfg => cfg.TryGet(nameof(TestConfiguration.Property), out value), Times.Exactly(2));
        }

        [Fact]
        public void NonCachingOptionsSnapshotPreventsCaching()
        {
            const string propertyValue = "Value";
            var configurationRoot = CreateConfigurationRoot(propertyValue);

            var services = new ServiceCollection();
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.Configure<TestConfiguration>(configurationRoot);

            var serviceProvider = CreateServiceProvider(services);

            var testConfiguration = serviceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
            Assert.NotNull(testConfiguration);
            Assert.Equal(propertyValue, testConfiguration.Value.Property);
            var testConfiguration2 = serviceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
            Assert.Same(testConfiguration, testConfiguration2);

            using (var scope = serviceProvider.CreateScope())
            {
                var scopedInstance = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
                Assert.NotSame(scopedInstance, testConfiguration);
                Assert.NotNull(scopedInstance.Value);
                Assert.NotSame(scopedInstance.Value, testConfiguration.Value);
            }
        }

        /// <summary>
        /// Compare with <see cref="NonCachingOptionsSnapshotPreventsCaching"/>.
        /// 
        /// Not an actual test, just the demo, that shows that default <see cref="IOptionsSnapshot{TOptions}"/> implementation
        /// caches the value it wraps if underlying <see cref="IConfigurationProvider"/> does not report changes.
        /// 
        /// KeyVault secret injection provides its own caching (through the <see cref="NuGet.Services.KeyVault.CachingSecretReader"/>),
        /// and at the same time provider does not support change tracking (we can only do a hard retry reading from KeyVault).
        /// </summary>
        /// <remarks>
        /// Should pass if the "[Fact]" below is uncommented, but, technically, tests the library code and our implementation
        /// does not rely on the presence of the caching, instead, it enforces no caching, so nothing would break if the default
        /// implementation would stop doing it, hence no need to test it.
        /// </remarks>
        [Fact(Skip = "The default IOptionsSnapshot<TOptions> behavior demo")]
        public void RegularOptionsSnapshotCaches()
        {
            const string propertyValue = "Value";
            var configurationRoot = CreateConfigurationRoot(propertyValue);

            var services = new ServiceCollection();
            services.AddOptions(); /* This line is different, inside, it injects the default implementation of the IOptionsSnapshot{TOptions} */
            services.Configure<TestConfiguration>(configurationRoot);

            var serviceProvider = CreateServiceProvider(services);

            var testConfiguration = serviceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
            Assert.NotNull(testConfiguration);
            Assert.Equal(propertyValue, testConfiguration.Value.Property);
            var testConfiguration2 = serviceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
            Assert.Same(testConfiguration, testConfiguration2);

            using (var scope = serviceProvider.CreateScope())
            {
                var scopedInstance = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
                Assert.NotSame(scopedInstance, testConfiguration);
                Assert.NotNull(scopedInstance.Value);
                Assert.Same(scopedInstance.Value, testConfiguration.Value); /* this one is different, too */
            }
        }

        [Fact]
        public void InjectingProviderInjects()
        {
            const string keyVaultVarName = "$$KeyVaultVar$$";
            const string propertyPrefix = "Value ";
            const string propertyValue = propertyPrefix + keyVaultVarName;
            const string keyVaultVarValue = "KeyVaultVar-123";
            const string properyInjectedValue = propertyPrefix + keyVaultVarValue;

            var injectorMock = new Mock<ISecretInjector>();
            injectorMock
                .Setup(injector => injector.InjectAsync(propertyValue))
                .ReturnsAsync(properyInjectedValue);

            var configurationBuilder = new ConfigurationBuilder()
                .Add(new InjectedTestConfigurationSource(propertyValue, injectorMock.Object));
            var configurationRoot = configurationBuilder.Build();

            var services = new ServiceCollection();
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.Configure<TestConfiguration>(configurationRoot);

            var serviceProvider = CreateServiceProvider(services);

            var testConfiguration = serviceProvider.GetRequiredService<IOptionsSnapshot<TestConfiguration>>();
            Assert.Equal(properyInjectedValue, testConfiguration.Value.Property);
        }

        private static IConfigurationRoot CreateConfigurationRoot(string propertyValue, out Mock<Microsoft.Extensions.Configuration.IConfigurationProvider> configurationProviderMock)
        {
            var configurationSource = new TestConfigurationSource(propertyValue);
            var configurationBuilder = new ConfigurationBuilder().Add(configurationSource);
            var configurationRoot = configurationBuilder.Build();
            configurationProviderMock = configurationSource.LastMock;
            return configurationRoot;
        }

        private static IConfigurationRoot CreateConfigurationRoot(string propertyValue)
        {
            return CreateConfigurationRoot(propertyValue, out Mock<Microsoft.Extensions.Configuration.IConfigurationProvider> _);
        }

        private static IServiceProvider CreateServiceProvider(IServiceCollection serviceCollection)
        {
            var serviceProviderFactory = new DefaultServiceProviderFactory();
            return serviceProviderFactory.CreateServiceProvider(serviceCollection);
        }

        private static Mock<Microsoft.Extensions.Configuration.IConfigurationProvider> CreateConfigurationProviderMock(string propertyValue)
        {
            var changeTokenMock = new Mock<IChangeToken>();
            changeTokenMock.SetupGet(token => token.HasChanged).Returns(false);
            changeTokenMock.SetupGet(token => token.ActiveChangeCallbacks).Returns(false);

            var configurationProviderMock = new Mock<Microsoft.Extensions.Configuration.IConfigurationProvider>();
            configurationProviderMock
                .Setup(cfg => cfg.GetChildKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Returns<IEnumerable<string>, string>((earlierKeys, parentPath) =>
                {
                    var prefix = parentPath == null ? "" : parentPath + ConfigurationPath.KeyDelimiter;
                    return new[] { nameof(TestConfiguration.Property) }
                        .Where(s => s.StartsWith(prefix))
                        .Concat(earlierKeys);
                });

            configurationProviderMock.Setup(cfg => cfg.GetReloadToken()).Returns(changeTokenMock.Object);

            string nullValue = null;
            configurationProviderMock.Setup(cfg => cfg.TryGet(It.IsAny<string>(), out nullValue)).Returns(false);
            string value = propertyValue;
            configurationProviderMock.Setup(cfg => cfg.TryGet(nameof(TestConfiguration.Property), out value)).Returns(true).Verifiable();

            return configurationProviderMock;
        }

        private class TestConfiguration
        {
            public string Property { get; set; }
        }

        private class TestConfigurationSource : Microsoft.Extensions.Configuration.IConfigurationSource
        {
            public TestConfigurationSource(string propertyValue)
            {
                this.PropertyValue = propertyValue;
            }

            public string PropertyValue { get; }
            public Mock<Microsoft.Extensions.Configuration.IConfigurationProvider> LastMock { get; private set; }

            public virtual Microsoft.Extensions.Configuration.IConfigurationProvider Build(IConfigurationBuilder builder)
            {
                LastMock = CreateConfigurationProviderMock(PropertyValue);
                return LastMock.Object;
            }
        }

        private class InjectedTestConfigurationSource : TestConfigurationSource
        {
            private ISecretInjector _secretInjector;

            public InjectedTestConfigurationSource(string propertyValue, ISecretInjector secretInjector)
                : base(propertyValue)
            {
                this._secretInjector = secretInjector;
            }

            public override Microsoft.Extensions.Configuration.IConfigurationProvider Build(IConfigurationBuilder builder)
            {
                var baseProvider = base.Build(builder);
                return new KeyVaultInjectingConfigurationProvider(baseProvider, this._secretInjector);
            }
        }
    }
}