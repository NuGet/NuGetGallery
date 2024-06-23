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

        private static bool HasAnyCertificate(User user)
        {
            return user.UserCertificates.Any() || user.UserCertificatePatterns.Any();
        }
    }
}