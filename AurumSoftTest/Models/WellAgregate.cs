namespace AurumSoftTest.Models;

public record WellAgregate(
    string WellId,
    double X,
    double Y,
    double TotalDepth,
    int IntervalCount,
    double WeightedAvgPorosity,
    string DominantRock);