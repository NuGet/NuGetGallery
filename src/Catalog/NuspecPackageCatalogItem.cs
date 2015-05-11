// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class NuspecPackageCatalogItem : PackageCatalogItem
    {
        XDocument _nuspec;
        DateTime? _refreshed;
        IEnumerable<PackageEntry> _entries;
        long? _packageSize;
        string _packageHash;
        readonly IEnumerable<GraphAddon> _catalogSections;
        DateTime? _createdDate;
        DateTime? _lastEditedDate;
        DateTime? _publishedDate;
        string _licenseNames;
        string _licenseReportUrl;

        public NuspecPackageCatalogItem(string path)
        {
            Path = path;
            _refreshed = null;
            _entries = null;
            _packageSize = null;
            _packageHash = null;
        }

        public NuspecPackageCatalogItem(XDocument nuspec, DateTime? refreshed = null, IEnumerable<PackageEntry> entries = null, long? packageSize = null, string packageHash = null, IEnumerable<GraphAddon> catalogSections = null)
        {
            _nuspec = nuspec;
            _refreshed = refreshed;
            _entries = entries;
            _packageSize = packageSize;
            _packageHash = packageHash;
            _catalogSections = catalogSections;
        }

        public NuspecPackageCatalogItem(XDocument nuspec, DateTime? refreshed = null, IEnumerable<PackageEntry> entries = null, long? packageSize = null, string packageHash = null, IEnumerable<GraphAddon> catalogSections = null, DateTime? createdDate =null, DateTime? lastEditedDate = null, DateTime? publishedDate = null, string licenseNames = null, string licenseReportUrl = null)
        {
            _nuspec = nuspec;
            _refreshed = refreshed;
            _entries = entries;
            _packageSize = packageSize;
            _packageHash = packageHash;
            _catalogSections = catalogSections;
            _createdDate = createdDate;
            _lastEditedDate = lastEditedDate;
            _publishedDate = publishedDate;
            _licenseNames = licenseNames;
            _licenseReportUrl = licenseReportUrl;
        }

        public string Path
        {
            get;
            private set;
        }

        protected override XDocument GetNuspec()
        {
            if (_nuspec == null)
            {
                lock(this)
                {
                    if (_nuspec == null)
                    {
                        using (StreamReader reader = new StreamReader(Path))
                        {
                            return XDocument.Load(reader);
                        }
                    }
                }
            }

            return _nuspec;
        }

        protected override DateTime? GetRefreshed()
        {
            return _refreshed;
        }

        protected override DateTime? GetPublished()
        {
            return _publishedDate;
        }

        protected override DateTime? GetLastEdited()
        {
            return _lastEditedDate;
        }

        protected override string GetLicenseNames()
        {
            return _licenseNames;
        }

        protected override string GetLicenseReportUrl()
        {
            return _licenseReportUrl;
        }

        protected override DateTime? GetCreated()
        {
            return _createdDate;
        }

        protected override IEnumerable<PackageEntry> GetEntries()
        {
            return _entries;
        }

        protected override long? GetPackageSize()
        {
            return _packageSize;
        }

        protected override string GetPackageHash()
        {
            return _packageHash;
        }

        protected override IEnumerable<GraphAddon> GetAddons()
        {
            return _catalogSections ?? base.GetAddons();
        }
    }
}
