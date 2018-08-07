// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// Non-exception result of calling
    /// <see cref="IPackageUploadService.ValidatePackageAsync(string, PackageArchiveReader, User)"/>.
    /// </summary>
    public class PackageValidationResult
    {
        private static readonly IReadOnlyList<string> EmptyList = new string[0];

        public PackageValidationResult(PackageValidationResultType type, string message)
            : this(type, message, warnings: null)
        {
        }

        public PackageValidationResult(PackageValidationResultType type, string message, IReadOnlyList<string> warnings)
        {
            if (type != PackageValidationResultType.Accepted && message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Type = type;
            Message = message;
            Warnings = warnings ?? EmptyList;
        }

        public PackageValidationResultType Type { get; }
        public string Message { get; }
        public IReadOnlyList<string> Warnings { get; }

        public static PackageValidationResult Accepted()
        {
            return new PackageValidationResult(
                PackageValidationResultType.Accepted,
                message: null,
                warnings: null);
        }

        public static PackageValidationResult AcceptedWithWarnings(IReadOnlyList<string> warnings)
        {
            return new PackageValidationResult(
                PackageValidationResultType.Accepted,
                message: null,
                warnings: warnings);
        }

        public static PackageValidationResult Invalid(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new PackageValidationResult(
                PackageValidationResultType.Invalid,
                message,
                warnings: null);
        }
    }
}