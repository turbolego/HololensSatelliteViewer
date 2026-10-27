using HololensSatelliteViewer.Services;
using Xunit;

namespace HololensSatelliteViewer.Tests
{
    public class BitmapGlyphFontTests
    {
        [Fact]
        public void GlyphRowsUseSevenColumnsAndNineRows()
        {
            foreach (char character in BitmapGlyphFont.SupportedCharacters)
            {
                var rows = BitmapGlyphFont.GetRows(character);

                Assert.Equal(BitmapGlyphFont.GlyphHeight, rows.Count);
                Assert.All(rows, row => Assert.Equal(BitmapGlyphFont.GlyphWidth, row.Length));
            }

            Assert.Contains('#', BitmapGlyphFont.GetRows('A')[0]);
        }

        [Fact]
        public void UnsupportedCharacterUsesBlankGlyph()
        {
            var rows = BitmapGlyphFont.GetRows('!');

            Assert.Equal(BitmapGlyphFont.GlyphHeight, rows.Count);
            Assert.All(rows, row => Assert.DoesNotContain('#', row));
        }
    }
}
