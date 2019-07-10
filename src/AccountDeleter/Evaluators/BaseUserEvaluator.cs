using System;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    public abstract class BaseUserEvaluator : IUserEvaluator
    {
        protected readonly Guid _id;

        public BaseUserEvaluator()
        {
            _id = Guid.NewGuid();
        }

        public string EvaluatorId
        {
            get
            {
                return _id.ToString();
            }
        }

        public abstract bool CanUserBeDeleted(User user);
    }
}
