using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGetGallery.Operations
{
    [InheritedExport]
    public interface ICommand
    {
        CommandAttribute CommandAttribute { get; }

        IList<string> Arguments { get; }

        void Execute();
    }
}
