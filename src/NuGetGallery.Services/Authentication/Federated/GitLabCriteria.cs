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
		private string _namespacePath = string.Empty;
		private string _projectPath = string.Empty;
		private string? _ref;
		private string? _environment;

		/// <summary>
		/// GitLab namespace (group) path, e.g. "my-group" or "my-group/my-subgroup".
		/// </summary>
		[JsonPropertyName("namespacePath")]
		public string NamespacePath
		{
			get => _namespacePath;
			set => _namespacePath = value?.Trim() ?? string.Empty;
		}

		/// <summary>
		/// GitLab project path (without the namespace), e.g. "my-project".
		/// </summary>
		[JsonPropertyName("projectPath")]
		public string ProjectPath
		{
			get => _projectPath;
			set => _projectPath = value?.Trim() ?? string.Empty;
		}

		/// <summary>
		/// Optional Git ref filter, e.g. "main" or "refs/heads/main".
		/// Matched against the "ref" claim in the GitLab OIDC token.
		/// </summary>
		[JsonPropertyName("ref")]
		public string? Ref
		{
			get => _ref;
			set => _ref = NormalizeOptionalValue(value);
		}

		/// <summary>
		/// Optional GitLab environment name, e.g. "production".
		/// Matched against the "environment" claim in the GitLab OIDC token.
		/// </summary>
		[JsonPropertyName("environment")]
		public string? Environment
		{
			get => _environment;
			set => _environment = NormalizeOptionalValue(value);
		}

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

			return errors.Count > 0 ? string.Join(" ", errors) : null;
		}

		public GitLabCriteria Clone()
		{
			return new GitLabCriteria
			{
				_namespacePath = _namespacePath,
				_projectPath = _projectPath,
				_ref = _ref,
				_environment = _environment,
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
