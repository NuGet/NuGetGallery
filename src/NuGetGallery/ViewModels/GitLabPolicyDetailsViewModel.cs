// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;

#nullable enable

namespace NuGetGallery
{
	/// <summary>
	/// View model for GitLab CI/CD publisher details.
	/// </summary>
	[DebuggerDisplay("{NamespacePath,nq}/{ProjectPath,nq}")]
	public sealed class GitLabPolicyDetailsViewModel : TrustedPublisherPolicyDetailsViewModel
	{
		public GitLabPolicyDetailsViewModel(GitLabCriteria criteria)
		{
			Criteria = criteria ?? new GitLabCriteria();
		}

		public override FederatedCredentialType PublisherType => FederatedCredentialType.GitLabCI;

		public GitLabCriteria Criteria { get; }

		[Required]
		public string NamespacePath
		{
			get => Criteria.NamespacePath;
			set => Criteria.NamespacePath = value;
		}

		[Required]
		public string ProjectPath
		{
			get => Criteria.ProjectPath;
			set => Criteria.ProjectPath = value;
		}

		public string? Ref
		{
			get => Criteria.Ref;
			set => Criteria.Ref = value;
		}

		public string? Environment
		{
			get => Criteria.Environment;
			set => Criteria.Environment = value;
		}

		public static GitLabPolicyDetailsViewModel FromViewJson(string json)
		{
			var model = new GitLabPolicyDetailsViewModel(new GitLabCriteria());
			var properties = JObject.Parse(json);

			if (properties.TryGetValue(nameof(NamespacePath), out var namespacePath))
			{
				model.NamespacePath = namespacePath.ToString();
			}
			if (properties.TryGetValue(nameof(ProjectPath), out var projectPath))
			{
				model.ProjectPath = projectPath.ToString();
			}
			if (properties.TryGetValue(nameof(Ref), out var refValue))
			{
				model.Ref = refValue.ToString();
			}
			if (properties.TryGetValue(nameof(Environment), out var environment))
			{
				model.Environment = environment.ToString();
			}

			return model;
		}

		public static GitLabPolicyDetailsViewModel FromDatabaseJson(string json)
		{
			var criteria = GitLabCriteria.FromDatabaseJson(json);
			var model = new GitLabPolicyDetailsViewModel(criteria);
			if (criteria.Validate() is string error)
			{
				throw new InvalidOperationException($"Invalid GitLab policy details: {error}");
			}

			return model;
		}
	}
}
