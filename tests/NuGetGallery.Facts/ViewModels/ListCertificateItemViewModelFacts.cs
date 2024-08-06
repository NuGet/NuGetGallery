// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class ListCertificateItemViewModelFacts
    {
        [Fact]
        public void Constructor_WhenCertificateIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ListCertificateItemViewModel(certificate: null, deleteUrl: "a"));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Theory]
        [InlineData("a", "b")]
        [InlineData("c", null)]
        [InlineData("d", "")]
        public void Constructor_InitializesProperties(string thumbprint, string deleteUrl)
        {
            var certificate = new Certificate() { Thumbprint = thumbprint };
            var viewModel = new ListCertificateItemViewModel(certificate, deleteUrl);

            Assert.Equal(thumbprint, viewModel.Thumbprint);
            Assert.Equal(deleteUrl, viewModel.DeleteUrl);
            Assert.Equal(!string.IsNullOrEmpty(deleteUrl), viewModel.CanDelete);
        }
    }
}