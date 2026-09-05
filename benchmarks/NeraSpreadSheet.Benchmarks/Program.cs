using BenchmarkDotNet.Running;
using NeraSpreadSheet.Benchmarks;

if (args.Length > 0 && args[0] == "--perf-008")
{
    await PERF008Harness.RunAsync(args.Skip(1).ToArray());
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
