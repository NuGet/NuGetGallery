// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
	/// <summary>
	/// The message to fail a validation set. The validation tracking ID fully identifies the validation set to
	/// fail; the package identity, type, and key are all resolved from the stored validation set by the consumer.
	/// </summary>
	public class FailValidationSetData
	{
		public FailValidationSetData(Guid validationTrackingId)
		{
			if (validationTrackingId == Guid.Empty)
			{
				throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
			}

			ValidationTrackingId = validationTrackingId;
		}

		public Guid ValidationTrackingId { get; }
	}
}
