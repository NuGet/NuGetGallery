// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.ViewModels
{
    public class SignerViewModelFacts
    {
        [Theory]
        [InlineData("a", "b", true)]
        [InlineData(null, null, null)]
        public void Constructor_InitializesProperties(string username, string displayText, bool? hasCertificate)
        {
            var viewModel = new SignerViewModel(username, displayText, hasCertificate);

            Assert.Equal(username, viewModel.Username);
            Assert.Equal(displayText, viewModel.DisplayText);
            Assert.Equal(hasCertificate, viewModel.HasCertificate);
        }
    }
}