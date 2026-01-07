// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Owin;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticatorFacts
    {
        public class TheNameProperty
        {
            [Fact]
            public void GivenANonMatchingTypeName_ItUsesTheFullTypeName()
            {
                Assert.Equal("AMisnamedAuthicator", new AMisnamedAuthicator().Name);
            }

            [Fact]
            public void GivenAMatchingTypeName_ItUsesTheShortenedName()
            {
                Assert.Equal("ATest", new ATestAuthenticator().Name);
            }
        }

        public class TheConstructor
        {
            [Fact]
            public void DefaultsConfigurationToDisabled()
            {
                var auther = new ATestAuthenticator();
                Assert.NotNull(auther.BaseConfig);
                Assert.False(auther.BaseConfig.Enabled);
                Assert.Null(auther.BaseConfig.AuthenticationType);
            }
        }

        public class TheStartupMethod : TestContainer
        {
            [Fact]
            public async Task LoadsConfigFromConfigurationService()
            {
                // Arrange
                var configurationService = GetConfigurationService();
                var authConfig = new AuthenticatorConfiguration();
                var auther = new ATestAuthenticator();

                // Act
                await auther.Startup(configurationService, Get<IAppBuilder>());

                // Assert
                Assert.Equal(authConfig.Enabled, auther.BaseConfig.Enabled);
                Assert.Equal(authConfig.AuthenticationType, auther.BaseConfig.AuthenticationType);
            }

            [Fact]
            public async Task DoesNotAttachToOwinAppIfDisabled()
            {
                // Arrange
                var auther = new ATestAuthenticator();

                var tempAuthConfig = new AuthenticatorConfiguration();

                var mockConfiguration = GetConfigurationService();
                mockConfiguration.Settings[$"{Authenticator.AuthPrefix}{auther.Name}.{nameof(tempAuthConfig.Enabled)}"] = "false";

                // Act
                await auther.Startup(mockConfiguration, Get<IAppBuilder>());

                // Assert
                Assert.Null(auther.AttachedTo);
            }

            [Fact]
            public async Task AttachesToOwinAppIfEnabled()
            {
                // Arrange
                var auther = new ATestAuthenticator();

                var tempAuthConfig = new AuthenticatorConfiguration();

                var mockConfiguration = GetConfigurationService();
                mockConfiguration.Settings[$"{Authenticator.AuthPrefix}{auther.Name}.{nameof(tempAuthConfig.Enabled)}"] = "true";

                // Act
                await auther.Startup(mockConfiguration, Get<IAppBuilder>());

                // Assert
                Assert.Same(Get<IAppBuilder>(), auther.AttachedTo);
            }
        }

        public class TheGetAllAvailableMethod
        {
            [Fact]
            public void IgnoresAbstractAndNonAuthenticatorTypes()
            {
                // Act
                var authenticators = Authenticator.GetAllAvailable(new[] {
                    typeof(ATestAuthenticator),
                    typeof(Authenticator),
                    typeof(TheGetAllAvailableMethod)
                }).ToArray();

                Assert.Single(authenticators);
                Assert.IsType<ATestAuthenticator>(authenticators[0]);
            }
        }

        public class TheAuthenticatorOfTCreateConfigObjectOverride
        {
            [Fact]
            public void ReturnsInstanceOfGenericParameter()
            {
                Assert.IsType<AMisnamedConfig>(new AMisnamedAuthicator().CreateConfigObject());
            }
        }

        private class ATestAuthenticator : Authenticator
        {
            public IAppBuilder AttachedTo { get; private set; }

            protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
            {
                AttachedTo = app;
                base.AttachToOwinApp(config, app);
            }
        }

        private class AMisnamedAuthicator : Authenticator<AMisnamedConfig> { }
        private class AMisnamedConfig : AuthenticatorConfiguration { }
    }
}
