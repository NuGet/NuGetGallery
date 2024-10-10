﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class EntraIdServicePrincipalCriteria
    {
        [JsonConstructor]
        public EntraIdServicePrincipalCriteria(Guid tenantId, Guid objectId)
        {
            TenantId = tenantId == Guid.Empty ? throw new ArgumentNullException(nameof(tenantId)) : tenantId;
            ObjectId = objectId == Guid.Empty ? throw new ArgumentNullException(nameof(objectId)) : objectId;
        }

        [JsonPropertyName("tid")]
        public Guid TenantId { get; set; }

        [JsonPropertyName("oid")]
        public Guid ObjectId { get; set; }
    }
}
