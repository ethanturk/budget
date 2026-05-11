using BudgetApp.Infrastructure.Auth;

namespace BudgetApp.UnitTests.Auth;

public sealed class MasterPasswordHasherTests
{
    [Fact]
    public void HashPassword_ProducesValueThatCanBeVerified()
    {
        var hasher = new MasterPasswordHasher();

        var hash = hasher.HashPassword("correct horse battery staple");

        Assert.True(hasher.VerifyPassword(hash, "correct horse battery staple"));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new MasterPasswordHasher();
        var hash = hasher.HashPassword("correct horse battery staple");

        var isValid = hasher.VerifyPassword(hash, "wrong password");

        Assert.False(isValid);
    }
}
