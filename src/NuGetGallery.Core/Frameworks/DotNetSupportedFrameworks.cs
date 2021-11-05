// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using static NuGet.Frameworks.FrameworkConstants;

namespace NuGetGallery.Frameworks
{
    /// <summary>
    /// This class contains documented supported frameworks.
    /// </summary>
    /// <remarks>
    /// All these frameworks were retrieved from dotnet documentation: https://docs.microsoft.com/en-us/dotnet/standard/frameworks#supported-target-frameworks.
    /// </remarks>
    public class DotNetSupportedFrameworks
    {
        public static readonly NuGetFramework Net50 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(5, 0, 0, 0));
        public static readonly NuGetFramework Net60 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(6, 0, 0, 0));

        public static readonly NuGetFramework NetCoreApp10 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(1, 0, 0, 0));
        public static readonly NuGetFramework NetCoreApp11 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(1, 1, 0, 0));
        public static readonly NuGetFramework NetCoreApp20 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 0, 0, 0));
        public static readonly NuGetFramework NetCoreApp21 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 1, 0, 0));
        public static readonly NuGetFramework NetCoreApp22 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 2, 0, 0));
        public static readonly NuGetFramework NetCoreApp30 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(3, 0, 0, 0));
        public static readonly NuGetFramework NetCoreApp31 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(3, 1, 0, 0));

        public static readonly NuGetFramework NetStandard10 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 0, 0, 0));
        public static readonly NuGetFramework NetStandard11 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 1, 0, 0));
        public static readonly NuGetFramework NetStandard12 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 2, 0, 0));
        public static readonly NuGetFramework NetStandard13 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 3, 0, 0));
        public static readonly NuGetFramework NetStandard14 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 4, 0, 0));
        public static readonly NuGetFramework NetStandard15 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 5, 0, 0));
        public static readonly NuGetFramework NetStandard16 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 6, 0, 0));
        public static readonly NuGetFramework NetStandard20 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(2, 0, 0, 0));
        public static readonly NuGetFramework NetStandard21 = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(2, 1, 0, 0));

        public static readonly NuGetFramework Net11 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(1, 1, 0, 0));
        public static readonly NuGetFramework Net2 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(2, 0, 0, 0));
        public static readonly NuGetFramework Net35 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(3, 5, 0, 0));
        public static readonly NuGetFramework Net4 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 0, 0, 0));
        public static readonly NuGetFramework Net403 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 0, 3, 0));
        public static readonly NuGetFramework Net45 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 0, 0));
        public static readonly NuGetFramework Net451 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 1, 0));
        public static readonly NuGetFramework Net452 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 2, 0));
        public static readonly NuGetFramework Net46 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 0, 0));
        public static readonly NuGetFramework Net461 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 1, 0));
        public static readonly NuGetFramework Net462 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 2, 0));
        public static readonly NuGetFramework Net47 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 0, 0));
        public static readonly NuGetFramework Net471 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 1, 0));
        public static readonly NuGetFramework Net472 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 2, 0));
        public static readonly NuGetFramework Net48 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 8, 0, 0));

        public static readonly NuGetFramework NetCore = new NuGetFramework(FrameworkIdentifiers.NetCore, EmptyVersion);
        public static readonly NuGetFramework NetCore45 = new NuGetFramework(FrameworkIdentifiers.NetCore, new Version(4, 5, 0, 0));
        public static readonly NuGetFramework NetCore451 = new NuGetFramework(FrameworkIdentifiers.NetCore, new Version(4, 5, 1, 0));

        public static readonly NuGetFramework NetFM = new NuGetFramework(FrameworkIdentifiers.NetMicro, EmptyVersion);

        public static readonly NuGetFramework SL4 = new NuGetFramework(FrameworkIdentifiers.Silverlight, new Version(4, 0, 0, 0));
        public static readonly NuGetFramework SL5 = new NuGetFramework(FrameworkIdentifiers.Silverlight, new Version(5, 0, 0, 0));

        public static readonly NuGetFramework WP = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, EmptyVersion);
        public static readonly NuGetFramework WP7 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(7, 0, 0, 0));
        public static readonly NuGetFramework WP75 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(7, 5, 0, 0));
        public static readonly NuGetFramework WP8 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(8, 0, 0, 0));
        public static readonly NuGetFramework WP81 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(8, 1, 0, 0));
        public static readonly NuGetFramework WPA81 = new NuGetFramework(FrameworkIdentifiers.WindowsPhoneApp, new Version(8, 1, 0, 0));

        public static readonly NuGetFramework UAP = new NuGetFramework(FrameworkIdentifiers.UAP, EmptyVersion);
        public static readonly NuGetFramework UAP10 = new NuGetFramework(FrameworkIdentifiers.UAP, Version10);

        public static IReadOnlyList<NuGetFramework> GetSupportedFrameworks()
        {
            return new List<NuGetFramework>
            {
                Net50,
                Net60,
                NetCoreApp10,
                NetCoreApp11,
                NetCoreApp20,
                NetCoreApp21,
                NetCoreApp22,
                NetCoreApp30,
                NetCoreApp31,
                NetStandard10,
                NetStandard11,
                NetStandard12,
                NetStandard13,
                NetStandard14,
                NetStandard15,
                NetStandard16,
                NetStandard20,
                NetStandard21,
                Net11,
                Net2,
                Net35,
                Net4,
                Net403,
                Net45,
                Net451,
                Net452,
                Net46,
                Net461,
                Net462,
                Net47,
                Net471,
                Net472,
                Net48,
                NetCore,
                NetCore45,
                NetCore451,
                NetFM,
                SL4,
                SL5,
                WP,
                WP7,
                WP75,
                WP8,
                WP81,
                WPA81,
                UAP,
                UAP10
            };
        }
    }
}