// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using static NuGet.Frameworks.FrameworkConstants;
using static NuGet.Frameworks.FrameworkConstants.CommonFrameworks;

namespace NuGetGallery.Frameworks
{
    /// <summary>
    /// This class contains documented supported frameworks.
    /// </summary>
    /// <remarks>
    /// All these frameworks were retrieved from the following sources:
    /// dotnet documentation: https://docs.microsoft.com/en-us/dotnet/standard/frameworks.
    /// nuget documentation: https://docs.microsoft.com/en-us/nuget/reference/target-frameworks.
    /// nuget client FrameworkConstants.CommonFrameworks: https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Frameworks/FrameworkConstants.cs#L78.
    /// </remarks>
    public static class SupportedFrameworks
    {
        public static readonly NuGetFramework MonoAndroid = new NuGetFramework(FrameworkIdentifiers.MonoAndroid, EmptyVersion);
        public static readonly NuGetFramework MonoTouch = new NuGetFramework(FrameworkIdentifiers.MonoTouch, EmptyVersion);
        public static readonly NuGetFramework MonoMac = new NuGetFramework(FrameworkIdentifiers.MonoMac, EmptyVersion);
        public static readonly NuGetFramework Net48 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 8, 0, 0));
        public static readonly NuGetFramework Net50Windows = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version5, "windows");
        public static readonly NuGetFramework Net60Android = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "android");
        public static readonly NuGetFramework Net60Ios = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "ios");
        public static readonly NuGetFramework Net60MacOs = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "macos");
        public static readonly NuGetFramework Net60MacCatalyst = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "maccatalyst");
        public static readonly NuGetFramework Net60Tizen = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "tizen");
        public static readonly NuGetFramework Net60TvOs = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "tvos");
        public static readonly NuGetFramework Net60Windows = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6, "windows");
        public static readonly NuGetFramework NetCore = new NuGetFramework(FrameworkIdentifiers.NetCore, EmptyVersion);
        public static readonly NuGetFramework NetFM = new NuGetFramework(FrameworkIdentifiers.NetMicro, EmptyVersion);
        public static readonly NuGetFramework UAP = new NuGetFramework(FrameworkIdentifiers.UAP, EmptyVersion);
        public static readonly NuGetFramework Win = new NuGetFramework(FrameworkIdentifiers.Windows, EmptyVersion);
        public static readonly NuGetFramework WinRt = new NuGetFramework(FrameworkIdentifiers.WinRT, EmptyVersion);
        public static readonly NuGetFramework WP = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, EmptyVersion);
        public static readonly NuGetFramework XamarinIOs = new NuGetFramework(FrameworkIdentifiers.XamarinIOs, EmptyVersion);
        public static readonly NuGetFramework XamarinMac = new NuGetFramework(FrameworkIdentifiers.XamarinMac, EmptyVersion);
        public static readonly NuGetFramework XamarinPlaystation3 = new NuGetFramework(FrameworkIdentifiers.XamarinPlayStation3, EmptyVersion);
        public static readonly NuGetFramework XamarinPlayStation4 = new NuGetFramework(FrameworkIdentifiers.XamarinPlayStation4, EmptyVersion);
        public static readonly NuGetFramework XamarinPlayStationVita = new NuGetFramework(FrameworkIdentifiers.XamarinPlayStationVita, EmptyVersion);
        public static readonly NuGetFramework XamarinTvOs = new NuGetFramework(FrameworkIdentifiers.XamarinTVOS, EmptyVersion);
        public static readonly NuGetFramework XamarinWatchOs = new NuGetFramework(FrameworkIdentifiers.XamarinWatchOS, EmptyVersion);
        public static readonly NuGetFramework XamarinXbox360 = new NuGetFramework(FrameworkIdentifiers.XamarinXbox360, EmptyVersion);
        public static readonly NuGetFramework XamarinXboxOne = new NuGetFramework(FrameworkIdentifiers.XamarinXboxOne, EmptyVersion);

        public static readonly IReadOnlyList<NuGetFramework> AllSupportedNuGetFrameworks;

        static SupportedFrameworks()
        {
            AllSupportedNuGetFrameworks = new List<NuGetFramework>
            {
                AspNet, AspNet50, AspNetCore, AspNetCore50,
                Dnx, Dnx45, Dnx451, Dnx452, DnxCore, DnxCore50,
                DotNet, DotNet50, DotNet51, DotNet52, DotNet53, DotNet54, DotNet55, DotNet56,
                MonoAndroid, MonoMac, MonoTouch,
                Native,
                Net11, Net2, Net35, Net4, Net403, Net45, Net451, Net452, Net46, Net461, Net462, Net463, Net47, Net471, Net472, Net48,
                Net50, Net50Windows, Net60, Net60Android, Net60Ios, Net60MacCatalyst, Net60MacOs, Net60TvOs, Net60Windows,
                NetCore, NetCore45, NetCore451, NetCore50,
                NetCoreApp10, NetCoreApp11, NetCoreApp20, NetCoreApp21, NetCoreApp22, NetCoreApp30, NetCoreApp31,
                NetFM,
                NetStandard, NetStandard10, NetStandard11, NetStandard12, NetStandard13, NetStandard14, NetStandard15, NetStandard16, NetStandard17, NetStandard20, NetStandard21,
                NetStandardApp15,
                SL4, SL5,
                Tizen3, Tizen4, Tizen6,
                UAP, UAP10,
                Win, Win8, Win81, Win10,
                WinRt,
                WP, WP7, WP75, WP8, WP81, WPA81,
                XamarinIOs, XamarinMac, XamarinPlaystation3, XamarinPlayStation4, XamarinPlayStationVita, XamarinTvOs, XamarinWatchOs, XamarinXbox360, XamarinXboxOne
            };
        }
    }
}