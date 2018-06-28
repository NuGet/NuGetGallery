// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Data
{
    public static class Registrations
    {
        public static MemoryStorage CreateTestRegistrations()
        {
            var registrationStorage = new MemoryStorage(new Uri("https://api.nuget.org/container1/"));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "cursor.json"),
                new StringStorageContent(TestRegistrationEntries.CursorJson));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "businessframework/index.json"),
                new StringStorageContent(TestRegistrationEntries.BusinessFrameworkIndexJson));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "businessframework/0.2.0.json"),
                new StringStorageContent(TestRegistrationEntries.BusinessFrameworkVersion1));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "automapper/index.json"),
                new StringStorageContent(TestRegistrationEntries.AutomapperIndexJson));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "automapper/1.1.0.118.json"),
                new StringStorageContent(TestRegistrationEntries.AutomapperVersion1));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "antlr/index.json"),
                new StringStorageContent(TestRegistrationEntries.AntlrIndexJson));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "antlr/3.1.1.json"),
                new StringStorageContent(TestRegistrationEntries.AntlrVersion1));

            registrationStorage.Content.TryAdd(
                new Uri(registrationStorage.BaseAddress, "antlr/3.1.3.42154.json"),
                new StringStorageContent(TestRegistrationEntries.AntlrVersion2));

            return registrationStorage;
        }
    }
}