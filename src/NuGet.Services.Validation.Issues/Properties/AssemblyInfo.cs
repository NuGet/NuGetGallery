// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NuGet.Services.Validation.Issues")]
[assembly: AssemblyProduct("NuGet.Services.Validation.Issues")]
[assembly: ComVisible(false)]
[assembly: Guid("4f497fbd-91cf-4fa5-b948-4375bebfd2c8")]
[assembly: AssemblyDescription("User-visible issues emitted by NuGet services")]
[assembly: AssemblyCopyright("Copyright © .NET Foundation 2017")]
[assembly: AssemblyCompany(".NET Foundation")]

#if SIGNED_BUILD
[assembly: InternalsVisibleTo("NuGet.Services.Validation.Issues.Tests,PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]
#else
[assembly: InternalsVisibleTo("NuGet.Services.Validation.Issues.Tests")]
#endif
