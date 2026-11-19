using Xunit;
using Xunit.Abstractions;

namespace FootplateIR.Core.Tests;

public class ParserTests
{
    private readonly ITestOutputHelper _out;
    public ParserTests(ITestOutputHelper output) => _out = output;

    private static AppData Data() => Store.New();

    [Fact]
    public void WhatsAppMessages_AreReadAsTwoReports()
    {
        var d = Data();
        var res = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters, "WhatsApp");
        foreach (var m in res.Messages) _out.WriteLine(m);
        Assert.Equal(2, res.Reports.Count);

        var a = res.Reports[0];
        Assert.Equal(new DateOnly(2026, 7, 21), a.Date);
        Assert.Equal("Night Footplate Inspection", a.Kind);
        Assert.Equal(new[] { "Safety", "Monsoon" }, a.Purposes);
        Assert.Equal("BUDI", a.FromStation);
        Assert.Equal("CNA", a.ToStation);
        Assert.Equal("01910", a.TrainNo);
        Assert.Equal(new TimeOnly(0, 27), a.Departure);
        Assert.Equal(new TimeOnly(2, 44), a.Arrival);
        Assert.Equal("22228", a.LocoNo);
        Assert.Equal("WAP4", a.LocoType);
        Assert.Equal("Cab2", a.Cab);
        Assert.Equal("Brij Mohan Lodha", a.LocoPilot);
        Assert.Equal("Dinesh Patidar", a.AssistantLocoPilot);
        Assert.Equal("Sr. ALP", a.AlpDesignation);
        Assert.Equal("Dharmendra Kumar Meena", a.TrainManager);
        Assert.Equal("Dharam Pal Jangid", a.InspectorName);
        Assert.Equal("7976273600", a.Mobile);
        foreach (var s in a.Sightings) _out.WriteLine($"{s.Category} | {s.Location} | {s.Count} | {s.Condition} | {s.Jacket} | {s.Remarks}");
        Assert.Equal(6, a.Sightings.Count);
        Assert.Contains(a.Sightings, s => s.Location == "54/08" && s.Condition == "Sleeping" && s.Category == "Monsoon Patrolman");
        Assert.Contains(a.Sightings, s => s.Location == "40/14" && s.Count == 2 && s.Category == "Stationary Watchman");
        Assert.Contains(a.Sightings, s => s.Location == "128/2-3" && s.Count == 1);
        Assert.Contains(a.Sightings, s => s.Location == "JNRI" && s.Count == 2 && s.Jacket == "Worn");
        Assert.Equal(4, a.Observations.Count);
        Assert.Equal("Clear throughout", a.Visibility);
        Assert.Empty(a.Shortcomings);
        Assert.Equal(14, a.Locations.Count);
        Assert.Contains(a.Locations, l => l.Location == "61/12" && l.Feature == "SEJ");
        Assert.Contains(a.Locations, l => l.Location == "JNRI S3 to S4");

