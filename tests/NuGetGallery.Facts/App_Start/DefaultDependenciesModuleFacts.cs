using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Moq;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery.App_Start
{
    public class DefaultDependenciesModuleFacts
    {
        private static void SetupMockConfigService(Mock<IGalleryConfigurationService> mockConfigService,
            IAppConfiguration appConfig)
        {
            mockConfigService.Setup(x => x.GetCurrent()).Returns(Task.FromResult(appConfig));
            mockConfigService.Setup(x => x.Current).Returns(appConfig);
        }

        private static IContainer CreateContainerBuilder(IGalleryConfigurationService configService)
        {
            var builder = new ContainerBuilder();

            var defaultDependenciesModule = new DefaultDependenciesModule
            {
                ConfigurationServiceFactory = d => configService
            };
            builder.RegisterModule(defaultDependenciesModule);

            return builder.Build();
        }

        public void ChangesWhenConfigurationChanges()
        {
            var mockAppConfig = new Mock<IAppConfiguration>();
            var mockConfigService = new Mock<IGalleryConfigurationService>();
            SetupMockConfigService(mockConfigService, mockAppConfig.Object);

            foreach (var propertyInfo in mockAppConfig.GetType().GetProperties())
            {
                mockAppConfig.Setup(x => propertyInfo.GetMethod.Invoke(x, null));
            }

            var container = CreateContainerBuilder(mockConfigService.Object);
        }
    }
}
