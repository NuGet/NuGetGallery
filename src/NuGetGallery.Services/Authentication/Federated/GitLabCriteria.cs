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
    /// Represents trusted publisher policies for GitLab CI/CD. Stored in the
    /// dbo.FederatedCredentialPolicies.Criteria field and used by both the UI
    /// view model layer and backend processing.
    /// </summary>
    [DebuggerDisplay("{NamespacePath,nq}/{ProjectPath,nq}")]
    public class GitLabCriteria
    {
        public const int ValidationExpirationDays = 7;

        private string _namespacePath = string.Empty;
        private string? _namespaceId;
        private string _projectPath = string.Empty;
        private string? _projectId;
        private string? _ref;
        private string? _environment;

        /// <summary>
        /// GitLab namespace (group or user) path, e.g. "my-group".
        /// </summary>
        [JsonPropertyName("namespacePath")]
        public string NamespacePath
        {
            get => _namespacePath;
            set => _namespacePath = NormalizeRequiredValue(value);
        }

        /// <summary>
        /// GitLab namespace numeric ID. Obtained from the token on first use (TOFU).
        /// </summary>
        [JsonPropertyName("namespaceId")]
        public string? NamespaceId
        {
            get => _namespaceId;
            set => _namespaceId = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// GitLab project path (without namespace prefix), e.g. "my-project".
        /// </summary>
        [JsonPropertyName("projectPath")]
        public string ProjectPath
        {
            get => _projectPath;
            set => _projectPath = NormalizeRequiredValue(value);
        }

        /// <summary>
        /// GitLab project numeric ID. Obtained from the token on first use (TOFU).
        /// </summary>
        [JsonPropertyName("projectId")]
        public string? ProjectId
        {
            get => _projectId;
            set => _projectId = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// Optional GitLab ref (branch or tag), e.g. "main".
        /// </summary>
        [JsonPropertyName("ref")]
        public string? Ref
        {
            get => _ref;
            set => _ref = NormalizeOptionalValue(value);
        }

        /// <summary>
        /// Optional GitLab environment name, e.g. "production".
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
        /// GitLab policy is considered validated when namespace and project IDs are set.
        /// The policy can be created without these IDs, and later validated upon first use
        /// or user manually updating the policy.
        /// </remarks>
        [JsonPropertyName("validateBy")]
        public DateTimeOffset? ValidateByDate { get; set; }

        /// <summary>
        /// GitLab policy is permanently enabled when both namespace and project IDs are set.
        /// </summary>
        public bool IsPermanentlyEnabled => !string.IsNullOrEmpty(NamespaceId) && !string.IsNullOrEmpty(ProjectId);

        public int EnabledDaysLeft
        {
            get
            {
                if (IsPermanentlyEnabled)
                {
                    return int.MaxValue;
                }

                if (ValidateByDate.HasValue)
                {
                    var daysLeft = Math.Ceiling((ValidateByDate.Value - DateTimeOffset.UtcNow).TotalDays);
                    return Math.Max((int)daysLeft, 0);
                }

                return 0;
            }
        }

        private static string NormalizeRequiredValue(string? value)
            => value?.Trim() ?? string.Empty;

        private static string? NormalizeOptionalValue(string? value)
        {
            value = value?.Trim() ?? string.Empty;
            return value.Length == 0 ? null : value;
        }

        /// <summary>
        /// Validates the current configuration for required GitLab project details.
        /// </summary>
        /// <returns>A string containing validation error messages if any required details are missing;
        /// otherwise, <see langword="null"/>.</returns>
        public string? Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(NamespacePath))
            {
                errors.Add("The GitLab namespace path is required.");
            }

            if (string.IsNullOrEmpty(ProjectPath))
            {
                errors.Add("The GitLab project path is required.");
            }

            if (!IsPermanentlyEnabled && !ValidateByDate.HasValue)
            {
                errors.Add("The validate-by date is required.");
            }

            return errors.Count > 0 ? string.Join(" ", errors) : null;
        }

        /// <summary>
        /// Initializes the validation date and resets IDs if not permanently enabled.
        /// </summary>
        internal void InitializeValidateByDate()
        {
            if (IsPermanentlyEnabled)
            {
                ValidateByDate = null;
            }
            else
            {
                NamespaceId = ProjectId = string.Empty;
                DateTimeOffset date = DateTimeOffset.UtcNow + TimeSpan.FromDays(ValidationExpirationDays);
                ValidateByDate = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 0, 0, TimeSpan.Zero);
            }
        }

        public GitLabCriteria Clone()
        {
            return new GitLabCriteria
            {
                _namespacePath = _namespacePath,
                _namespaceId = _namespaceId,
                _projectPath = _projectPath,
                _projectId = _projectId,
                _ref = _ref,
                _environment = _environment,
                ValidateByDate = this.ValidateByDate,
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

        public static GitLabCriteria FromDatabaseJson(string json)
            => JsonSerializer.Deserialize<GitLabCriteria>(json) ?? throw new ArgumentException(nameof(json));
    }
}
