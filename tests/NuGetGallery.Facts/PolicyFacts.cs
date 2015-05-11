// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Controllers;
using Xunit;

namespace NuGetGallery
{
    // Tests for various code style and policy facts
    // Unit tests seem to be the best way to check these things. FxCop is probably better but harder.
    public class PolicyFacts
    {
        [Fact]
        public void AllAdminControllersHaveAuthorizeAttributeOnClassSettingAllowedRolesToAdmins()
        {
            var failingTypes = (from t in TypesInTheSameNamespaceAs(typeof(AdminControllerBase))
                                where t.GetInterfaces().Contains(typeof(IController))
                                let a = t.GetCustomAttribute<AuthorizeAttribute>(inherit: true)
                                where a == null || !String.Equals(a.Roles, Constants.AdminRoleName)
                                select t)
                               .ToList();

            Assert.False(failingTypes.Any(),
                         "The following Admin controllers are unsecured:" + Environment.NewLine +
                         String.Join(Environment.NewLine, failingTypes.Select(t => "* " + t.FullName)));
        }

        private IEnumerable<Type> TypesInTheSameNamespaceAs(Type type)
        {
            return
                type.Assembly.GetTypes()
                    .Where(t => String.Equals(t.Namespace, type.Namespace, StringComparison.Ordinal))
                    .Where(t => t.GetCustomAttribute<GeneratedCodeAttribute>() == null);
        }
    }
}
