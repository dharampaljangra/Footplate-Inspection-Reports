using Xunit;
using Xunit.Abstractions;

namespace FootplateIR.Core.Tests;

public class ExportTests
{
    private readonly ITestOutputHelper _out;
    public ExportTests(ITestOutputHelper output) => _out = output;

    private static (AppData Data, List<InspectionReport> Reports) Sample()
    {
        var d = Store.New();
        var reports = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters).Reports;
        d.Reports.AddRange(reports);
        d.Rosters.AddRange(Patrol.ParseRosters(SampleTexts.Roster));
        return (d, reports);
    }

    [Theory]
    [InlineData(ExportFormat.Pdf)]
    [InlineData(ExportFormat.Word)]
    [InlineData(ExportFormat.Excel)]
    [InlineData(ExportFormat.Html)]
    public void Report_ExportsAndComesBackExactly(ExportFormat f)
    {
        var (d, reports) = Sample();
        var r = reports[1];
        var bytes = Export.Report(r, d, f);
        Assert.True(bytes.Length > 1000);
        var path = Path.Combine(Path.GetTempPath(), "fir-test-" + Guid.NewGuid() + Export.Extension(f));
        File.WriteAllBytes(path, bytes);
        _out.WriteLine(path + " " + bytes.Length);
        var back = FileImport.Read(path, Store.New());
        Assert.True(back.Exact);
        var x = Assert.Single(back.Reports);
        Assert.Equal(r.Id, x.Id);
        Assert.Equal(r.Sightings.Count, x.Sightings.Count);
        Assert.Equal(r.Locations.Count, x.Locations.Count);
        if (f == ExportFormat.Pdf)
        {
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            // The PDF text itself must also read back as a report (as a PDF made by anyone else would).
            Assert.Contains("NIGHT FOOTPLATE INSPECTION NOTE", back.Text.Length > 0 ? back.Text : ReadPlain(path));
        }
        File.Delete(path);
    }

    private static string ReadPlain(string path)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }

    [Fact]
    public void PdfText_WithoutEmbeddedData_IsReadBackAsReport()
    {
        var (d, reports) = Sample();
        var doc = ReportLayout.Report(reports[0], d);
        doc.EmbeddedData = null;
        var path = Path.Combine(Path.GetTempPath(), "fir-plain-" + Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, PdfRenderer.Render(doc));
        var back = FileImport.Read(path, Store.New());
        _out.WriteLine(back.Text);
        Assert.False(back.Exact);
        var r = Assert.Single(back.Reports);
        Assert.Equal(reports[0].TrainNo, r.TrainNo);
        Assert.Equal(reports[0].Departure, r.Departure);
        Assert.Equal(reports[0].Locations.Count, r.Locations.Count);
        File.Delete(path);
    }

    [Theory]
    [InlineData(ExportFormat.Pdf)]
    [InlineData(ExportFormat.Word)]
    [InlineData(ExportFormat.Excel)]
    [InlineData(ExportFormat.Html)]
    [InlineData(ExportFormat.Text)]
    [InlineData(ExportFormat.Csv)]
    public void Periods_Registers_Rosters_Export(ExportFormat f)
    {
        var (d, reports) = Sample();
        Assert.True(Export.Reports(reports, d, f, "July 2026").Length > 200);
        Assert.True(Export.Register(reports, d, f, "July 2026").Length > 200);
        if (f is ExportFormat.Pdf or ExportFormat.Word or ExportFormat.Excel or ExportFormat.Html)
        {
            Assert.True(Export.Document(ReportLayout.LocationRegister(reports, d, "2026", false), f).Length > 200);
            Assert.True(Export.Document(ReportLayout.Roster(d.Rosters, d), f).Length > 200);
        }
    }

    [Fact]
    public void Backup_MergesBothWays()
    {
        var (a, _) = Sample();
        var b = Store.New();
        var s = Store.Merge(b, Store.Load(Save(a)));
        Assert.Equal(2, s.ReportsAdded);
        Store.Delete(b, b.Reports[0]);
        var s2 = Store.Merge(a, b);
        Assert.Equal(1, s2.ReportsDeleted);
        Assert.Single(a.Reports);
    }

    private static string Save(AppData d)
    {
        var p = Path.Combine(Path.GetTempPath(), "fir-backup-" + Guid.NewGuid() + ".json");
        Store.Save(d, p);
        return p;
    }
}
