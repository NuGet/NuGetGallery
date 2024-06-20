// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Mvc;
using NuGetGallery.Filters;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.Controllers
{
    public class ControllerTests
    {
        private readonly ITestOutputHelper _output;

        public ControllerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private class ControllerActionRuleException : IEquatable<ControllerActionRuleException>
        {
            public Type Controller { get; private set; }

            public string Name { get; private set; }

            public ControllerActionRuleException(Type controller, string name)
            {
                Controller = controller;
                Name = name;
            }

            public ControllerActionRuleException(MethodInfo method)
            {
                Controller = method.DeclaringType;
                Name = method.Name;
            }

            public bool Equals(ControllerActionRuleException other)
            {
                return other != null && other.Controller == Controller && other.Name == Name;
            }

            public override int GetHashCode()
            {
                return Controller.GetHashCode() ^ Name.GetHashCode();
            }
        }

        [Fact]
        public void AllActionsHaveAntiForgeryTokenIfNotGet()
        {
            // Arrange

            // If an action only supports these verbs, it doesn't need the anti-forgery token attribute.
            var verbExceptions = new HttpVerbs[]
            {
                HttpVerbs.Get,
                HttpVerbs.Head
            };

            // These actions cannot have the AntiForgery token attribute.
            // For example: API methods intended to be called by clients.
            var exceptions = new ControllerActionRuleException[]
            {
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.CreatePackagePut)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.CreateSymbolPackagePutAsync)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.CreatePackagePost)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.CreatePackageVerificationKeyAsync)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.DeletePackage)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.PublishPackage)),
                new ControllerActionRuleException(typeof(ApiController), nameof(ApiController.DeprecatePackage)),
                new ControllerActionRuleException(typeof(PackagesController), nameof(PackagesController.DisplayPackage)),
            };

            // Act
            var assembly = Assembly.GetAssembly(typeof(AppController));

            var actions = GetAllActions(exceptions)
                // Filter out methods that only support verbs that are exceptions.
                .Where(m =>
                {
                    var attributes = m.GetCustomAttributes()
                        .Where(a => typeof(ActionMethodSelectorAttribute)
                        .IsAssignableFrom(a.GetType()));

                    // Non-actions cannot be requested via HTTP.
                    if (attributes.Any(a => a.GetType() == typeof(NonActionAttribute)))
                    {
                        _output.WriteLine($"{DisplayMethodName(m)} - non-action");
                        return false;
                    }

                    var isGetOrHead = attributes
                        .All(a =>
                            a.GetType() == typeof(HttpGetAttribute) ||
                            a.GetType() == typeof(HttpHeadAttribute) ||
                            (a.GetType() == typeof(AcceptVerbsAttribute) &&
                                (a as AcceptVerbsAttribute).Verbs.All(v =>
                                    verbExceptions.Any(ve => v.Equals(ve.ToString(), StringComparison.InvariantCultureIgnoreCase)))));

                    if (isGetOrHead)
                    {
                        _output.WriteLine($"{DisplayMethodName(m)} - GET or HEAD");
                    }

                    return !isGetOrHead;
                })
                .Select(m =>
                {
                    _output.WriteLine($"{DisplayMethodName(m)} - to be verified");
                    return m;
                });

            // Assert
            var actionsMissingAntiForgeryToken = actions
                .Where(m => !(m.GetCustomAttributes().Any(a => a.GetType() == typeof(ValidateAntiForgeryTokenAttribute))));

            Assert.Empty(actionsMissingAntiForgeryToken);
        }

        [Fact]
        public void AllActionsHaveUIAuthorizeAttribute()
        {
            // Arrange

            // These actions are allowed to continue to support a discontinued login.
            var expectedActionsSupportingDiscontinuedLogins = new ControllerActionRuleException[]
            {
                new ControllerActionRuleException(typeof(AccountsController<,>), nameof(UsersController.Confirm)),
                new ControllerActionRuleException(typeof(AccountsController<,>), nameof(UsersController.ConfirmationRequired)),
                new ControllerActionRuleException(typeof(AccountsController<,>), nameof(UsersController.ConfirmationRequiredPost)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.Thanks)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.TransformToOrganization)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.ConfirmTransformToOrganizationRedirect)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.ConfirmTransformToOrganization)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.RejectTransformToOrganizationRedirect)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.RejectTransformToOrganization)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.CancelTransformToOrganizationRedirect)),
                new ControllerActionRuleException(typeof(UsersController), nameof(UsersController.CancelTransformToOrganization)),
            };

            // Act
            var assembly = Assembly.GetAssembly(typeof(AppController));

            var actions = GetAllActions();

            // Assert

            // No actions should have the base authorize attribute!
            var actionsWithBaseAuthorizeAttribute = actions
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType() == typeof(AuthorizeAttribute)));

            Assert.Empty(actionsWithBaseAuthorizeAttribute);

            // Only certain actions should support discontinued password login
            var actionsSupportingDiscontinuedLogins = actions
                .Where(m => m.GetCustomAttributes().Any(a => a is UIAuthorizeAttribute attribute && attribute.AllowDiscontinuedLogins))
                .Select(m => new ControllerActionRuleException(m))
                .Distinct();

            Assert.Empty(actionsSupportingDiscontinuedLogins.Except(expectedActionsSupportingDiscontinuedLogins));
            Assert.Empty(expectedActionsSupportingDiscontinuedLogins.Except(actionsSupportingDiscontinuedLogins));
        }

        private IEnumerable<MethodInfo> GetAllActions(IEnumerable<ControllerActionRuleException> exceptions = null)
        {
            return Assembly.GetAssembly(typeof(AppController))
                .GetTypes()
                // Get all types that are controllers.
                .Where(typeof(Controller).IsAssignableFrom)
                // Get all public methods of those types.
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public))
                // Filter out compiler generated methods.
                .Where(m => !m.GetCustomAttributes(typeof(CompilerGeneratedAttribute), inherit: true).Any())
                .OrderBy(DisplayMethodName)
                // Filter out exceptions.
                .Where(m =>
                {
                    var possibleException = new ControllerActionRuleException(m);
                    var isNotException = !exceptions?.Any(a => a.Equals(possibleException)) ?? true;

                    if (!isNotException)
                    {
                        _output.WriteLine($"{DisplayMethodName(m)} - excluded due to exception");
                    }

                    return isNotException;
                });
        }

        private static string DisplayMethodName(MethodInfo m)
        {
            return $"{m.DeclaringType.FullName}.{m.Name}";
        }
    }
}