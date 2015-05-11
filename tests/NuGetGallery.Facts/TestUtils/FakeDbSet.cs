// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NuGetGallery
{
    public class FakeDbSet<T> : IDbSet<T> where T : class
    {
        FakeEntitiesContext _fakeEntitiesContext;
        ObservableCollection<T> _data;
        IQueryable<T> _queryable;
        Dictionary<PropertyInfo, Action<object>> _collectionPropertyMutators;

        public FakeDbSet(FakeEntitiesContext fakeEntitiesContext)
        {
            _fakeEntitiesContext = fakeEntitiesContext;
            _data = new ObservableCollection<T>();
            _queryable = new EnumerableQuery<T>(_data);
            _collectionPropertyMutators = new Dictionary<PropertyInfo, Action<object>>();

            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                if (property.CanRead && property.CanWrite && property.PropertyType.IsGenericType)
                {
                    var genericType = property.PropertyType.GetGenericTypeDefinition();
                    if (genericType == typeof(ICollection<>))
                    {
                        _collectionPropertyMutators.Add(property, GetMutator(property));
                    }
                }
            }
        }

        public Action<object> GetMutator(PropertyInfo property)
        {
            var methodInfo = typeof(FakeDbSet<T>).GetMethod(
                "GetMutator2",
                BindingFlags.Static | BindingFlags.NonPublic);
            methodInfo = methodInfo.MakeGenericMethod(property.PropertyType.GetGenericArguments()[0]);
            return (Action<object>)methodInfo.Invoke(null, new object[] { property, _fakeEntitiesContext });
        }

        static Action<object> GetMutator2<TElement>(PropertyInfo property, FakeEntitiesContext fakeContext)
            where TElement : class
        {
            return obj =>
            {
                var originalCollection = (ICollection<TElement>)property.GetValue(obj);
                if (originalCollection == null)
                {
                    originalCollection = new Collection<TElement>();
                }

                var mutatedCollection = new ObservableCollection<TElement>(originalCollection);
                mutatedCollection.CollectionChanged +=
                    new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
                    (col, args) =>
                    {
                        var set = fakeContext.Set<TElement>();
                        if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                        {
                            foreach (var item in args.NewItems.Cast<TElement>())
                            {
                                set.Add(item);
                            }
                        }
                    });
                property.SetValue(obj, mutatedCollection);
            };
        }

        public virtual T Find(params object[] keyValues)
        {
            throw new NotImplementedException("Derive from FakeDbSet<T> and override Find");
        }

        public T Add(T item)
        {
            foreach (var kvp in _collectionPropertyMutators)
            {
                kvp.Value.Invoke(item);
            }

            _data.Add(item);
            return item;
        }

        public T Remove(T item)
        {
            _data.Remove(item);
            return item;
        }

        public T Attach(T item)
        {
            foreach (var kvp in _collectionPropertyMutators)
            {
                kvp.Value.Invoke(item);
            }

            _data.Add(item);
            return item;
        }

        public T Detach(T item)
        {
            _data.Remove(item);
            return item;
        }

        public T Create()
        {
            return Activator.CreateInstance<T>();
        }

        public TDerivedEntity Create<TDerivedEntity>() where TDerivedEntity : class, T
        {
            return Activator.CreateInstance<TDerivedEntity>();
        }

        public ObservableCollection<T> Local
        {
            get { return _data; }
        }

        public Type ElementType
        {
            get { return typeof(T); }
        }

        public Expression Expression
        {
            get { return _queryable.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return _queryable.Provider; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _data.GetEnumerator();
        }
    }
}
