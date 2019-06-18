using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteTemplater : ITemplater
    {
        private IOptionsSnapshot<AccountDeleteConfiguration> _options;
        private Dictionary<string, string> _replacements;

        public AccountDeleteTemplater(IOptionsSnapshot<AccountDeleteConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _replacements = new Dictionary<string, string>();
        }

        public bool AddReplacement(string toReplace, string replaceWith)
        {
            _replacements.Add(toReplace, replaceWith);
            return true;
        }

        public string FillTemplate(string template)
        {
            var baseReplacements = _options.Value.TemplateReplacements;
            var output = template;
            foreach (var replacement in baseReplacements)
            {
                output = output.Replace(replacement.Key, replacement.Value);
            }

            foreach (var replacement in _replacements)
            {
                output = output.Replace(replacement.Key, replacement.Value);
            }

            return output;
        }
    }
}