        var b = res.Reports[1];
        Assert.Equal("CNA", b.FromStation);
        Assert.Equal("SGAC", b.ToStation);
        Assert.Equal("19608", b.TrainNo);
        Assert.Equal("SDAH", b.Shed);
        Assert.Equal("Cab1", b.Cab);
        Assert.Equal("Rahul Sisodia", b.AssistantLocoPilot);
        foreach (var s in b.Sightings) _out.WriteLine($"{s.Category} | {s.Location} | {s.Count} | {s.Condition} | {s.Jacket} | {s.Remarks}");
        Assert.Equal(7, b.Sightings.Count);
        Assert.Contains(b.Sightings, s => s.Location == "66/0" && s.Jacket == "Not worn" && s.Condition == "Alert");
        Assert.Contains(b.Sightings, s => s.Location == "SNAR" && s.Jacket == "Worn");
        Assert.Contains("rain", b.Weather);
        Assert.Single(b.Shortcomings);
        Assert.Equal(17, b.Locations.Count);
    }

    [Fact]
    public void Message_RoundTrips()
    {
        var d = Data();
        var first = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters).Reports;
        foreach (var r in first)
        {
            var text = ReportText.ToMessage(r, d.Profile);
            _out.WriteLine(text);
            var again = ReportParser.Parse(text, d.Profile, d.Masters).Reports.Single();
            Assert.Equal(r.TrainNo, again.TrainNo);
            Assert.Equal(r.Departure, again.Departure);
            Assert.Equal(r.LocoLine, again.LocoLine);
            Assert.Equal(r.Sightings.Count, again.Sightings.Count);
            Assert.Equal(r.Observations.Count, again.Observations.Count);
            Assert.Equal(r.Locations.Count, again.Locations.Count);
            Assert.Equal(r.Shortcomings.Count, again.Shortcomings.Count);
        }
    }

    [Fact]
    public void InspectionNotePdf_18Aug_IsRead()
    {
        var d = Data();
        var res = FileImport.Read(Path.Combine("Samples", "Night_Footplate_Inspection_Note_18.08.2026.pdf"), d);
        _out.WriteLine(res.Text);
        foreach (var m in res.Messages) _out.WriteLine(m);
        var r = Assert.Single(res.Reports);
        foreach (var s in r.Sightings) _out.WriteLine($"{s.Category} | {s.Location} | {s.Count} | {s.Condition} | {s.Jacket} | {s.Remarks}");
        Assert.Equal(new DateOnly(2026, 8, 18), r.Date);
        Assert.Equal("01910", r.TrainNo);
        Assert.Equal("BUDI", r.FromStation);
        Assert.Equal("CNA", r.ToStation);
        Assert.Equal(new TimeOnly(0, 16), r.Departure);
        Assert.Equal(new TimeOnly(2, 25), r.Arrival);
        Assert.Equal("37440", r.LocoNo);
        Assert.Equal("Night Footplate Inspection", r.Kind);
        Assert.True(r.Locations.Count >= 15, $"locations {r.Locations.Count}");
        Assert.Equal(2, r.Shortcomings.Count);
        Assert.True(r.Sightings.Count >= 6, $"sightings {r.Sightings.Count}");
        Assert.Contains("satisfactory", r.Remarks);
    }

    [Fact]
    public void InspectionNotePdf_18Oct_IsRead()
    {
        var d = Data();
        var res = FileImport.Read(Path.Combine("Samples", "Night_Footplate_Inspection_BNDI-KOTA_18.10.2026.pdf"), d);
        _out.WriteLine(res.Text);
        var r = Assert.Single(res.Reports);
        Assert.Equal(new DateOnly(2026, 10, 18), r.Date);
        Assert.Equal("BNDI", r.FromStation);
        Assert.Equal("KOTA", r.ToStation);
        Assert.Equal(new TimeOnly(0, 50), r.Departure);
        Assert.Equal(new TimeOnly(4, 4), r.Arrival);
        Assert.Equal("31416", r.LocoNo);
        Assert.Equal("Ram Prakash Meena", r.LocoPilot);
        Assert.Equal(7, r.Locations.Count);
        Assert.Contains(r.Locations, l => l.Feature.Contains("SEJ"));
    }

    [Fact]
    public void DutyRosters_AreRead()
    {
        var list = Patrol.ParseRosters(SampleTexts.Roster);
        foreach (var r in list)
        {
            _out.WriteLine(r.Title);
            foreach (var b in r.Beats) _out.WriteLine($"  {b.No} {b.Range}: {string.Join(", ", b.Persons.Select(p => p.Display + " " + p.Mobile))}");
        }
        Assert.Equal(2, list.Count);
        var cwp = list[0];
        Assert.Equal("Cold Weather Patrolman", cwp.Category);
        Assert.Equal(new DateOnly(2026, 11, 19), cwp.Date);
        Assert.Equal(10, cwp.Beats.Count);
        Assert.Equal(77, cwp.Beats[0].FromKm);
        Assert.Equal("9000001111", cwp.Beats[0].Persons[0].Mobile);
        Assert.False(cwp.Beats[2].Persons[0].MobileLooksRight);
        Assert.Contains(cwp.Beats[4].Persons, p => p.HomeGuard && p.Name == "Ram Swaroop");
        var key = list[1];
        Assert.Equal("Keyman", key.Category);
        Assert.Equal(new DateOnly(2026, 11, 20), key.Date);
        Assert.Equal(6, key.Beats.Count);
        Assert.Equal("9000023331", key.Beats[0].Persons[0].Mobile);
        Assert.Equal("9000027775", key.Beats[4].Persons[0].Mobile);
    }

    /// <summary>A duty list printed as a table (Sr no., K/M, name, mobile) like the BSSL list - dummy names and numbers.</summary>
    [Fact]
    public void DutyRosterTablePdf_IsRead()
    {
        var pdf = new PdfWriter();
        pdf.NewPage();
        float y = 60;
        void Line(string s) { pdf.Text(40, y, s, 10); y += 16; }
        Line("Sr no.K/M Name of patrolman Mobile no. GPS");
        Line("1 117/0 Patrolman One 9000100001");
        Line("121/0 Patrolman Two 9000100002");
        Line("2 125/0 Patrolman Three (HG) 9000100003");
        Line("121/0 Patrolman Four 9000100004");
        Line("3 149/0 Patrolman Five 90001 00005");
        Line("152/900 Patrolman Six 9000100006");
        Line("Cold weather patrolman km 117/0 - 152/18   Date 19-20/11/2026");
        var path = Path.Combine(Path.GetTempPath(), "fir-roster-" + Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, pdf.ToBytes());
        var res = FileImport.Read(path, Data());
        _out.WriteLine(res.Text);
        var r = Assert.Single(res.Rosters);
        foreach (var b in r.Beats) _out.WriteLine($"  {b.No} {b.Range}: {string.Join(", ", b.Persons.Select(p => p.Display + " " + p.Mobile))}");
        Assert.Equal(new DateOnly(2026, 11, 19), r.Date);
        Assert.Equal("Cold Weather Patrolman", r.Category);
        Assert.Equal(3, r.Beats.Count);
        Assert.Equal(117, r.Beats[0].FromKm);
        Assert.Equal(121, r.Beats[0].ToKm);
        Assert.Equal(152.9, r.Beats[^1].ToKm, 2);
        Assert.True(r.Beats[1].Persons[0].HomeGuard);
        Assert.Equal("9000100005", r.Beats[2].Persons[0].Mobile);
        File.Delete(path);
    }

    [Fact]
    public void Chart_PositionsAndCheck()
    {
        var d = Data();
        var chart = d.Charts[0];
        Assert.Equal(38, chart.Beats.Count);
        var p12 = chart.Beats[0];
        Assert.Equal(1, Patrol.PositionAt(chart, p12, Patrol.NightMinutes(new TimeOnly(23, 0))));
        Assert.Equal(5, Patrol.PositionAt(chart, p12, Patrol.NightMinutes(new TimeOnly(1, 20))));
        Assert.Equal(5, Patrol.PositionAt(chart, p12, Patrol.NightMinutes(new TimeOnly(1, 35))));
        Assert.Equal(1, Patrol.PositionAt(chart, p12, Patrol.NightMinutes(new TimeOnly(4, 10))));
        Assert.Equal(9, Patrol.PositionAt(chart, chart.Beats[1], Patrol.NightMinutes(new TimeOnly(23, 0))));
        Assert.Null(Patrol.PositionAt(chart, p12, Patrol.NightMinutes(new TimeOnly(22, 0))));

        var r = new InspectionReport
        {
            Date = new DateOnly(2026, 12, 10), FromStation = "BUDI", ToStation = "CNA", TrainNo = "01910",
            Departure = new TimeOnly(0, 27), Arrival = new TimeOnly(2, 44),
            Sightings = { new StaffSighting { Category = "Cold Weather Patrolman", Location = "48/17", Count = 2 } }
        };
        var rows = Patrol.Check(r, chart, d.Masters.Chainage);
        foreach (var x in rows) _out.WriteLine($"{x.Beat.Patrolmen} {x.Beat.Range} {x.ExpectedKm} {x.ExpectedTime} {x.Status} {x.Detail}");
        Assert.True(rows.Count >= 25);
        Assert.Contains(rows, x => x.Status == Patrol.NotNoticed);
        Assert.Contains(rows, x => x.Seen is not null);
    }
}
