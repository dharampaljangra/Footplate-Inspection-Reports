using Xunit;
using Xunit.Abstractions;

namespace FootplateIR.Core.Tests;

public class RulesTests
{
    private readonly ITestOutputHelper _out;
    public RulesTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Schedule_InCharge_July2026()
    {
        var d = Store.New();
        var reports = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters).Reports;
        var rows = Rules.Schedule(reports, Rules.RoleInCharge, 2026, 7, new DateOnly(2026, 8, 5));
        foreach (var r in rows) _out.WriteLine($"{r.Item} | {r.Period} | {r.Status} | {string.Join(",", r.Done)} | {r.Note}");
        Assert.Equal("Done", rows.First(r => r.Ref == "Table-1B Sl. 14").Status);
        Assert.Contains("00.00 and 04.00", rows.First(r => r.Ref == "Table-1B Sl. 14").Note);
        var fortnights = rows.Where(r => r.Ref == "Table-1B Sl. 3").ToList();
        Assert.Equal(2, fortnights.Count);
        Assert.Equal("Not done", fortnights[0].Status);
        Assert.Equal("Done", fortnights[1].Status);
        Assert.Equal("Done", rows.First(r => r.Item.StartsWith("Monsoon")).Status);
        var sectional = Rules.Schedule(reports, Rules.RoleSectional, 2026, 7, new DateOnly(2026, 8, 5));
        Assert.Equal(2, sectional.Count(r => r.Item.StartsWith("Monsoon")));
        Assert.True(Rules.All.Length > 30);
        Assert.Contains(Rules.Search("jacket"), r => r.Ref == "Para 1004(5)");
        Assert.NotEmpty(Export.Document(Rules.ScheduleDocument(d, Rules.RoleInCharge, 2026, 7), ExportFormat.Pdf));
    }

    [Fact]
    public void Checks_PrintedAndReadBack()
    {
        var d = Store.New();
        var r = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters).Reports[0];
        r.Checks = new List<string>(Rules.NightFootplateChecks);
        var doc = ReportLayout.Report(r, d);
        doc.EmbeddedData = null;
        var path = Path.Combine(Path.GetTempPath(), "fir-checks-" + Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, PdfRenderer.Render(doc));
        var back = Assert.Single(FileImport.Read(path, Store.New()).Reports);
        _out.WriteLine(string.Join(" | ", back.Checks));
        Assert.Equal(Rules.NightFootplateChecks.Length, back.Checks.Count);
        Assert.Equal(r.Sightings.Count, back.Sightings.Count);
        File.Delete(path);
    }

    [Fact]
    public void ChartSummary_HasUnitsAndTotal()
    {
        var d = Store.New();
        var r = new InspectionReport
        {
            Date = new DateOnly(2026, 12, 10), FromStation = "BUDI", ToStation = "CNA", TrainNo = "01910",
            Departure = new TimeOnly(0, 27), Arrival = new TimeOnly(2, 44),
            Sightings =
            {
                new StaffSighting { Category = "Cold Weather Patrolman", Location = "48/17", Count = 2 },
                new StaffSighting { Category = "Cold Weather Patrolman", Location = "35/10", Count = 2, Condition = "Sleeping" }
            }
        };
        var rows = Patrol.Check(r, d.Charts[0], d.Masters.Chainage);
        var sum = Patrol.Summary(rows);
        foreach (var x in sum) _out.WriteLine(x.ToString());
        var total = sum[^1];
        Assert.Equal("Total", total.Unit);
        Assert.Contains(sum, x => x.Unit == "BUDI");
        Assert.Contains(sum, x => x.Unit == "MLGH");
        Assert.Equal(sum.Take(sum.Count - 1).Sum(x => x.PairsDue), total.PairsDue);
        Assert.Equal(4, total.MenSeen);
        Assert.Equal(2, total.MenDeficient);
        Assert.Equal(total.PairsDue, total.AsPerChart + total.OffChart + total.NotRecorded);
    }
}
