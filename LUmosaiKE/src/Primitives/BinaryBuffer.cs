using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LUmosaiKE.Primitives;

public sealed class BinaryBuffer : IDisposable
{
    private readonly IMemoryOwner<byte> _bufferOwner;
    private readonly Memory<byte>       _buffer;
    
    private readonly int  _capacity;
    private          int  _usage;
    private          bool _isDisposed;
    
    public BinaryBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        _bufferOwner = MemoryPool<byte>.Shared.Rent(capacity);
        _buffer      = _bufferOwner.Memory.Slice(0, capacity);
        _capacity    = capacity;
    }
    
    public BinaryBufferField<T> CreateLayout<T>() where T : unmanaged
    {
        ThrowIfDisposed();
        var size = Unsafe.SizeOf<T>();
        
        if (_usage + size > _capacity)
            throw new InvalidOperationException($"Failed to create the layout because this exceeds the binary buffer's capacity of {_capacity} bytes.");

        var layout = new BinaryBufferField<T>(this, _usage);
        _usage += size;
        return layout;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _bufferOwner.Dispose();
        _isDisposed = true;
    }
    
    internal T Get<T>(int offset) where T : unmanaged
    {
        ThrowIfDisposed();
        ReadOnlySpan<byte> span  = _buffer.Span;
        ReadOnlySpan<byte> slice = span.Slice(offset, Unsafe.SizeOf<T>());
        return MemoryMarshal.Read<T>(slice);
    }

    internal void Set<T>(int offset, T value) where T : unmanaged
    {
        ThrowIfDisposed();
        Span<byte> span = _buffer.Span;
        Span<byte> slice = span.Slice(offset, Unsafe.SizeOf<T>());
        MemoryMarshal.Write(slice, in value);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new Exception("You cannot operate on a binary buffer that has been disposed.");
    }
}

public sealed class BinaryBufferField<T> where T : unmanaged
{
    private readonly BinaryBuffer _buffer;
    private readonly int _offset;
    
    internal BinaryBufferField(BinaryBuffer buffer, int offset)
    {
        _buffer = buffer;
        _offset = offset;
    }

    public T Value
    {
        get => Get();
        set => Set(value);
    }

    public T    Get()        => _buffer.Get<T>(_offset);
    public void Set(T value) => _buffer.Set<T>(_offset, value);
}