# NuGet.Services.FeatureFlags

Enables dynamic toggling of features. These features are controlled by the NuGet.org admin panel.

Examples to use this library:

```csharp
IFeatureFlagClient features = ...;
IFeatureFlagUser currentUser = ...;

if (features.Enabled("NuGetGallery.TyposquattingDetection"))
{
    ...
}

if (flights.Enabled("NuGetGallery.TyposquattingDetection", currentUser))
{
    ...
}
```
