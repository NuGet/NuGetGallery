// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Autofac.Builder;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Adds type registration parameter that instructs Autofac to use keyed resolution for a certain constructor parameter type.
        /// </summary>
        /// <param name="registrationBuilder">Registration builder object returned by <see cref="Autofac.RegistrationExtensions.RegisterType{TImplementer}(ContainerBuilder)"/> call</param>
        /// <param name="parameterType">The type of the constructor parameter to use keyed resolution for.</param>
        /// <param name="key">The key to use</param>
        /// <returns>Registration builder object passed as <paramref name="registrationBuilder"/> to support fluent API</returns>
        public static IRegistrationBuilder<TLimit, TReflectionActivatorData, TStyle> 
            WithKeyedParameter<TLimit, TReflectionActivatorData, TStyle>(this IRegistrationBuilder<TLimit, TReflectionActivatorData, TStyle> registrationBuilder, Type parameterType, string key)
            where TReflectionActivatorData : ReflectionActivatorData
        {
            return registrationBuilder.WithParameter(
                (pi, ctx) => pi.ParameterType == parameterType,
                (pi, ctx) => ctx.ResolveKeyed(key, parameterType));
        }

        /// <summary>
        /// Registers type with one keyed parameter
        /// </summary>
        /// <typeparam name="TService">The type to register as</typeparam>
        /// <typeparam name="TImplementer">The implementation to register</typeparam>
        /// <typeparam name="TKeyedParameter">Keyed parameter type</typeparam>
        /// <param name="containerBuilder"><see cref="ContainerBuilder"/> instance</param>
        /// <param name="parameterKey">The key to use for keyed parameter</param>
        /// <returns>Registration builder object</returns>
        public static IRegistrationBuilder<TImplementer, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            RegisterTypeWithKeyedParameter<TService, TImplementer, TKeyedParameter>(this ContainerBuilder containerBuilder, string parameterKey)
        {
            return containerBuilder
                .RegisterType<TImplementer>()
                .WithKeyedParameter(typeof(TKeyedParameter), parameterKey)
                .As<TService>();
        }

        /// <summary>
        /// Registers *keyed* type with one keyed parameter
        /// </summary>
        /// <typeparam name="TService">The type to register as</typeparam>
        /// <typeparam name="TImplementer">The implementation to register</typeparam>
        /// <typeparam name="TKeyedParameter">Keyed parameter type</typeparam>
        /// <param name="containerBuilder"><see cref="ContainerBuilder"/> instance</param>
        /// <param name="typeKey">The key to register type with.</param>
        /// <param name="parameterKey">The key to use for keyed parameter</param>
        /// <returns>Registration builder object</returns>
        public static IRegistrationBuilder<TImplementer, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            RegisterKeyedTypeWithKeyedParameter<TService, TImplementer, TKeyedParameter>(this ContainerBuilder containerBuilder, string typeKey, string parameterKey)
        {
            return containerBuilder
                .RegisterType<TImplementer>()
                .WithKeyedParameter(typeof(TKeyedParameter), parameterKey)
                .Keyed<TService>(typeKey);
        }
    }
}
