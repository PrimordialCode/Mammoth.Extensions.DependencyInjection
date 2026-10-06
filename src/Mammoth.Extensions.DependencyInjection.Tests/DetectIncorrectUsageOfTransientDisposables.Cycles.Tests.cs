using System.Diagnostics;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DetectIncorrectUsageOfTransientDisposablesCycleTests
{
    [TestMethod]
    [DataRow("native", "self")]
    [DataRow("native", "two")]
    [DataRow("native", "long")]
    [DataRow("native", "keyed")]
    [DataRow("native", "open-generic")]
    [DataRow("native", "mixed-generic")]
    [DataRow("disabled", "self")]
    [DataRow("disabled", "two")]
    [DataRow("disabled", "long")]
    [DataRow("disabled", "keyed")]
    [DataRow("disabled", "open-generic")]
    [DataRow("disabled", "mixed-generic")]
    [DataRow("diagnostics", "self")]
    [DataRow("diagnostics", "two")]
    [DataRow("diagnostics", "long")]
    [DataRow("diagnostics", "keyed")]
    [DataRow("diagnostics", "open-generic")]
    [DataRow("diagnostics", "mixed-generic")]
    [DataRow("native", "enumerable")]
    [DataRow("native", "any-enumerable")]
    [DataRow("native", "keyed-builtin")]
    [DataRow("disabled", "enumerable")]
    [DataRow("disabled", "any-enumerable")]
    [DataRow("disabled", "keyed-builtin")]
    [DataRow("diagnostics", "enumerable")]
    [DataRow("diagnostics", "any-enumerable")]
    [DataRow("diagnostics", "keyed-builtin")]
    [DataRow("native", "disposable")]
    [DataRow("disabled", "disposable")]
    [DataRow("diagnostics", "disposable")]
    [DataRow("diagnostics", "decorator")]
    [DataRow("diagnostics", "singleton-scope")]
    [DataRow("native", "deep")]
    [DataRow("disabled", "deep")]
    [DataRow("diagnostics", "deep")]
    [DataRow("diagnostics", "deep-scoped")]
    [DataRow("diagnostics", "deep-singleton")]
    [DataRow("diagnostics", "deep-support-scoped")]
    [DataRow("diagnostics", "deep-valid")]
    [DataRow("native", "deep-valid")]
    [DataRow("disabled", "deep-valid")]
    [DataRow("diagnostics", "factory")]
    [DataRow("diagnostics", "any-key")]
    public async Task RuntimeCyclesFailInBoundedChildProcess(string mode, string scenario)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "CycleProbe");
        const string assembly = "Mammoth.Extensions.DependencyInjection.CycleProbe";
#if NETFRAMEWORK
        var fileName = Path.Combine(directory, assembly + ".exe");
        var arguments = mode + " " + scenario;
#else
        var fileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var arguments = "\"" + Path.Combine(directory, assembly + ".dll") + "\" " + mode + " " + scenario;
#endif
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        Assert.IsTrue(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            Assert.IsTrue(process.WaitForExit(15000), $"{mode}/{scenario}: child exceeded the 15-second cycle bound.");
            Assert.AreEqual(0, process.ExitCode, await errors);
            StringAssert.Contains(await output, "PASS " + mode + " " + scenario);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                Assert.IsTrue(process.WaitForExit(5000), "Cycle probe did not exit after termination.");
            }
        }
    }
}
