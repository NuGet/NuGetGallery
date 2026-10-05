// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery
{
    /// <summary>
    /// Delivers per-artifact promotion results through the existing email infrastructure.
    /// </summary>
    public class StagingPromotionNotificationService : IStagingPromotionNotificationService
    {
        private readonly IMessageService _messages;
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _packageUrlTemplate;
        private readonly string _managePackagesUrl;
        private readonly string _emailSettingsUrl;

        public StagingPromotionNotificationService(IMessageService messages, IMessageServiceConfiguration configuration,
            string packageUrlTemplate, string managePackagesUrl, string emailSettingsUrl)
        {
            _messages = messages ?? throw new ArgumentNullException(nameof(messages));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(packageUrlTemplate))
            {
                throw new ArgumentException("The public package URL template is required.", nameof(packageUrlTemplate));
            }

            _packageUrlTemplate = packageUrlTemplate;
            _managePackagesUrl = ValidateUrl(managePackagesUrl, nameof(managePackagesUrl));
            _emailSettingsUrl = ValidateUrl(emailSettingsUrl, nameof(emailSettingsUrl));
        }

        public Task SendAsync(User owner, StagingPromotionArtifact artifact)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (artifact == null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            return _messages.SendMessageAsync(new StagingPromotionCompletedMessage(_configuration, owner, artifact, _packageUrlTemplate, _managePackagesUrl, _emailSettingsUrl));
        }

        private static string ValidateUrl(string url, string parameterName)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("An absolute HTTP or HTTPS URL is required.", parameterName);
            }

            return parsed.AbsoluteUri;
        }
    }
}
