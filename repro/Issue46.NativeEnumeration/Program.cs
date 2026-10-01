using Mammoth.DependencyInjection.Regression;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddScoped(typeof(IRepository<>), typeof(DefaultRepository<>));
services.AddSingleton<IRepository<string>, ClosedRepository>();
services.AddKeyedTransient(typeof(IRepository<>), "backup", typeof(BackupRepository<>));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;
try
{
    var before = sp.GetServices<IRepository<string>>().ToArray();
    var first = sp.GetKeyedServices<IRepository<string>>("backup").ToArray();
    var compilation = NativeResolverCompilation.Observe<IRepository<string>>(provider, "backup");
    var second = sp.GetKeyedServices<IRepository<string>>("backup").ToArray();
    compilation.WaitForReplacement();
    Console.WriteLine($"DI={typeof(ServiceProvider).Assembly.FullName}; framework={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; native accessor replacement observed");
    Require(before.Length == 2 && before[0] is DefaultRepository<string> && before[1] is ClosedRepository, "cold unkeyed membership");
    Require(first.Length == 1 && first[0] is BackupRepository<string> && second.Length == 1 && second[0] is BackupRepository<string>, "cold keyed membership");
    var previousKeyed = second[0];
    for (int i = 0; i < 100; i++)
    {
        var unkeyed = sp.GetServices<IRepository<string>>().ToArray();
        var keyed = sp.GetKeyedServices<IRepository<string>>("backup").ToArray();
        if (i == 0) Console.WriteLine($"hot unkeyed={string.Join(",",unkeyed.Select(x=>x.GetType().Name))}; keyed={string.Join(",",keyed.Select(x=>x.GetType().Name))}");
        Require(unkeyed.Length == 2 && unkeyed[0] is DefaultRepository<string> && unkeyed[1] is ClosedRepository, $"hot unkeyed membership iteration {i}");
        Require(keyed.Length == 1 && keyed[0] is BackupRepository<string>, $"hot keyed membership iteration {i}");
        Require(ReferenceEquals(before[0],unkeyed[0]) && ReferenceEquals(before[1],unkeyed[1]), "scoped/singleton reuse");
        Require(!ReferenceEquals(previousKeyed,keyed[0]), "transient creation");
        previousKeyed = keyed[0];
    }
    Console.WriteLine("PASS: 100 interleaved enumerations after observed native compilation.");
    return 0;
}
catch(Exception error)
{
    Console.WriteLine("FAIL: "+error);
    return 1;
}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
public interface IRepository<T> { }
public sealed class DefaultRepository<T> : IRepository<T> { }
public sealed class ClosedRepository : IRepository<string> { }
public sealed class BackupRepository<T> : IRepository<T> { }
