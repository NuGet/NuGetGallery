// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class AzurePipelinesCriteria
    {
        public const string OrganizationIdClaim = "org_id";
        public const string ProjectIdClaim = "prj_id";
        public const string DefinitionIdClaim = "def_id";
        public const string RepositoryIdClaim = "rpo_id";
        public const string RepositoryRefClaim = "rpo_ref";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private string _organizationId = string.Empty;
        private string _projectId = string.Empty;
        private string _definitionId = string.Empty;
        private string _repositoryId = string.Empty;
        private string _repositoryRef = string.Empty;

        [JsonPropertyName(OrganizationIdClaim)]
        public string OrganizationId
        {
            get => _organizationId;
            set => _organizationId = Normalize(value);
        }

        [JsonPropertyName(ProjectIdClaim)]
        public string ProjectId
        {
            get => _projectId;
            set => _projectId = Normalize(value);
        }

        [JsonPropertyName(DefinitionIdClaim)]
        public string DefinitionId
        {
            get => _definitionId;
            set => _definitionId = Normalize(value);
        }

        [JsonPropertyName(RepositoryIdClaim)]
        public string RepositoryId
        {
            get => _repositoryId;
            set => _repositoryId = Normalize(value);
        }

        [JsonPropertyName(RepositoryRefClaim)]
        public string RepositoryRef
        {
            get => _repositoryRef;
            set => _repositoryRef = Normalize(value);
        }

        public string? Validate()
        {
            var errors = new List<string>();

            AddRequiredError(errors, OrganizationId, "organization ID");
            AddRequiredError(errors, ProjectId, "project ID");
            AddRequiredError(errors, DefinitionId, "pipeline definition ID");
            AddRequiredError(errors, RepositoryId, "repository ID");
            AddRequiredError(errors, RepositoryRef, "repository ref");

            return errors.Count == 0 ? null : string.Join(" ", errors);
        }

        public string ToDatabaseJson()
        {
            return JsonSerializer.Serialize(this, SerializerOptions);
        }

        public static AzurePipelinesCriteria FromDatabaseJson(string json)
        {
            return JsonSerializer.Deserialize<AzurePipelinesCriteria>(json, SerializerOptions)
                ?? throw new ArgumentException(nameof(json));
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static void AddRequiredError(List<string> errors, string value, string displayName)
        {
            if (string.IsNullOrEmpty(value))
            {
                errors.Add($"The Azure Pipelines {displayName} is required.");
            }
        }
    }
}
