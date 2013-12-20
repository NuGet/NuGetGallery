using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Moq;
using NuGet.Services.Models;
using NuGet.Services.TestInfrastructure;
using Xunit;

namespace NuGet.Services.ServiceModel
{
    public class NuGetServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public async Task ItAssignsAUniqueNameToTheServiceInstance()
            {
                // Arrange
                var host = new TestServiceHost();

                // Act
                string[] names = new string[2];
                await Task.WhenAll(
                    Task.Factory.StartNew(() =>
                    {
                        var instanceOne = new TestService(host);
                        names[0] = instanceOne.InstanceName.ToString();
                    }),
                    Task.Factory.StartNew(() =>
                    {
                        var instanceTwo = new TestService(host);
                        names[1] = instanceTwo.InstanceName.ToString();
                    }));

                // Assert
                Assert.NotEqual(names[0], names[1]);
                Assert.Contains("local_dc42_testhost_TestService_IN0", names);
                Assert.Contains("local_dc42_testhost_TestService_IN1", names);
            }
        }

        public class TheStartMethod
        {
            public async Task ItInjectsPropertiesWithPublicSetters()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var service = new TestServiceWithComponents(host);

                // Act
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Assert
                Assert.Same(service.Something, container.Resolve<SomeComponent>());
                Assert.Null(service.SomethingElse);
            }

            public async Task ItInvokesOnStart()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var service = new TestService(host);

                // Act
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Assert
                Assert.True(service.WasStarted);
            }

            public async Task ItConfiguresShutdownTokenToCallOnShutdown()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var service = new TestService(host);
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Act
                host.Shutdown();

                // Assert
                Assert.True(service.WasShutdown);
            }
        }

        public class TheRunMethod
        {
            public async Task ItInvokesOnRun()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var service = new TestService(host);
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Act
                await service.Run();

                // Assert
                Assert.True(service.WasRun);
            }
        }

        // Helper methods
        private static IContainer CreateTestContainer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<SomeComponent>().SingleInstance();
            builder.RegisterType<SomeOtherComponent>().SingleInstance();
            var container = builder.Build();
            return container;
        }

        // Helper nested classes
        public class TestServiceWithComponents : TestService
        {
            public SomeComponent Something { get; set; }
            public SomeOtherComponent SomethingElse { get; set; }

            public TestServiceWithComponents(ServiceHost host) : base(host) { }
        }

        public class SomeComponent
        {
        }

        public class SomeOtherComponent
        {
        }
    }
}
