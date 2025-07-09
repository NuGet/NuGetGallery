// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;

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
        private string _repositoryOwner = string.Empty;
        private string _repositoryOwnerId = string.Empty;
        private string _repository = string.Empty;
        private string _repositoryId = string.Empty;
        private string _workflowFile = string.Empty;
        private string _environment = string.Empty;

        public GitHubPolicyDetailsViewModel()
        {
        }

        public override FederatedCredentialType PublisherType => FederatedCredentialType.GitHubActions;

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
        /// GitHub repository owner id. Obtained from GitHub.
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
        /// GitHub repository id. Obtained from GitHub.
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
        public bool IsPermanentlyEnabled => !string.IsNullOrEmpty(RepositoryOwnerId) && !string.IsNullOrEmpty(RepositoryId);

        /// <summary>
        /// UTC date and time when the publisher details need to be validated by.
        /// </summary>
        /// <remarks>
        /// GitHub ppolicy is considered validated when owner and repo IDs are set.
        /// The policy can be created without these IDs, and later validated upon first use
        /// or user manually updating the policy.
        /// </remarks>
        public DateTimeOffset? ValidateByDate { get; set; }

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

                return ValidationExpirationDays;
            }
        }

        public override string Validate()
        {
            var errors = new List<string>();

            InitialieValidateByDate();

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

        internal void InitialieValidateByDate()
        {
            if (!IsPermanentlyEnabled)
            {
                // Make sure we have both or nothing, owner and repo IDs. Round date to the next hour.
                _repositoryOwnerId = _repositoryId = string.Empty;
                DateTimeOffset date = DateTimeOffset.UtcNow + TimeSpan.FromDays(ValidationExpirationDays);

                // Truncate to the current hour (zero out minutes/seconds)
                ValidateByDate = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 0, 0, TimeSpan.Zero);
            }
            else
            {
                ValidateByDate = null;
            }
        }

        public override TrustedPublisherPolicyDetailsViewModel Update(string javaScriptJson)
        {
            var model = new GitHubPolicyDetailsViewModel();
            model._repositoryOwner = _repositoryOwner;
            model._repositoryOwnerId = _repositoryOwnerId;
            model._repository = _repository;
            model._repositoryId = _repositoryId;
            model._workflowFile = _workflowFile;
            model._environment = _environment;
            model.ValidateByDate = ValidateByDate;

            model.UpdateFromJavaScript(javaScriptJson);
            return model;
        }

        public static GitHubPolicyDetailsViewModel FromJavaScriptJson(string json)
        {
            var model = new GitHubPolicyDetailsViewModel();
            model.UpdateFromJavaScript(json);
            return model;
        }

        private void UpdateFromJavaScript(string json)
        { 
            var properties = JObject.Parse(json);

            // MUST MATCH GitHub details serialization in page-trusted-publishing.js.
            if (properties.TryGetValue(nameof(RepositoryOwner), out var owner))
            {
                RepositoryOwner = owner?.ToString();
            }
            if (properties.TryGetValue(nameof(RepositoryOwnerId), out var ownerId))
            {
                RepositoryOwnerId = ownerId?.ToString();
            }
            if (properties.TryGetValue(nameof(Repository), out var repository))
            {
                Repository = repository?.ToString();
            }
            if (properties.TryGetValue(nameof(RepositoryId), out var repositoryId))
            {
                RepositoryId = repositoryId?.ToString();
            }
            if (properties.TryGetValue(nameof(WorkflowFile), out var workflowFile))
            {
                WorkflowFile = workflowFile?.ToString();
            }
            if (properties.TryGetValue(nameof(Environment), out var environment))
            {
                Environment = environment?.ToString();
            }
        }

        public override string ToDatabaseJson()
        {
            // When storing in database we want to serialize the object differently than when passing to JavaScript.
            // This gives us flexibility to change the model without breaking existing data.
            var properties = new Dictionary<string, object>
            {
                { "owner", RepositoryOwner },
                { "repository", Repository },
                { "workflow", WorkflowFile },
            };

            if (!string.IsNullOrEmpty(RepositoryOwnerId))
            {
                properties["ownerId"] = RepositoryOwnerId;
            }
            if (!string.IsNullOrEmpty(RepositoryId))
            {
                properties["repositoryId"] = RepositoryId;
            }
            if (!string.IsNullOrEmpty(Environment))
            {
                properties["environment"] = Environment;
            }
            if (ValidateByDate.HasValue)
            {
                properties["validateBy"] = properties["validateBy"] = ValidateByDate.Value.ToString("yyyy-MM-ddTHH:00:00Z"); // UTC format with hour precision
            }

            // Serialize to JSON
            string json = JsonConvert.SerializeObject(properties, Formatting.None);
            return json;
        }

        public static GitHubPolicyDetailsViewModel FromDatabaseJson(string json)
        {
            var properties = JObject.Parse(json);
            var model = new GitHubPolicyDetailsViewModel
            {
                RepositoryOwner = properties["owner"]?.ToString(),
                Repository = properties["repository"]?.ToString(),
                WorkflowFile = properties["workflow"]?.ToString(),
                RepositoryOwnerId = properties["ownerId"]?.ToString(),
                RepositoryId = properties["repositoryId"]?.ToString(),
                Environment = properties["environment"]?.ToString(),
                // ValidateByDate = DateTimeOffset.TryParseExact(properties["validateBy"]?.ToString(), "yyyy-MM-ddTHH:00:00Z", null, DateTimeStyles.AssumeUniversal, out var validateBy) ? validateBy : null,
                ValidateByDate = DateTimeOffset.TryParse(properties["validateBy"]?.ToString(), null, DateTimeStyles.AssumeUniversal, out var validateBy) ? validateBy : null,
            };

            return model;
        }
    }
}
