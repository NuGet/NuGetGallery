using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core.Registration;
using Autofac.Features.ResolveAnything;
using Moq;
using NuGet.Services.ServiceModel;
using Xunit;

namespace NuGet.Services
{
    // Tests that verify the expected behavior of the IoC container. These were mostly just so that I (anurse)
    //  could verify that Autofac behaved the way I expected, but they can also serve as a criteria for what
    //  any replacement IoC container needs to satisfy in order to work :).
    public class ContainerBehaviorFacts
    {
        [Fact]
        public void ItAllowsTwoOfTheSameTypeToBeRegisteredInSingleInstanceMode()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<AService>().SingleInstance();
            builder.RegisterType<AService>().SingleInstance();

            var container = builder.Build();

            // Act
            var resolved = container.Resolve<IReadOnlyList<AService>>();

            // Assert
            Assert.Equal(2, resolved.Count);
            Assert.NotEqual(resolved[1].Id, resolved[0].Id);
        }

        [Fact]
        public void ItThrowsWhenThereIsAnAmbiguousMatchForADependency()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());
            builder.RegisterType<AService>().SingleInstance();
            builder.RegisterType<AService>().SingleInstance();
            builder.RegisterType<AnotherService>().SingleInstance();

            var container = builder.Build();

            // Act
            var resolved = container.Resolve<AConsumer>();
        }

        [Fact]
        public void ChildScopeCanSeeParentScopeObjects()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<AService>().SingleInstance();
            
            var container = builder.Build();
            var scope = container.BeginLifetimeScope();

            // Act
            var resolved = scope.Resolve<AService>();

            // Assert
            Assert.NotNull(resolved);
        }

        [Fact]
        public void ParentScopeCanNotSeeChildScopeObjects()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<AService>().SingleInstance();

            var container = builder.Build();
            var scope = container.BeginLifetimeScope(r => r.RegisterType<AnotherService>().SingleInstance());

            // Act
            var resolved = container.ResolveOptional<AnotherService>();

            // Assert
            Assert.Null(resolved);
        }

        [Fact]
        public void ChildScopeCannotSeeSiblingScopeObjects()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<AService>().SingleInstance();

            var container = builder.Build();
            var otherScope = container.BeginLifetimeScope(r => r.RegisterType<AnotherService>().SingleInstance());
            var scope = container.BeginLifetimeScope();

            // Act
            var resolved = scope.ResolveOptional<AnotherService>();

            // Assert
            Assert.Null(resolved);
        }

        [Fact]
        public void CanBeConfiguredToResolveObjectThatWasNotRegistered()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());
            builder.RegisterType<AService>().SingleInstance();

            var container = builder.Build();
            var scope = container.BeginLifetimeScope(r => r.RegisterType<AnotherService>().SingleInstance());

            // Act
            var resolved = scope.Resolve<AConsumer>();

            // Assert
            Assert.NotNull(resolved.AnotherService);
            Assert.NotNull(resolved.Service);
        }

        [Fact]
        public void BehaviorWhenConstructorArgumentsNotSatisfied()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterType<AService>().SingleInstance();

            var container = builder.Build();
            
            // Act
            Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<AConsumer>());
        }

        public class AService
        {
            private static int _counter = 0;

            public AService()
            {
                Id = Interlocked.Increment(ref _counter) - 1;
            }

            public int Id { get; private set; }
        }

        public class AnotherService
        {
            private static int _counter = 0;

            public AnotherService()
            {
                Id = Interlocked.Increment(ref _counter) - 1;
            }

            public int Id { get; private set; }
        }

        public class AConsumer
        {
            public AService Service { get; private set; }
            public AnotherService AnotherService { get; private set; }

            public AConsumer(AService service, AnotherService anotherService)
            {
                Service = service;
                AnotherService = anotherService;
            }
        }
    }
}
