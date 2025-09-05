// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
	/// <summary>
	/// The message to fail a validation set.
	/// </summary>
	public class FailValidationSetData
	{
		public FailValidationSetData(Guid validationTrackingId, string packageId, string packageVersion)
		{
			if (validationTrackingId == Guid.Empty)
			{
				throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
			}

			if (string.IsNullOrEmpty(packageId))
			{
				throw new ArgumentException("Package ID is required", nameof(packageId));
			}

			if (string.IsNullOrEmpty(packageVersion))
			{
				throw new ArgumentException("Package version is required", nameof(packageVersion));
			}

			ValidationTrackingId = validationTrackingId;
			PackageId = packageId;
			PackageVersion = packageVersion;
		}

		public Guid ValidationTrackingId { get; }
		public string PackageId { get; }
		public string PackageVersion { get; }
	}
}
