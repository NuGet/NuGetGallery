// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ScopeSubjectTypeConverterFacts
    {
        private ScopeSubjectTypeConverter Converter = new ScopeSubjectTypeConverter();

        private IEnumerable<Type> GetActionTypes()
        {
            return
                typeof(ScopeSubjectTypeConverter)
                    .Assembly
                    .GetTypes()
                    .SelectMany(t => t.GetInterfaces())
                    .Where(x =>
                        x.IsGenericType &&
                        x.GetGenericTypeDefinition() == typeof(IActionRequiringEntityPermissions<>))
                    .Select(x => x.GetGenericArguments()[0])
                    // A collection of ReservedNamespaces cannot be converted to a scope subject.
                    .Where(x => x != typeof(IReadOnlyCollection<ReservedNamespace>))
                    // Generic parameters themselves (e.g. Class<T> : IInterface<T>) will appear in this list, but they will have a null FullName.
                    // They need to be filtered out.
                    .Where(x => x.FullName != null);
        }

        [Fact]
        public void CanConvertFromForAllPossibleActionTypes()
        {
            var actionTypes = GetActionTypes();

            Assert.True(actionTypes.All(t => Converter.CanConvertFrom(t)));
        }

        [Fact]
        public void ThereAreConvertFromTestsForAllActionTypes()
        {
            var actionTypes = GetActionTypes();

            foreach (var actionType in actionTypes)
            {
                // Check for a test named "ConvertFrom{actionType.Name}ReturnsExpected".
                // Each possible type of action must have a test.
                var scopeSubjectConverterFactsType = typeof(ScopeSubjectTypeConverterFacts);
                var method = scopeSubjectConverterFactsType.GetMethod($"ConvertFrom{actionType.Name}ReturnsExpected", new Type[0]);
                Assert.True(method.CustomAttributes.Any(a => a.AttributeType == typeof(FactAttribute)));
            }
        }

        [Fact]
        public void ConvertFromPackageRegistrationReturnsExpected()
        {
            var pr = new PackageRegistration { Id = "howdy" };

            Assert.Equal(pr.Id, Converter.ConvertFrom(pr));
        }

        [Fact]
        public void ConvertFromPackageReturnsExpected()
        {
            var pr = new PackageRegistration { Id = "howdy" };
            var p = new Package { PackageRegistration = pr };

            Assert.Equal(pr.Id, Converter.ConvertFrom(p));
        }

        [Fact]
        public void ConvertFromActionOnNewPackageContextReturnsExpected()
        {
            var context = new ActionOnNewPackageContext("howdy", new Mock<IReservedNamespaceService>().Object);

            Assert.Equal(context.PackageId, Converter.ConvertFrom(context));
        }
    }
}
