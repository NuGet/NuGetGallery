// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    internal class DbAsyncQueryProviderMock
        : IDbAsyncQueryProvider
    {
        private readonly IQueryable _queryable;

        public DbAsyncQueryProviderMock(IQueryable queryable)
        {
            _queryable = queryable;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return _queryable.Provider.CreateQuery(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return _queryable.Provider.CreateQuery<TElement>(expression);
        }

        public object Execute(Expression expression)
        {
            return _queryable.Provider.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _queryable.Provider.Execute<TResult>(expression);
        }

        public Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute(expression));
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute<TResult>(expression));
        }
    }
}