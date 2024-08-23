// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public class ServiceStatusTests
    {
        [Fact]
        public void SetsFieldsAsExpected()
        {
            var lastBuilt = new DateTime(2018, 11, 13);
            var lastUpdated = new DateTime(2018, 11, 14);

            var rootComponentName = "name";
            var rootComponentDescription = "description";
            var rootComponentStatus = (ComponentStatus)99;
            var rootComponent = new ReadOnlyComponent(
                rootComponentName,
                rootComponentDescription,
                rootComponentStatus,
                Enumerable.Empty<IReadOnlyComponent>());

            var eventPath = "path";
            var eventStartTime = new DateTime(2018, 11, 15);
            var eventEndTime = new DateTime(2018, 11, 16);
            var messageTime = new DateTime(2018, 11, 17);
            var messageContents = "contents";
            var recentEvents = new[] 
            {
                new Event(
                    eventPath, 
                    eventStartTime, 
                    eventEndTime, 
                    new[] 
                    {
                        new Message(
                            messageTime, 
                            messageContents)
                    })
            };

            var status = new ServiceStatus(lastBuilt, lastUpdated, rootComponent, recentEvents);

            // Assert ServiceStatus
            Assert.Equal(lastBuilt, status.LastBuilt);
            Assert.Equal(lastUpdated, status.LastUpdated);
            Assert.Equal(rootComponent, status.ServiceRootComponent);
            AssertUtility.AssertComponent(rootComponent, status.ServiceRootComponent);
            Assert.Equal(recentEvents, status.Events);
            AssertUtility.AssertAll(recentEvents, status.Events, AssertUtility.AssertEvent);

            // Assert ReadOnlyComponent
            var actualRootComponent = status.ServiceRootComponent;
            Assert.Equal(rootComponentName, actualRootComponent.Name);
            Assert.Equal(rootComponentDescription, actualRootComponent.Description);
            Assert.Equal(rootComponentStatus, actualRootComponent.Status);

            // Assert Event
            var actualEvent = status.Events.Single();
            Assert.Equal(eventPath, actualEvent.AffectedComponentPath);
            Assert.Equal(eventStartTime, actualEvent.StartTime);
            Assert.Equal(eventEndTime, actualEvent.EndTime);

            // Assert Message
            var actualMessage = actualEvent.Messages.Single();
            Assert.Equal(messageTime, actualMessage.Time);
            Assert.Equal(messageContents, actualMessage.Contents);
        }

        [Fact]
        public void SerializesAndDeserializesCorrectly()
        {
            var expected = CreateStatus();

            var statusString = JsonConvert.SerializeObject(expected);
            var actual = JsonConvert.DeserializeObject<ServiceStatus>(statusString);

            AssertUtility.AssertStatus(expected, actual);
        }

        private static ServiceStatus CreateStatus()
        {
            return new ServiceStatus(
                GetDate(),
                GetDate(), 
                CreateRootComponent(),
                new[] { CreateEvent(), CreateEvent(), CreateEvent() });
        }

        private static IComponent CreateRootComponent()
        {
            return CreateComponent(
                new TreeComponent(
                    GetString(), 
                    GetString(), 
                    new[] 
                    {
                        CreateTreeComponent(),
                        CreateActivePassiveComponent(),
                        CreateActiveActiveComponent(),
                        CreateSubComponent(),
                        CreateTreeComponent(),
                        CreateActivePassiveComponent(),
                        CreateActiveActiveComponent(),
                        CreateSubComponent(),
                        CreateTreeComponent(),
                        CreateActivePassiveComponent(),
                        CreateActiveActiveComponent(),
                        CreateSubComponent()
                    }));
        }

        private static IComponent CreateTreeComponent()
        {
            return CreateComponent(
                new TreeComponent(
                    GetString(), 
                    GetString(), 
                    new[] 
                    {
                        CreateSubComponent(),
                        CreateSubComponent()
                    }));
        }

        private static IComponent CreateActivePassiveComponent()
        {
            return CreateComponent(
                new ActivePassiveComponent(
                    GetString(),
                    GetString(),
                    new[]
                    {
                        CreateSubComponent(),
                        CreateSubComponent()
                    }));
        }

        private static IComponent CreateActiveActiveComponent()
        {
            return CreateComponent(
                new ActiveActiveComponent(
                    GetString(),
                    GetString(),
                    new[]
                    {
                        CreateSubComponent(),
                        CreateSubComponent()
                    }));
        }

        private static IComponent CreateSubComponent()
        {
            return CreateComponent(
                new LeafComponent(
                    GetString(), 
                    GetString()));
        }

        private static Event CreateEvent()
        {
            return new Event(GetString(), GetDate(), GetDate(), new[] { CreateMessage(), CreateMessage() });
        }

        private static Message CreateMessage()
        {
            return new Message(GetDate(), GetString());
        }

        private static IComponent CreateComponent(IComponent component)
        {
            var status = GetComponentStatus();
            if (status != ComponentStatus.Up)
            {
                component.Status = status;
            }

            return component;
        }

        private static int _currentStatusIndex = 0;
        private static IEnumerable<ComponentStatus> _statuses = Enum.GetValues(typeof(ComponentStatus)).Cast<ComponentStatus>();

        private static ComponentStatus GetComponentStatus()
        {
            _currentStatusIndex = (_currentStatusIndex + 1) % _statuses.Count();
            return _statuses.ElementAt(_currentStatusIndex);
        }

        private static string _currentString = "a";
        
        private static string GetString()
        {
            var last = _currentString[_currentString.Length - 1];
            if (last == 'z')
            {
                _currentString += 'a';
            }
            else
            {
                _currentString = _currentString.Substring(0, _currentString.Length - 1) + (char)(last + 1);
            }

            return _currentString;
        }

        private static DateTime _currentDate = new DateTime(2018, 7, 3);
        private static TimeSpan _diffDate = new TimeSpan(1, 0, 0);

        private static DateTime GetDate()
        {
            _currentDate += _diffDate;
            return _currentDate;
        }
    }
}
