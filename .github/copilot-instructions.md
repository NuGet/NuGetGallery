# Instructions for AIs

This repository is NuGet Gallery.

## Target Frameworks

This repository targets multiple C# frameworks:
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

### Indentation and Whitespace

* C# code uses tabs (not spaces) for indentation.
* Your mission is to make diffs as absolutely as small as possible, preserving existing code formatting.
* If you encounter additional spaces or formatting within existing code blocks, LEAVE THEM AS-IS.
* If you encounter code comments, LEAVE THEM AS-IS.

### Brackets and Spaces

* Place a space prior to any parentheses `(`
* Opening braces { should be on a new line after the control statement
* Closing braces } should be on their own line aligned with the control statement


### Example Code Style

Examples of properly formatted code:

```csharp
Foo ();
Bar (1, 2, "test");
myarray [0] = 1;

if (someValue)
{
    // Code here
}

try
{
    // Code here
}
catch (Exception e) {
    // Code here
}
```

## File Header

Source files should include the following copyright header:

// Copyright (c) .NET Foundation. All rights reserved.  
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

## Asynchronous Programming

* Use async/await pattern consistently.
* Ensure method names end with "Async" when they return Task or Task<T>.
* Never use .Result or .Wait() on Tasks - this can lead to deadlocks.
* Pass CancellationToken parameters when appropriate.
* Use ConfigureAwait(false) when appropriate in library code.

## Exception Handling

* Use specific exception types rather than generic Exception if possible.
* Include meaningful exception messages.
* Validate parameters at the beginning of methods.
* Avoid swallowing exceptions without proper logging.

## Database Migrations

When creating or modifying database migrations:

* Use EntityFramework migration patterns.
* Ensure proper Up() and Down() implementations.
* Use appropriate data types and constraints.
* Don't modify existing migrations after they've been deployed.
* Create new migrations for changes to the schema.

## URL Handling

For URL handling:

* Convert HTTP URLs to HTTPS where appropriate.
* Preserve query parameters and paths when modifying URLs.
* Follow the URL normalization patterns in the codebase.
* Use `PackageHelper.TryPrepareUrlForRendering()` for sanitizing URLs.
* Follow security best practices for URL validation.

## Logging and Telemetry

* Use the appropriate logging mechanism for the component:
  * Use `ITelemetryService` for application telemetry.
  * Include relevant context in log entries.
* Avoid logging sensitive information.
* Use appropriate log levels based on severity.

## Testing

When adding new functionality:

* Use xUnit when adding new tests.
* Add appropriate unit tests in the corresponding Facts project.
* Follow the existing test patterns and naming conventions.
* Use theories with inline data for testing multiple scenarios.
* Mock external dependencies when appropriate.
* Test both success and error paths.

## Security Best Practices

* Always validate user input.
* Use proper CSRF/XSRF protection with anti-forgery tokens.
* Follow secure coding practices to prevent injection attacks.
* Use proper encoding for output displayed to users.
* Never store sensitive information in client-side code.

## JavaScript and Frontend

* JavaScript uses standard ES5 syntax for compatibility.
* When working with JavaScript:
  * Use 'use strict' directive
  * Follow existing patterns for DOM manipulation
  * Properly handle browser compatibility issues
* Follow accessibility best practices (WCAG) in UI components.

## Documentation

* Keep code documentation up-to-date.
* Document public APIs with XML comments.
* Include examples in documentation where helpful.
* Use Markdown for text formatting in comments and documentation.
