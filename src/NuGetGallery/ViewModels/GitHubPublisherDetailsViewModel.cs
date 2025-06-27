// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// View model for GitHub publisher details.
    /// </summary>
    /// <remarks>
    /// DO NOT change the property names. They are serialized to JSON which is stored in DB.
    /// </remarks>
    [DebuggerDisplay("{WorkflowPath,nq}")]
    public sealed class GitHubPublisherDetailsViewModel : PublisherDetailsViewModel
    {
        public GitHubPublisherDetailsViewModel()
        {
        }

        protected override string NameInternal => "GitHub";

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [Required]
        public string RepositoryOwner { get; set; } = string.Empty;

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [Required]
        public string Repository { get; set; } = string.Empty;

        /// <summary>
        /// GitHub repository id. Obtained from GitHub API.
        /// </summary>
        [Required]
        public int RepositoryId { get; set; }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [Required]
        public string WorkflowFile { get; set; } = string.Empty;

        public string WorkflowPath => $"{RepositoryOwner}/{Repository}/.github/workflows/{WorkflowFile}";

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        public string Environment { get; set; } = string.Empty;

        public override string Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(RepositoryOwner))
            {
                errors.Add(Strings.GitHub_OwnerRequired);
            }

            if (string.IsNullOrWhiteSpace(Repository))
            {
                errors.Add(Strings.GitHub_RepositoryRequired);
            }

            if (RepositoryId <= 0)
            {
                errors.Add(Strings.GitHub_RepositoryIdRequired);
            }

            if (string.IsNullOrWhiteSpace(WorkflowFile))
            {
                errors.Add(Strings.GitHub_WorkflowFileRequired);
            }

            return errors.Count > 0 ? string.Join(", ", errors) : null;
        }

        public string Serialize()
            => JsonConvert.SerializeObject(this, Formatting.Indented,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });

        public static GitHubPublisherDetailsViewModel Deserialize(string json)
            => JsonConvert.DeserializeObject<GitHubPublisherDetailsViewModel>(json);
    }
}
