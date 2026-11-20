using Xunit;

namespace FootplateIR.Core.Tests;

public class OheMastTests
{
    [Fact]
    public void Register_IsBuiltIn_And_GivesExactChainage()
    {
        Store.New();
        Assert.True(OheMasts.Count >= 1390);
        var m = OheMasts.Find("KM 40/14");
        Assert.NotNull(m);
        Assert.Equal(m!.Chainage, Patrol.KmOf("40/14")!.Value, 3);
        Assert.Equal(OheMasts.Find("35/10")!.Chainage, Patrol.KmOf("35/10-15")!.Value, 3);
        Assert.Equal(6.007, Patrol.KmOf("OHE 5/17")!.Value, 3);       // mast 5/17 stands at KM 6.007
        Assert.Equal(152.9, Patrol.KmOf("152/900")!.Value, 3);          // KM / metre stays KM / metre
        Assert.Contains("35/10 - 35/15", OheMasts.Describe("35/10-15"));
        Assert.Equal("", OheMasts.Describe("JNRI S3 to S4"));
        Assert.Equal("40/14", OheMasts.Nearest(m.Chainage)!.Mast);
    }

    [Fact]
    public void ExtraMasts_AreAdded()
    {
        var d = Store.New();
        var extra = OheMasts.Parse("Mast,Km,Metre,Chainage,MastType,Line,Drawing\n140/3,140,120,,B-150,,140-145 LOP");
        Assert.Single(extra);
        d.Masters.ExtraMasts.AddRange(extra);
        OheMasts.SetExtra(d.Masters.ExtraMasts);
        Assert.Equal(140.12, Patrol.KmOf("KM 140/3")!.Value, 3);
        OheMasts.SetExtra(null);
    }

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
