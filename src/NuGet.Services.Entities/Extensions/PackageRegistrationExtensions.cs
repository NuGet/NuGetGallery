// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            return IsAcceptableSigningCertificate(packageRegistration, thumbprint, durableIdentityValue: null);
        }

        /// <summary>
        /// Determines if a signing certificate is valid for signing the specified package registration, either
        /// because its thumbprint is registered or because its durable identity value is linked to a signing account.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <param name="thumbprint">A certificate thumbprint.</param>
        /// <param name="durableIdentityValue">The certificate's durable identity value, or <c>null</c>.</param>
        /// <returns>A flag indicating whether the certificate is acceptable for signing.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        public static bool IsAcceptableSigningCertificate(
            this PackageRegistration packageRegistration,
            string thumbprint,
            string durableIdentityValue)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            return packageRegistration
                .GetSigningAccounts()
                .Any(account => CanUseCertificate(account, thumbprint)
                    || CanUseDurableIdentityValue(account, durableIdentityValue));
        }

        /// <summary>
        /// Gets the accounts whose certificates or durable identity values may sign the specified package
        /// registration: the required signer if there is one, otherwise every owner.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <returns>The signing accounts.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        public static IReadOnlyList<User> GetSigningAccounts(this PackageRegistration packageRegistration)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();

            if (requiredSigner == null)
            {
                return packageRegistration.Owners.ToList();
            }

            return new[] { requiredSigner };
        }

        private static bool HasAnyCertificate(User user)
        {
            return user.UserCertificates.Any() || user.UserDurableIdentityValues.Any();
        }

        private static bool CanUseCertificate(User user, string thumbprint)
        {
            return user.UserCertificates.Any(uc => string.Equals(uc.Certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanUseDurableIdentityValue(User user, string durableIdentityValue)
        {
            return durableIdentityValue != null
                && user.UserDurableIdentityValues.Any(ud => string.Equals(ud.DurableIdentityValue.Value, durableIdentityValue, StringComparison.Ordinal));
        }
    }
}