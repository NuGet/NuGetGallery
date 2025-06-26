// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;

namespace NuGetGallery
{
    public static class PublisherDetailsViewModelFactory
    {
        public static PublisherDetailsViewModel FromJson(string json)
        {
            try
            {
                var criteriaObj = JObject.Parse(json);
                var publisherName = criteriaObj["name"]?.ToString();
                if (!string.Equals(publisherName, "GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return GitHubPublisherDetailsViewModel.Deserialize(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
