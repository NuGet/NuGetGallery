// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class SecretInjectedConfigurationFacts
    {
        private SecretInjectedConfiguration _target;
        private Mock<IConfiguration> _configurationMock;
        private Mock<ICachingSecretInjector> _secretInjectorMock;
        private Mock<ILogger> _loggerMock;

        [Fact]
        public void InjectsSecretsWhenUsingIndexer()
        {
            _configurationMock
                .SetupGet(c => c[It.IsAny<string>()])
                .Returns("SomeString");
            var expectedString = "InjectedString";
            _secretInjectorMock
                .Setup(si => si.TryInjectCached("SomeString", It.IsAny<ILogger>(), out expectedString))
                .Returns(true);

            var result = _target["SomeString"];
            Assert.Equal(expectedString, result);
            _secretInjectorMock.Verify(si => si.Inject(It.IsAny<string>()), Times.Never);
            _secretInjectorMock.Verify(si => si.Inject(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
            _secretInjectorMock.Verify(si => si.InjectAsync(It.IsAny<string>()), Times.Never);
            _secretInjectorMock.Verify(si => si.InjectAsync(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
        }

        [Fact]
        public void FallsBackToInjectWhenNotCached()
        {
            _configurationMock
                .SetupGet(c => c[It.IsAny<string>()])
                .Returns("SomeString");
            string outValue = null;
            _secretInjectorMock
                .Setup(si => si.TryInjectCached("SomeString", out outValue))
                .Returns(false);
            _secretInjectorMock
                .Setup(si => si.Inject("SomeString", It.IsAny<ILogger>()))
                .Returns("InjectedString");

            var result = _target["SomeString"];
            Assert.Equal("InjectedString", result);
        }

        [Fact]
        public void ChildrenAreSecretInjectedSections()
        {
            _configurationMock
                .Setup(c => c.GetChildren())
                .Returns([Mock.Of<IConfigurationSection>(), Mock.Of<IConfigurationSection>()]);

            var children = _target.GetChildren();
            Assert.All(children, child => Assert.True(child is SecretInjectedConfigurationSection));
        }

        [Fact]
        public void GetSectionReturnsInjectedSection()
        {
            _configurationMock
                .Setup(c => c.GetSection("TestSection"))
                .Returns(Mock.Of<IConfigurationSection>());

            var section = _target.GetSection("TestSection");
            Assert.IsType<SecretInjectedConfigurationSection>(section);
        }

        [Fact]
        public void PassesThroughChangeToken()
        {
            // Technically, we actually need is that when the original configuration
            // section change token notifies about a change returned token should
            // notify as well. But the implementation just passes it through, and it is
            // easier to test that instead. In an unlikely case that this behavior
            // changes we'd need to rewrite this test with proper assumptions in mind.

            var changeToken = Mock.Of<IChangeToken>();
            _configurationMock
                .Setup(c => c.GetReloadToken())
                .Returns(changeToken);

            var result = _target.GetReloadToken();
            Assert.Same(changeToken, result);
        }

        public SecretInjectedConfigurationFacts()
        {
            _configurationMock = new Mock<IConfiguration>();
            _secretInjectorMock = new Mock<ICachingSecretInjector>();
            _loggerMock = new Mock<ILogger>();

            _target = new SecretInjectedConfiguration(_configurationMock.Object, _secretInjectorMock.Object, _loggerMock.Object);
        }
    }
}
