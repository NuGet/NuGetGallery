// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class OrganizationExtensionsFacts
    {
        [MemberData(nameof(GetUserAccountMembers_Input))]
        [Theory]
        public void Test_GetUserAccountMembers(Organization o, int expectedMemberCount)
        {
            // Act 
            var result = o.GetUserAccountMembers().Count();

            // Assert 
            Assert.Equal(expectedMemberCount, result);
        }

        public static IEnumerable<object[]> GetUserAccountMembers_Input
        {
            get
            {
                List<object[]> result = new List<object[]>();

                // Organization without any user account.
                Organization o1 = new Organization(){ Members = new List<Membership>(), Key = 0};
                result.Add(new object[] { o1, 0 });

                //  Organization one user account.
                var user2 = new User() { Username = "user21", Key = 21 };
                Organization o2 = new Organization()
                {
                    Key = 2,
                    Members = new List<Membership>(){ new Membership(){OrganizationKey = 2, Member = user2, MemberKey = user2.Key}}
                };
                result.Add(new object[] { o2, 1 });

                // Organization with one user account and one child organization. 
                var user3 = new User() { Username = "user31", Key = 31 };
                Organization o3 = new Organization()
                {
                    Key = 30,
                    Members = new List<Membership>() { new Membership() { OrganizationKey = 30, Member = user3, MemberKey = user3.Key } }
                };
                Organization o3P = new Organization()
                {
                    Key = 3,
                    Members = new List<Membership>()
                    { new Membership(){ OrganizationKey = 3, Member = o3, MemberKey = o3.Key },
                      new Membership(){OrganizationKey = 3, Member = user3, MemberKey = user3.Key }
                    }
                };
                result.Add(new object[] { o3P, 1 });

                // Organization with child organization and user accounts.
                var user41 = new User() { Username = "user41", Key = 41 };
                var user42 = new User() { Username = "user42", Key = 42 };
                Organization o4 = new Organization()
                {
                    Key = 40,
                    Members = new List<Membership>() { new Membership() { OrganizationKey = 40, Member = user41, MemberKey = user41.Key } }
                };
                Organization o4P = new Organization()
                {
                    Key = 4,
                    Members = new List<Membership>()
                    { new Membership(){ OrganizationKey = 4, Member = o4, MemberKey = o4.Key },
                      new Membership(){OrganizationKey = 4, Member = user41, MemberKey = user41.Key },
                      new Membership(){OrganizationKey = 4, Member = user42, MemberKey = user42.Key }
                    }
                };
                result.Add(new object[] { o4P, 2 });

                // Organization with multiple child organizations.
                var user51 = new User() { Username = "user51", Key = 51 };
                var user52 = new User() { Username = "user52", Key = 52 };
                Organization o51 = new Organization()
                {
                    Key = 501,
                    Members = new List<Membership>() { new Membership() { OrganizationKey = 501, Member = user51, MemberKey = user51.Key } }
                };
                Organization o52 = new Organization()
                {
                    Key = 502,
                    Members = new List<Membership>() { new Membership() { OrganizationKey = 502, Member = user52, MemberKey = user52.Key } }
                };
                Organization o5P = new Organization()
                {
                    Key = 5,
                    Members = new List<Membership>()
                    { new Membership(){ OrganizationKey = 5, Member = o51, MemberKey = o51.Key },
                      new Membership(){OrganizationKey = 5, Member = o52, MemberKey = o51.Key },
                      new Membership(){OrganizationKey = 5, Member = user52, MemberKey = user52.Key }
                    }
                };
                result.Add(new object[] { o5P, 2 });
                return result;
            }
        }
    }
}
