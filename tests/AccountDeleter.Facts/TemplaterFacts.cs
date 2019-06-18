using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.AccountDeleter.Facts
{
    public class TemplaterFacts
    {
        private Mock<IOptionsSnapshot<AccountDeleteConfiguration>> _optionsMock;

        public TemplaterFacts(ITestOutputHelper output)
        {
            _optionsMock = new Mock<IOptionsSnapshot<AccountDeleteConfiguration>>();
        }

        [Theory]
        [InlineData("USERNAME", "test", "test")]
        [InlineData("Do not replace", "test", "Do not replace")]
        [InlineData("USERNAME and things", "test", "test and things")]
        [InlineData("USERNAMEnospaces", "test", "testnospaces")]
        public void UsernameTokenIsReplaced(string templateString, string username, string expected)
        {
            var replacements = new Dictionary<string, string>();
            // Setup
            _optionsMock
                .Setup(om => om.Value)
                .Returns(new AccountDeleteConfiguration()
                {
                    TemplateReplacements = replacements
                });

            var templater = new AccountDeleteTemplater(_optionsMock.Object);
            templater.AddReplacement("USERNAME", username);

            // Act
            var result = templater.FillTemplate(templateString);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("test1test2", "none", "TEST1TEST2")]
        [InlineData("test1USERNAME", "replaceUser", "TEST1replaceUser")]
        [InlineData("USERNAME", "justUser", "justUser")]
        public void AdditionalReplacementsOverrideBase(string template, string newUsername, string expected)
        {
            var replacements = new Dictionary<string, string>()
            {
                { "test1", "TEST1" },
                { "test2", "TEST2" },
                { "USERNAME", "newUser" },
            };

            // Setup
            _optionsMock
                .Setup(om => om.Value)
                .Returns(new AccountDeleteConfiguration()
                {
                    TemplateReplacements = replacements
                });

            var templater = new AccountDeleteTemplater(_optionsMock.Object);
            templater.AddReplacement("USERNAME", newUsername);

            // Act
            var result = templater.FillTemplate(template);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
