# Mammoth.Extensions.DependencyInjection

## vNext

- AssemblyInspector: added Configure() method [#2](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/2).

## Breaking Changes

AssemblyInspector lifestyle selectors signature does not accepts "dependsOn" anymore, use the new Configure method instead:

```csharp
var descriptors = new AssemblyInspector()
	.FromAssemblyContaining<TestService>()
	.BasedOn(typeof(TestService))
	.WithServiceBase()
	.LifestyleTransient(dependsOn: new Dependency[]
	{
		Parameter.ForKey("param").Eq("nonexisting")
	});
```

becomes:

```csharp
var descriptors = new AssemblyInspector()
	.FromAssemblyContaining<TestService>()
	.BasedOn(typeof(TestService))
	.WithServiceBase()
	.Configure(cfg => cfg.DependsOn = new Dependency[]
	{
		Parameter.ForKey("param").Eq("nonexisting")
	})
	.LifestyleTransient();
```

## 0.1.2

- Fixed namespaces.

## 0.1.1

- NuGet Package and Github Actions (build, test, publish)

## 0.1.0

Initial Release

- Decorator: support registrering decorator pattern.
- DependsOn: register specific dependencies for constructor parameters.
- Registration Checks: allows to check if a service was registred in ServiceCollection and ServiceProvider
- AssemblyInspector: allows to inspect assemblies for types and register them easily.