// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace NuGetGallery.Services.Authentication
{
    public class AzureDevOpsCriteria
    {
        private string _organizationId = string.Empty;
        private string _pipelineId = string.Empty;

        [JsonPropertyName("organizationId")]
        public string OrganizationId
        {
            get => _organizationId;
            set => _organizationId = value?.Trim() ?? string.Empty;
        }

        [JsonPropertyName("pipelineId")]
        public string PipelineId
        {
            get => _pipelineId;
            set => _pipelineId = value?.Trim() ?? string.Empty;
        }

        public bool IsValid(out string error)
        {
            error = "";

            var isValidOrganizationId = Guid.TryParse(OrganizationId, out _);
            var isValidPipelineId = int.TryParse(PipelineId, out _);
            if (!isValidOrganizationId && !isValidPipelineId)
            {
                error = "The organization ID and pipeline ID are invalid.";
            }
            else if (!isValidOrganizationId)
            {
                error = "The organization ID is invalid.";
            }
            else if (!isValidPipelineId)
            {
                error = "The pipeline ID is invalid.";
            }

            return string.IsNullOrEmpty(error);
        }
    }
}
