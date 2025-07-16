// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class GitHubCriteria
    {
        private string _repositoryOwner = string.Empty;
        private string? _repositoryOwnerId = null;
        private string _repository = string.Empty;
        private string? _repositoryId;
        private string _workflowFile = string.Empty;
        private string? _environment;

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [JsonPropertyName("owner")]
        public string RepositoryOwner
        {
            get => _repositoryOwner;
            set => _repositoryOwner = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// GitHub repository owner id. Obtained from GitHub.
        /// </summary>
        [JsonPropertyName("ownerId")]
        public string? RepositoryOwnerId
        {
            get => _repositoryOwnerId;
            set => _repositoryOwnerId = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [JsonPropertyName("repository")]
        public string Repository
        {
            get => _repository;
            set => _repository = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// GitHub repository id. Obtained from GitHub.
        /// </summary>
        [JsonPropertyName("repositoryId")]
        public string? RepositoryId
        {
            get => _repositoryId;
            set => _repositoryId = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [JsonPropertyName("workflow")]
        public string WorkflowFile
        {
            get => _workflowFile;
            set => _workflowFile = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        [JsonPropertyName("environment")]
        public string? Environment
        {
            get => _environment;
            set => _environment = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// UTC date and time when the publisher details need to be validated by.
        /// </summary>
        /// <remarks>
        /// GitHub ppolicy is considered validated when owner and repo IDs are set.
        /// The policy can be created without these IDs, and later validated upon first use
        /// or user manually updating the policy.
        /// </remarks>
        [JsonPropertyName("validateBy")]
        public DateTimeOffset? ValidateByDate { get; set; }

        /// <summary>
        /// GitHub policy is considered validated when owner and repo IDs are set.
        /// </summary>
        public bool IsPermanentlyEnabled => !string.IsNullOrEmpty(RepositoryOwnerId) && !string.IsNullOrEmpty(RepositoryId);

        private static string? NormalizeOptionalValue(string? value)
        {
            value = value?.Trim() ?? string.Empty;
            return value.Length == 0 ? null : value;
        }

        public GitHubCriteria Clone()
        {
            return new GitHubCriteria
            {
                _repositoryOwner = _repositoryOwner,
                _repositoryOwnerId = _repositoryOwnerId,
                _repository = _repository,
                _repositoryId = _repositoryId,
                _workflowFile = _workflowFile,
                _environment = _environment,
                ValidateByDate = this.ValidateByDate
            };
        }

        public string ToDatabaseJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions() {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyProperties = true });
        }

        public static GitHubCriteria FromDatabaseJson(string json)
            => JsonSerializer.Deserialize<GitHubCriteria>(json) ?? throw new ArgumentException(nameof(json));
    }
}
