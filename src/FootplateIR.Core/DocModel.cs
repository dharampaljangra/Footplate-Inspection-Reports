namespace FootplateIR.Core;

/// <summary>
/// A printable document described once (headings, label grids, tables, numbered paragraphs, signature)
/// and drawn by each exporter: PDF, Word, Excel and HTML all look alike.
/// </summary>
public sealed class DocModel
{
    public string Title { get; set; } = "";
    /// <summary>Footer text (left); the page number is printed on the right.</summary>
    public string Footer { get; set; } = "";
    public string Author { get; set; } = Defaults.AuthorCredit;
    public bool Landscape { get; set; }
    public List<Block> Blocks { get; } = new();
    /// <summary>JSON of the reports, embedded so the app can import its own files exactly.</summary>
    public string? EmbeddedData { get; set; }

    public DocModel Add(Block b) { Blocks.Add(b); return this; }
}

public abstract class Block { }

/// <summary>Centred heading line.</summary>
public sealed class TitleBlock : Block
{
    public string Text { get; init; } = "";
    public float Size { get; init; } = 12;
    public bool Bold { get; init; } = true;
    public string Color { get; init; } = Theme.Navy;
}

public sealed class RuleBlock : Block
{
    public float Thickness { get; init; } = 0.8f;
    public string Color { get; init; } = Theme.Navy;
}

/// <summary>"1. Inspection Particulars"</summary>
public sealed class SectionBlock : Block
{
    public string Text { get; init; } = "";
}

/// <summary>Label / value pairs, two pairs to a row, labels shaded (inspection particulars).</summary>
public sealed class GridBlock : Block
{
    public List<(string Label, string Value)> Pairs { get; init; } = new();
    public int PairsPerRow { get; init; } = 2;
}

public sealed class TableBlock : Block
{
    public string[] Headers { get; init; } = Array.Empty<string>();
    public List<string[]> Rows { get; init; } = new();
    /// <summary>Relative column widths.</summary>
    public float[] Widths { get; init; } = Array.Empty<float>();
    /// <summary>Columns printed in bold (e.g. the location).</summary>
    public int[] BoldColumns { get; init; } = Array.Empty<int>();
    /// <summary>Columns centred (S. No., Nos.).</summary>
    public int[] CenterColumns { get; init; } = Array.Empty<int>();
    /// <summary>Rows to highlight (deficiencies), by index.</summary>
    public HashSet<int> HighlightRows { get; init; } = new();
    public float FontSize { get; init; } = 9;
}

/// <summary>A paragraph; with a number it is printed with a hanging indent, with a lead it starts in bold ("Station Staff: ").</summary>
public sealed class ParaBlock : Block
{
    public string Number { get; init; } = "";
    public string Lead { get; init; } = "";
    public string Text { get; init; } = "";
    public float Size { get; init; } = 10;
    public bool Italic { get; init; }
    public bool Justify { get; init; } = true;
    public float Indent { get; init; }
}

/// <summary>Right-aligned signature lines (first in bold).</summary>
public sealed class SignatureBlock : Block
{
    public string[] Lines { get; init; } = Array.Empty<string>();
}

public sealed class SpacerBlock : Block
{
    public float Height { get; init; } = 6;
}

public sealed class PageBreakBlock : Block { }

public static class Theme
{
    public const string Navy = "1F3864";
    public const string LabelFill = "E8EEF7";
    public const string AltFill = "F5F8FC";
    public const string Border = "9AA9C1";
    public const string Warn = "FDECEC";
    public const string Grey = "555555";
}
