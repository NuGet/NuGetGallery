// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class TrustedTestCert
    {
        public static TrustedTestCert<X509Certificate2> Create(
            X509Certificate2 cert,
            X509StorePurpose storePurpose,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
        {
            return new TrustedTestCert<X509Certificate2>(
                cert,
                x => x,
                storePurpose,
                storeName,
                storeLocation,
                maximumValidityPeriod);
        }

        public static TrustedTestCert<X509Certificate2> Create(
            X509Certificate2 cert,
            X509StorePurpose[] storePurposes,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
        {
            return new TrustedTestCert<X509Certificate2>(
                cert,
                x => x,
                storePurposes,
                storeName,
                storeLocation,
                maximumValidityPeriod);
        }
    }

    /// <summary>
    /// Give a certificate full trust for the life of the object.
    /// </summary>
    public class TrustedTestCert<T> : IDisposable
    {
        public X509Certificate2 TrustedCert { get; }

        public T Source { get; }

        public StoreName StoreName { get; }

        public StoreLocation StoreLocation { get; }

        private readonly X509StorePurpose[] _storePurposes;
        private bool _isDisposed;

        [Obsolete("Use the constructor that takes an X.509 store purpose.")]
        public TrustedTestCert(
            T source,
            Func<T, X509Certificate2> getCert,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
            : this(source, getCert, X509StorePurpose.CodeSigning, storeName, storeLocation, maximumValidityPeriod)
        {
        }

        public TrustedTestCert(
            T source,
            Func<T, X509Certificate2> getCert,
            X509StorePurpose storePurpose,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
            : this(source, getCert, new X509StorePurpose[] { storePurpose }, storeName, storeLocation, maximumValidityPeriod)
        {
        }

        public TrustedTestCert(
            T source,
            Func<T, X509Certificate2> getCert,
            X509StorePurpose[] storePurposes,
            StoreName storeName = StoreName.TrustedPeople,
            StoreLocation storeLocation = StoreLocation.CurrentUser,
            TimeSpan? maximumValidityPeriod = null)
        {
            Source = source;
            TrustedCert = getCert(source);

            if (storePurposes is null || storePurposes.Length == 0)
            {
                throw new ArgumentException("Invalid store purpose", nameof(storePurposes));
            }

            _storePurposes = storePurposes;

            if (!maximumValidityPeriod.HasValue)
            {
                maximumValidityPeriod = TimeSpan.FromHours(2);
            }

            if (TrustedCert.NotAfter - TrustedCert.NotBefore > maximumValidityPeriod.Value)
            {
                throw new InvalidOperationException($"The certificate used is valid for more than {maximumValidityPeriod}.");
            }

            StoreName = storeName;
            StoreLocation = storeLocation;

            foreach (X509StorePurpose storePurpose in _storePurposes)
            {
                X509StoreUtilities.AddCertificateToStore(StoreLocation, StoreName, TrustedCert, storePurpose);
            }

            PublishCrl();
        }

        private void PublishCrl()
        {
            if (Source is TestCertificate testCertificate)
            {
                testCertificate.Crl?.Publish();
            }
        }

        private void DisposeCrl()
        {
            if (Source is TestCertificate testCertificate)
            {
                testCertificate.Crl?.Dispose();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                foreach (X509StorePurpose storePurpose in _storePurposes)
                {
                    X509StoreUtilities.RemoveCertificateFromStore(StoreLocation, StoreName, TrustedCert, storePurpose);
                }

                DisposeCrl();

                TrustedCert.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
