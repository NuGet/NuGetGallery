using NuGetGallery.Authentication;
using System;
using System.Collections.Generic;

namespace NuGetGallery.Infrastructure
{
    public class TestableApiScopeEvaluator : IApiScopeEvaluator
    {
        public ApiScopeEvaluationResult Result { get; set; }

        public Func<User> OwnerFactory { get; set; }

        public TestableApiScopeEvaluator()
        {
            OwnerFactory = () => null;
        }

        public ApiScopeEvaluationResult Evaluate<TEntity>(User currentUser, IEnumerable<Scope> scopes, IActionRequiringEntityPermissions<TEntity> action, TEntity entity, out User owner, params string[] requestedActions)
        {
            owner = OwnerFactory();
            return Result;
        }
    }
}
