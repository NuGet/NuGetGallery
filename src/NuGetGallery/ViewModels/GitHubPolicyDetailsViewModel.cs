// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public const int ValidationExpirationDays = GitHubCriteria.ValidationExpirationDays;


        public GitHubPolicyDetailsViewModel(GitHubCriteria criteria)
        {
            Criteria = criteria ?? new GitHubCriteria();
        }

        public override FederatedCredentialType PublisherType => FederatedCredentialType.GitHubActions;

        public GitHubCriteria Criteria { get; }

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [Required]
        public string RepositoryOwner
        {
            get => Criteria.RepositoryOwner;
            set => Criteria.RepositoryOwner = value;
        }

        /// <summary>
        /// GitHub repository owner id. Obtained from GitHub.
        /// </summary>
        public string? RepositoryOwnerId
        {
            get => Criteria.RepositoryOwnerId ?? string.Empty;
            set => Criteria.RepositoryOwnerId = value;
        }

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [Required]
        public string Repository
        {
            get => Criteria.Repository;
            set => Criteria.Repository = value;
        }

        /// <summary>
        /// GitHub repository id. Obtained from GitHub.
        /// </summary>
        public string? RepositoryId
        {
            get => Criteria.RepositoryId;
            set => Criteria.RepositoryId = value;
        }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [Required]
        public string WorkflowFile
        {
            get => Criteria.WorkflowFile;
            set => Criteria.WorkflowFile = value;
        }

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        public string? Environment
        {
            get => Criteria.Environment;
            set => Criteria.Environment = value;
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
            get => Criteria.ValidateByDate;
            set => Criteria.ValidateByDate = value;
        }

        /// <summary>
        /// GitHub policy is considered validated when owner and repo IDs are set.
        /// </summary>
        public bool IsPermanentlyEnabled => Criteria.IsPermanentlyEnabled;

        public int EnabledDaysLeft => Criteria.EnabledDaysLeft;

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

        public static GitHubPolicyDetailsViewModel FromDatabaseJson(string json)
        {
            var criteria = GitHubCriteria.FromDatabaseJson(json);
            var properties = JObject.Parse(json);
            var model = new GitHubPolicyDetailsViewModel(criteria);
            if (criteria.Validate() is string error)
            {
                throw new InvalidOperationException($"Invalid GitHub policy details: {error}");
            }

            return model;
        }
    }
}
