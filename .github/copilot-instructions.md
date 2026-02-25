# Copilot Workspace Instructions

## Project Overview

**Mammoth.Extensions.DependencyInjection** is a utility library providing advanced extensions for `Microsoft.Extensions.DependencyInjection` (v9.0.0+).

### Purpose

Extend the standard DI container with:
- **Decorator pattern** support for wrapping services (interface-based and class-based)
- **DependsOn** extensions to register services with explicit keyed dependencies
- **Keyed services** management utilities  
- **Service provider factory** with advanced registration tracking and resolution diagnostics

### Key Technologies

- **Language**: C# 14.0 with nullable reference types and implicit usings enabled
- **Target Frameworks**: `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`
- **Testing**: MSTest with coverlet code coverage
- **Versioning**: GitVersion (based on Git tags)
- **Packaging**: NuGet with source link support

---

## Build & Development

### Essential Commands

```powershell
# Restore and build
dotnet restore ./src/Mammoth.Extensions.DependencyInjection.sln
dotnet build ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release

# Run tests
dotnet test ./src/Mammoth.Extensions.DependencyInjection.sln

# Create NuGet package
dotnet pack ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --output ./artifacts
```

### Build Script

Use `build.ps1` for full CI pipeline (restore tools, versioning, build, test, pack).

### Development Workflow

1. Work in `src/` directory
2. Solution file: `src/Mammoth.Extensions.DependencyInjection.sln`
3. Main project: `src/Mammoth.Extensions.DependencyInjection/`
4. Tests: `src/Mammoth.Extensions.DependencyInjection.Tests/`
5. Common settings: `src/Directory.Build.props`

---

## Code Organization

### Main Library Structure

The library uses **partial classes** to organize extension methods by functionality:

| File | Purpose |
|------|---------|
| `ServiceCollectionExtensions.cs` | Base installer pattern |
| `ServiceCollectionExtensions.Decorators.cs` | Decorator registration methods |
| `ServiceCollectionExtensions.KeyedService.cs` | Keyed service helpers |
| `ServiceCollectionExtensions.Registration.cs` | Advanced registration utilities |
| `ServiceProviderExtensions.cs` | Service provider query methods |
| `ServiceProviderExtensions.Registration.cs` | Factory and registration tracking |
| `ServiceProviderExtensions.Reflection.cs` | Reflection-based utilities |

### Supporting Classes

- **`ServiceIdentifier`**: Uniquely identifies registered services
- **`ServiceDescriptorExtensions`**: Utilities for analyzing service descriptors
- **`TypeExtensions`**: Type inspection and filtering helpers
- **`ServiceProviderFactory`**: Advanced provider configuration
- **`DetectIncorrectUsageOfTransientDisposables`**: Diagnostic checks
- **`ResolutionContext`**: Tracks service resolution graph
- **`Configuration/` subfolder**: Configuration and diagnostic utilities
- **`Inspector/` subfolder**: Assembly inspection for type discovery

### Test Organization

Tests follow a 1:1 naming pattern with source files:

- `ServiceCollectionExtensions.Decorators.Tests.cs` → Tests for decorators
- `ServiceProviderExtensions.Resolution.Tests.cs` → Tests for resolution
- `ServiceProviderExtensions.Registration.Tests.cs` → Tests for registration
- `InjectAnArrayOfDependenciesTests.cs` → Feature-specific tests
- `TestServices.cs` → Shared test fixtures and utilities

---

## Code Conventions

### Style & Standards

- **XML Documentation**: All public types/methods must have `///` documentation
- **Nullability**: Strict null checking enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled to reduce boilerplate
- **Analysis**: Latest recommended .NET analyzers enabled with code style enforcement
- **Code Style**: Enforced during build (`<EnforceCodeStyleInBuild>True</EnforceCodeStyleInBuild>`)

### Naming Patterns

- Extension methods use `ServiceCollectionExtensions` and `ServiceProviderExtensions` partial classes
- Service identifiers use `ServiceIdentifier` struct for uniqueness
- Decorator factories prefix with `Decorate`
- Keyed service methods include `Keyed` in the name

### Common Patterns

1. **Fluent Extension Methods**: All service registration methods return the collection for chaining
2. **Generic Methods with Constraints**: Extensive use of generic constraints for type safety
3. **Factory Pattern**: Services often registered via factory delegates
4. **Descriptor Inspection**: Extension methods analyze `ServiceDescriptor` to apply transformations

---

## Testing Guidelines

### Test Framework

- **Framework**: MSTest (v4.0.2)
- **Coverage**: coverlet (v6.0.4)
- **Diagnostics**: Microsoft.Extensions.Diagnostics.Testing

### Test File Conventions

- Test files match source file names with `.Tests.cs` suffix
- Use `[TestMethod]` attribute
- Group related tests in `[TestClass]` classes
- Leverage `TestServices.cs` for common fixtures

### Running Tests

```powershell
# All tests
dotnet test ./src/Mammoth.Extensions.DependencyInjection.sln

# Specific test class
dotnet test --filter ClassName=ServiceCollectionExtensions.Decorators.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Architecture & Design Patterns

### Core Concepts

1. **Service Decoration**: Wraps existing service registrations with decorators without modifying the original
   - Works with transient, scoped, singleton, and keyed services
   - Supports interface-based and class-based decoration
   - Handles factory-registered services

2. **Keyed Services**: Built on .NET 8.0+ keyed service infrastructure
   - `DependsOn` extensions enable explicit keyed dependency registration
   - Service identifiers include both type and key information

3. **Reflection-Based Resolution**: Optional advanced diagnostics
   - `ServiceProviderExtensions.Reflection.cs` provides introspection capabilities
   - `ResolutionContextTrackingServiceProviderDecorator` tracks resolution chains (disabled by default)

### Common Pitfalls

- **Transient Disposables**: Use `DetectIncorrectUsageOfTransientDisposables` to catch services that shouldn't be transient
- **Circular Dependencies**: Decoration chains must not create cycles
- **Frame-level Compatibility**: Some reflection features require specific .NET versions (see `.Disabled.cs` files)

---

## Related Files & Resources

- **GitHub**: [PrimordialCode/Mammoth.Extensions.DependencyInjection](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection)
- **License**: MIT
- **Changelog**: `Changelog.md` - tracks features, breaking changes, and bug fixes
- **GitVersion Config**: `GitVersion.yml` - semantic versioning rules

---

## Tips for AI Assistants

### Before Making Changes

1. Check `Changelog.md` for recent breaking changes and version constraints
2. Verify target framework support in `Directory.Build.props`
3. Understand whether changes affect single or multiple target frameworks
4. Check if the feature is platform-specific (see `.Disabled.cs` files)

### When Adding Features

1. Update the appropriate partial class file based on functionality
2. Add XML documentation with examples
3. Create corresponding test file or add tests to existing one
4. Update `Changelog.md` under vNext section
5. Ensure changes work across all supported target frameworks

### Common Tasks

- **Add new extension method**: Add to the appropriate `ServiceCollectionExtensions.*.cs` or `ServiceProviderExtensions.*.cs` partial class
- **Add diagnostics**: Extend `DetectIncorrectUsageOfTransientDisposables` or configuration utilities
- **Enable reflection features**: Modify condition checks in `ServiceProviderExtensions.Reflection.cs`
- **Update supported versions**: Modify `Directory.Build.props` and adjust compatibility checks

