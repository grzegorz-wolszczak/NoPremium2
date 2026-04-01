using FluentAssertions;
using Main;

namespace UnitTests;

public class DataSizeConverterTests
{
   [Fact]
   public void ShouldConvertValueLessThanZero()
   {
      // Act
      var result =  DataSizeConverter.ToDataSize(-1);

      // Assert
      result.Should().Be("-1 B");
   }

   [Fact]
   public void ShouldConvertToZeroBytes()
   {
      // Act
      var result = DataSizeConverter.ToDataSize(0);

      // Assert
      result.Should().Be("0 B");
   }


   [Theory]
   [InlineData(1L, "1 B")]
   [InlineData(1023L, "1023 B")]
   [InlineData(1024L, "1 KB")]
   [InlineData(1024*1024L, "1 MB")]
   [InlineData(1024*1024*1024L, "1 GB")]
   [InlineData(1024*1024*1024*1024L, "1 TB")]
   [InlineData(-2007897211L, "-1.87 GB")]
   public void ShouldConvertToBytes(long valueInBytes, string exptectedOutput)
   {
      // Act
      var result = DataSizeConverter.ToDataSize(valueInBytes);

      // Assert
      result.Should().Be(exptectedOutput);
   }

   [Theory]
   [InlineData("0 B", 0L)]
   [InlineData("1 B", 1L)]
   [InlineData("1023 B", 1023L)]
   [InlineData("1 MB", 1024*1024L)]
   [InlineData("1.5 MB", 1572864L)]
   [InlineData("-1.5 MB", -1572864L)]
   public void ShouldConvertFromHumanReadable(string input, long expectedOutput)
   {
      // Arrange

      // Act
      var result = DataSizeConverter.FromHumanReadableToBytes(input);

      // Assert
      result.Should().Be(expectedOutput);

   }

   // [Theory]
   // [InlineData(1L, "1 B")]
   // [InlineData(1023L, "1023 B")]
   // [InlineData(1024L, "1 KB")]
   // [InlineData(1024*1024L, "1 MB")]
   // [InlineData(1024*1024*1024L, "1 GB")]
   // [InlineData(1024*1024*1024*1024L, "1 TB")]
   // [InlineData(322122547233L, "322.12 GB")]
   // public void ShouldConvertToBytes2(long valueInBytes, string exptectedOutput)
   // {
   //    // Act
   //    var result = DataSizeConverter.ToDataSize2(valueInBytes);
   //
   //    // Assert
   //    result.Should().Be(exptectedOutput);
   // }
}
