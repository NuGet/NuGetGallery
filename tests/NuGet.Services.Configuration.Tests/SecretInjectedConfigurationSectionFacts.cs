// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class SecretInjectedConfigurationSectionFacts
    {
        private Mock<IConfigurationSection> _configurationMock;
        private Mock<ICachingSecretInjector> _secretInjectorMock;
        private Mock<ILogger> _loggerMock;
        private SecretInjectedConfigurationSection _target;

        [Fact]
        public void PassesThroughKey()
        {
            _configurationMock
                .SetupGet(c => c.Key)
                .Returns("TestKey");

            Assert.Equal("TestKey", _target.Key);
        }

        [Fact]
        public void PassesThroughPath()
        {
            _configurationMock
                .SetupGet(c => c.Path)
                .Returns("TestPath");

            Assert.Equal("TestPath", _target.Path);
        }

        [Fact]
        public void InjectsSecretsIntoValue()
        {
            _configurationMock
                .SetupGet(c => c.Value)
                .Returns("SomeString");
            var expectedString = "InjectedString";
            _secretInjectorMock
                .Setup(si => si.TryInjectCached("SomeString", It.IsAny<ILogger>(), out expectedString))
                .Returns(true);

            var result = _target.Value;
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
                .SetupGet(c => c.Value)
                .Returns("SomeString");
            string outValue = null;
            _secretInjectorMock
                .Setup(si => si.TryInjectCached("SomeString", out outValue))
                .Returns(false);
            _secretInjectorMock
                .Setup(si => si.Inject("SomeString", It.IsAny<ILogger>()))
                .Returns("InjectedString");

            var result = _target.Value;
            Assert.Equal("InjectedString", result);
        }

        public SecretInjectedConfigurationSectionFacts()
        {
            _configurationMock = new Mock<IConfigurationSection>();
            _secretInjectorMock = new Mock<ICachingSecretInjector>();
            _loggerMock = new Mock<ILogger>();

            _target = new SecretInjectedConfigurationSection(_configurationMock.Object, _secretInjectorMock.Object, _loggerMock.Object);
        }
    }
}
