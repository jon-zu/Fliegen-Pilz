using System.Diagnostics;

namespace FliegenPilz.Util;

/// <summary>Value type capturing elapsed time in simulation ticks (milliseconds granularity).</summary>
public readonly struct Ticks : IComparable<Ticks>, IEquatable<Ticks>
{
    /// <summary>Underlying elapsed milliseconds.</summary>
    public ulong Milliseconds { get; }

    public Ticks(ulong milliseconds) => Milliseconds = milliseconds;

    public static Ticks Zero => new(0);

    public static Ticks FromMilliseconds(ulong milliseconds) => new(milliseconds);

    public static Ticks FromTimeSpan(TimeSpan span) => new((ulong)Math.Max(0, span.TotalMilliseconds));

    public TimeSpan ToTimeSpan() => TimeSpan.FromMilliseconds(Milliseconds);

    public static Ticks operator +(Ticks left, Ticks right) => new(left.Milliseconds + right.Milliseconds);

    public static Ticks operator +(Ticks left, ulong milliseconds) => new(left.Milliseconds + milliseconds);

    public static Ticks operator -(Ticks left, Ticks right) => left.Milliseconds >= right.Milliseconds
        ? new(left.Milliseconds - right.Milliseconds)
        : new(0);

    public static ulong operator -(Ticks left, ulong milliseconds) =>
        left.Milliseconds >= milliseconds ? left.Milliseconds - milliseconds : 0;

    public static bool operator >(Ticks left, Ticks right) => left.Milliseconds > right.Milliseconds;
    public static bool operator >=(Ticks left, Ticks right) => left.Milliseconds >= right.Milliseconds;
    public static bool operator <(Ticks left, Ticks right) => left.Milliseconds < right.Milliseconds;
    public static bool operator <=(Ticks left, Ticks right) => left.Milliseconds <= right.Milliseconds;

    public int CompareTo(Ticks other) => Milliseconds.CompareTo(other.Milliseconds);

    public bool Equals(Ticks other) => Milliseconds == other.Milliseconds;

    public override bool Equals(object? obj) => obj is Ticks other && Equals(other);

    public override int GetHashCode() => Milliseconds.GetHashCode();

    public override string ToString() => $"{Milliseconds}ms";
}

/// <summary>Clock that keeps a monotonic view of elapsed ticks, backed by <see cref="Stopwatch"/>.</summary>
public sealed class GlobalClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>Current elapsed ticks (milliseconds granularity).</summary>
    public Ticks Now => new((ulong)_stopwatch.ElapsedMilliseconds);

    /// <summary>Returns the elapsed ticks at a future offset.</summary>
    public Ticks AdvanceBy(TimeSpan span) => Now + Ticks.FromTimeSpan(span);
}
