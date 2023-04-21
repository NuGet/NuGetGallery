// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Helpers
{
    public static class DeprecationItemsHelper
    {
        public static IEnumerable<object[]> ValidObjects
        {
            get
            {
                yield return new object[] {
                        JObject.FromObject(new
                        {
                            Message = "message",
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                            AlternatePackage = new {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        })
                    };
                yield return new object[] {
                        JObject.FromObject(new
                        {
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                            AlternatePackage = new {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        })
                    };
                yield return new object[] {
                        JObject.FromObject(new
                        {
                            Message = "message",
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                            AlternatePackage = new {}
                        })
                    };
                yield return new object[] {
                        JObject.FromObject(new
                        {
                            Reasons = new [] { "Legacy" },
                            AlternatePackage = new {}
                        })
                    };
            }
        }

        public static IEnumerable<object[]> InvalidObjects
        {
            get
            {
                yield return new object[] {
                        JObject.FromObject(new
                        {
                            Message = "message",
                            AlternatePackage = new {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        })
                    };
                yield return new object[] { new JObject() };
                yield return new object[] { null };
            }
        }
    }
}
