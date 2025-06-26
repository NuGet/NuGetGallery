// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Newtonsoft.Json;

namespace NuGetGallery
{
    [DebuggerDisplay("{RepositoryOwner,nq}/{Repository,nq}")]
    public sealed class GitHubPublisherDetailsViewModel : PublisherDetailsViewModel
    {
        public GitHubPublisherDetailsViewModel()
        {
        }

        [JsonIgnore]
        public override string Name => "GitHub";

        /// <summary>
        /// GitHub organization/owner name.
        /// </summary>
        [Required]
        [JsonProperty("repository_owner")]
        public string RepositoryOwner { get; set; }

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        [Required]
        [JsonProperty("repository")]
        public string Repository { get; set; }

        /// <summary>
        /// GitHub repository id. Obtained from GitHub API.
        /// </summary>
        [Required]
        [JsonProperty("repository_id")]
        public int RepositoryId { get; set; }

        /// <summary>
        /// GitHub Action workflow file name, e.g. release.yml.
        /// </summary>
        [Required]
        [JsonProperty("workflow")]
        public string WorkflowFile { get; set; }

        [JsonIgnore]
        public string WorkflowPath => $"/.github/workflows/{WorkflowFile}";

        /// <summary>
        /// GitHub Action environment name, e.g. production.
        /// </summary>
        [JsonProperty("environment")]
        public string Environment { get; set; }

        public string Serialize()
            => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static GitHubPublisherDetailsViewModel Deserialize(string json)
            => JsonConvert.DeserializeObject<GitHubPublisherDetailsViewModel>(json);
    }
}
