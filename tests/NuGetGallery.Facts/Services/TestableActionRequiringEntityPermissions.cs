using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class TestableActionRequiringEntityPermissions
        : ActionRequiringEntityPermissions<TestablePermissionsEntity>
    {
        private Func<User, TestablePermissionsEntity, PermissionsCheckResult> _isAllowedOnEntity;

        public TestableActionRequiringEntityPermissions(PermissionsRequirement accountOnBehalfOfPermissionsRequirement, Func<User, TestablePermissionsEntity, PermissionsCheckResult> isAllowedOnEntity)
            : base(accountOnBehalfOfPermissionsRequirement)
        {
            _isAllowedOnEntity = isAllowedOnEntity;
        }

        protected override IEnumerable<User> GetOwners(TestablePermissionsEntity entity)
        {
            return entity != null ? entity.Owners : Enumerable.Empty<User>();
        }

        protected override PermissionsCheckResult CheckPermissionsForEntity(User account, TestablePermissionsEntity entity)
        {
            return _isAllowedOnEntity(account, entity);
        }
    }
}
