// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Non-exception result of calling
    /// <see cref="IPackageUploadService.ValidatePackageAsync(string, PackageArchiveReader, User)"/>.
    /// </summary>
    public class PackageValidationResult
    {
        public PackageValidationResult(PackageValidationResultType type, string message)
        {
            if (type != PackageValidationResultType.Accepted && message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Type = type;
            Message = message;
        }

        public PackageValidationResultType Type { get; }
        public string Message { get; }

        public static PackageValidationResult Accepted()
        {
            return new PackageValidationResult(PackageValidationResultType.Accepted, message: null);
        }

        public static PackageValidationResult Invalid(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new PackageValidationResult(PackageValidationResultType.Invalid, message);
        }
    }
}