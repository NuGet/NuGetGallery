// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery;
using NuGetGallery.Services;

namespace NuGet.VerifyMicrosoftPackage.Fakes
{
    public class FakeContentObjectService : IContentObjectService
    {
        public ILoginDiscontinuationConfiguration LoginDiscontinuationConfiguration => throw new NotImplementedException();

        public ICertificatesConfiguration CertificatesConfiguration => throw new NotImplementedException();

        public ISymbolsConfiguration SymbolsConfiguration => throw new NotImplementedException();

        public ITyposquattingConfiguration TyposquattingConfiguration => throw new NotImplementedException();

        public IGitHubUsageConfiguration GitHubUsageConfiguration => throw new NotImplementedException();

        public IABTestConfiguration ABTestConfiguration => throw new NotImplementedException();

        public IODataCacheConfiguration ODataCacheConfiguration => throw new NotImplementedException();

        public ICacheConfiguration CacheConfiguration => throw new NotImplementedException();

        public IQueryHintConfiguration QueryHintConfiguration => throw new NotImplementedException();

        public Task Refresh()
        {
            throw new NotImplementedException();
        }
    }
}
