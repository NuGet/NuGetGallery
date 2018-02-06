// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class LoginDeprecationService : ILoginDeprecationService
    {
        private readonly IContentService _contentService;

        public LoginDeprecationService(IContentService contentService)
        {
            _contentService = contentService;
        }

        public async Task<bool> IsLoginDiscontinuedAsync(AuthenticatedUser authenticatedUser)
        {
            return 
                authenticatedUser.CredentialUsed.IsPassword() && 
                (await GetPasswordDiscontinuationConfigurationAsync()).IsPasswordLoginDiscontinuedForUser(authenticatedUser.User);
        }

        private async Task<PasswordLoginDiscontinuationConfiguration> GetPasswordDiscontinuationConfigurationAsync()
        {
            var configString = (await _contentService.GetContentItemAsync(Constants.ContentNames.PasswordLoginDiscontinuationConfiguration, TimeSpan.FromHours(1))).ToString();
            if (string.IsNullOrEmpty(configString))
            {
                return new PasswordLoginDiscontinuationConfiguration(Enumerable.Empty<string>(), Enumerable.Empty<string>());
            }

            return JsonConvert.DeserializeObject<PasswordLoginDiscontinuationConfiguration>(configString);
        }

        public class PasswordLoginDiscontinuationConfiguration
        {
            public IEnumerable<string> DiscontinuedForDomains { get; }
            public IEnumerable<string> ExceptionsForEmailAddresses { get; }

            [JsonConstructor]
            public PasswordLoginDiscontinuationConfiguration(
                IEnumerable<string> discontinuedForDomains, 
                IEnumerable<string> exceptionsForEmailAddresses)
            {
                DiscontinuedForDomains = discontinuedForDomains;
                ExceptionsForEmailAddresses = exceptionsForEmailAddresses;
            }

            public bool IsPasswordLoginDiscontinuedForUser(User user)
            {
                if (!user.Confirmed)
                {
                    return false;
                }

                var email = user.ToMailAddress();
                return DiscontinuedForDomains.Contains(email.Host, StringComparer.OrdinalIgnoreCase) && 
                    !ExceptionsForEmailAddresses.Contains(email.Address);
            }
        }
    }
}