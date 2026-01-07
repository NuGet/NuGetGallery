// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The message to start a new validation.
    /// </summary>
    public class StartValidationData
    {
        public StartValidationData(
            Guid validationTrackingId,
            string contentType,
            Uri contentUrl,
            JObject properties)
        {
            if (validationTrackingId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
            }

            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("The content type property is required", nameof(contentType));
            }

            ValidationTrackingId = validationTrackingId;
            ContentType = contentType;
            ContentUrl = contentUrl ?? throw new ArgumentNullException(nameof(contentUrl));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public Guid ValidationTrackingId { get; set; }
        public string ContentType { get; set; }
        public Uri ContentUrl { get; set; }
        public JObject Properties { get; set; }
    }
}
