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
}
