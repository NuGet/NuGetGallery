// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

namespace NuGetGallery.DataServices
{
    public class NormalizeVersionInterceptorFacts
    {
        [Theory]
        [MemberData("NonBinaryExpressions")]
        public void ItReturnsNonBinaryExpressions(Expression expr)
        {
            // Arrange
            var interceptor = new NormalizeVersionInterceptor();

            // Act
            var result = interceptor.VisitAndConvert(expr, "Test");

            // Assert
            Assert.Same(expr, result);
        }

        [Theory]
        [MemberData("NonEqualityCheckExpressions")]
        public void ItReturnsNonEqualityCheckExpressions(Expression expr)
        {
            // Arrange
            var interceptor = new NormalizeVersionInterceptor();

            // Act
            var result = interceptor.VisitAndConvert(expr, "Test");

            // Assert
            Assert.Same(expr, result);
        }

        [Theory]
        [MemberData("NonMatchingExpressions")]
        public void ItReturnsNonMatchingExpressions(Expression expr)
        {
            // Arrange
            var interceptor = new NormalizeVersionInterceptor();

            // Act
            var result = interceptor.VisitAndConvert(expr, "Test");

            // Assert
            Assert.Same(expr, result);
        }

        [Fact]
        public void ItRewritesEqualityCheckOfVersionColumnAgainstConstantString()
        {
            // Arrange
            var interceptor = new NormalizeVersionInterceptor();
            Expression<Func<V2FeedPackage, bool>> expr = (p => p.Version == "01.00.02");

            // Act
            var result = interceptor.VisitAndConvert(expr, "Test");

            // Assert
            Assert.Equal(
                ((Expression<Func<V2FeedPackage, bool>>)(p => "1.0.2" == p.NormalizedVersion)).ToString(),
                result.ToString());
        }

        [Fact]
        public void ItRewritesEqualityCheckOfConstantStringAgainstVersionColumn()
        {
            // Arrange
            var interceptor = new NormalizeVersionInterceptor();
            Expression<Func<V2FeedPackage, bool>> expr = (p => "01.00.02" == p.Version);

            // Act
            var result = interceptor.VisitAndConvert(expr, "Test");

            // Assert
            Assert.Equal(
                ((Expression<Func<V2FeedPackage, bool>>)(p => "1.0.2" == p.NormalizedVersion)).ToString(),
                result.ToString());
        }

        // Theory Data
        public static IEnumerable<object[]> NonBinaryExpressions
        {
            get
            {
                yield return new object[] { Expression.Constant("Foo") };
                yield return new object[] { Expression.Call(Expression.Constant("foo"), "ToString", new Type[0]) };
                yield return new object[] { Expression.MakeMemberAccess(Expression.Constant("foo"), typeof(String).GetProperty("Length")) };
            }
        }

        public static IEnumerable<object[]> NonEqualityCheckExpressions
        {
            get
            {
                yield return new object[] { Expression.Add(Expression.Constant(1), Expression.Constant(1)) };
                yield return new object[] { Expression.Subtract(Expression.Constant(1), Expression.Constant(1)) };
                yield return new object[] { Expression.Multiply(Expression.Constant(1), Expression.Constant(1)) };
                yield return new object[] { Expression.Divide(Expression.Constant(1), Expression.Constant(1)) };

                yield return new object[] { Expression.And(Expression.Constant(true), Expression.Constant(false)) };
                yield return new object[] { Expression.AndAlso(Expression.Constant(true), Expression.Constant(false)) };
                yield return new object[] { Expression.Or(Expression.Constant(true), Expression.Constant(false)) };
                yield return new object[] { Expression.OrElse(Expression.Constant(true), Expression.Constant(false)) };
            }
        }

        public static IEnumerable<object[]> NonMatchingExpressions
        {
            get
            {
                // Wrong type
                yield return new object[] { (Expression<Func<Package, bool>>)(p => p.Version == "1.2.3") };
                yield return new object[] { (Expression<Func<Package, bool>>)(p => "1.2.3" == p.Version) };

                // Wrong property
                yield return new object[] { (Expression<Func<V2FeedPackage, bool>>)(p => p.Id == "1.2.3") };
                yield return new object[] { (Expression<Func<V2FeedPackage, bool>>)(p => "1.2.3" == p.Id) };

                // Non-constant version
                yield return new object[] { (Expression<Func<V2FeedPackage, bool>>)(p => p.NormalizedVersion == "1.2.3".ToUpper()) };
                yield return new object[] { (Expression<Func<V2FeedPackage, bool>>)(p => "1.2.3".ToUpper() == p.NormalizedVersion) };
            }
        }
    }
}
