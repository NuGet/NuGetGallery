// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class TelemetryService : ITelemetryService
    {
        public void TrackPackageSignatureMayBeInvalidatedEvent(PackageSignature signature)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void TrackPackageSignatureShouldBeInvalidatedEvent(PackageSignature signature)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void TrackUnableToValidateCertificateEvent(EndCertificate certificate)
        {
            // TODO
            throw new NotImplementedException();
        }
    }
}
