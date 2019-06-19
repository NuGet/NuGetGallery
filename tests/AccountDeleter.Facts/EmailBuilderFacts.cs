// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Messaging.Email;
using System.Collections.Generic;
using System.Net.Mail;
using Xunit;

namespace NuGetGallery.AccountDeleter.Facts
{
    public class EmailBuilderFacts
    {
        private Mock<IEmailBuilder> _accountDeleteEmailBuilder;

        public EmailBuilderFacts()
        {
            _accountDeleteEmailBuilder = new Mock<IEmailBuilder>();
        }



        [Theory]
        [InlineData("test1test2", "none", "test1test2")]
        [InlineData("test1USERNAME", "replaceUser", "test1replaceUser")]
        [InlineData("USERNAME", "justUser", "justUser")]
        public void UsernamerReplacedInBody(string template, string username, string expected)
        {
            // Setup
            _accountDeleteEmailBuilder.Setup(adeb => adeb.GetBody(It.IsAny<EmailFormat>()))
                .Returns(template);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username);

            // Act
            var resultHtml = disposableBuilder.GetBody(EmailFormat.Html);
            var resultMarkdown = disposableBuilder.GetBody(EmailFormat.Markdown);
            var resultPlainText = disposableBuilder.GetBody(EmailFormat.PlainText);

            // Assert
            Assert.Equal(expected, resultHtml);
            Assert.Equal(expected, resultMarkdown);
            Assert.Equal(expected, resultPlainText);
        }

        [Theory]
        [InlineData("test1test2", "none", "test1test2")]
        [InlineData("test1USERNAME", "replaceUser", "test1replaceUser")]
        [InlineData("USERNAME", "justUser", "justUser")]
        public void UsernamerReplacedInSubject(string template, string username, string expected)
        {
            // Setup
            _accountDeleteEmailBuilder.Setup(adeb => adeb.GetSubject())
                .Returns(template);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username);

            // Act
            var result = disposableBuilder.GetSubject();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
