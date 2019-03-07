// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Features
{
    /// <summary>
    /// The result of calling <see cref="FeatureFlagFileStorageService.TrySaveAsync(string, string)"/>.
    /// </summary>
    public class FeatureFlagSaveResult
    {
        private FeatureFlagSaveResult(FeatureFlagSaveResultType type, string message = null)
        {
            Type = type;
            Message = message ?? string.Empty;
        }

        public static readonly FeatureFlagSaveResult Ok = new FeatureFlagSaveResult(FeatureFlagSaveResultType.Ok);
        public static readonly FeatureFlagSaveResult Conflict = new FeatureFlagSaveResult(FeatureFlagSaveResultType.Conflict);

        public static FeatureFlagSaveResult Invalid(string message)
        {
            return new FeatureFlagSaveResult(FeatureFlagSaveResultType.Invalid, message);
        }

        /// <summary>
        /// An error code explaining whether the save operation succeeded.
        /// </summary>
        public FeatureFlagSaveResultType Type { get; }

        /// <summary>
        /// A non-null string explaining the result. Empty unless <see cref="Type"/> is <see cref="FeatureFlagSaveResultType.Invalid"/>.
        /// </summary>
        public string Message { get; }
    }
}
