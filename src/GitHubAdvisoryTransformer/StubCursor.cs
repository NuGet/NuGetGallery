using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace GitHubAdvisoryTransformer.Cursor {
    public class StubCursor {
    }

    public class ReadWriteCursor<DateTimeOffset>
    {
        public DateTimeOffset Value { get; set; }

        public Task Load(CancellationToken token) {
            return Task.CompletedTask;
        }

        public Task Save(CancellationToken token) {
            return Task.CompletedTask;
        }
    }

}
