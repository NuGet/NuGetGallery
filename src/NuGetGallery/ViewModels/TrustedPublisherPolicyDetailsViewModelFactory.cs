// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;

namespace NuGetGallery
{
    public static class TrustedPublisherPolicyDetailsViewModelFactory
    {
        /// <summary>
        /// Creates a <see cref="TrustedPublisherPolicyDetailsViewModel"/> instance from a JSON string from database.
        /// </summary>
        /// <remarks>
        /// Same model can be serialized to JSON differently when passing data to JavaScript versus when storing it in SQL database.
        /// </remarks>
        public static TrustedPublisherPolicyDetailsViewModel FromDatabaseJson(string json)
        {
            try
            {
                var properties = JObject.Parse(json);
                var publisherName = properties["name"]?.ToString();
                if (!string.Equals(publisherName, "GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return GitHubPolicyDetailsViewModel.FromDatabaseJson(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a <see cref="TrustedPublisherPolicyDetailsViewModel"/> instance from a JSON string from database.
        /// </summary>
        /// <remarks>
        /// Same model can be serialized to JSON differently when passing data to JavaScript versus when storing it in SQL database.
        /// </remarks>
        public static TrustedPublisherPolicyDetailsViewModel FromJavaScriptJson(string json)
        {
            try
            {
                var properties = JObject.Parse(json);
                var publisherName = properties["Name"]?.ToString();
                if (!string.Equals(publisherName, "GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var model = new GitHubPolicyDetailsViewModel();
                return model.Update(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
