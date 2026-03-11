namespace AurumSoftTest.Models;

public record Interval(double DepthFrom, double DepthTo, string Rock, double Porosity)
{
    public double Length => DepthTo - DepthFrom;
}