// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Messages;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageContentBuilderTests
    {
        public class TheBuildMethodWithImplicitStatus
            : TheBuildMethodTest
        {
            protected override string Invoke(MessageType type, IComponent component, ComponentStatus status)
            {
                return Builder.Build(type, component);
            }

            protected override ComponentStatus GetStatus(IComponent component, ComponentStatus status)
            {
                return component.Status;
            }
        }

        public class TheBuildMethodWithExplicitStatus
            : TheBuildMethodTest
        {
            protected override string Invoke(MessageType type, IComponent component, ComponentStatus status)
            {
                return Builder.Build(type, component, status);
            }

            protected override ComponentStatus GetStatus(IComponent component, ComponentStatus status)
            {
                return status;
            }
        }

        public abstract class TheBuildMethodTest
            : MessageContentBuilderTest
        {
            [Fact]
            public void ThrowsIfMissingTemplateForType()
            {
                var type = MessageType.Manual;
                var component = CreateTestComponent(
                    NuGetServiceComponentFactory.RootName, 
                    NuGetServiceComponentFactory.GalleryName);
                var status = ComponentStatus.Degraded;

                Assert.Throws<ArgumentException>(() => Invoke(type, component, status));
            }

            [Theory]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            public void ThrowsIfMissingActionDescriptionForPath(MessageType type)
            {
                var component = CreateTestComponent("missing");
                var status = ComponentStatus.Degraded;

                Assert.Throws<ArgumentException>(() => Invoke(type, component, status));
            }
            
            [Theory]
            [ClassData(typeof(BuildsContentsSuccessfully_Data))]
            public void BuildsContentsSuccessfully(MessageType type, string[] names, ComponentStatus status, Func<string, string> getExpected)
            {
                var root = new NuGetServiceComponentFactory().Create();
                var component = root.GetByNames(names);
                var result = Invoke(type, component, status);
                Assert.Equal(
                    getExpected(GetStatus(component, status).ToString().ToLowerInvariant()), 
                    result);
            }

            protected abstract string Invoke(
                MessageType type,
                IComponent component,
                ComponentStatus status);

            protected abstract ComponentStatus GetStatus(
                IComponent component, 
                ComponentStatus status);
        }

        public class MessageContentBuilderTest
        {
            public MessageContentBuilder Builder { get; }

            public MessageContentBuilderTest()
            {
                Builder = new MessageContentBuilder(
                    Mock.Of<ILogger<MessageContentBuilder>>());
            }
        }

        public static IComponent CreateTestComponent(params string[] names)
        {
            IComponent bottom = null;
            IComponent root = null;
            foreach (var name in names.Reverse())
            {
                if (bottom == null)
                {
                    bottom = new TestComponent(name);
                    root = bottom;
                }
                else
                {
                    root = new TestComponent(name, new[] { root });
                }
            }
            
            return bottom ?? throw new ArgumentException(nameof(names));
        }

        public class BuildsContentsSuccessfully_Data : IEnumerable<object[]>
        {
            public IEnumerable<Tuple<MessageType, string[], ComponentStatus, Func<string, string>>> Data = 
                new Tuple<MessageType, string[], ComponentStatus, Func<string, string>>[]
                {
                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName },
                        ComponentStatus.Degraded,
                        status => $"**NuGet.org is {status}.** You may encounter issues browsing the NuGet Gallery."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName },
                        ComponentStatus.Degraded,
                        status => $"**NuGet.org is no longer {status}.** You should no longer encounter any issues browsing the NuGet Gallery. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName },
                        ComponentStatus.Down,
                        status => $"**NuGet.org is {status}.** You may encounter issues browsing the NuGet Gallery."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName },
                        ComponentStatus.Down,
                        status => $"**NuGet.org is no longer {status}.** You should no longer encounter any issues browsing the NuGet Gallery. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Degraded,
                        status => $"**China V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed from China."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Degraded,
                        status => $"**China V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed from China. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Down,
                        status => $"**China V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed from China."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Down,
                        status => $"**China V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed from China. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Degraded,
                        status => $"**Global V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Degraded,
                        status => $"**Global V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Down,
                        status => $"**Global V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Down,
                        status => $"**Global V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName },
                        ComponentStatus.Degraded,
                        status => $"**V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName },
                        ComponentStatus.Degraded,
                        status => $"**V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName },
                        ComponentStatus.Down,
                        status => $"**V3 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V3 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName },
                        ComponentStatus.Down,
                        status => $"**V3 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V3 feed. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName },
                        ComponentStatus.Degraded,
                        status => $"**V2 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V2 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName },
                        ComponentStatus.Degraded,
                        status => $"**V2 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V2 feed. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName },
                        ComponentStatus.Down,
                        status => $"**V2 Protocol Restore is {status}.** You may encounter issues restoring packages from NuGet.org's V2 feed."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName },
                        ComponentStatus.Down,
                        status => $"**V2 Protocol Restore is no longer {status}.** You should no longer encounter any issues restoring packages from NuGet.org's V2 feed. Thank you for your patience."),
                    
                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName },
                        ComponentStatus.Degraded,
                        status => $"**Restore is {status}.** You may encounter issues restoring packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName },
                        ComponentStatus.Degraded,
                        status => $"**Restore is no longer {status}.** You should no longer encounter any issues restoring packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName },
                        ComponentStatus.Down,
                        status => $"**Restore is {status}.** You may encounter issues restoring packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName },
                        ComponentStatus.Down,
                        status => $"**Restore is no longer {status}.** You should no longer encounter any issues restoring packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Degraded,
                        status => $"**China Search is {status}.** You may encounter issues searching for packages from China."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Degraded,
                        status => $"**China Search is no longer {status}.** You should no longer encounter any issues searching for packages from China. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Down,
                        status => $"**China Search is {status}.** You may encounter issues searching for packages from China."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName },
                        ComponentStatus.Down,
                        status => $"**China Search is no longer {status}.** You should no longer encounter any issues searching for packages from China. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Degraded,
                        status => $"**Global Search is {status}.** You may encounter issues searching for packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Degraded,
                        status => $"**Global Search is no longer {status}.** You should no longer encounter any issues searching for packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Down,
                        status => $"**Global Search is {status}.** You may encounter issues searching for packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName },
                        ComponentStatus.Down,
                        status => $"**Global Search is no longer {status}.** You should no longer encounter any issues searching for packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName },
                        ComponentStatus.Degraded,
                        status => $"**Search is {status}.** You may encounter issues searching for packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName },
                        ComponentStatus.Degraded,
                        status => $"**Search is no longer {status}.** You should no longer encounter any issues searching for packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName },
                        ComponentStatus.Down,
                        status => $"**Search is {status}.** You may encounter issues searching for packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName },
                        ComponentStatus.Down,
                        status => $"**Search is no longer {status}.** You should no longer encounter any issues searching for packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName },
                        ComponentStatus.Degraded,
                        status => $"**Package Publishing is {status}.** You may encounter issues uploading new packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName },
                        ComponentStatus.Degraded,
                        status => $"**Package Publishing is no longer {status}.** You should no longer encounter any issues uploading new packages. Thank you for your patience."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.Start,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName },
                        ComponentStatus.Down,
                        status => $"**Package Publishing is {status}.** You may encounter issues uploading new packages."),

                    Tuple.Create<MessageType, string[], ComponentStatus, Func<string, string>>(
                        MessageType.End,
                        new[] { NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName },
                        ComponentStatus.Down,
                        status => $"**Package Publishing is no longer {status}.** You should no longer encounter any issues uploading new packages. Thank you for your patience."),

                };

            public IEnumerator<object[]> GetEnumerator() => Data
                .Select(t => new object[] { t.Item1, t.Item2, t.Item3, t.Item4 })
                .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void BuildsContentSuccessfullyTestsAllVisibleComponents()
        {
            var root = new NuGetServiceComponentFactory().Create();
            var components = new BuildsContentsSuccessfully_Data().Data
                .Select(t => root.GetByNames(t.Item2));
            foreach (var component in root.GetAllVisibleComponents())
            {
                if (root == component)
                {
                    continue;
                }

                if (!components.Any(c => c == component))
                {
                    throw new KeyNotFoundException(component.Path);
                }
            }
        }
    }
}
