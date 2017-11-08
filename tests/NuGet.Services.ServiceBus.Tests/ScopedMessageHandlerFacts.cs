// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace NuGet.Services.ServiceBus.Tests
{
    public class ScopedMessageHandlerFacts
    {
        [Fact]
        public async Task CreatesMessageHandlerUsingScope()
        {
            // Arrange
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var handlerMock = new Mock<IMessageHandler<object>>();

            scopeFactoryMock
                .Setup(f => f.CreateScope())
                .Returns(scopeMock.Object);

            scopeMock
                .SetupGet(s => s.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            serviceProviderMock
                .Setup(p => p.GetService(typeof(IMessageHandler<object>)))
                .Returns(handlerMock.Object);

            var target = new ScopedMessageHandler<object>(scopeFactoryMock.Object);

            // Act - send two empty messages.
            await target.HandleAsync(new object());
            await target.HandleAsync(new object());

            // Assert
            scopeFactoryMock.Verify(f => f.CreateScope(), Times.Exactly(2));
            serviceProviderMock.Verify(p => p.GetService(typeof(IMessageHandler<object>)), Times.Exactly(2));
            handlerMock.Verify(h => h.HandleAsync(It.IsAny<object>()), Times.Exactly(2));
        }
    }
}
