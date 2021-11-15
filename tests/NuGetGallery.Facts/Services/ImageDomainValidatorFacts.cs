// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class ImageDomainValidatorFacts
    {
        public class GetReadMeHtmlMethod
        {
            private readonly ImageDomainValidator _imageDomainValidator;
            private readonly Mock<IContentObjectService> _contentObjectService;

            public GetReadMeHtmlMethod()
            {
                _contentObjectService = new Mock<IContentObjectService>();
                _imageDomainValidator = new ImageDomainValidator(_contentObjectService.Object);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionForNullUrl()
            {
                Assert.Throws<ArgumentNullException>(() => _imageDomainValidator.TryPrepareImageUrlForRendering(null, out string readyUriString));
            }

            [Theory]
            [InlineData("https://api.bintray.com/example/image.svg", true, "https://api.bintray.com/example/image.svg", true)]
            [InlineData("http://api.bintray.com/example/image.svg", true, "https://api.bintray.com/example/image.svg", true)]
            [InlineData("https://www.codefactor.io/repository/github/andy840119/Synthesia.MetaDataParser/badge", true, "https://codefactor.io/repository/github/andy840119/Synthesia.MetaDataParser/badge", true)]
            [InlineData("https://travis-ci.org/Azure/azure-relay-aspnetserver.svg?branch=dev", false, null, false)]
            [InlineData("https://github.com/cedx/where.dart/actions/workflows/build.yaml/badge.svg?branch=develop", false, "https://github.com/cedx/where.dart/actions/workflows/build.yaml/badge.svg?branch=develop", true)]
            [InlineData("https://git@github.com/peaceiris/actions-gh-pages/actions/workflows/dev-image.yml/something/badge.svg", false, null, false)]
            [InlineData("https://github.com/cedx/where.dart/workflows/build.yaml/badge.svg?branch=develop", false, "https://github.com/cedx/where.dart/workflows/build.yaml/badge.svg?branch=develop", true)]
            [InlineData("https://git@github.com/peaceiris/actions-gh-pages/workflows/dev-image.yml/something/badge.svg", false, null, false)]
            public void TryPrepareImageUrlForRendering(string input, bool istrusted,  string expectedOutput, bool expectConversion)
            {
                _contentObjectService
                    .Setup(x => x.TrustedImageDomains.IsImageDomainTrusted(It.IsAny<string>()))
                    .Returns(istrusted); 
                Assert.Equal(expectConversion, _imageDomainValidator.TryPrepareImageUrlForRendering(input, out string readyUriString));
                Assert.Equal(expectedOutput, readyUriString);
            }
        }
    }
}
