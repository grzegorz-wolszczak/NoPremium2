using FluentAssertions;
using Main.Utility;

namespace UnitTests;

public class WhitespaceInsensitiveStringTest
{
   [Theory]
   [InlineData("abc")]
   public void ShouldReturnValueFromInitializaiton(string input)
   {
      // Act
      var s = new WhitespaceInsensitiveString(input);

      // Assert
      s.Raw.Should().Be(input);
   }

   [Theory]
   [InlineData((string)null!)]
   [InlineData("")]
   public void ShouldNotbeAbleToCreateStringWithInvalidValue(string? input)
   {
      // Act
      var act = () => new WhitespaceInsensitiveString(input);

      // Assert
      act.Should().Throw<Exception>().WithMessage("Input cannot be null or empty");
   }

   [Theory]
   [InlineData(" ab ", "ab")]
   [InlineData(" a b ", "a b")]
   [InlineData(" a  b ", "a b")]
   [InlineData(" a\r\nb ", "ab")]
   [InlineData(" a\rb ", "ab")]
   [InlineData("a\nb", "ab")]
   public void ShouldRemoveRedundantWhiteSpaceCharactersFromInput(string input, string exptectedOutput)
   {
      //Arrange

      // Act
      var s = new WhitespaceInsensitiveString(input);

      // Assert
      s.ToString().Should().Be(exptectedOutput);
   }


   [Fact]
   public void ShouldCompareSameString()
   {
      // Arrange
      var s1 = new WhitespaceInsensitiveString("  a  b  ");
      var s2 = new WhitespaceInsensitiveString("a b");
      // Act/Assert
      (s1 == s2).Should().BeTrue();
      (s2 == s1).Should().BeTrue();
      s1.Equals(s2).Should().BeTrue();
      s2.Equals(s1).Should().BeTrue();
      s1.GetHashCode().Should().Be(s2.GetHashCode());
      // Assert
   }


}
