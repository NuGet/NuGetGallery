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
        public void SerializesAndDeserializesCorrectly()
        {
            var expected = CreateStatus();

            var statusString = JsonConvert.SerializeObject(expected);
            var actual = JsonConvert.DeserializeObject<ServiceStatus>(statusString);

            AssertUtility.AssertStatus(expected, actual);
        }

        private static ServiceStatus CreateStatus()
        {
            return new ServiceStatus(GetDate(), CreateRootComponent(), new[] { CreateEvent(), CreateEvent(), CreateEvent() });
        }

        private static IComponent CreateRootComponent()
        {
            return CreateComponent(new TreeComponent(GetString(), GetString(), new[] { CreateTreeComponent(), CreatePrimarySecondaryComponent(), CreateSubComponent(), CreateTreeComponent(), CreatePrimarySecondaryComponent(), CreateSubComponent() }));
        }

        private static IComponent CreateTreeComponent()
        {
            return CreateComponent(new TreeComponent(GetString(), GetString(), new[] { CreateSubComponent(), CreateSubComponent() }));
        }

        private static IComponent CreatePrimarySecondaryComponent()
        {
            return CreateComponent(new PrimarySecondaryComponent(GetString(), GetString(), new[] { CreateSubComponent(), CreateSubComponent() }));
        }

        private static IComponent CreateSubComponent()
        {
            return CreateComponent(new LeafComponent(GetString(), GetString()));
        }

        private static IEvent CreateEvent()
        {
            return new Event(GetString(), GetComponentStatus(), GetDate(), GetDate(), new[] { CreateMessage(), CreateMessage() });
        }

        private static IMessage CreateMessage()
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
                _currentString = _currentString.Substring(0, _currentString.Length - 1) + (last + 1);
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
