using BenchmarkDotNet.Running;
using ZeroAlloc.ValueObjects.Benchmarks;

BenchmarkRunner.Run<ValueObjectBenchmarks>(args: args);
