// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Thrown when a package that is expected to be repository signed is missing the signature.
    /// </summary>
    public class MissingRepositorySignatureException : Exception
    {
        public MissingRepositorySignatureException(string message, MissingRepositorySignatureReason reason)
          : base(message)
        {
            Reason = reason;

            Data.Add(nameof(Reason), reason);
        }

        public MissingRepositorySignatureReason Reason { get; }
    }

    /// <summary>
    /// The reason why a <see cref="MissingRepositorySignatureException"/> was thrown.
    /// </summary>
    public enum MissingRepositorySignatureReason
    {
        /// <summary>
        /// The package isn't signed.
        /// </summary>
        Unsigned,

        /// <summary>
        /// The package is signed with an unknown signature type.
        /// </summary>
        UnknownSignature,

        /// <summary>
        /// The package is author signed but doesn't have a repository countersignature.
        /// </summary>
        AuthorSignedNoRepositoryCountersignature,
    }
}
