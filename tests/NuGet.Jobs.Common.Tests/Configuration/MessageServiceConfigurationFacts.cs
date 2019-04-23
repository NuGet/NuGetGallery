// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using NuGet.Jobs.Configuration;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGet.Jobs.Common.Tests.Configuration
{
    public class MessageServiceConfigurationFacts
    {
        public MessageServiceConfigurationFacts()
        {
            Target = new MessageServiceConfiguration();
        }

        public MessageServiceConfiguration Target { get; }

        [Fact]
        public void ParsesStringToMailAddress()
        {
            Target.GalleryOwner = "Test Person <me@example.com>";

            var casted = (IMessageServiceConfiguration)Target;
            Assert.Equal("Test Person", casted.GalleryOwner.DisplayName);
            Assert.Equal("me@example.com", casted.GalleryOwner.Address);
        }

        [Fact]
        public void ParsesMailAddressToString()
        {
            var casted = (IMessageServiceConfiguration)Target;

            casted.GalleryOwner = new MailAddress("me@example.com", "Test Person");

            Assert.Equal("Test Person <me@example.com>", Target.GalleryOwner);
        }

        [Fact]
        public void MailAddressesHaveDefaultValue()
        {
            Assert.Equal("NuGet Gallery <support@nuget.org>", Target.GalleryOwner);
            Assert.Equal("NuGet Gallery <support@nuget.org>", Target.GalleryNoReplyAddress);
        }
    }
}
