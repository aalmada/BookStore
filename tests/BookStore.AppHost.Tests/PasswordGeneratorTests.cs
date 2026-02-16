using Bogus;

namespace BookStore.AppHost.Tests.Helpers;

public class PasswordGeneratorTests
{
    [Test]
    public async Task GenerateFakePassword_ShouldMeetAllRequirements()
    {
        // Generate 100 passwords and verify they all meet requirements
        for (int i = 0; i < 100; i++)
        {
            var password = FakeDataGenerators.GenerateFakePassword();

            // ASP.NET Core Identity requirements
            await Assert.That(password.Length).IsGreaterThanOrEqualTo(8);
            await Assert.That(password.Any(char.IsUpper)).IsTrue();
            await Assert.That(password.Any(char.IsLower)).IsTrue();
            await Assert.That(password.Any(char.IsDigit)).IsTrue();
            await Assert.That(password.Any(c => !char.IsLetterOrDigit(c))).IsTrue();
        }
    }

    [Test]
    public void GenerateFakePassword_OutputSamples()
    {
        // Output 10 sample passwords for manual inspection
        for (int i = 0; i < 10; i++)
        {
            var password = FakeDataGenerators.GenerateFakePassword();
            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            Console.WriteLine($"Password: '{password}' (len:{password.Length}, Upper:{hasUpper}, Lower:{hasLower}, Digit:{hasDigit}, Special:{hasSpecial})");
        }
    }
}
