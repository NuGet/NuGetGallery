// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public interface IRegistrationPersistence
    {
        Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load();
        Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration);
    }
}
