using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;

namespace NuGetGallery.Diagnostics
{
    public class MockDiagnosticsService : IDiagnosticsService
    {
        private ConcurrentDictionary<string, IDiagnosticsSource> _sources = new ConcurrentDictionary<string, IDiagnosticsSource>();

        public IDiagnosticsSource GetSource(string name)
        {
            return _sources.GetOrAdd(name, str => new Mock<IDiagnosticsSource>().Object);
        }
    }
}
