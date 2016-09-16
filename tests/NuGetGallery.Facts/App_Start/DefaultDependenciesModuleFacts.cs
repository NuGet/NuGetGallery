using System;
using System.Collections.Generic;
using System.Linq;
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
        private static Tuple<Mock<IGalleryConfigurationService>, Mock<IAppConfiguration>> CreateMockConfigTuple()
        {
            var mockAppConfig = new Mock<IAppConfiguration>();

            var mockConfigService = new Mock<IGalleryConfigurationService>();
            mockConfigService.Setup(x => x.GetCurrent()).Returns(Task.FromResult(mockAppConfig.Object));

            return Tuple.Create(mockConfigService, mockAppConfig);
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
            var configTuple = CreateMockConfigTuple();

            var mockAppConfig = configTuple.Item2;
            foreach (var propertyInfo in mockAppConfig.GetType().GetProperties())
            {
                mockAppConfig.Setup(x => propertyInfo.GetMethod.Invoke(x, null));
            }

            var container = CreateContainerBuilder(configTuple.Item1.Object);
        }
    }
}
