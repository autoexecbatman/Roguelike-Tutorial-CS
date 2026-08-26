/*
 * The picture that should be on screen, as data rather than as pixels.
 *
 * Usage - compose one with FrameComposer, then either inspect it in a test or blit it:
 *
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     char glyph = frame.GlyphAt(new Point(40, 12));   // -> '@'
 *     string picture = frame.ToText();                 // rows joined by newlines
 *
 * ToText is what makes drawing testable: an expected frame can be written in a test as an
 * ASCII picture and compared as a string.
 *
 * Refuses a null array, and an array whose length disagrees with the dimensions.
 */

using System;
using System.Text;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RenderedFrame
{
    // Glyphs in row-major order, one per cell.
    private readonly char[] _glyphs;

    // Colours in row-major order, matching _glyphs cell for cell.
    private readonly Color[] _foregrounds;

    /// <summary>Number of cells across.</summary>
    public int Width { get; }

    /// <summary>Number of cells down.</summary>
    public int Height { get; }

    /// <summary>
    /// Wraps the two parallel arrays produced by FrameComposer. Throws ArgumentException when
    /// either length disagrees with the dimensions, which would mean a bug in the composer.
    /// </summary>
    public RenderedFrame(int width, int height, char[] glyphs, Color[] foregrounds)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentNullException.ThrowIfNull(foregrounds);

        // A length mismatch is a programming error in the composer, not a runtime condition.
        if (glyphs.Length != width * height || foregrounds.Length != width * height)
        {
            throw new ArgumentException("Glyph and colour arrays must hold exactly width * height entries.");
        }

        Width = width;
        Height = height;
        _glyphs = glyphs;
        _foregrounds = foregrounds;
    }

    /// <summary>The character at the position. Throws ArgumentOutOfRangeException off the frame.</summary>
    public char GlyphAt(Point position)
    {
        RejectPositionOffTheFrame(position);

        return _glyphs[(position.Y * Width) + position.X];
    }

    /// <summary>The colour at the position. Throws ArgumentOutOfRangeException off the frame.</summary>
    public Color ForegroundAt(Point position)
    {
        RejectPositionOffTheFrame(position);

        return _foregrounds[(position.Y * Width) + position.X];
    }

    /// <summary>
    /// The whole frame as text, one line per row, joined with newlines and with no trailing
    /// newline. This is what tests compare against an expected ASCII picture.
    /// </summary>
    public string ToText()
    {
        StringBuilder text = new StringBuilder();

        for (int row = 0; row < Height; row++)
        {
            // A separator before every row but the first leaves no trailing newline.
            if (row > 0)
            {
                text.Append('\n');
            }

            text.Append(_glyphs, row * Width, Width);
        }

        return text.ToString();
    }

    // Shared guard; reading outside the frame is always a caller error.
    private void RejectPositionOffTheFrame(Point position)
    {
        if (position.X < 0 || position.X >= Width || position.Y < 0 || position.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the frame.");
        }
    }
}
