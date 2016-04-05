// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Areas.Admin;
using Xunit;

namespace NuGetGallery
{
    public class PagerDutyClientFacts
    {
        [Fact]
        public void GetEmailAliasFromOnCallUser()
        {
            var response = "{\"users\":[" +
                           "{\"id\":\"ABCDEF1\",\"name\":\"On-call User\",\"email\":\"on-call@sample.org\", \"on_call\":" +
                                "[" +
                                    "{\"level\":1,\"start\":null,\"end\":null,\"escalation_policy\":{ \"id\":\"sampleId\",\"name\":\"Support Requests\"}}" +
                                "]" +
                           "}," +
                           "{\"id\":\"ABCDEF2\",\"name\":\"NuGet Core Team\",\"email\":\"nugetcore@sample.org\", \"on_call\":" +
                                "[" +
                                    "{\"level\":1,\"start\":null,\"end\":null,\"escalation_policy\":{ \"id\":\"sampleId\",\"name\":\"Support Requests\"}}" +
                                "]" +
                           "}," +
                           "{\"id\":\"ABCDEF3\",\"name\":\"Off-call User\",\"email\":\"off-call@sample.org\",\"on_call\":" +
                                "[" +
                                    "{\"level\":1,\"start\":null,\"end\":null,\"escalation_policy\":{ \"id\":\"sampleId2\",\"name\":\"Sev1 - Service Interruption\"}}," +
                                    "{\"level\":3,\"start\":null,\"end\":null,\"escalation_policy\":{ \"id\":\"sampleId\",\"name\":\"Support Requests\"}}" +
                                "]" +
                           "}," +
                           "{\"id\":\"ABCDEF4\",\"name\":\"Off-call User 2\",\"email\":\"off-call-2@sample.org\",\"on_call\":" +
                                "[" +
                                    "{\"level\":3,\"start\":null,\"end\":null,\"escalation_policy\":{ \"id\":\"sampleId2\",\"name\":\"Sev1 - Service Interruption\"}}" +
                                "]" +
                           "}], " +
                           "\"active_account_users\":11,\"limit\":25,\"offset\":0,\"total\":4}";
            var username = PagerDutyClient.GetEmailAliasFromOnCallUser(response, "sampleId");
            Assert.Equal("on-call", username);
        }
    }
}