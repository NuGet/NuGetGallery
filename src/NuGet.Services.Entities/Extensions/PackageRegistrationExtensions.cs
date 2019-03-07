// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Services.Entities
{
    public static class PackageRegistrationExtensions
    {
        /// <summary>
        /// Determines if package signing is allowed for the specified package registration.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <returns>A flag indicating whether package signing is allowed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        public static bool IsSigningAllowed(this PackageRegistration packageRegistration)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();

            if (requiredSigner == null)
            {
                return packageRegistration.Owners.Any(owner => HasAnyCertificate(owner));
            }

            return HasAnyCertificate(requiredSigner);
        }

        /// <summary>
        /// Determines if package signing is required for the specified package registration.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <returns>A flag indicating whether package signing is required.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        public static bool IsSigningRequired(this PackageRegistration packageRegistration)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();

            if (requiredSigner == null)
            {
                return packageRegistration.Owners.All(HasAnyCertificate);
            }

            return HasAnyCertificate(requiredSigner);
        }

        /// <summary>
        /// Determines if the certificate with specified thumbprint is valid for signing the specified package registration.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <param name="thumbprint">A certificate thumbprint.</param>
        /// <returns>A flag indicating whether the certificate is acceptable for signing.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        public static bool IsAcceptableSigningCertificate(this PackageRegistration packageRegistration, string thumbprint)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();

            if (requiredSigner == null)
            {
                return packageRegistration.Owners.Any(owner => CanUseCertificate(owner, thumbprint));
            }

            return CanUseCertificate(requiredSigner, thumbprint);
        }

        private static bool HasAnyCertificate(User user)
        {
            return user.UserCertificates.Any();
        }

        private static bool CanUseCertificate(User user, string thumbprint)
        {
            return user.UserCertificates.Any(uc => string.Equals(uc.Certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
        }
    }
}