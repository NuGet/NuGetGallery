// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using System;

namespace NuGetGallery
{
    /// <summary>
    /// View model for GitHub publisher details.
    /// </summary>
    /// <remarks>
    /// DO NOT change the property names. They are serialized to JSON which is stored in DB.
    /// </remarks>
    [DebuggerDisplay("{RepositoryOwner,nq}/{Repository,nq}/.github/workflows/{WorkflowFile,nq}")]
    public sealed class GitHubPublisherDetailsViewModel : PublisherDetailsViewModel
    {
        public const int ValidationExpirationDays = 7;
        private string _repositoryOwner = string.Empty;
        private string _repositoryOwnerId = string.Empty;
        private string _repository = string.Empty;
        private string _repositoryId = string.Empty;
        private string _workflowFile = string.Empty;
        private string _environment = string.Empty;

        public GitHubPublisherDetailsViewModel()
        {
        }

        public override string Name => "GitHub";

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [Required]
        public string RepositoryOwner 
        { 
            get => _repositoryOwner; 
            set => _repositoryOwner = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub repository owner id. Obtained from GitHub API.
        /// </summary>
        public string RepositoryOwnerId 
        { 
            get => _repositoryOwnerId; 
            set => _repositoryOwnerId = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [Required]
        public string Repository 
        { 
            get => _repository; 
            set => _repository = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub repository id. Obtained from GitHub API.
        /// </summary>
        public string RepositoryId 
        { 
            get => _repositoryId; 
            set => _repositoryId = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [Required]
        public string WorkflowFile 
        { 
            get => _workflowFile; 
            set => _workflowFile = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        public string Environment 
        { 
            get => _environment; 
            set => _environment = value?.Trim() ?? string.Empty; 
        }

        /// <summary>
        /// GitHub policy is considered validated when owner and repo IDs are set.
        /// </summary>
        public bool IsGitHubIdsAvailable => !string.IsNullOrEmpty(RepositoryOwnerId) && !string.IsNullOrEmpty(RepositoryId);

        /// <summary>
        /// UTC date and time when the publisher details need to be validated by.
        /// </summary>
        /// <remarks>
        /// GitHub ppolicy is considered validated when owner and repo IDs are set.
        /// The policy can be created without these IDs, and later validated upon first use
        /// or user manually updating the policy.
        /// </remarks>
        public DateTime? ValidateByDate { get; set; }

        public override string Validate()
        {
            var errors = new List<string>();

            if (!this.IsGitHubIdsAvailable)
            {
                // Make sure we have both or nothing, owner and repo IDs.
                RepositoryOwnerId = RepositoryId = string.Empty;

                // This method is called for create and update operations only. In both cases we need to reset the validation date.
                DateTime utcNow = DateTime.UtcNow + TimeSpan.FromDays(ValidationExpirationDays);
                this.ValidateByDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                this.ValidateByDate = null;
            }
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

        public override string ToJson()
            => JsonConvert.SerializeObject(this, Formatting.Indented,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });

        public static GitHubPublisherDetailsViewModel FromJson(string json)
        {
            var model = JsonConvert.DeserializeObject<GitHubPublisherDetailsViewModel>(json);
            model.Validate();
            return model;
        }
    }
}
