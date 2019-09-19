// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class GravatarProxyServiceFacts : IDisposable
    {
        private static readonly User User = new User
        {
            Username = "Foo",
            EmailAddress = "foo@test.example",
        };

        private static readonly User UnconfirmedUser = new User
        {
            Username = "Bar",
            EmailAddress = "bar@test.example",
        };

        private const string UserGravatarUrl = "https://secure.gravatar.com/avatar/9394f8a07bb4df241de4660b315f8a90?s=100&r=g&d=retro";
        private const string UserGravatarUrlSize512 = "https://secure.gravatar.com/avatar/9394f8a07bb4df241de4660b315f8a90?s=512&r=g&d=retro";
        private const string UnconfirmedUserGravatarUrl = "https://secure.gravatar.com/avatar/9394f8a07bb4df241de4660b315f8a90?s=100&r=g&d=retro";

        private readonly HttpResponseMessage _validGravatarResponse;

        private DelegateHttpMessageHandler _messageHandler;
        private Mock<IEntityRepository<User>> _users;
        private Mock<IFeatureFlagService> _features;

        private readonly GravatarProxyService _target;

        public GravatarProxyServiceFacts()
        {
            _messageHandler = new DelegateHttpMessageHandler();
            _validGravatarResponse = new HttpResponseMessage();
            _validGravatarResponse.StatusCode = HttpStatusCode.OK;
            _validGravatarResponse.Content = new StringContent("Hello", Encoding.UTF8, "image/png");

            _features = new Mock<IFeatureFlagService>();

            var users = new List<User>
            {
                User,
                UnconfirmedUser,
            };

            _users = new Mock<IEntityRepository<User>>();
            _users
                .Setup(u => u.GetAll())
                .Returns(() => users.AsQueryable());

            var httpClient = new HttpClient(_messageHandler);

            _target = new GravatarProxyService(
                httpClient,
                _users.Object,
                _features.Object,
                Mock.Of<ILogger<GravatarProxyService>>());
        }

        [Fact]
        public async Task WhenProxyingDisabled_ReturnsNull()
        {
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(false);

            var result = await _target.GetAvatarOrNullAsync("Hello", 100);

            Assert.Null(result);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);

        }

        [Fact]
        public async Task WhenInvalidUser_ReturnsNull()
        {
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            var result = await _target.GetAvatarOrNullAsync("This is a nonexistent username", 100);

            Assert.Null(result);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);
            _users
                .Verify(u => u.GetAll(), Times.Once);
        }

        [Fact]
        public async Task ReturnsGravatarUrl()
        {
            // Arrange
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            _messageHandler.AddHandler(UserGravatarUrl, message => _validGravatarResponse);

            // Act
            var result = await _target.GetAvatarOrNullAsync(User.Username, 100);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello", ToString(result.AvatarStream));
            Assert.Equal("image/png", result.ContentType);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);
            _users
                .Verify(u => u.GetAll(), Times.Once);
        }

        [Fact]
        public async Task PrefersConfirmedEmailAddress()
        {
            // Arrange
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            User.UnconfirmedEmailAddress = "ignored@example.test";

            _messageHandler.AddHandler(UserGravatarUrl, message => _validGravatarResponse);

            // Act
            var result = await _target.GetAvatarOrNullAsync(User.Username, 100);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello", ToString(result.AvatarStream));
            Assert.Equal("image/png", result.ContentType);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);
            _users
                .Verify(u => u.GetAll(), Times.Once);
        }

        public async Task FallsbackToUnconfirmedEmailAddress()
        {
            // Arrange
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            _messageHandler.AddHandler(UnconfirmedUserGravatarUrl, message => _validGravatarResponse);

            // Act
            var result = await _target.GetAvatarOrNullAsync(UnconfirmedUser.Username, 100);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello", ToString(result.AvatarStream));
            Assert.Equal("image/png", result.ContentType);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);
            _users
                .Verify(u => u.GetAll(), Times.Once);
        }

        [Fact]
        public async Task LimitsImageSize()
        {
            // Arrange
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            _messageHandler.AddHandler(UserGravatarUrlSize512, message => _validGravatarResponse);

            // Act
            var result = await _target.GetAvatarOrNullAsync(User.Username, 1000);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello", ToString(result.AvatarStream));
            Assert.Equal("image/png", result.ContentType);

            _features
                .Verify(f => f.IsGravatarProxyEnabled(), Times.Once);
            _users
                .Verify(u => u.GetAll(), Times.Once);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task HandlesUnsuccessfulResponses(HttpStatusCode statusCode)
        {
            // Arrange
            _features
                .Setup(f => f.IsGravatarProxyEnabled())
                .Returns(true);

            _messageHandler.AddHandler(UserGravatarUrlSize512, message => new HttpResponseMessage(statusCode));

            // Act
            var result = await _target.GetAvatarOrNullAsync(User.Username, 1000);

            // Assert
            Assert.Null(result);
        }

        public void Dispose()
        {
            _validGravatarResponse.Dispose();
        }

        private string ToString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private class DelegateHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers;

            public DelegateHttpMessageHandler()
            {
                _handlers = new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>();
            }

            public void AddHandler(string uri, Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handlers.Add(uri, handler);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!_handlers.TryGetValue(request.RequestUri.ToString(), out var handler))
                {
                    throw new InvalidOperationException($"No routes registered for {request.RequestUri}");
                }

                return Task.FromResult(handler(request));
            }
        }
    }
}
