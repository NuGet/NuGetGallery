using Microsoft.Extensions.Options;
using System;

namespace NuGetGallery.AccountDeleter.Evaluators
{
    public class UserEvaluatorFactory : IUserEvaluatorFactory
    {
        private IOptionsSnapshot<AccountDeleteConfiguration> _options; 

        public UserEvaluatorFactory(IOptionsSnapshot<AccountDeleteConfiguration> options)
        {
            _options = options ?? throw new ArgumentException(nameof(options));
        }

        public IUserEvaluator GetEvaluatorForSource(string source)
        {
            throw new NotImplementedException();
        }
    }
}
