// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Moq;

namespace NuGetGallery.Framework
{
    internal class LooseMocksRegistrationSource
        : IRegistrationSource
    {
        private readonly bool _onlyInterfaces;
#pragma warning disable 0618
        private readonly MockFactory _repository;
#pragma warning restore 0618
        private readonly MethodInfo _createMethod;
        private readonly MethodInfo _createWithArgsMethod;

        public LooseMocksRegistrationSource(MockRepository repository, bool onlyInterfaces)
        {
            _onlyInterfaces = onlyInterfaces;
            _repository = repository;
            _createMethod = repository.GetType().GetMethod("Create", new Type[] { });
            _createWithArgsMethod = repository.GetType().GetMethod("Create", new [] { typeof(object[]) });
        }

        public IEnumerable<IComponentRegistration> RegistrationsFor(Service service, Func<Service, IEnumerable<IComponentRegistration>> registrationAccessor)
        {
            var swt = service as IServiceWithType;
            if (swt == null)
            {
                yield break;
            }

            if (_onlyInterfaces && !swt.ServiceType.IsInterface)
            {
                yield break;
            }

            var existingReg = registrationAccessor(service);
            if (existingReg.Any())
            {
                yield break;
            }

            var reg = RegistrationBuilder.ForDelegate((c, p) =>
            {
                var constructors = swt.ServiceType.GetConstructors();
                if (constructors.Any() && !constructors.Any(ctor => !ctor.GetParameters().Any()))
                {
                    var constructor = constructors.First();
                    List<object> parameterValues = new List<object>();
                    foreach (var parameterInfo in constructor.GetParameters())
                    {
                        parameterValues.Add(c.Resolve(parameterInfo.ParameterType));
                    }

                    var createMethodWithArgs = _createWithArgsMethod.MakeGenericMethod(swt.ServiceType);
                    var mockWithArgs = ((Mock)createMethodWithArgs.Invoke(_repository, new object[] { parameterValues.ToArray() }));
                    return mockWithArgs.Object;
                }

                var createMethod = _createMethod.MakeGenericMethod(swt.ServiceType);
                var mock = ((Mock)createMethod.Invoke(_repository, null));
                return mock.Object;
            }).As(swt.ServiceType).SingleInstance().CreateRegistration();

            yield return reg;
        }

        public bool IsAdapterForIndividualComponents
        {
            get { return false; }
        }
    }
}