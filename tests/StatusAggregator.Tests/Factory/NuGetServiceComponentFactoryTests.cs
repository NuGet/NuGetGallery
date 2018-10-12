// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Status;
using StatusAggregator.Factory;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class NuGetServiceComponentFactoryTests
    {
        public class TheCreateMethod : NuGetServiceComponentFactoryTest
        {
            /// <summary>
            /// This test guarantees the shape of the NuGet service's component does not change.
            /// </summary>
            [Fact]
            public void ContainsExpectedSchema()
            {
                AssertConstants();

                var root = Factory.Create();

                Assert.Equal(NuGetServiceComponentFactory.RootName, root.Name);
                Assert.Equal(4, root.SubComponents.Count());

                AssertGallery(root);
                AssertRestore(root);
                AssertSearch(root);
                AssertUpload(root);
            }

            private void AssertConstants()
            {
                Assert.Equal("NuGet", NuGetServiceComponentFactory.RootName);
                Assert.Equal("NuGet.org", NuGetServiceComponentFactory.GalleryName);
                Assert.Equal("Restore", NuGetServiceComponentFactory.RestoreName);
                Assert.Equal("Search", NuGetServiceComponentFactory.SearchName);
                Assert.Equal("Package Publishing", NuGetServiceComponentFactory.UploadName);

                Assert.Equal("V2 Protocol", NuGetServiceComponentFactory.V2ProtocolName);
                Assert.Equal("V3 Protocol", NuGetServiceComponentFactory.V3ProtocolName);

                Assert.Equal("Global", NuGetServiceComponentFactory.GlobalRegionName);
                Assert.Equal("China", NuGetServiceComponentFactory.ChinaRegionName);

                Assert.Equal("North Central US", NuGetServiceComponentFactory.UsncInstanceName);
                Assert.Equal("South Central US", NuGetServiceComponentFactory.UsscInstanceName);
                Assert.Equal("East Asia", NuGetServiceComponentFactory.EaInstanceName);
                Assert.Equal("Southeast Asia", NuGetServiceComponentFactory.SeaInstanceName);
            }

            private void AssertGallery(IComponent root)
            {
                var gallery = GetSubComponent(root, NuGetServiceComponentFactory.GalleryName);
                Assert.Equal(2, gallery.SubComponents.Count());

                var galleryUsnc = GetSubComponent(gallery, NuGetServiceComponentFactory.UsncInstanceName);
                Assert.Empty(galleryUsnc.SubComponents);
                var galleryUssc = GetSubComponent(gallery, NuGetServiceComponentFactory.UsscInstanceName);
                Assert.Empty(galleryUssc.SubComponents);
            }

            private void AssertRestore(IComponent root)
            {
                var restore = GetSubComponent(root, NuGetServiceComponentFactory.RestoreName);
                Assert.Equal(2, restore.SubComponents.Count());

                var restoreV2 = GetSubComponent(restore, NuGetServiceComponentFactory.V2ProtocolName);
                Assert.Equal(2, restoreV2.SubComponents.Count());
                var restoreV2Usnc = GetSubComponent(restoreV2, NuGetServiceComponentFactory.UsncInstanceName);
                Assert.Empty(restoreV2Usnc.SubComponents);
                var restoreV2Ussc = GetSubComponent(restoreV2, NuGetServiceComponentFactory.UsscInstanceName);
                Assert.Empty(restoreV2Ussc.SubComponents);

                var restoreV3 = GetSubComponent(restore, NuGetServiceComponentFactory.V3ProtocolName);
                Assert.Equal(2, restoreV3.SubComponents.Count());
                var restoreV3Global = GetSubComponent(restoreV3, NuGetServiceComponentFactory.GlobalRegionName);
                Assert.Empty(restoreV3Global.SubComponents);
                var restoreV3China = GetSubComponent(restoreV3, NuGetServiceComponentFactory.ChinaRegionName);
                Assert.Empty(restoreV3China.SubComponents);
            }

            private void AssertSearch(IComponent root)
            {
                var search = GetSubComponent(root, NuGetServiceComponentFactory.SearchName);
                Assert.Equal(2, search.SubComponents.Count());

                var searchGlobal = GetSubComponent(search, NuGetServiceComponentFactory.GlobalRegionName);
                Assert.Equal(2, searchGlobal.SubComponents.Count());
                var searchGlobalUsnc = GetSubComponent(searchGlobal, NuGetServiceComponentFactory.UsncInstanceName);
                Assert.Empty(searchGlobalUsnc.SubComponents);
                var searchGlobalUssc = GetSubComponent(searchGlobal, NuGetServiceComponentFactory.UsscInstanceName);
                Assert.Empty(searchGlobalUssc.SubComponents);

                var searchChina = GetSubComponent(search, NuGetServiceComponentFactory.ChinaRegionName);
                Assert.Equal(2, searchChina.SubComponents.Count());
                var searchChinaEA = GetSubComponent(searchChina, NuGetServiceComponentFactory.EaInstanceName);
                Assert.Empty(searchChinaEA.SubComponents);
                var searchChinaSea = GetSubComponent(searchChina, NuGetServiceComponentFactory.SeaInstanceName);
                Assert.Empty(searchChinaSea.SubComponents);
            }

            private void AssertUpload(IComponent root)
            {
                var upload = GetSubComponent(root, NuGetServiceComponentFactory.UploadName);
                Assert.Empty(upload.SubComponents);
            }

            private IComponent GetSubComponent(IComponent component, string name)
            {
                return component.GetByNames(component.Name, name);
            }
        }

        public class NuGetServiceComponentFactoryTest
        {
            public NuGetServiceComponentFactory Factory { get; }

            public NuGetServiceComponentFactoryTest()
            {
                Factory = new NuGetServiceComponentFactory();
            }
        }
    }
}