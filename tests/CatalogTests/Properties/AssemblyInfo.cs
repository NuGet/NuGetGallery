// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

// All classes that use RegistrationMakerCatalogItem.PackagePathProvider are not thread safe and cannot be tested in parallel.
// Disabling test parallelization to prevent random unexpected failures due to race conditions.
// https://github.com/NuGet/Engineering/issues/2410
[assembly: CollectionBehavior(DisableTestParallelization = true)]
