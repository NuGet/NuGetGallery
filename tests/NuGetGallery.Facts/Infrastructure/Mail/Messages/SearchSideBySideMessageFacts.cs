// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Mail;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SearchSideBySideMessageFacts
    {
        public class Sender : Facts
        {
            [Fact]
            public void IsGalleryNoReplyAddress()
            {
                var message = CreateMessage();

                Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
            }
        }

        public class GetRecipients : Facts
        {
            [Fact]
            public void RecipientIsGalleryOwner()
            {
                var message = CreateMessage();

                var recipients = message.GetRecipients();

                Assert.Equal(new[] { Configuration.GalleryOwner }, recipients.To.ToArray());
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            [InlineData("don't contact me")]
            public void ReplyToIsNullWithInvalidEmailAddress(string emailAddress)
            {
                Model.EmailAddress = emailAddress;
                var message = CreateMessage();

                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
            }

            [Theory]
            [InlineData("me@example.com", "me@example.com")]
            [InlineData("   me@example.com  ", "me@example.com")]
            public void ReplyToIsEmailAddressWithValidEmailAddress(string emailAddress, string expected)
            {
                Model.EmailAddress = emailAddress;
                var message = CreateMessage();

                var recipients = message.GetRecipients();

                Assert.Equal(new[] { new MailAddress(expected) }, recipients.ReplyTo.ToArray());
            }
        }

        public class GetSubject : Facts
        {
            [Fact]
            public void IsBasedOnConfiguration()
            {
                var message = CreateMessage();

                Assert.Equal($"[{Configuration.GalleryOwner.DisplayName}] Search Feedback", message.GetSubject());
            }
        }

        public class GetMarkdownBody : Facts
        {
            [Fact]
            public void HasExpectedBodyWithEmptyModel()
            {
                var message = CreateMessage();

                var body = message.GetBody(EmailFormat.Markdown);

                Assert.Equal(@"The following feedback has come from the search side-by-side page.

**Search Query:** [](https://localhost/experiments/search-sxs?q=json)

**Old Hits:** 0

**New Hits:** 0

", body);
            }
            [Fact]
            public void HasExpectedBodyWithFullModel()
            {
                Model.BetterSide = "new ";
                Model.Comments = "  These are some comments.\r\nWith two lines!\r\n";
                Model.EmailAddress = "  me@example.com  ";
                Model.ExpectedPackages = "  NuGet.Versioning,  NuGet.Protocol";
                Model.MostRelevantPackage = "  NuGet.Frameworks   ";
                Model.OldHits = 23;
                Model.NewHits = 42;
                Model.SearchTerm = "nuget";
                var message = CreateMessage();

                var body = message.GetBody(EmailFormat.Markdown);

                Assert.Equal(@"The following feedback has come from the search side-by-side page.

**Search Query:** [nuget](https://localhost/experiments/search-sxs?q=json)

**Old Hits:** 23

**New Hits:** 42

**Which results are better?** new

**What was the most relevant package?** NuGet.Frameworks

**Name at least one package you were expecting to see.** NuGet.Versioning,  NuGet.Protocol

**Comments:**
These are some comments.
With two lines!

**Email:** me@example.com

", body);
            }
        }

        public abstract class Facts : MarkdownMessageBuilderFacts
        {
            public SearchSideBySideViewModel Model { get; }
            public string SearchUrl { get; set; }

            public Facts()
            {
                Model = new SearchSideBySideViewModel();
                SearchUrl = "https://localhost/experiments/search-sxs?q=json";
            }

            public SearchSideBySideMessage CreateMessage()
            {
                return new SearchSideBySideMessage(
                    Configuration,
                    Model,
                    SearchUrl);
            }
        }
    }
}
