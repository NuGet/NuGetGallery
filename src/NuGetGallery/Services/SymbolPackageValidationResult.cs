// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// The type of <see cref="SymbolPackageValidationResult"/>
    /// </summary>
    public enum SymbolPackageValidationResultType
    {
        /// <summary>
        /// The symbols package is valid based on the performed validations. Note that the caller may perform other validations
        /// so this is not an all inclusive validation.
        /// </summary>
        Accepted,

        /// <summary>
        /// The symbols package is invalid based on the package content.
        /// </summary>
        Invalid,

        /// <summary>
        /// The user is unauthorized to upload the symbols package.
        /// </summary>
        UserNotAllowedToUpload,

        /// <summary>
        /// There is no corresponding nuget package available for the uploaded symbols package.
        /// </summary>
        MissingPackage,

        /// <summary>
        /// There is a symbols package already present, pending validations.
        /// </summary>
        SymbolsPackagePendingValidation
    }

    public class SymbolPackageValidationResult
    {
        public SymbolPackageValidationResultType Type;
        public string Message;
        public Package Package;

        public SymbolPackageValidationResult(SymbolPackageValidationResultType type, string message)
            : this(type, message, package: null)
        {
        }

        public SymbolPackageValidationResult(SymbolPackageValidationResultType type, string message, Package package)
        {
            if (type != SymbolPackageValidationResultType.Accepted && message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Type = type;
            Message = message;
            Package = package;
        }

        public static SymbolPackageValidationResult Accepted()
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.Accepted,
                message: null);
        }

        public static SymbolPackageValidationResult AcceptedForPackage(Package package)
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.Accepted,
                message: null,
                package: package);
        }

        public static SymbolPackageValidationResult Invalid(string message)
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.Invalid,
                message: message);
        }

        public static SymbolPackageValidationResult UserNotAllowedToUpload(string message)
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.UserNotAllowedToUpload,
                message: message);
        }

        public static SymbolPackageValidationResult MissingPackage(string message)
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.MissingPackage,
                message: message);
        }

        public static SymbolPackageValidationResult SymbolsPackageExists(string message)
        {
            return new SymbolPackageValidationResult(
                SymbolPackageValidationResultType.SymbolsPackagePendingValidation,
                message: message);
        }
    }
}