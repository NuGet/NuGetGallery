using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Moq;
using NuGet.Services.Models;
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
                Assert.Contains("local_DC42_testhost_TestService_IN0", names);
                Assert.Contains("local_DC42_testhost_TestService_IN1", names);
            }
        }

        public class TheStartMethod
        {
            public async Task ItInjectsPropertiesWithPublicSetters()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var service = new TestService(host);

                // Act
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Assert
                Assert.Same(service.Something, container.Resolve<SomeService>());
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

            public async Task InsideTheRunMethodTheServiceInstanceNameIsCorrect()
            {
                // Arrange
                var host = new TestServiceHost();
                var container = CreateTestContainer();
                var bogusName = new ServiceInstanceName(
                    new ServiceHostName(
                        new DatacenterName(
                            "bogus",
                            1),
                        "bogus"),
                    "bogus",
                    42);
                var evt = new TaskCompletionSource<object>();

                ServiceInstanceName actual = null;
                var service = new TestService(host) {
                    CustomOnRun = async () => {
                        // Wait for the other task to have tried to mess with us
                        await evt.Task;

                        actual = ServiceInstanceName.GetCurrent();
                    }
                };
                await service.Start(container, ServiceInstanceEntry.FromService(service));

                // Act
                var runTask = service.Run();
                await Task.Factory.StartNew(() =>
                {
                    // Set the instance name to something bogus
                    ServiceInstanceName.SetCurrent(bogusName);
                    evt.TrySetResult(null);
                });
                await runTask;

                // Assert
                Assert.Equal(service.InstanceName, actual);
            }
        }

        // Helper methods
        private static IContainer CreateTestContainer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<SomeService>().SingleInstance();
            builder.RegisterType<SomeOtherService>().SingleInstance();
            var container = builder.Build();
            return container;
        }

        // Helper nested classes
        public class TestServiceHost : ServiceHost
        {
            public override ServiceHostDescription Description
            {
                get
                {
                    return new ServiceHostDescription(
                        new ServiceHostName(
                            new DatacenterName(
                                "local",
                                42),
                            "testhost"),
                        "testmachine");
                }
            }

            protected override void InitializePlatformLogging()
            {
            }

            protected override IEnumerable<Type> GetServices()
            {
                return Enumerable.Empty<Type>();
            }
        }

        public class TestService : NuGetService
        {
            public bool WasRun { get; private set; }
            public bool WasStarted { get; private set; }
            public bool WasShutdown { get; private set; }

            public SomeService Something { get; set; }
            public SomeOtherService SomethingElse { get; set; }

            public Func<Task> CustomOnRun { get; set; }

            public TestService(ServiceHost host) : base("TestService", host) { }

            protected override Task OnRun()
            {
                WasRun = true;
                
                if (CustomOnRun != null)
                {
                    return CustomOnRun();
                }
                else
                {
                    TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
                    tcs.TrySetResult(null);
                    return tcs.Task;
                }
            }

            protected override Task<bool> OnStart()
            {
                WasStarted = true;
                return base.OnStart();
            }

            protected override void OnShutdown()
            {
                WasShutdown = true;
                base.OnShutdown();
            }
        }

        public class SomeService
        {
        }

        public class SomeOtherService
        {
        }
    }
}
