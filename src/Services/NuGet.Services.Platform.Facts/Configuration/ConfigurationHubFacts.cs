using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.ServiceModel;
using Xunit;

namespace NuGet.Services.Configuration
{
    public class ConfigurationHubFacts
    {
        public class TheGetSettingMethod
        {
            [Fact]
            public void ReturnsValueInHost()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("FooBar")).Returns("Baz");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSetting("FooBar");

                // Assert
                Assert.Equal("Baz", actual);
            }
        }

        public class TheGetSectionMethod
        {
            [Fact]
            public void LeavesObjectAtDefaultValuesIfNoSettingsPresent()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<TestConfiguration>();

                // Assert
                Assert.Null(actual.Foo);
                Assert.Equal(0, actual.Bar);
                Assert.Equal("Default!", actual.Baz);
                Assert.Null(actual.WEIRDNAME);
                Assert.Equal(128, actual.NotAConfigProperty);
            }

            [Fact]
            public void UsesPrefixToReadProperties()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("Test.Foo")).Returns("foo");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<TestConfiguration>();

                // Assert
                Assert.Equal("foo", actual.Foo);
                Assert.Equal(0, actual.Bar);
                Assert.Equal("Default!", actual.Baz);
                Assert.Null(actual.WEIRDNAME);
                Assert.Equal(128, actual.NotAConfigProperty);
            }

            [Fact]
            public void UsesConfigurationSectionAttributeAsPrefixWhenPresent()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("Different.Foo")).Returns("foo");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<ADifferentConfigClassName>();

                // Assert
                Assert.Equal("foo", actual.Foo);
            }

            [Fact]
            public void UsesFullTypeNameIfNameDoesNotEndConfigurationAndNoAttributePresent()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("NoAttribute.Foo")).Returns("foo");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<NoAttribute>();

                // Assert
                Assert.Equal("foo", actual.Foo);
            }

            [Fact]
            public void UsesDefaultTypeConverters()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("Test.Bar")).Returns("1234");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<TestConfiguration>();

                // Assert
                Assert.Null(actual.Foo);
                Assert.Equal(1234, actual.Bar);
                Assert.Equal("Default!", actual.Baz);
                Assert.Null(actual.WEIRDNAME);
                Assert.Equal(128, actual.NotAConfigProperty);
            }

            [Fact]
            public void UsesDisplayNameToMatchPropertiesWithConfigSettings()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("Test.NormalName")).Returns("abc");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<TestConfiguration>();

                // Assert
                Assert.Null(actual.Foo);
                Assert.Equal(0, actual.Bar);
                Assert.Equal("Default!", actual.Baz);
                Assert.Equal("abc", actual.WEIRDNAME);
                Assert.Equal(128, actual.NotAConfigProperty);
            }

            [Fact]
            public void DoesNotSetPropertiesWithAPrivateSetter()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("Test.NotAConfigProperty")).Returns("821");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<TestConfiguration>();

                // Assert
                Assert.Null(actual.Foo);
                Assert.Equal(0, actual.Bar);
                Assert.Equal("Default!", actual.Baz);
                Assert.Null(actual.WEIRDNAME);
                Assert.Equal(128, actual.NotAConfigProperty);
            }

            [Fact]
            public void HandlesCustomSections()
            {
                // Arrange
                var host = new Mock<ServiceHost>();
                host.Setup(h => h.GetConfigurationSetting("TotallyCustom")).Returns("yep");
                host.Setup(h => h.GetConfigurationSetting("Custom.TotallyCustom")).Returns("werd");
                var hub = new ConfigurationHub(host.Object);

                // Act
                var actual = hub.GetSection<CustomConfiguration>();

                // Assert
                Assert.Equal("yep", actual.Foo);
                Assert.Equal("werd", actual.FooPrefixed);
            }
        }

        public class CustomConfiguration : ICustomConfigurationSection
        {
            public string Foo { get; private set; }
            public string FooPrefixed { get; private set; }

            public void Resolve(string prefix, ConfigurationHub hub)
            {
                Foo = hub.GetSetting("TotallyCustom");
                FooPrefixed = hub.GetSetting(prefix + "TotallyCustom");
            }
        }

        [ConfigurationSection("Different")]
        public class ADifferentConfigClassName
        {
            public string Foo { get; set; }
        }

        public class NoAttribute
        {
            public string Foo { get; set; }
        }

        public class TestConfiguration
        {
            private int _notConfig = 128;

            public string Foo { get; set; }
            public int Bar { get; set; }

            [DefaultValue("Default!")]
            public string Baz { get; set; }

            [DisplayName("NormalName")]
            public string WEIRDNAME { get; set; }

            public int NotAConfigProperty { get { return _notConfig; } private set { _notConfig = value; } }
        }
    }
}
