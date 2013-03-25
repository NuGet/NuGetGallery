using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading;
using System.Web;

namespace NuGetGallery.Data
{
    public class DbModelManager : IDbModelManager
    {
        private readonly object _syncLock = new object();
        private DbCompiledModel _model;

        public IDbModelFactory ModelFactory { get; protected set; }

        protected DbModelManager()
        {
        }

        public DbModelManager(IDbModelFactory modelFactory)
        {
            ModelFactory = modelFactory;
        }

        public DbCompiledModel GetCurrentModel()
        {
            if (_model == null)
            {
                // Lock in order to safely construct a model once
                lock (_syncLock)
                {
                    // Ye olde double-check locking
                    if (_model == null)
                    {
                        _model = ModelFactory.CreateModel();
                    }
                }
            }
            return _model;
        }

        public void ReplaceCurrentModel(DbCompiledModel model)
        {
            // Atomically replace the current model with the new one
            Interlocked.Exchange(ref _model, model);
        }
    }
}