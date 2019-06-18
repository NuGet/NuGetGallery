

using Moq;
using NuGet.Services.Messaging.Email;
using NuGetGallery.AccountDeleter;
using NuGetGallery.AccountDeleter.Messengers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Xunit;

namespace AccountDeleter.Facts
{
    public class EmailBuilderFacts
    {
        private Mock<IEmailBuilder> _accountDeleteEmailBuilder;

        public EmailBuilderFacts()
        {
            _accountDeleteEmailBuilder = new Mock<IEmailBuilder>();
        }

        [Theory]
        [InlineData("USERNAME", "test", "test")]
        [InlineData("Do not replace", "test", "Do not replace")]
        [InlineData("USERNAME and things", "test", "test and things")]
        [InlineData("USERNAMEnospaces", "test", "testnospaces")]
        public void UsernameTokenIsReplacedInBody(string templateString, string username, string expected)
        {
            // Setup
            _accountDeleteEmailBuilder
                .Setup(adeb => adeb.GetBody(It.IsAny<EmailFormat>()))
                .Returns(templateString);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username);

            // Act
            var resultHtml = disposableBuilder.GetBody(EmailFormat.Html);
            var resultMd = disposableBuilder.GetBody(EmailFormat.Markdown);
            var resultText = disposableBuilder.GetBody(EmailFormat.PlainText);

            // Assert
            Assert.Equal(expected, resultHtml);
            Assert.Equal(expected, resultMd);
            Assert.Equal(expected, resultText);
        }

        [Theory]
        [InlineData("USERNAME", "test", "test")]
        [InlineData("Do not replace", "test", "Do not replace")]
        [InlineData("USERNAME and things", "test", "test and things")]
        [InlineData("USERNAMEnospaces", "test", "testnospaces")]
        public void UsernameTokenIsReplacedInSubject(string templateString, string username, string expected)
        {
            // Setup
            _accountDeleteEmailBuilder
                .Setup(adeb => adeb.GetSubject())
                .Returns(templateString);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username);

            // Act
            var result = disposableBuilder.GetSubject();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
