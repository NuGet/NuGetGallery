// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;

namespace StatusAggregator.Factory
{
    /// <summary>
    /// Helps create an <see cref="IComponent"/> that represents the NuGet service as well as paths to its subcomponents.
    /// </summary>
    public class NuGetServiceComponentFactory : IComponentFactory
    {
        public const string RootName = "NuGet";
        public const string GalleryName = "NuGet.org";
        public const string RestoreName = "Restore";
        public const string SearchName = "Search";
        public const string UploadName = "Package Publishing";

        public const string V2ProtocolName = "V2 Protocol";
        public const string V3ProtocolName = "V3 Protocol";

        public const string GlobalRegionName = "Global";
        public const string ChinaRegionName = "China";

        public const string UsncInstanceName = "North Central US";
        public const string UsscInstanceName = "South Central US";
        public const string EaInstanceName = "East Asia";
        public const string SeaInstanceName = "Southeast Asia";
        
        public IComponent Create()
        {
            return new TreeComponent(
                RootName,
                "",
                new IComponent[]
                {
                    new ActivePassiveComponent(
                        GalleryName,
                        "Browsing the Gallery website",
                        new[]
                        {
                            new LeafComponent(UsncInstanceName, "Primary region"),
                            new LeafComponent(UsscInstanceName, "Backup region")
                        }),
                    new TreeComponent(
                        RestoreName,
                        "Downloading and installing packages from NuGet",
                        new IComponent[]
                        {
                            new TreeComponent(
                                V3ProtocolName,
                                "Restore using the V3 API",
                                new[]
                                {
                                    new LeafComponent(GlobalRegionName, "V3 restore for users outside of China"),
                                    new LeafComponent(ChinaRegionName, "V3 restore for users inside China")
                                }),
                            new ActiveActiveComponent(
                                V2ProtocolName,
                                "Restore using the V2 API",
                                new[]
                                {
                                    new LeafComponent(UsncInstanceName, "Primary region"),
                                    new LeafComponent(UsscInstanceName, "Backup region")
                                })
                        }),
                    new TreeComponent(
                        SearchName,
                        "Searching for new and existing packages in Visual Studio or the Gallery website",
                        new[]
                        {
                            new ActiveActiveComponent(
                                GlobalRegionName,
                                "Search for packages outside China",
                                new[]
                                {
                                    new LeafComponent(UsncInstanceName, "Primary region"),
                                    new LeafComponent(UsscInstanceName, "Backup region")
                                }),
                            new ActiveActiveComponent(
                                ChinaRegionName,
                                "Search for packages inside China",
                                new[]
                                {
                                    new LeafComponent(EaInstanceName, "Primary region"),
                                    new LeafComponent(SeaInstanceName, "Backup region")
                                })
                        }),
                    new LeafComponent(UploadName, "Uploading new packages to NuGet.org")
                });
        }
    }
}
