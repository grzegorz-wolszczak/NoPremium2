using Core.Maybe;
using FluentAssertions;
using Main.EmailReading;
using TddXt.AnyRoot.Strings;
using static TddXt.AnyRoot.Root;

namespace Unittests;

public class VoucherCodeExtractorSpecifications
{
   [Test]
   public void ShouldExtractCodeFromHtmlMessage()
   {
      // Arrange
      var rawVoucherCode = "057fce4d69858f3c6647";
      var message =
         $@"""<html>\r\n<head>\r\n<meta http-equiv=\""Content-Type\"" content=\""text/html; charset=utf-8\"">\r\n</head>\r\n<body style=\""text-align:center; font-size: 12px; margin: 0 auto; width: 600px; font-family: Tahoma, sans-serif; color: #2F65A1; line
-height: 18px;\"">\r\n\r\n<!-- container -->\r\n\r\n<div style=\""border: 1px solid gray; box-shadow: 0px 0px 10px gray; width: 600 px; background-color: #EDF8FC; height: auto; margin-top:25px; margin-bottom: 25px;\"">\r\n\r\n\t<div style=\""width:100%; min-height: 80px; background-color: #0F243C;\"">\r\n\t\t<!-- logo -->\r\n\r\n\t\t<div style=\""flo
at:left; padding: 25 px; \"">\r\n\t\t\t<img src=\""http://www.nopremium.pl/images/logo.png\"" />\r\n\t\t</div>\r\n\r\n\t\t<!-- logo end -->\r\n\r\n\t</div>\r\n\t<!-- right header end -->\r\n\t<!-- header -->\r\n\r\n\t<div style=\""width: 550px; font-size: 28px; text-align: left; margin-bottom: 35px; margin-left: 25px; margin-top: 20px;\"">\r\n\t\t<s
trong>Witaj, regular.p!</strong>\r\n\t</div>\r\n\r\n\t<!-- header end -->\r\n\r\n\t<!-- main content -->\r\n\r\n\t<div style=\""width: 550px; text-align: left; margin: 25 px; padding:25px;\"">\r\n\r\n\tNa Twoim koncie zuzyto <strong>20 GB</strong> danych. W zwiazku z tym otrzymujesz darmowy kod doladowujacy na <strong>2 GB</strong>.<br /><br />\r
\n\tTwój kod doladowujacy: <strong>{rawVoucherCode}</strong><br /><br />\r\n\tZaloguj sie na swoim koncie w NoPremium.pl i wpisz kod w zakladce <strong>Kod doladowujacy</strong>.<br /><br />\r\n\tMozesz równiez sprzedac lub podarowac kod doladowujacy innemu uzytkownikowi serwisu NoPremium.pl.<br /><br />\r\n\tOtrzymany kod jest wazny przez 
7 dni od daty wydania.<br /><br />\r\n\r\n\t<h3>Kontakt</h3>\r\n\r\n\tW razie problemów zapraszamy do kontaktu z Biurem Obslugi Uzytkownika: <br /><br />\r\n\t- zakladka Pomoc dostepna po zalogowaniu w serwisie<br />\r\n\t- formularz kontaktowy na stronie glównej<br />\r\n    - email: nopremium@nopremium.pl<br /><br />\r\n\r\n    Dziekujemy i z
yczymy wysokich transferów!<br />\r\n    <strong>Zaloga NoPremium.pl</strong>\r\n\r\n\t<br /><br />\r\n\t</div>\r\n\r\n\t<!-- main content end -->\r\n\r\n</div>\r\n\r\n<!-- container end -->\r\n\r\n\r\n\r\n\r\n<img src=\""https://ea.pstmrk.it/open?m=v3_1.a9NVES4YWhCvZmJP6WaSmQ.Xc0wa_2Zf3KRKk0a6HbPj9wDO4R7yf7DzdJDq4xun0IRCOtG3QPUfVAWVmuXKQoxfAWiA
CORq4jkecMBjafW7PA3P9kvgTLHgyzMVM2nfGQzCLOF2IooWToIgSN4HqTLnxFAC5SlUgABO3waKyp50krRaSe6ydRSstFDeb9YOiviY6l4dFL-eCWJtSh2d-pGuOtrbg9lfxxHsYXa-k3aAVwgqUvD5avqdw2b-tjnzcSLjz5ocKv4HgdOSfAbZTwMAVmAhorIyVAl6hzJLU1Vr7HjLQLOBoPt7Oja8H3jN27GVA6YDUbvyMDSlZ9ViTe87HV-5Wn95Lz6hOjfi8O3Uen4Rl-mCbJfLop23Nv0pOn_vWjWv7RyGM36nv81kL3bNEt27mYbrIkX4o9nO6yols7-2rMLQ2P
KcZU-jtL33siEubK7w3Mx7Ik-RG1vbGC7t6cjPsUoT73OeGhdqF3j5fRhHT3D-Np4qrK5Jdc1SgE3WfJZ_fTyKjKL7NtT0-UtL5aUs4D7UDbulsRCxhhdwebeGiK2gEok-s2_NWHFFVxUoJxvYt85s7WSFjAdfCL29CpddTT9fZVbuojBtArKUA\"" width=\""1\"" height=\""1\"" border=\""0\"" alt=\""\"" /></body>\r\n</html>""'";
      var extractor = CreateExtractor();


      // Act
      var maybeResult = extractor.ExtractCodeFrom(message);

      // Assert

      maybeResult.IsSomething().Should().BeTrue();
      var code = maybeResult.Value();
      code.Should().Be(rawVoucherCode);
   }

   [Test]
   public void ShouldExtractCodeFromMessage()
   {
      // Arrange
      var rawVoucherCode = "31a46b88c0e92b263ad9";
      var message = $@"       Witaj, regular.p!

Na Twoim koncie zużyto 20 GB danych. W związku z tym otrzymujesz darmowy kod doładowujący na 2 GB.

Twój kod doładowujący: {rawVoucherCode}

Zaloguj się na swoim koncie w NoPremium.pl i wpisz kod w zakładce Kod doładowujący.

Możesz również sprzedać lub podarować kod doładowujący innemu użytkownikowi serwisu NoPremium.pl.

Otrzymany kod jest ważny przez 7 dni od daty wydania.

Kontakt
W razie problemów zapraszamy do kontaktu z Biurem Obsługi Użytkownika:
	- zakładka Pomoc dostępna po zalogowaniu w serwisie
	- formularz kontaktowy na stronie głównej
    - email: nopremium@nopremium.pl

Dziękujemy i życzymy wysokich transferów!
Załoga NoPremium.pl";
      var extractor = CreateExtractor();


      // Act
      var maybeResult = extractor.ExtractCodeFrom(message);

      // Assert

      maybeResult.IsSomething().Should().BeTrue();
      var code = maybeResult.Value();
      code.Should().Be(rawVoucherCode);
   }

   private static VoucherCodeExtractor CreateExtractor()
   {
      return new VoucherCodeExtractor();
   }

   [Test]
   public void ShouldNotBeAbleToExtractCodeFromMessage()
   {
      // Arrange
      var extractor = CreateExtractor();

      // Act
      var maybeResult = extractor.ExtractCodeFrom(Any.String());

      // Assert
      maybeResult.IsSomething().Should().BeFalse();
   }
}
