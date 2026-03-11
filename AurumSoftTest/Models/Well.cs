namespace AurumSoftTest.Models;

public class Well
{
    public Well(string wellId, double x, double y)
    {
        WellId = wellId;
        X = x;
        Y = y;
        Intervals = new();
    }

    public string WellId { get; }
    public double X { get; }
    public double Y { get; }
    public List<Interval> Intervals { get; }
}