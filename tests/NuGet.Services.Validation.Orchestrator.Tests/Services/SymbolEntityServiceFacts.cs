// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolEntityServiceFacts
    {
        public class TheFindPackageByIdAndVersionStrictMethod : FactsBase
        {
            public TheFindPackageByIdAndVersionStrictMethod(ITestOutputHelper output) : base(output) { }

            [Fact]
            public void FindPackageByIdAndVersionStrictReturnsTheValidatingSymbolPackage()
            {
                // Arrange
                string packageId = "Test";
                string packageVersion = "1.1.0";
                int packageKey = 1;

                Package p = new Package()
                {
                    NormalizedVersion = packageVersion,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Key = packageKey
                };

                List<SymbolPackage> testSymbols = new List<SymbolPackage>()
                {
                    new SymbolPackage()
                    {
                        Package = p,
                        PackageKey = p.Key,
                        Key = 1,
                        StatusKey = PackageStatus.Available
                    },
                     new SymbolPackage()
                    {
                       Package = p,
                        PackageKey = p.Key,
                        Key = 2,
                        StatusKey = PackageStatus.Validating
                    }
                };

                _coreSymbolPackageService.Setup(c => c.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).
                    Returns(testSymbols);

                // Act 
                var symbol = _target.FindPackageByIdAndVersionStrict(packageId, packageVersion);

                // Assert
                Assert.Equal(2, symbol.Key);
            }

            [Fact]
            public void FindPackageByIdAndVersionStrictReturnsNullIfNotAnySymbolsInValidatingState()
            {
                // Arrange
                string packageId = "Test";
                string packageVersion = "1.1.0";
                int packageKey = 1;

                Package p = new Package()
                {
                    NormalizedVersion = packageVersion,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Key = packageKey
                };

                List<SymbolPackage> testSymbols = new List<SymbolPackage>()
                {
                    new SymbolPackage()
                    {
                        Package = p,
                        PackageKey = p.Key,
                        Key = 1,
                        StatusKey = PackageStatus.Available
                    }
                };

                _coreSymbolPackageService.Setup(c => c.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).
                    Returns(testSymbols);

                // Act 
                var symbol = _target.FindPackageByIdAndVersionStrict(packageId, packageVersion);

                // Assert
                Assert.Null(symbol);
            }
        }

        public class TheUpdateStatusAsyncMethod : FactsBase
        {
            public TheUpdateStatusAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task UpdateStatusAsyncMethodNullCheck()
            {
                // Act + Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => _target.UpdateStatusAsync(null, PackageStatus.Validating));
            }

            [Fact]
            public void UpdateStatusAsyncMethodNoopWhenSameStatus()
            {
                // Arrange
                string packageId = "Test";
                string packageVersion = "1.1.0";
                int packageKey = 1;

                Package p = new Package()
                {
                    NormalizedVersion = packageVersion,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Key = packageKey
                };
                SymbolPackage s = new SymbolPackage()
                {
                    Package = p,
                    PackageKey = p.Key,
                    Key = 1,
                    StatusKey = PackageStatus.Available
                };
                List<SymbolPackage> testSymbols = new List<SymbolPackage>(){s};

                _coreSymbolPackageService.Setup(c => c.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).
                    Returns(testSymbols);

                // Act 
                var symbol = _target.UpdateStatusAsync(s, PackageStatus.Available);

                // Assert
                _coreSymbolPackageService.Verify(c => c.UpdateStatusAsync(It.IsAny<SymbolPackage>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task UpdateStatusAsyncMethodChangeToDeleteThePreviousAvailableState()
            {
                // Arrange
                string packageId = "Test";
                string packageVersion = "1.1.0";
                int packageKey = 1;

                Package p = new Package()
                {
                    NormalizedVersion = packageVersion,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Key = packageKey
                };
                SymbolPackage s1 = new SymbolPackage()
                {
                    Package = p,
                    PackageKey = p.Key,
                    Key = 1,
                    StatusKey = PackageStatus.Available
                };
                SymbolPackage s2 = new SymbolPackage()
                {
                    Package = p,
                    PackageKey = p.Key,
                    Key = 2,
                    StatusKey = PackageStatus.Validating
                };

                List<SymbolPackage> testSymbols = new List<SymbolPackage>(){ s1, s2 };

                _coreSymbolPackageService.Setup(c => c.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).
                    Returns(testSymbols);
                _coreSymbolPackageService.Setup(c => c.UpdateStatusAsync(It.Is<SymbolPackage>(symbol => symbol.Key == 1), PackageStatus.Deleted, false)).
                     Callback(
                     () => s1.StatusKey = PackageStatus.Deleted
                     ).
                     Returns(Task.FromResult(0));
                _coreSymbolPackageService.Setup(c => c.UpdateStatusAsync(It.Is<SymbolPackage>(symbol => symbol.Key == 2), PackageStatus.Available, true)).
                    Callback(
                    () => s2.StatusKey = PackageStatus.Available
                    ).
                    Returns(Task.FromResult(0));

                // Act 
                await _target.UpdateStatusAsync(s2,PackageStatus.Available);

                // Assert
                Assert.Equal(PackageStatus.Available, s2.StatusKey);
                Assert.Equal(PackageStatus.Deleted, s1.StatusKey);
            }

            [Fact]
            public async Task UpdateStatusAsyncMethodDoesNotUpdatePreviosAvailableStatusOnFailedValidation()
            {
                // Arrange
                string packageId = "Test";
                string packageVersion = "1.1.0";
                int packageKey = 1;

                Package p = new Package()
                {
                    NormalizedVersion = packageVersion,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Key = packageKey
                };
                SymbolPackage s1 = new SymbolPackage()
                {
                    Package = p,
                    PackageKey = p.Key,
                    Key = 1,
                    StatusKey = PackageStatus.Available
                };
                SymbolPackage s2 = new SymbolPackage()
                {
                    Package = p,
                    PackageKey = p.Key,
                    Key = 2,
                    StatusKey = PackageStatus.Validating
                };

                List<SymbolPackage> testSymbols = new List<SymbolPackage>() { s1, s2 };

                _coreSymbolPackageService.Setup(c => c.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).
                    Returns(testSymbols);
                _coreSymbolPackageService.Setup(c => c.UpdateStatusAsync(It.Is<SymbolPackage>(symbol => symbol.Key == 1), PackageStatus.Deleted, false)).
                    Callback(
                    () => s1.StatusKey = PackageStatus.Deleted
                    ).
                    Returns(Task.FromResult(0));
                _coreSymbolPackageService.Setup(c => c.UpdateStatusAsync(It.Is<SymbolPackage>(symbol => symbol.Key == 2), PackageStatus.FailedValidation, true)).
                    Callback(
                    () => s2.StatusKey = PackageStatus.FailedValidation
                    ).
                    Returns(Task.FromResult(0));

                // Act 
                await _target.UpdateStatusAsync(s2, PackageStatus.FailedValidation);

                // Assert
                Assert.Equal(PackageStatus.FailedValidation, s2.StatusKey);
                Assert.Equal(PackageStatus.Available, s1.StatusKey);
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<ICoreSymbolPackageService> _coreSymbolPackageService;
            protected readonly Mock<IEntityRepository<SymbolPackage>> _symbolEntityRepository;

            protected readonly SymbolEntityService _target;

            public FactsBase(ITestOutputHelper output)
            {
                _coreSymbolPackageService = new Mock<ICoreSymbolPackageService>();
                _symbolEntityRepository = new Mock<IEntityRepository<SymbolPackage>>();
                _target = new SymbolEntityService(_coreSymbolPackageService.Object, _symbolEntityRepository.Object);
            }
        }
    }
}
