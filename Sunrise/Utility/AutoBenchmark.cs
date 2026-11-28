using System;
using System.Diagnostics;

namespace Sunrise.Utility;

internal class AutoBenchmark(string targetName, float reportInterval = 10) : IDisposable
{
    readonly Stopwatch _consumedTime = new();
    readonly Stopwatch _totalTime = new();
    int _count;

    static bool Enabled => Config.Instance.Benchmark;

    public IDisposable? Start()
    {
        if (!Enabled || _consumedTime.IsRunning)
            return null;

        _count++;
        _totalTime.Start();
        _consumedTime.Start();

        return this;
    }

    public void Stop()
    {
        if (!_consumedTime.IsRunning)
            return;

        _consumedTime.Stop();

        if (_totalTime.Elapsed.TotalSeconds >= reportInterval)
            Report();
    }

    void Report()
    {
        double seconds = _totalTime.Elapsed.TotalSeconds;

        TimeSpan consumed = _consumedTime.Elapsed;

        Log.Info($"Time consumed by {targetName} in last {seconds:F2}s: {consumed.TotalMilliseconds:F2} ms ({consumed.TotalSeconds / seconds:P2}). Calls: {_count}.");

        _consumedTime.Reset();
        _totalTime.Restart();
        _count = 0;
    }

    void IDisposable.Dispose() => Stop();
}
