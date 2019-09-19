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
        private readonly HttpClient _httpClient;
        private readonly IEntityRepository<User> _users;
        private readonly IFeatureFlagService _features;
        private readonly ILogger<GravatarProxyService> _logger;

        public GravatarProxyService(
            HttpClient httpClient,
            IEntityRepository<User> users,
            IFeatureFlagService features,
            ILogger<GravatarProxyService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
                var url = GravatarHelper.Url(user.EmailAddress ?? user.UnconfirmedEmailAddress, imageSize);

                // The response will be disposed when the caller disposes the content stream.
                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType.MediaType;
                var contentStream = await response.Content.ReadAsStreamAsync();

                return new GravatarProxyResult(contentStream, contentType);
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Unable to fetch profile picture for user {Username}", username);
                return null;
            }
        }
    }
}
