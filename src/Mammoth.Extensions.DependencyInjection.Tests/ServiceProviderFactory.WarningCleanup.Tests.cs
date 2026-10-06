using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ServiceProviderFactoryWarningCleanupTests
{
    public static IEnumerable<object[]> WarningFailureCases()
    {
        foreach (var diagnostics in new[] { false, true })
        foreach (var failure in new[] { "resolve", "create", "enabled", "log" })
        foreach (var disposal in new[] { "sync", "async", "dual" })
        foreach (var disposeAsync in new[] { false, true })
        {
            if (disposal != "async" || disposeAsync)
                yield return [diagnostics, failure, disposal, disposeAsync];
        }
    }

    [TestMethod]
    [DynamicData(nameof(WarningFailureCases))]
    public async Task WarningFailureReturnsProviderAndRetainsNativeOwnership(
        bool diagnostics, string failure, string disposal, bool disposeAsync)
    {
        var resource = CreateResource(disposal);
        var callerOwned = new SyncResource();
        var expected = new InvalidOperationException("warning " + failure + " failed");
        var factory = new RecordingLoggerFactory(failure, expected);
        var services = CreateServices();
        var activations = 0;
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<Resource>(_ => resource);
        services.AddKeyedSingleton<Resource>("caller", callerOwned);
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            activations++;
            provider.GetRequiredService<Resource>();
            provider.GetRequiredKeyedService<Resource>("caller");
            if (failure == "resolve")
                throw expected;
            return factory;
        });

        var provider = diagnostics ? Build(services) : services.BuildServiceProvider();
        try
        {
            if (!diagnostics)
            {
                var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("control");
                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning("control");
                });
                Assert.AreSame(expected, actual);
            }
            Assert.AreEqual(1, activations);
            Assert.AreEqual(failure == "resolve" ? 0 : 1, factory.CreateCalls);
            Assert.AreEqual(failure is "enabled" or "log" ? 1 : 0, factory.Logger.EnabledCalls);
            Assert.AreEqual(failure == "log" ? 1 : 0, factory.Logger.LogCalls);
            Assert.AreEqual(0, resource.Disposals);
            Assert.AreEqual(0, factory.Disposals);
            Assert.IsNotNull(provider.GetRequiredService<ServiceTypes>());
        }
        finally
        {
            if (disposeAsync)
                await provider.DisposeAsync();
            else
                provider.Dispose();
        }

        Assert.AreEqual(1, resource.Disposals);
        Assert.AreEqual(disposeAsync && disposal != "sync" ? 1 : 0, resource.AsyncDisposals);
        Assert.AreEqual(failure == "resolve" ? 0 : 1, factory.Disposals);
        Assert.AreEqual(0, callerOwned.Disposals);
        // The returned provider retains native idempotent disposal behavior.
        await provider.DisposeAsync();
        provider.Dispose();
        Assert.AreEqual(1, resource.Disposals);
        Assert.AreEqual(failure == "resolve" ? 0 : 1, factory.Disposals);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StandardLoggingEmitsKeyedAndUnkeyedWarningsWithoutActivatingScopedLogger(bool validateOnBuild)
    {
        var services = CreateServices();
        services.AddKeyedTransient(typeof(OpenResource<>), "key");
        services.AddLogging(builder => builder.AddFakeLogging());
        var logger = new AsyncLogger();
        var loggerActivations = 0;
        services.AddScoped<ILogger<ServiceProviderFactory>>(_ =>
        {
            loggerActivations++;
            return logger;
        });
        var options = CreateOptions();
        options.ValidateOnBuild = validateOnBuild;

        await using var provider = Build(services, options);
        var records = provider.GetFakeLogCollector().GetSnapshot();
        Assert.AreEqual(2, records.Count);
        Assert.AreEqual(0, loggerActivations);
        Assert.AreEqual(0, logger.Disposals);
        foreach (var record in records)
        {
            Assert.AreEqual(typeof(ServiceProviderFactory).FullName, record.Category);
            Assert.AreEqual(LogLevel.Warning, record.Level);
            Assert.AreEqual(1, record.Id.Id);
            StringAssert.Contains(record.Message, "ServiceType: " + typeof(OpenResource<>));
            StringAssert.Contains(record.Message, "ImplementationType: " + typeof(OpenResource<>));
        }
        StringAssert.Contains(records[0].Message, "ServiceKey: (null)");
        StringAssert.Contains(records[1].Message, "ServiceKey: key");

        // The custom scoped registration remains usable by its actual consumer.
        await using (var scope = provider.CreateAsyncScope())
            Assert.AreSame(logger, scope.ServiceProvider.GetRequiredService<ILogger<ServiceProviderFactory>>());
        Assert.AreEqual(1, loggerActivations);
        Assert.AreEqual(1, logger.Disposals);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingLoggerFactoryDoesNotActivateDirectLogger(bool throwFromFactory)
    {
        var services = CreateServices();
        var logger = new AsyncLogger();
        var activations = 0;
        services.AddScoped<ILogger<ServiceProviderFactory>>(_ =>
        {
            activations++;
            if (throwFromFactory)
                throw new InvalidOperationException("direct logger must not be activated");
            return logger;
        });

        await using var provider = Build(services);
        Assert.AreEqual(0, activations);
        Assert.AreEqual(0, logger.Disposals);
        Assert.IsNotNull(provider.GetRequiredService<ServiceTypes>());
    }

    [TestMethod]
    [DataRow("disabled")]
    [DataRow("no-options")]
    [DataRow("no-warning")]
    [DataRow("excluded")]
    [DataRow("strict")]
    public void WarningControlsDoNotActivateLoggerFactory(string mode)
    {
        var services = CreateServices();
        var activations = 0;
        services.AddSingleton<ILoggerFactory>(_ =>
        {
            activations++;
            throw new InvalidOperationException("logger factory must not be activated");
        });
        var options = CreateOptions();
        if (mode == "disabled")
            options.DetectIncorrectUsageOfTransientDisposables = false;
        if (mode == "no-warning")
            services.Remove(services.First(descriptor => descriptor.ServiceType == typeof(OpenResource<>)));
        if (mode == "excluded")
            options.DetectIncorrectUsageOfTransientDisposablesExclusionPatterns =
                ["^" + Regex.Escape(typeof(OpenResource<>).FullName!) + "$"];
        if (mode == "strict")
        {
            options.ThrowOnOpenGenericTransientDisposable = true;
            var error = Assert.ThrowsExactly<InvalidOperationException>(() => Build(services, options));
            StringAssert.Contains(error.Message, "Trying to register an open generic transient disposable service");
        }
        else
        {
            using var provider = mode == "no-options"
                ? ServiceProviderFactory.CreateServiceProvider(services)
                : Build(services, options);
        }
        Assert.AreEqual(0, activations);
    }

    [TestMethod]
    public void BuildValidationFailureStillPropagates()
    {
        var services = CreateServices();
        services.AddSingleton<MissingDependency>();
        var options = CreateOptions();
        options.ValidateOnBuild = true;

        var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, options));
        StringAssert.Contains(error.ToString(), nameof(IMissing));
    }

    [TestMethod]
    public void LoggerFactoryAndCallerInstancesRetainTheirOriginalOwnership()
    {
        var factory = new RecordingLoggerFactory();
        var services = CreateServices();
        services.AddSingleton<ILoggerFactory>(factory);
        using (var provider = Build(services))
        {
            Assert.AreEqual(1, factory.CreateCalls);
            Assert.AreEqual(1, factory.Logger.Warnings);
            Assert.AreEqual(0, factory.Disposals);
        }
        Assert.AreEqual(0, factory.Disposals);
    }

    [TestMethod]
    public void WarningFailureDoesNotSwallowLaterProviderDisposalFailure()
    {
        var expected = new InvalidOperationException("resource disposal failed");
        var resource = new ThrowingResource(expected);
        var services = CreateServices();
        services.AddSingleton(_ => resource);
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            provider.GetRequiredService<ThrowingResource>();
            throw new InvalidOperationException("logger factory failed");
        });
        var provider = Build(services);

        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(provider.Dispose));
        Assert.AreEqual(1, resource.Disposals);
    }

    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(OpenResource<>));
        // Native controls need the support type supplied explicitly.
        services.AddSingleton(new ServiceTypes());
        return services;
    }

    private static ExtendedServiceProviderOptions CreateOptions() => new()
    {
        DetectIncorrectUsageOfTransientDisposables = true,
        ThrowOnOpenGenericTransientDisposable = false,
        ValidateOnBuild = false,
        ValidateScopes = true
    };

    private static ServiceProvider Build(IServiceCollection services, ExtendedServiceProviderOptions? options = null) =>
        ServiceProviderFactory.CreateServiceProvider(services, options ?? CreateOptions());

    private static Resource CreateResource(string disposal) => disposal switch
    {
        "sync" => new SyncResource(),
        "async" => new AsyncResource(),
        _ => new DualResource()
    };

    public sealed class OpenResource<T> : IDisposable { public void Dispose() { } }
    public interface IMissing;
    public sealed class MissingDependency(IMissing missing) { public IMissing Missing { get; } = missing; }
    public abstract class Resource
    {
        public int Disposals;
        public int AsyncDisposals;
    }
    public sealed class SyncResource : Resource, IDisposable
    {
        public void Dispose() => Disposals++;
    }
    public sealed class AsyncResource : Resource, IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Disposals++;
            AsyncDisposals++;
        }
    }
    public sealed class DualResource : Resource, IDisposable, IAsyncDisposable
    {
        public void Dispose() => Disposals++;
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Disposals++;
            AsyncDisposals++;
        }
    }
    public sealed class ThrowingResource(Exception error) : IDisposable
    {
        public int Disposals;
        public void Dispose() { Disposals++; throw error; }
    }

    public class RecordingLogger(string? failure = null, Exception? error = null) : ILogger<ServiceProviderFactory>
    {
        public int Warnings;
        public int EnabledCalls;
        public int LogCalls;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel)
        {
            EnabledCalls++;
            if (failure == "enabled")
                throw error!;
            return true;
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogCalls++;
            if (failure == "log")
                throw error!;
            Warnings++;
        }
    }
    public sealed class AsyncLogger : RecordingLogger, IAsyncDisposable
    {
        public int Disposals;
        public async ValueTask DisposeAsync() { await Task.Yield(); Disposals++; }
    }
    public sealed class RecordingLoggerFactory(string? failure = null, Exception? error = null) : ILoggerFactory
    {
        public readonly RecordingLogger Logger = new(failure, error);
        public int CreateCalls;
        public int Disposals;
        public ILogger CreateLogger(string categoryName)
        {
            CreateCalls++;
            if (failure == "create")
                throw error!;
            return Logger;
        }
        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();
        public void Dispose() => Disposals++;
    }
}
