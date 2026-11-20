using Xunit;

namespace FootplateIR.Core.Tests;

public class OheMastTests
{
    [Fact]
    public void Register_PlacesMastLocations()
    {
        Assert.True(OheMasts.All.Count > 1000);
        Assert.Equal(40.8685, OheMasts.Find("40/14")!.Chainage, 4);
        Assert.Equal(40.8685, OheMasts.Find("KM 40/014")!.Chainage, 4);
        Assert.Equal(48.7555, Patrol.KmOf("48/17")!.Value, 4);
        Assert.Equal(48.7555, Patrol.KmOf("KM 48/17")!.Value, 4);
        var r = OheMasts.Range("35/10-15")!.Value;
        Assert.True(r.FromRegister);
        Assert.Equal(35.4915, r.From, 4);
        Assert.Equal(35.8245, r.To, 4);
        Assert.Contains("km", OheMasts.Describe("KM 35/10-15"));
        Assert.Equal("", OheMasts.Describe("JNRI S3 to S4"));
        // Outside the register: average span.
        Assert.Equal(120 + 13 * OheMasts.AverageSpan, Patrol.KmOf("120/13")!.Value, 3);
        Assert.Equal(152.9, Patrol.KmOf("152/900")!.Value, 3);
        Assert.Equal(1.703, Patrol.KmOf("GWCN", Store.New().Masters.Chainage)!.Value, 3);
    }

    [Fact]
    public void PrintedMastKm_DoesNotDisturbReadingBack()
    {
        var d = Store.New();
        var r = ReportParser.Parse(SampleTexts.WhatsApp, d.Profile, d.Masters).Reports[0];
        var doc = ReportLayout.Report(r, d);
        doc.EmbeddedData = null;
        var path = Path.Combine(Path.GetTempPath(), "fir-mast-" + Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, PdfRenderer.Render(doc));
        var back = Assert.Single(FileImport.Read(path, Store.New()).Reports);
        Assert.Equal(r.Locations.Count, back.Locations.Count);
        Assert.Equal(r.Locations.Select(l => l.Feature), back.Locations.Select(l => l.Feature));
        Assert.Contains(back.Sightings, s => s.Location == "40/14" && s.Count == 2);
        File.Delete(path);
    }
}
