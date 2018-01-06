using NuGetGallery.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Infrastructure
{
    public class TestableApiScopeEvaluator : IApiScopeEvaluator
    {
        public ApiScopeEvaluationResult Result { get; set; }

        public Func<User> OwnerFactory { get; set; }

        private User _expectedCurrentUser;

        private object _expectedAction;

        private Func<object, bool> _expectedEntity;

        private string[] _expectedRequestedActions;

        public TestableApiScopeEvaluator()
        {
            OwnerFactory = () => null;
        }

        public ApiScopeEvaluationResult Evaluate<TEntity>(User currentUser, IEnumerable<Scope> scopes, IActionRequiringEntityPermissions<TEntity> action, TEntity entity, out User owner, params string[] requestedActions)
        {
            if (_expectedCurrentUser != null && _expectedCurrentUser != currentUser)
            {
                throw new ArgumentException($"{nameof(Evaluate)} was called with a different {nameof(currentUser)} than expected!");
            }

            if (_expectedAction != null && _expectedAction != action)
            {
                throw new ArgumentException($"{nameof(Evaluate)} was called with a different {nameof(action)} than expected!");
            }

            if (_expectedEntity != null && !_expectedEntity(entity))
            {
                throw new ArgumentException($"{nameof(Evaluate)} was called with a different {nameof(entity)} than expected!");
            }

            if (_expectedRequestedActions != null && !_expectedRequestedActions.SequenceEqual(requestedActions))
            {
                throw new ArgumentException($"{nameof(Evaluate)} was called with a different {nameof(requestedActions)} than expected!");
            }

            owner = OwnerFactory();
            return Result;
        }

        public void Setup<TEntity>(User expectedCurrentUser = null, IActionRequiringEntityPermissions<TEntity> expectedAction = null, Func<TEntity, bool> expectedEntity = null, params string[] expectedRequestedActions)
        {
            _expectedCurrentUser = expectedCurrentUser;
            _expectedAction = expectedAction;
            _expectedEntity = expectedEntity as Func<object, bool>;
            _expectedRequestedActions = expectedRequestedActions;
        }

        public void Setup<TEntity>(User expectedCurrentUser = null, IActionRequiringEntityPermissions<TEntity> expectedAction = null, TEntity expectedEntity = default(TEntity), params string[] expectedRequestedActions)
        {
            Setup(
                expectedCurrentUser, 
                expectedAction, 
                (entity) => entity as object == expectedEntity as object, 
                expectedRequestedActions);
        }
    }
}
