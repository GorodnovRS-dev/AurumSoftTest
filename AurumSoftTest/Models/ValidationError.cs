namespace AurumSoftTest.Models;

public record ValidationError(int LineNumber, string WellId, string Message);