
using FluentAssertions;
using Main;
using Main.Logging;
using Moq;
using Xunit.Abstractions;

namespace UnitTests;



public class HtmlDataExtractorTests
{
   private readonly ITestOutputHelper _helper;
   private readonly string _queuedFilesContent;
   private readonly string _getSignedInfoContent;
   private readonly string _preprocessedFilesContent;
   private readonly Mock<IMyLogger> _loggerMock;

   public HtmlDataExtractorTests(ITestOutputHelper helper)
   {
      _helper = helper;
      _queuedFilesContent = File.ReadAllText("queued_files_html_content.txt");
      _getSignedInfoContent = File.ReadAllText("signed_info_files_content.txt");
      _preprocessedFilesContent = File.ReadAllText("preprocessed_files.txt");
      _loggerMock = new Mock<IMyLogger>();
   }

   [Fact]
   public void ShouldGetQueuedLinks()
   {
      // Arrange

      var sut = new HtmlDataExtractor(_loggerMock.Object);

      // Act
      var result = sut.GetQueuedLinks(_queuedFilesContent);

      // Assert
      result.Count.Should().Be(2);
      var first = result[0];
      var second = result[1];

      first.HashId.Should().Be("43999438");
      first.Name.ToString().Should().Be("Heroes 3: Żniwiarz Dusz #21 - Jaśnie pan barbarzyńca");

      second.HashId.Should().Be("43999400");
      second.Name.ToString().Should().Be("Diablo 2: Resurrected [PL] [HC] Barbarzyńca #12");

   }

   [Fact]
   public void ShouldGetBaseTransferInBytes()
   {
      // Arrange
      var sut = new HtmlDataExtractor(_loggerMock.Object);

      // Act
      string result = sut.GetBaseTransferString(_getSignedInfoContent);

      // Assert
      result.Should().Be("22.12 GB");
   }

   [Fact]
   public void ShouldExtractFileHashesFromContent()
   {
      // Arrange
      var sut = new HtmlDataExtractor(_loggerMock.Object);
      // Act
      var hashes = sut.GetFileHashes(_preprocessedFilesContent);

      // Assert
      hashes.Count.Should().Be(1);
      hashes[0].Should().Be("405afb3408");

   }

   [Fact]
   public void ShouldExtractFileHashesFromContent2()
   {
      // Arrange
      var sut = new HtmlDataExtractor(_loggerMock.Object);
      // Act
      var result = sut.GetFilesToDownloadData(_preprocessedFilesContent);
      result.HasValue.Should().BeTrue();
      // Assert
      result.Value.Hashes.Count.Should().Be(1);
      result.Value.Hashes[0].Should().Be("405afb3408");
      result.Value.SizeBytes.Should().Be(437780480L);

   }

}
