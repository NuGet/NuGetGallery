## NuGet.Services.Testing.Entities

**Subsystem: Unit testing ✅**

This library provides infrastructure to unit test Entity Framework entities.

Example usage:

```csharp
Mock<IValidationEntitiesContext> validationContextMock = ...;

validationContextMock.Mock(
    packageValidations: new List<PackageValidation>
    {
        new PackageValidation { ... }
    });

var validationContext = validationContextMock.Object;

Assert.Single(validationContext.PackageValidations);
```