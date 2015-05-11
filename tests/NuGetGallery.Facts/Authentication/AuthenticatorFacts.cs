// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
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
            public void LoadsConfigFromConfigurationService()
            {
                // Arrange
                var authConfig = new AuthenticatorConfiguration();
                GetMock<ConfigurationService>()
                    .Setup(c => c.ResolveConfigObject(It.IsAny<AuthenticatorConfiguration>(), "Auth.ATest."))
                    .Returns(authConfig);
                var auther = new ATestAuthenticator();

                // Act
                auther.Startup(Get<ConfigurationService>(), Get<IAppBuilder>());

                // Assert
                Assert.Same(authConfig, auther.BaseConfig);
            }

            [Fact]
            public void DoesNotAttachToOwinAppIfDisabled()
            {
                // Arrange
                var authConfig = new AuthenticatorConfiguration()
                {
                    Enabled = false
                };
                GetMock<ConfigurationService>()
                    .Setup(c => c.ResolveConfigObject(It.IsAny<AuthenticatorConfiguration>(), "Auth.ATest."))
                    .Returns(authConfig);
                var auther = new ATestAuthenticator();

                // Act
                auther.Startup(Get<ConfigurationService>(), Get<IAppBuilder>());

                // Assert
                Assert.Null(auther.AttachedTo);
            }

            [Fact]
            public void AttachesToOwinAppIfEnabled()
            {
                // Arrange
                var authConfig = new AuthenticatorConfiguration()
                {
                    Enabled = true
                };
                GetMock<ConfigurationService>()
                    .Setup(c => c.ResolveConfigObject(It.IsAny<AuthenticatorConfiguration>(), "Auth.ATest."))
                    .Returns(authConfig);
                var auther = new ATestAuthenticator();

                // Act
                auther.Startup(Get<ConfigurationService>(), Get<IAppBuilder>());

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
                var authers = Authenticator.GetAllAvailable(new [] {
                    typeof(ATestAuthenticator),
                    typeof(Authenticator),
                    typeof(TheGetAllAvailableMethod)
                }).ToArray();

                Assert.Equal(1, authers.Length);
                Assert.IsType<ATestAuthenticator>(authers[0]);
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

        private class ATestAuthenticator : Authenticator {
            public IAppBuilder AttachedTo { get; private set; }

            protected override void AttachToOwinApp(ConfigurationService config, IAppBuilder app)
            {
                AttachedTo = app;
                base.AttachToOwinApp(config, app);
            }
        }

        private class AMisnamedAuthicator : Authenticator<AMisnamedConfig> { }
        private class AMisnamedConfig : AuthenticatorConfiguration { }
    }
}
