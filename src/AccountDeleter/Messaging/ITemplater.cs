using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public interface ITemplater
    {
        bool AddReplacement(string toReplace, string replaceWith);

        string FillTemplate(string template);
    }
}
