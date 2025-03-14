// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Common;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class CertificateStoreUtilities
    {
        public static StoreLocation GetTrustedCertificateStoreLocation(bool readOnly = false)
        {
            // According to https://github.com/dotnet/runtime/blob/master/docs/design/features/cross-platform-cryptography.md#x509store
            // use different approaches for Windows, Mac and Linux.
            if (readOnly)
            {
                return StoreLocation.LocalMachine;
            }
            return (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsMacOSX) ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        }

        internal static StoreName GetCertificateAuthorityStoreName()
        {
            // According to https://github.com/dotnet/runtime/issues/48207#issuecomment-778293907,
            // only My, Root (RO), and Disallowed stores work on macOS.
            // So use different approaches for Windows, Mac and Linux.

            return (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsLinux) ? StoreName.CertificateAuthority : StoreName.My;
        }

        internal static StoreName GetTrustedCertificateStoreNameForLeafOrSelfIssuedCertificate()
        {
            // On MacOs, if we add the leaf or self-issued certificate into LocalMachine\Root, the private key will not be accessed.
            // So the dotnet signing command tests will fail for:
            //  "Object contains only the public half of a key pair. A private key must also be provided."
            return StoreName.My;
        }

        internal static StoreLocation GetTrustedCertificateStoreLocationForLeafOrSelfIssuedCertificate()
        {
            // According to https://github.com/dotnet/runtime/blob/master/docs/design/features/cross-platform-cryptography.md#x509store
            // use different approaches for Windows, Mac and Linux.
            return RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        }
    }
}
