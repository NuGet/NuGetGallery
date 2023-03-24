// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Collections.Generic;

namespace NuGetGallery.Helpers
{
    public static class DeprecationItemsHelper
    {
        public static IEnumerable<object[]> ValidObjects
        {
            get
            {
                yield return new object[] {
                        new Deprecation()
                        {
                            Message = "message",
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                            AlternatePackage = new AlternatePackage() {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        }
                    };
                yield return new object[] {
                        new Deprecation()
                        {
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                            AlternatePackage = new AlternatePackage() {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        }
                    };
                yield return new object[] {
                        new Deprecation()
                        {
                            Message = "message",
                            Reasons = new [] { "Other", "Legacy", "CriticalBugs" },
                        }
                    };
                yield return new object[] {
                        new Deprecation()
                        {
                            Reasons = new [] { "Legacy" },
                        }
                    };
            }
        }

        public static IEnumerable<object[]> InvalidObjects
        {
            get
            {
                yield return new object[] {
                        new Deprecation()
                        {
                            Message = "message",
                            AlternatePackage = new AlternatePackage() {
                                Id = "AnotherId",
                                Range = "[13.0.2-beta1, )"
                            }
                        }
                    };
                yield return new object[] { new Deprecation() };
                yield return new object[] { null };
            }
        }
    }
}
