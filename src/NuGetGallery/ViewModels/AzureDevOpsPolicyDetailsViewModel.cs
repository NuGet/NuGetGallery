// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// View model for Azure DevOps pipeline publisher details.
    /// </summary>
    [DebuggerDisplay("{OrganizationId,nq}/{PipelineId,nq}")]
    public sealed class AzureDevOpsPolicyDetailsViewModel : TrustedPublisherPolicyDetailsViewModel
    {
        public AzureDevOpsPolicyDetailsViewModel(AzureDevOpsCriteria criteria)
        {
            Criteria = criteria ?? new AzureDevOpsCriteria();
        }

        public override FederatedCredentialType PublisherType => FederatedCredentialType.AzureDevOps;

        public AzureDevOpsCriteria Criteria { get; }

        public string OrganizationId
        {
            get => Criteria.OrganizationId;
            set => Criteria.OrganizationId = value;
        }

        public string PipelineId
        {
            get => Criteria.PipelineId;
            set => Criteria.PipelineId = value;
        }

        // Azure DevOps policies have no trust-on-first-use activation period.
        public bool IsPermanentlyEnabled => true;

        public int EnabledDaysLeft => 1;

        public static AzureDevOpsPolicyDetailsViewModel FromViewJson(string json)
        {
            var properties = JObject.Parse(json);
            var model = new AzureDevOpsPolicyDetailsViewModel(new AzureDevOpsCriteria());

            if (properties.TryGetValue(nameof(OrganizationId), out var organizationId))
            {
                model.OrganizationId = organizationId.ToString();
            }

            if (properties.TryGetValue(nameof(PipelineId), out var pipelineId))
            {
                model.PipelineId = pipelineId.ToString();
            }

            return model;
        }

        public static AzureDevOpsPolicyDetailsViewModel FromDatabaseJson(string json)
        {
            var criteria = JsonSerializer.Deserialize<AzureDevOpsCriteria>(json);
            string error = null;
            if (criteria == null || !criteria.IsValid(out error))
            {
                throw new InvalidOperationException($"Invalid Azure DevOps policy details: {error}");
            }

            return new AzureDevOpsPolicyDetailsViewModel(criteria);
        }

        public string ToDatabaseJson() => JsonSerializer.Serialize(Criteria);
    }
}
