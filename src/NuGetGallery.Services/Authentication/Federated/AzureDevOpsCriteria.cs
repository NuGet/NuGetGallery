// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

            if (string.IsNullOrEmpty(OrganizationId) && string.IsNullOrEmpty(PipelineId))
            {
                error = "The organization ID and pipeline ID are empty.";
            }
            else if (string.IsNullOrEmpty(OrganizationId))
            {
                error = "The organization ID is empty.";
            }
            else if (string.IsNullOrEmpty(PipelineId))
            {
                error = "The pipeline ID is empty.";
            }

            return string.IsNullOrEmpty(error);
        }
    }
}
