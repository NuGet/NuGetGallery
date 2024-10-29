// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class CredentialRevokedMessageFacts : MarkdownMessageBuilderFacts
    {
        private const string TestLeakedUrl = "TestLeakedUrl";
        private const string TestRevocationSource = "TestRevocationSource";
        private const string TestManageApiKeyUrl = "TestManageApiKeyUrl";
        private const string TestContactUrl = "TestContactUrl";

        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.ApiKeyCredential, TestLeakedUrl, TestRevocationSource, TestManageApiKeyUrl, TestContactUrl };
                    yield return new object[] { Configuration, null, TestLeakedUrl, TestRevocationSource, TestManageApiKeyUrl, TestContactUrl };
                    yield return new object[] { Configuration, Fakes.ApiKeyCredential, null, TestRevocationSource, TestManageApiKeyUrl, TestContactUrl };
                    yield return new object[] { Configuration, Fakes.ApiKeyCredential, TestLeakedUrl, null, TestManageApiKeyUrl, TestContactUrl };
                    yield return new object[] { Configuration, Fakes.ApiKeyCredential, TestLeakedUrl, TestRevocationSource, null, TestContactUrl };
                    yield return new object[] { Configuration, Fakes.ApiKeyCredential, TestLeakedUrl, TestRevocationSource, TestManageApiKeyUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Credential credential,
                string leakedUrl,
                string revocationSource,
                string manageApiKeyUrl,
                string contactUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new CredentialRevokedMessage(
                    configuration,
                    credential,
                    leakedUrl,
                    revocationSource,
                    manageApiKeyUrl,
                    contactUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            private readonly Credential _credential;

            public TheGetRecipientsMethod()
            {
                _credential = new Credential();
                _credential.User = Fakes.RequestingUser;
            }

            [Fact]
            public void AddsUserMailAddressToToList()
            {
                var message = CreateMessage(_credential);
                var recipients = message.GetRecipients();

                Assert.Single(recipients.To);
                Assert.Equal(Fakes.RequestingUser.ToMailAddress(), recipients.To[0]);
            }

            [Fact]
            public void AddsGalleryOwnerMailAddressToCCList()
            {
                var message = CreateMessage(_credential);
                var recipients = message.GetRecipients();

                Assert.Single(recipients.CC);
                Assert.Equal(Configuration.GalleryOwner, recipients.CC[0]);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(_credential);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage(_credential);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
            }
        }

        [Fact]
        public void SetsGalleryOwnerAsSender()
        {
            var message = CreateMessage(Fakes.ApiKeyCredential);

            Assert.Equal(Configuration.GalleryOwner, message.Sender);
        }

        public class TheGetSubjectMethod
        {
            [Fact]
            public void ReturnsExpectedSubjectForCredentialWithDescription()
            {
                var credential = new Credential();
                credential.Description = "TestApiKey";

                var message = CreateMessage(credential);

                var subject = message.GetSubject();
                Assert.Equal("[NuGetGallery] API key 'TestApiKey' revoked due to a potential leak", subject);
            }

            [Fact]
            public void ReturnsExpectedSubjectForCredentialWithNullDescription()
            {
                var message = CreateMessage(Fakes.ApiKeyCredential);

                var subject = message.GetSubject();
                Assert.Equal("[NuGetGallery] API key revoked due to a potential leak", subject);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyWithCredentialDescription)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyWithCredentialDescription)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyWithCredentialDescription)]
            public void ReturnsExpectedBodyForCredentialWithDescription(EmailFormat format, string expectedString)
            {
                var credential = new Credential();
                credential.Description = "TestApiKey";
                credential.User = Fakes.RequestingUser;

                var message = CreateMessage(credential);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyWithNullCredentialDescription)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyWithNullCredentialDescription)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyWithNullCredentialDescription)]
            public void ReturnsExpectedBodyForCredentialWithNullDescription(EmailFormat format, string expectedString)
            {
                var credential = new Credential();
                credential.User = Fakes.RequestingUser;

                var message = CreateMessage(credential);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        private static CredentialRevokedMessage CreateMessage(Credential credential)
        {
            return new CredentialRevokedMessage(
                    Configuration,
                    credential,
                    TestLeakedUrl,
                    TestRevocationSource,
                    TestManageApiKeyUrl,
                    TestContactUrl);
        }

        private const string _expectedMarkdownBodyWithCredentialDescription =
           @"Hi requestingUser,

This is your friendly NuGet security bot! It appears that an API key 'TestApiKey' associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.

Your key was found here: <TestLeakedUrl>

In the future, please be mindful of accidentally posting your API keys publicly!

You can regenerate this key or create a new one on the [Manage API Keys](TestManageApiKeyUrl) page.

Here are the recommended ways to manage API keys:
- Save the API key into a local NuGet.Config using the [NuGet CLI](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use [environment variables](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.
- Use [GitHub encrypted secrets](https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets) to store and access API keys.

Need help? Reply to this email or [contact support](TestContactUrl).

Thanks,  
The NuGetGallery Team";

        private const string _expectedPlainTextBodyWithCredentialDescription =
            @"Hi requestingUser,

This is your friendly NuGet security bot! It appears that an API key 'TestApiKey' associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.

Your key was found here: <TestLeakedUrl>

In the future, please be mindful of accidentally posting your API keys publicly!

You can regenerate this key or create a new one on the Manage API Keys (TestManageApiKeyUrl) page.

Here are the recommended ways to manage API keys:

- Save the API key into a local NuGet.Config using the NuGet CLI (https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use environment variables (https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.
- Use GitHub encrypted secrets (https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets) to store and access API keys.

Need help? Reply to this email or contact support (TestContactUrl).

Thanks,  
The NuGetGallery Team";

        private const string _expectedHtmlBodyWithCredentialDescription =
            "<p>Hi requestingUser,</p>\n" +
"<p>This is your friendly NuGet security bot! It appears that an API key 'TestApiKey' associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.</p>\n" +
"<p>Your key was found here: <TestLeakedUrl></p>\n" +
"<p>In the future, please be mindful of accidentally posting your API keys publicly!</p>\n" +
"<p>You can regenerate this key or create a new one on the <a href=\"TestManageApiKeyUrl\">Manage API Keys</a> page.</p>\n" +
"<p>Here are the recommended ways to manage API keys:</p>\n" +
"<ul>\n" +
"<li>Save the API key into a local NuGet.Config using the <a href=\"https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey\">NuGet CLI</a>. This file should NOT be checked-in to version control or GitHub;</li>\n" +
"<li>Use <a href=\"https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value\">environment variables</a> to set and access API keys.</li>\n" +
"<li>Use <a href=\"https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets\">GitHub encrypted secrets</a> to store and access API keys.</li>\n" +
"</ul>\n" +
"<p>Need help? Reply to this email or <a href=\"TestContactUrl\">contact support</a>.</p>\n" +
"<p>Thanks,<br />\n" +
"The NuGetGallery Team</p>\n";

        private const string _expectedMarkdownBodyWithNullCredentialDescription =
            @"Hi requestingUser,

This is your friendly NuGet security bot! It appears that an API key associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.

Your key was found here: <TestLeakedUrl>

In the future, please be mindful of accidentally posting your API keys publicly!

You can regenerate this key or create a new one on the [Manage API Keys](TestManageApiKeyUrl) page.

Here are the recommended ways to manage API keys:
- Save the API key into a local NuGet.Config using the [NuGet CLI](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use [environment variables](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.
- Use [GitHub encrypted secrets](https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets) to store and access API keys.

Need help? Reply to this email or [contact support](TestContactUrl).

Thanks,  
The NuGetGallery Team";

        private const string _expectedPlainTextBodyWithNullCredentialDescription =
            @"Hi requestingUser,

This is your friendly NuGet security bot! It appears that an API key associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.

Your key was found here: <TestLeakedUrl>

In the future, please be mindful of accidentally posting your API keys publicly!

You can regenerate this key or create a new one on the Manage API Keys (TestManageApiKeyUrl) page.

Here are the recommended ways to manage API keys:

- Save the API key into a local NuGet.Config using the NuGet CLI (https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use environment variables (https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.
- Use GitHub encrypted secrets (https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets) to store and access API keys.

Need help? Reply to this email or contact support (TestContactUrl).

Thanks,  
The NuGetGallery Team";

        private const string _expectedHtmlBodyWithNullCredentialDescription =
            "<p>Hi requestingUser,</p>\n" +
"<p>This is your friendly NuGet security bot! It appears that an API key associated with your account was posted at TestRevocationSource. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.</p>\n" +
"<p>Your key was found here: <TestLeakedUrl></p>\n" +
"<p>In the future, please be mindful of accidentally posting your API keys publicly!</p>\n" +
"<p>You can regenerate this key or create a new one on the <a href=\"TestManageApiKeyUrl\">Manage API Keys</a> page.</p>\n" +
"<p>Here are the recommended ways to manage API keys:</p>\n" +
"<ul>\n" +
"<li>Save the API key into a local NuGet.Config using the <a href=\"https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey\">NuGet CLI</a>. This file should NOT be checked-in to version control or GitHub;</li>\n" +
"<li>Use <a href=\"https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value\">environment variables</a> to set and access API keys.</li>\n" +
"<li>Use <a href=\"https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets\">GitHub encrypted secrets</a> to store and access API keys.</li>\n" +
"</ul>\n" +
"<p>Need help? Reply to this email or <a href=\"TestContactUrl\">contact support</a>.</p>\n" +
"<p>Thanks,<br />\n" +
"The NuGetGallery Team</p>\n";
    }
}
