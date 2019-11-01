// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class GravatarProxyService : IGravatarProxyService
    {
        private const string HttpContentTypeHeaderName = "Content-Type";
        private const string HttpContentTypeDefaultValue = "application/octet-stream";

        private const string GravatarHttpClientName = "gravatar";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEntityRepository<User> _users;
        private readonly IFeatureFlagService _features;
        private readonly ILogger<GravatarProxyService> _logger;

        public GravatarProxyService(
            IHttpClientFactory httpClientFactory,
            IEntityRepository<User> users,
            IFeatureFlagService features,
            ILogger<GravatarProxyService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _users = users ?? throw new ArgumentNullException(nameof(users));
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GravatarProxyResult> GetAvatarOrNullAsync(string username, int imageSize)
        {
            if (!_features.IsGravatarProxyEnabled())
            {
                return null;
            }

            var user = _users.GetAll().FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                _logger.LogWarning("Could not find an account with username {Username}", username);
                return null;
            }

            try
            {
                var emailAddress = user.EmailAddress ?? user.UnconfirmedEmailAddress;
                var useEnSubdomain = _features.ProxyGravatarEnSubdomain();

                var url = GravatarHelper.RawUrl(emailAddress, imageSize, useEnSubdomain);

                // The response will be disposed when the caller disposes the content stream.
                var client = _httpClientFactory.CreateClient(GravatarHttpClientName);
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                string contentType = null;
                if (response.Content.Headers.TryGetValues(HttpContentTypeHeaderName, out var contentTypes))
                {
                    contentType = contentTypes.FirstOrDefault();
                }

                return new GravatarProxyResult(
                    await response.Content.ReadAsStreamAsync(),
                    contentType ?? HttpContentTypeDefaultValue);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Unable to fetch profile picture for user {Username}", username);
                return null;
            }
        }
    }
}
