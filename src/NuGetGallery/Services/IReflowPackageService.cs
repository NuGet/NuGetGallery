// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
	public interface IReflowPackageService
	{
		/// <summary>
		/// Reflows the metadata for a package from its binary.
		/// </summary>
		/// <param name="id">The package ID.</param>
		/// <param name="version">The package version.</param>
		/// <param name="reason">An optional reason for the reflow, recorded in the audit log.</param>
		/// <returns>The reflowed package, or null if not found.</returns>
		Task<Package> ReflowAsync(string id, string version, string reason = null, string callerIdentity = null);
	}
}
