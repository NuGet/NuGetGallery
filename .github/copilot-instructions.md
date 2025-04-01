# Instructions for AIs

This repository is NuGet Gallery.

## Target Frameworks

This repository targets multiple frameworks:
* `.NET Framework 4.7.2`
* `.NET Standard 2.1`
* `.NET Standard 2.0`

Consider these characteristics when generating or modifying code.

## Nullable Reference Types

When opting C# code into nullable reference types:

* Only make the following changes when asked to do so.

* Add `#nullable enable` at the top of the file.

* Don't *ever* use `!` to handle `null`!

* Declare variables non-nullable, and check for `null` at entry points.

* Use `throw new ArgumentNullException(nameof(parameter))` in `.NET Framework` and `.NET Standard` projects.

## Formatting

C# code uses tabs (not spaces) and the following code-formatting style:

* Your mission is to make diffs as absolutely as small as possible, preserving existing code formatting.

* If you encounter additional spaces or formatting within existing code blocks, LEAVE THEM AS-IS.

* If you encounter code comments, LEAVE THEM AS-IS.

* Place a space prior to any parentheses `(` or `[`

* Use `""` for empty string and *not* `string.Empty`

* Use `[]` for empty arrays and *not* `Array.Empty<T>()`

Examples of properly formatted code:

Examples of properly formatted code:

```csharp
Foo ();
Bar (1, 2, "test");
myarray [0] = 1;

if (someValue) {
    // Code here
}

try {
    // Code here
} catch (Exception e) {
    // Code here
}
```

## File Header

Source files should include the following copyright header:

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

## Database Migrations

When creating or modifying database migrations:

* Use EntityFramework migration patterns.
* Ensure proper Up() and Down() implementations.
* Use appropriate data types and constraints.

## URL Handling

For URL handling:

* Convert HTTP URLs to HTTPS where appropriate.
* Preserve query parameters and paths when modifying URLs.
* Follow the URL normalization patterns in the codebase.

## Testing

When adding new functionality:

* Use xUnit when adding new tests.
* Add appropriate unit tests in the corresponding Facts project.
* Follow the existing test patterns and naming conventions.
* Use theories with inline data for testing multiple scenarios.
