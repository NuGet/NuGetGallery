// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;
#nullable enable


namespace NuGetGallery
{
    /// <summary>
    /// View model for GitHub publisher details.
    /// </summary>
    /// <remarks>
    /// DO NOT change the property names. They are serialized to JSON which is stored in DB.
    /// </remarks>
    [DebuggerDisplay("{RepositoryOwner,nq}/{Repository,nq}/.github/workflows/{WorkflowFile,nq}")]
    public sealed class GitHubPolicyDetailsViewModel : TrustedPublisherPolicyDetailsViewModel
    {
        public const int ValidationExpirationDays = 7;

        private readonly GitHubCriteria _criteria;

        public GitHubPolicyDetailsViewModel(GitHubCriteria criteria)
        {
            _criteria = criteria ?? new GitHubCriteria();
        }

        public override FederatedCredentialType PublisherType => FederatedCredentialType.GitHubActions;

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [Required]
        public string RepositoryOwner
        {
            get => _criteria.RepositoryOwner;
            set => _criteria.RepositoryOwner = value;
        }

        /// <summary>
        /// GitHub repository owner id. Obtained from GitHub.
        /// </summary>
        public string? RepositoryOwnerId
        {
            get => _criteria.RepositoryOwnerId ?? string.Empty;
            set => _criteria.RepositoryOwnerId = value;
        }

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [Required]
        public string Repository
        {
            get => _criteria.Repository;
            set => _criteria.Repository = value;
        }

        /// <summary>
        /// GitHub repository id. Obtained from GitHub.
        /// </summary>
        public string? RepositoryId
        {
            get => _criteria.RepositoryId;
            set => _criteria.RepositoryId = value;
        }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [Required]
        public string WorkflowFile
        {
            get => _criteria.WorkflowFile;
            set => _criteria.WorkflowFile = value;
        }

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        public string? Environment
        {
            get => _criteria.Environment;
            set => _criteria.Environment = value;
        }

        /// <summary>
        /// UTC date and time when the publisher details need to be validated by.
        /// </summary>
        /// <remarks>
        /// GitHub ppolicy is considered validated when owner and repo IDs are set.
        /// The policy can be created without these IDs, and later validated upon first use
        /// or user manually updating the policy.
        /// </remarks>
        public DateTimeOffset? ValidateByDate
        {
            get => _criteria.ValidateByDate;
            set => _criteria.ValidateByDate = value;
        }

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

        public override string? Validate()
        {
            InitializeValidateByDate();
            return ValidateInternal();
        }

        private string? ValidateInternal()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(RepositoryOwner))
            {
                errors.Add(Strings.GitHub_OwnerRequired);
            }

            if (string.IsNullOrEmpty(Repository))
            {
                errors.Add(Strings.GitHub_RepositoryRequired);
            }

            if (string.IsNullOrEmpty(WorkflowFile))
            {
                errors.Add(Strings.GitHub_WorkflowFileRequired);
            }


            return errors.Count > 0 ? string.Join(", ", errors) : null;
        }

        internal void InitializeValidateByDate()
        {
            if (!IsPermanentlyEnabled)
            {
                // Make sure we have both or nothing, owner and repo IDs. Round date to the hour.
                RepositoryOwnerId = RepositoryId = string.Empty;
                DateTimeOffset date = DateTimeOffset.UtcNow + TimeSpan.FromDays(ValidationExpirationDays);
                ValidateByDate = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 0, 0, TimeSpan.Zero);
            }
            else
            {
                ValidateByDate = null;
            }
        }

        /// <inheritdoc />
        public override TrustedPublisherPolicyDetailsViewModel Update(string viewJson)
        {
            var model = new GitHubPolicyDetailsViewModel(_criteria.Clone());
            model.UpdateFromViewJson(viewJson);
            return model;
        }

        public static GitHubPolicyDetailsViewModel FromViewJson(string json)
        {
            var model = new GitHubPolicyDetailsViewModel(new GitHubCriteria());
            model.UpdateFromViewJson(json);
            return model;
        }

        private void UpdateFromViewJson(string json)
        {
            var properties = JObject.Parse(json);

            // MUST MATCH GitHub details serialization in page-trusted-publishing.js.
            if (properties.TryGetValue(nameof(RepositoryOwner), out var owner))
            {
                RepositoryOwner = owner.ToString();
            }
            if (properties.TryGetValue(nameof(RepositoryOwnerId), out var ownerId))
            {
                RepositoryOwnerId = ownerId.ToString();
            }

            if (properties.TryGetValue(nameof(Repository), out var repository))
            {
                Repository = repository.ToString();
            }
            if (properties.TryGetValue(nameof(RepositoryId), out var repositoryId))
            {
                RepositoryId = repositoryId.ToString();
            }

            if (properties.TryGetValue(nameof(WorkflowFile), out var workflowFile))
            {
                WorkflowFile = workflowFile.ToString();
            }
            if (properties.TryGetValue(nameof(Environment), out var environment))
            {
                Environment = environment.ToString();
            }
        }

        public override string ToDatabaseJson() => _criteria.ToDatabaseJson();

        public static GitHubPolicyDetailsViewModel FromDatabaseJson(string json)
        {
            var criteria = GitHubCriteria.FromDatabaseJson(json);
            var properties = JObject.Parse(json);
            var model = new GitHubPolicyDetailsViewModel(criteria);
            if (model.ValidateInternal() is string error)
            {
                throw new InvalidOperationException($"Invalid GitHub policy details: {error}");
            }

            return model;
        }
    }
}
