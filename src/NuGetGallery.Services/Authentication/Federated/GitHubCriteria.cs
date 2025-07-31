// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
#nullable enable

namespace NuGetGallery.Services.Authentication
{
    /// <summary>
    /// Represents trusted publisher policies for GitHub Actions.  Stored in the
    /// dbo.FederatedCredentialPolicies.Criteria field and used by both the UI
    /// view model layer and backend processing.
    /// </summary>
    [DebuggerDisplay("{RepositoryOwner,nq}/{Repository,nq}/.github/workflows/{WorkflowFile,nq}")]
    public class GitHubCriteria
    {
        public const int ValidationExpirationDays = 7;

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

        public int EnabledDaysLeft
        {
            get
            {
                if (IsPermanentlyEnabled)
                {
                    return int.MaxValue; // Permanently enabled, no expiration.
                }

                if (ValidateByDate.HasValue)
                {
                    var daysLeft = Math.Ceiling((ValidateByDate.Value - DateTimeOffset.UtcNow).TotalDays);
                    return Math.Max((int)daysLeft, 0); // Ensure non-negative days left.
                }

                return 0;
            }
        }

        private static string? NormalizeOptionalValue(string? value)
        {
            value = value?.Trim() ?? string.Empty;
            return value.Length == 0 ? null : value;
        }

        /// <summary>
        /// Validates the current configuration for required GitHub repository details.
        /// </summary>
        /// <remarks>This method checks for the presence of essential GitHub repository information,
        /// including the repository owner, repository name, and workflow file. If any of these are missing, it returns
        /// a list of error messages.</remarks>
        /// <returns>A string containing a comma-separated list of validation error messages if any required details are missing;
        /// otherwise, <see langword="null"/>.</returns>
        public string? Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(RepositoryOwner))
            {
                errors.Add("The GitHub repository owner is required.");
            }

            if (string.IsNullOrEmpty(Repository))
            {
                errors.Add("The GitHub repository is required.");
            }

            if (string.IsNullOrEmpty(WorkflowFile))
            {
                errors.Add("The GitHub Action workflow file is required.");
            }

            if (!IsPermanentlyEnabled && !ValidateByDate.HasValue)
            {
                errors.Add("The validate-by date is required.");
            }

            return errors.Count > 0 ? string.Join(" ", errors) : null;
        }

        /// <summary>
        /// Initializes the validation date and returns <see langword="true"/> if its value has changed.
        /// </summary>
        internal void InitializeValidateByDate()
        {
            if (IsPermanentlyEnabled)
            {
                // Reset the validation date if the policy is permanently enabled.
                var changed = ValidateByDate.HasValue;
                ValidateByDate = null;
            }
            else
            {
                // Ensure consistent temporary state, i.e. IDs are empty and validation date is set to 7 days from now..
                RepositoryOwnerId = RepositoryId = string.Empty;
                DateTimeOffset date = DateTimeOffset.UtcNow + TimeSpan.FromDays(ValidationExpirationDays);
                ValidateByDate = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 0, 0, TimeSpan.Zero);
            }
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
            return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyProperties = true
            });
        }

        public static GitHubCriteria FromDatabaseJson(string json)
            => JsonSerializer.Deserialize<GitHubCriteria>(json) ?? throw new ArgumentException(nameof(json));
    }
}
