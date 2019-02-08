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
        private static readonly IReadOnlyList<IGalleryMessage> EmptyList = new IGalleryMessage[0];

        public PackageValidationResult(PackageValidationResultType type, IGalleryMessage message)
            : this(type, message, warnings: null)
        {
        }

        public PackageValidationResult(PackageValidationResultType type, string message)
            : this(type, new PlainTextOnlyValidationMessage(message))
        {
        }

        public PackageValidationResult(PackageValidationResultType type, IGalleryMessage message, IReadOnlyList<IGalleryMessage> warnings)
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
        public IGalleryMessage Message { get; }
        public IReadOnlyList<IGalleryMessage> Warnings { get; }

        public static PackageValidationResult Accepted()
        {
            return new PackageValidationResult(
                PackageValidationResultType.Accepted,
                message: null,
                warnings: null);
        }

        public static PackageValidationResult AcceptedWithWarnings(IReadOnlyList<IGalleryMessage> warnings)
        {
            return new PackageValidationResult(
                PackageValidationResultType.Accepted,
                message: null,
                warnings: warnings);
        }

        public static PackageValidationResult Invalid(string message)
            => Invalid(new PlainTextOnlyValidationMessage(message));

        public static PackageValidationResult Invalid(IGalleryMessage message)
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