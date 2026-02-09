// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;

namespace NuGetGallery
{
    public class PackageOwnerConfirmationModel
    {
        public PackageOwnerConfirmationModel(string packageId, string username, ConfirmOwnershipResult result)
            : this(packageId, username, result, failureMessage: null)
        {
        }

        public PackageOwnerConfirmationModel(string packageId, string username, ConfirmOwnershipResult result, string? failureMessage)
        {
            Result = result;
            PackageId = packageId;
            Username = username;
            FailureMessage = failureMessage;

            if (result != ConfirmOwnershipResult.Failure && failureMessage is not null)
            {
                throw new ArgumentException("Failure message must only be provided when the result is a failure.", nameof(failureMessage));
            }
        }

        public ConfirmOwnershipResult Result { get; }

        public string PackageId { get; }

        public string Username { get; }

        /// <summary>
        /// Set when <see cref="Result"/> is <see cref="ConfirmOwnershipResult.Failure"/>.
        /// </summary>
        public string? FailureMessage { get; }
    }
}
