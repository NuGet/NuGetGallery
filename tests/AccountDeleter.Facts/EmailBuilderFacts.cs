

using Moq;
using NuGet.Services.Messaging.Email;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Xunit;

namespace NuGetGallery.AccountDeleter.Facts
{
    public class EmailBuilderFacts
    {
        private Mock<IEmailBuilder> _accountDeleteEmailBuilder;
        private Mock<ITemplater> _templater;

        public EmailBuilderFacts()
        {
            _accountDeleteEmailBuilder = new Mock<IEmailBuilder>();
            _templater = new Mock<ITemplater>();
        }

        [Fact]
        public void GetBodyCallsTemplater()
        {
            // Setup
            var username = "testUser";
            var templaterReturn = "replaced";
            _accountDeleteEmailBuilder
                .Setup(adeb => adeb.GetBody(It.IsAny<EmailFormat>()))
                .Returns("BODY");

            _templater
                .Setup(t => t.AddReplacement(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
            _templater
                .Setup(t => t.FillTemplate(It.IsAny<string>()))
                .Returns(templaterReturn);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username, _templater.Object);

            // Act
            var resultHtml = disposableBuilder.GetBody(EmailFormat.Html);
            var resultMd = disposableBuilder.GetBody(EmailFormat.Markdown);
            var resultText = disposableBuilder.GetBody(EmailFormat.PlainText);

            // Assert
            Assert.Equal(templaterReturn, resultHtml);
            Assert.Equal(templaterReturn, resultMd);
            Assert.Equal(templaterReturn, resultText);
            _templater.Verify(t => t.AddReplacement(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _templater.Verify(t => t.FillTemplate(It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public void GetSubjectCallsTemplater()
        {
            // Setup
            var username = "testUser";
            var templaterReturn = "replaced";
            _accountDeleteEmailBuilder
                .Setup(adeb => adeb.GetBody(It.IsAny<EmailFormat>()))
                .Returns("BODY");

            _templater
                .Setup(t => t.AddReplacement(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
            _templater
                .Setup(t => t.FillTemplate(It.IsAny<string>()))
                .Returns(templaterReturn);

            var disposableBuilder = new DisposableEmailBuilder(_accountDeleteEmailBuilder.Object, new EmailRecipients(new List<MailAddress>()), username, _templater.Object);

            // Act
            var result = disposableBuilder.GetSubject();

            // Assert
            Assert.Equal(templaterReturn, result);
            _templater.Verify(t => t.AddReplacement(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _templater.Verify(t => t.FillTemplate(It.IsAny<string>()), Times.Once);
        }
    }
}
