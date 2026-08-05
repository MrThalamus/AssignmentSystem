using AssignmentSystem.Infrastructure.Security;

namespace AssignmentSystem.Tests.Security;

public class PasswordHashingTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void A_hash_verifies_against_the_password_it_was_made_from()
    {
        var hash = _hasher.Hash("Str0ngPassword");

        Assert.True(_hasher.Verify("Str0ngPassword", hash));
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var hash = _hasher.Hash("Str0ngPassword");

        Assert.False(_hasher.Verify("str0ngpassword", hash));
        Assert.False(_hasher.Verify("Str0ngPassword ", hash));
        Assert.False(_hasher.Verify("", hash));
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time()
    {
        // Per-password salts mean two accounts sharing a password are not obvious
        // from the stored values, and a precomputed table is useless.
        var first = _hasher.Hash("SharedPassword1");
        var second = _hasher.Hash("SharedPassword1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("SharedPassword1", first));
        Assert.True(_hasher.Verify("SharedPassword1", second));
    }

    [Fact]
    public void The_iteration_count_is_stored_with_the_hash()
    {
        // This is what allows the work factor to be raised later without locking
        // existing accounts out.
        Assert.StartsWith("100000.", _hasher.Hash("SomePassword1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-hash")]
    [InlineData("abc.def.ghi")]
    [InlineData("100000.notbase64!.alsonotbase64!")]
    public void A_malformed_stored_hash_is_rejected_rather_than_throwing(string storedHash)
    {
        Assert.False(_hasher.Verify("AnyPassword1", storedHash));
    }
}
