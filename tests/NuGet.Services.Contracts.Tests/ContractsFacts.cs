// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGet.Services
{
    public class ContractsFacts
    {
        [Fact]
        public void ShouldOnlyHaveInterfacesAndEnums()
        {
            // Arrange
            var assembly = typeof(ValidationStatus).Assembly;
            var exclude = new[]
            {
                // included in the assembly by newer language versions
                "Microsoft.CodeAnalysis.EmbeddedAttribute",
                "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
                // generated polyfills from PolySharp
                "System.Runtime.CompilerServices.NullableAttribute",
                "System.Runtime.CompilerServices.NullableContextAttribute",
                "System.Index",
                "System.Range",
                "System.Runtime.Versioning.RequiresPreviewFeaturesAttribute",
                "System.Runtime.CompilerServices.AsyncMethodBuilderAttribute",
                "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute",
                "System.Runtime.CompilerServices.CollectionBuilderAttribute",
                "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
                "System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute",
                "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute",
                "System.Runtime.CompilerServices.IsExternalInit",
                "System.Runtime.CompilerServices.ModuleInitializerAttribute",
                "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute",
                "System.Runtime.CompilerServices.ParamCollectionAttribute",
                "System.Runtime.CompilerServices.RequiredMemberAttribute",
                "System.Runtime.CompilerServices.RequiresLocationAttribute",
                "System.Runtime.CompilerServices.SkipLocalsInitAttribute",
                "System.Diagnostics.CodeAnalysis.AllowNullAttribute",
                "System.Diagnostics.CodeAnalysis.ConstantExpectedAttribute",
                "System.Diagnostics.CodeAnalysis.DisallowNullAttribute",
                "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute",
                "System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute",
                "System.Diagnostics.CodeAnalysis.ExperimentalAttribute",
                "System.Diagnostics.CodeAnalysis.MaybeNullAttribute",
                "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
                "System.Diagnostics.CodeAnalysis.MemberNotNullAttribute",
                "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute",
                "System.Diagnostics.CodeAnalysis.NotNullAttribute",
                "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute",
                "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
                "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute",
                "System.Diagnostics.CodeAnalysis.StringSyntaxAttribute",
                "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute",
                "System.Index+ThrowHelper",
                "System.Range+HashHelpers",
                "System.Range+ThrowHelper",
            };

            // Act
            var types = assembly.GetTypes();

            // Assert
            Assert.NotEmpty(types);
            foreach (var type in types)
            {
                if (exclude.Contains(type.FullName))
                {
                    continue;
                }
                Assert.True(type.IsEnum || type.IsInterface, $"{type.FullName} must either be an interface or an enum.");
            }
        }
    }
}
