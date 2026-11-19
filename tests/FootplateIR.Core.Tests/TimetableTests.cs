using ClosedXML.Excel;
using Xunit;
using Xunit.Abstractions;

namespace FootplateIR.Core.Tests;

public class TimetableTests
{
    private readonly ITestOutputHelper _out;
    public TimetableTests(ITestOutputHelper output) => _out = output;

    // Dummy timings in the shapes the app reads (not a real time table).
    private const string Text = """
Train No: 01910 PRYJ PUNE SPL (Daily)
1  KOTA JN (KOTA)   Source  23:50   Day 1
2  BUNDI (BUDI)     00:25   00:27   Day 2   32.2 km
3  SINGHPUR (SNAR)  00:41   00:42   Day 2
4  JALINDRI (JNRI)  01:05   01:06   Day 2
5  MANDALGARH (MLGH) 01:35  01:37   Day 2
6  BASSI BERISAL (BSSL) 02:15 02:16 Day 2
7  CHANDERIYA (CNA) 02:44   02:49   Day 2
8  CHITTAURGARH (COR) 03:05  Destination
19608 ADI KOAA EXP
CNA 03:15 03:17
BSSL 03:40 03:41
MLGH 04:20 04:22
BUDI 05:20 05:22
KOTA 05:55 06:05
SGAC 06:15 Destination
""";

    [Fact]
    public void Text_IsRead()
    {
        var d = Store.New();
        Assert.True(Timetable.LooksLikeTimetable(Text));
        var list = Timetable.Parse(Text, d.Masters);
        foreach (var t in list) _out.WriteLine(Timetable.ToText(t));
        Assert.Equal(2, list.Count);
        var a = list[0];
        Assert.Equal("01910", a.TrainNo);
        Assert.Equal("Daily", a.Days);
        Assert.Equal(8, a.Stops.Count);
        Assert.Null(a.Stops[0].Arr);
        Assert.Equal(new TimeOnly(23, 50), a.Stops[0].Dep);
        Assert.Equal(new TimeOnly(0, 27), a.Stop("BUDI")!.Dep);
        Assert.Null(a.Stops[^1].Dep);
        Assert.Equal(6, list[1].Stops.Count);
        // Written back and read again.
        var again = Timetable.Parse(Timetable.ToText(a), d.Masters).Single();
        Assert.Equal(a.Stops.Count, again.Stops.Count);
        Assert.Equal((new TimeOnly(0, 27), new TimeOnly(2, 44)), Timetable.TimesFor(a, "BUDI", "CNA"));
    }

    [Fact]
    public void Passing_And_ScheduleBasedChartCheck()
    {
        var d = Store.New();
        d.Trains.AddRange(Timetable.Parse(Text, d.Masters));
        var at = Timetable.Passing(d.Trains, "BUDI", new TimeOnly(22, 0), new TimeOnly(7, 0), d.Masters.Chainage);
        foreach (var p in at) _out.WriteLine($"{p.Time} {p.Train.TrainNo} {p.Detail} up={p.TowardsHigherKm}");
        Assert.Equal(2, at.Count);
        Assert.Equal("01910", at[0].Train.TrainNo);
        Assert.True(at[0].TowardsHigherKm);
        Assert.False(at[1].TowardsHigherKm);
        var km = Timetable.Passing(d.Trains, "48/17", new TimeOnly(22, 0), new TimeOnly(7, 0), d.Masters.Chainage);
        Assert.Equal(2, km.Count);

        var r = new InspectionReport
        {
            Date = new DateOnly(2026, 12, 10), FromStation = "BUDI", ToStation = "CNA", TrainNo = "01910",
            Departure = new TimeOnly(0, 30), Arrival = new TimeOnly(2, 50)
        };
        Assert.True(Patrol.UsesSchedule(r, d.Masters.Chainage, d.Trains));
        var even = Patrol.TimeAtKm(r, d.Masters.Chainage, 66.0);
        var sched = Patrol.TimeAtKm(r, d.Masters.Chainage, 66.0, d.Trains);
        _out.WriteLine($"JNRI even speed {even}, by schedule {sched}");
        Assert.NotNull(sched);
        Assert.NotEqual(even, sched);
        var rows = Patrol.Check(r, d.Charts[0], d.Masters.Chainage, null, 1.0, d.Trains);
        Assert.True(rows.Count >= 25);
    }

    [Fact]
    public void WorkingTimeTable_Excel_IsRead()
    {
        var path = Path.Combine(Path.GetTempPath(), "fir-wtt-" + Guid.NewGuid() + ".xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("WTT");
            ws.Cell(1, 1).Value = "Station";
            ws.Cell(1, 2).Value = "01910";
            ws.Cell(1, 3).Value = "19608";
            string[] st = { "KOTA", "BUDI", "MLGH", "CNA" };
            string[] a = { "23:50", "00:25/00:27", "01:35/01:37", "02:44" };
            string[] b = { "06:05", "05:20/05:22", "04:20/04:22", "03:15" };
            for (int i = 0; i < st.Length; i++)
            {
                ws.Cell(i + 2, 1).Value = st[i];
                ws.Cell(i + 2, 2).Value = a[i];
                ws.Cell(i + 2, 3).Value = b[i];
            }
            wb.SaveAs(path);
        }
        var res = FileImport.Read(path, Store.New());
        foreach (var t in res.Trains) _out.WriteLine(Timetable.ToText(t));
        Assert.Equal(2, res.Trains.Count);
        var x = res.Trains.First(t => t.TrainNo == "01910");
        Assert.Equal(new TimeOnly(0, 25), x.Stop("BUDI")!.Arr);
        Assert.Equal(new TimeOnly(0, 27), x.Stop("BUDI")!.Dep);
        File.Delete(path);
    }
}
