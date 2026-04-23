using System;
using ZeroAlloc.ValueObjects;
using ZeroAlloc.ValueObjects.AotSmoke;

// Exercise every generator-emitted surface we care about under PublishAot=true:
//  * ULID, UUIDv7, and Sequential TypedIds (the three that need no external state).
//    Snowflake is skipped here because its worker-id provider is out-of-band state
//    best covered by the xUnit suite; it doesn't add AOT signal.
//  * A [ValueObject] class — structural equality + GetHashCode + ToString.
//
// Each block returns 1 on failure so we get a single exit code for CI to gate on.

var a = OrderId.New();
var b = OrderId.New();
if (a == b) return Fail("OrderId.New() produced duplicate values");

var aStr = a.ToString();
if (aStr.Length != 26) return Fail($"OrderId ToString expected 26 chars, got {aStr.Length}");
if (!OrderId.TryParse(aStr, null, out var aRound) || aRound != a)
    return Fail("OrderId round-trip via TryParse failed");

var m = MessageId.New();
var mStr = m.ToString();
if (mStr.Length != 36) return Fail($"MessageId ToString expected 36 chars, got {mStr.Length}");
if (!MessageId.TryParse(mStr, null, out var mRound) || mRound != m)
    return Fail("MessageId round-trip via TryParse failed");

var s1 = SequenceId.New();
var s2 = SequenceId.New();
// Sequential ids must strictly increase — two adjacent New() calls on the same
// thread should never collide.
if (s1 == s2) return Fail("SequenceId.New() produced duplicates");

// ValueObject: two separately-constructed instances with identical state must be equal
// and produce the same hash. This path is entirely generator-emitted, no reflection.
var x = new Money("EUR", 42.5m);
var y = new Money("EUR", 42.5m);
var z = new Money("USD", 42.5m);
if (!x.Equals(y)) return Fail("Money equality: identical state not equal");
if (x.GetHashCode() != y.GetHashCode()) return Fail("Money hash: identical state produced different hashes");
if (x.Equals(z)) return Fail("Money equality: different currency compared equal");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
