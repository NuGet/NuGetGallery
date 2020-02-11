// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery.Auditing;

namespace NuGet.VerifyMicrosoftPackage.Fakes
{
    public class FakeAuditingService : IAuditingService
    {
        public Task SaveAuditRecordAsync(AuditRecord record)
        {
            throw new NotImplementedException();
        }
    }
}
