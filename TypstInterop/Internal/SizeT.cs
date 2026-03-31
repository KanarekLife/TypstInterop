namespace TypstInterop.Internal;

internal struct size_t
{
    public nuint Value;

    public static implicit operator nuint(size_t s) => s.Value;

    public static implicit operator size_t(nuint v) => new() { Value = v };

    public static implicit operator int(size_t s) => (int)s.Value;

    public static implicit operator size_t(int v) => new() { Value = (nuint)v };

    public static implicit operator long(size_t s) => (long)s.Value;

    public static implicit operator size_t(long v) => new() { Value = (nuint)v };
}
