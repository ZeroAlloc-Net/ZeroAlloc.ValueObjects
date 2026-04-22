using System;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Thrown when a TypedId operation cannot proceed because runtime state is misconfigured
/// (e.g. <see cref="IdStrategy.Snowflake"/> generation attempted without a registered
/// <see cref="ISnowflakeWorkerIdProvider"/>).
/// </summary>
public sealed class TypedIdException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="TypedIdException"/> class.</summary>
    public TypedIdException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedIdException"/> class with a
    /// message describing the misconfiguration.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TypedIdException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedIdException"/> class with a
    /// message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public TypedIdException(string message, Exception innerException) : base(message, innerException) { }
}
