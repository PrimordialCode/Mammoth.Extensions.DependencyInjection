# Contributing to Mammoth.Extensions.DependencyInjection

Thank you for your interest in contributing! This guide provides a quick overview. For detailed technical conventions and project context, see [`.github/copilot-instructions.md`](.github/copilot-instructions.md).

## Quick Start

1. **Fork and clone** the repository
2. **Read the instructions**: [`.github/copilot-instructions.md`](.github/copilot-instructions.md) covers code organization, conventions, and build commands
3. **Build locally**:
   ```powershell
   dotnet restore ./src/Mammoth.Extensions.DependencyInjection.sln
   dotnet build ./src/Mammoth.Extensions.DependencyInjection.sln
   dotnet test ./src/Mammoth.Extensions.DependencyInjection.sln
   ```
4. **Create a branch** for your feature or fix
5. **Make your changes** (see sections below)
6. **Run tests** and ensure all pass
7. **Open a Pull Request**

## Project Structure

- **`src/Mammoth.Extensions.DependencyInjection/`** — Main library (extension methods, decorators, keyed services)
- **`src/Mammoth.Extensions.DependencyInjection.Tests/`** — Unit tests (MSTest + coverlet)
- **`src/Directory.Build.props`** — Shared build configuration (frameworks, C# version, analyzers)
- **`Changelog.md`** — Release notes and breaking changes
- **`.github/copilot-instructions.md`** — Full technical context for development

## Making Changes

### Adding Extension Methods

1. Add to the appropriate partial class:
   - `ServiceCollectionExtensions.*.cs` for service registration
   - `ServiceProviderExtensions.*.cs` for provider queries/operations
2. Include XML documentation (`///`) with examples for all public methods
3. Follow fluent API pattern (return `IServiceCollection` for chaining where applicable)
4. Add corresponding tests in `*.Tests.cs` matching your source file name

### Adding Decorators or Keyed Service Features

- Refer to `ServiceCollectionExtensions.Decorators.cs` and `ServiceCollectionExtensions.KeyedService.cs`
- Ensure compatibility with transient, scoped, singleton, and factory registrations
- Target the library for `netstandard2.0`; run tests on `net472`, `net8.0`, `net9.0`, `net10.0`
- Use `ServiceIdentifier` to track keyed services uniquely

### Targeting Specific Frameworks

- Some features may not work on all frameworks (e.g., reflection features on `netstandard2.0`)
- Use `.Disabled.cs` file pattern for conditional compilation (see `ServiceProviderExtensions.Reflection.Disabled.cs`)
- Verify `Directory.Build.props` target framework constraints

## Code Conventions

- **C# 14.0** with nullable reference types enabled
- **Named parameters**: Use explicit parameter names in calls
- **Nullability**: Strict checking; mark nullable types with `?`
- **Analyzers**: Latest recommended rules enforced at build time
- **Tests**: Use `[TestMethod]` / `[TestClass]` (MSTest); leverage `TestServices.cs` for fixtures

## Testing

- **Run all tests**: `dotnet test ./src/Mammoth.Extensions.DependencyInjection.sln`
- **Run specific test class**: `dotnet test --filter ClassName=MyTestsClass`
- **With coverage**: `dotnet test --collect:"XPlat Code Coverage"`
- **Test files follow naming**: `ServiceCollectionExtensions.Decorators.Tests.cs` matches `ServiceCollectionExtensions.Decorators.cs`

## Documentation & Changelog

Every feature or fix should update **`Changelog.md`**:

1. Find the **`## vNext`** section at the top
2. Add a bullet point describing your change:
   ```markdown
   ## vNext
   
   - Added support for wrapping async factory registrations [#XX](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/XX).
   ```
3. Reference the issue number if applicable
4. If a **breaking change**, prefix with "### Breaking Changes" section

## Pull Request Checklist

- [ ] Code builds without warnings (`dotnet build --configuration Release`)
- [ ] All tests pass (`dotnet test`)
- [ ] New public APIs have XML documentation
- [ ] Changelog updated in `vNext` section
- [ ] Changes verified across target frameworks (at least build the library for `netstandard2.0` and run tests on `net472`/`net8.0+`)
- [ ] No transient disposables in new code (use `DetectIncorrectUsageOfTransientDisposables` diagnostic)

## Asking for Help

If you need guidance on architecture, decorators, keyed services, or multi-framework compatibility:
- Check [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for detailed patterns
- Review existing code in similar feature areas
- Open a discussion issue if you have design questions

## License

By contributing, you agree your work will be licensed under the **MIT License** (see `LICENSE` file).

---

**Questions?** See the [GitHub repository](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection) or open an issue.
