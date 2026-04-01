using FluentAssertions;
using Main.Utility;

namespace UnitTests;

public class PrettyHtmlTests
{
   [Fact]
   public void ShouldPrettyfyHtml()
   {
      // Arrange
      string htmlInput = @"




					<div class=""subpage_subtitle universal_subtitle_ico"" style=""color: red;"">Pliki nieprzetworzone (1)</div>


			<table class=""table"">

				<tbody>

				<tr class=""table_head"">
					<td>Link</td>
					<td>Błąd</td>
				</tr>

				 				<tr class=""table_content"">
					<td>https://www.youtube.com/watch?v=yRFrJMXNn8k</td>
					<td>Błąd połączenia z hostingiem - spróbuj wyszukać inny link</td>
				</tr>
								</tbody>
				</table>



";

      var expectedOutput = @"<div class=""subpage_subtitle universal_subtitle_ico"" style=""color: red;"">
    Pliki nieprzetworzone (1)
</div>
<table class=""table"">
    <tbody>
        <tr class=""table_head"">
            <td>
                Link
            </td>
            <td>
                Błąd
            </td>
        </tr>
        <tr class=""table_content"">
            <td>
                https://www.youtube.com/watch?v=yRFrJMXNn8k
            </td>
            <td>
                Błąd połączenia z hostingiem - spróbuj wyszukać inny link
            </td>
        </tr>
    </tbody>
</table>";

      // Act
      var result = HtmlPretty.Prettyfy(htmlInput);

      result.Should().Be(expectedOutput);
   }
}
