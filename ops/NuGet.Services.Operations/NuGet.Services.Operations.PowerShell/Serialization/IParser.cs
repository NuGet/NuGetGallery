using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public interface IParser<out T> where T : NuOpsComponentBase
    {
        T Parse(XElement element);
    }
}
