// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ClientTelemetryPIIProcessorTests
    {
        [Fact]
        public void NullTelemetryItemDoesNotThorw()
        {
            // Arange
            string userName = "user1";
            var piiProcessor = CreatePIIProcessor(false, userName);

            // Act
            piiProcessor.Process(null);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UrlIsUpdatedOnPIIAction(bool actionIsPII)
        {
            // Arange
            string userName = "user1";
            var piiProcessor = CreatePIIProcessor(actionIsPII, userName);
            RequestTelemetry telemetryItem = new RequestTelemetry();
            telemetryItem.Url = new Uri("https://localhost/user1");

            // Act
            piiProcessor.Process(telemetryItem);

            // Assert
            string expected = actionIsPII ? "https://localhost/" : telemetryItem.Url.ToString();
            Assert.Equal(expected, telemetryItem.Url.ToString());
        }

        [Fact]
        public void UrlIsUpdatedOnUserContext()
        {
            // Arange
            string userName = "user1";
            var piiProcessor = CreatePIIProcessor(false, userName);
            RequestTelemetry telemetryItem = new RequestTelemetry();
            telemetryItem.Url = new Uri($"https://localhost/route1?username={userName}");

            // Act
            piiProcessor.Process(telemetryItem);

            // Assert
            string expected = "https://localhost/route1?username=username";
            Assert.Equal(expected, telemetryItem.Url.ToString());
        }

        private ClientTelemetryPIIProcessor CreatePIIProcessor(bool isPIIOperation, string userName)
        {
            return new TestClientTelemetryPIIProcessor(new TestProcessorNext(), isPIIOperation, userName);
        }

        private class TestProcessorNext : ITelemetryProcessor
        {
            public void Process(ITelemetry item)
            {
            }
        }

        private class TestClientTelemetryPIIProcessor : ClientTelemetryPIIProcessor
        {
            private User _testUser;
            private bool _isPIIOperation;

            public TestClientTelemetryPIIProcessor(ITelemetryProcessor next, bool isPIIOperation, string userName) : base(next)
            {
                _isPIIOperation = isPIIOperation;
                _testUser = new User(userName);
            }

            protected override bool IsPIIOperation(string operationName)
            {
                return _isPIIOperation;
            }

            protected override HttpContextBase GetHttpContext()
            {
                return new TestHttpContext();
            }

            protected override IOwinContext GetOwingContext(HttpContextBase httpContext)
            {
                return new TestOwinContext(_testUser);
            }
        }

        private class TestHttpContext : HttpContextBase
        {
            public override HttpRequestBase Request
            {
                get
                {
                    var request = new Mock<HttpRequestBase>();
                    request.Setup(m => m.IsAuthenticated).Returns(true);
                    return request.Object;
                }
            }
        }

        private class TestOwinContext : IOwinContext
        {
            private User _user;
            public TestOwinContext(User user)
            {
                _user = user;
                Environment = new Dictionary<string, object>();
                Environment.Add(Constants.CurrentUserOwinEnvironmentKey, _user);
                var owinUser = new Mock<IPrincipal>();
                var request = new Mock<IOwinRequest>();
                request.Setup(m => m.User).Returns(owinUser.Object);
                Request = request.Object;
            }

            public IOwinRequest Request
            {
                get;
            }

            public IOwinResponse Response => throw new NotImplementedException();

            public IAuthenticationManager Authentication => throw new NotImplementedException();

            public IDictionary<string, object> Environment
            {
                get;
            }

            public TextWriter TraceOutput { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public T Get<T>(string key)
            {
                throw new NotImplementedException();
            }

            public IOwinContext Set<T>(string key, T value)
            {
                throw new NotImplementedException();
            }
        }
    }
}
