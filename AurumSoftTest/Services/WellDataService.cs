using AurumSoftTest.Models;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AurumSoftTest.Services;

public class WellDataService
{
    public async Task<(List<WellAgregate> Summaries, List<ValidationError> Errors)> LoadAsync(string path)
    {
        var errors = new List<ValidationError>();
        var wells = new Dictionary<string, Well>(StringComparer.OrdinalIgnoreCase);

        var lineNumber = 0;
        await foreach (var raw in File.ReadLinesAsync(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var line = raw.Trim();

            if (lineNumber == 1 && line.StartsWith("WellId", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ParseLine(line, lineNumber, out var parseLine, out var currentErrors))
            {
                errors.AddRange(currentErrors);
                continue;
            }

            var (wellId, x, y, depthFrom, depthTo, rock, porosity) = parseLine;

            if (!wells.TryGetValue(wellId, out var well))
            {
                if (Validate(lineNumber, wellId, depthFrom, depthTo, rock, porosity, out var validationErrors))
                {
                    well = new Well(wellId, x, y);
                    well.Intervals.Add(new Interval(depthFrom, depthTo, rock, porosity));
                    wells.Add(wellId, well);
                }
                else
                {
                    errors.AddRange(validationErrors);
                }
            }
            else
            {
                if (Validate(lineNumber, wellId, x, y, depthFrom, depthTo, rock, porosity, well, out var validationErrors))
                {
                    well.Intervals.Add(new Interval(depthFrom, depthTo, rock, porosity));
                }
                else
                {
                    errors.AddRange(validationErrors);
                }
            }
        }

        var agregateWells = wells.Values
            .Select(Agregate)
            .OrderBy(s => s.WellId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (agregateWells, errors);
    }

    private bool ParseLine(string line, int lineNumber, out (string, double, double, double, double, string, double) parseLine, out List<ValidationError> errors)
    {
        errors = new List<ValidationError>();
        var parts = line.Split(';');
        if (parts.Length < 7)
        {
            var wellId = parts.Length > 0 ? parts[0] : string.Empty;
            errors.Add(new ValidationError(lineNumber, wellId, "Недостаточно столбцов в строке"));
            parseLine = default;
            return false;
        }

        var wellIdParsed = parts[0].Trim();
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
        {
            errors.Add(new ValidationError(lineNumber, wellIdParsed, $"Некорректное значение X: {parts[1]}"));
        }
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            errors.Add(new ValidationError(lineNumber, wellIdParsed, $"Некорректное значение Y: {parts[2]}"));
        }
        if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var depthFrom))
        {
            errors.Add(new ValidationError(lineNumber, wellIdParsed, $"Некорректное значение DepthFrom: {parts[3]}"));
        }
        if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var depthTo))
        {
            errors.Add(new ValidationError(lineNumber, wellIdParsed, $"Некорректное значение DepthTo: {parts[4]}"));
        }
        var rock = parts[5].Trim();
        if (!double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var porosity))
        {
            errors.Add(new ValidationError(lineNumber, wellIdParsed, $"Некорректное значение Porosity: {parts[6]}"));
        }

        parseLine = (wellIdParsed, x, y, depthFrom, depthTo, rock, porosity);
        return errors.Count == 0;
    }

    private bool Validate(int lineNumber, string wellId, double depthFrom, double depthTo, string rock, double porosity, out List<ValidationError> errors)
    {
        errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(rock))
        {
            errors.Add(new ValidationError(lineNumber, wellId, "Rock не должен быть пустым"));
        }
        if (depthFrom < 0)
        {
            errors.Add(new ValidationError(lineNumber, wellId, "DepthFrom должен быть >= 0"));
        }
        if (depthFrom >= depthTo)
        {
            errors.Add(new ValidationError(lineNumber, wellId, "DepthFrom должен быть меньше DepthTo"));
        }
        if (porosity is < 0 or > 1)
        {
            errors.Add(new ValidationError(lineNumber, wellId, "Porosity должна быть в диапазоне [0..1]"));
        }

        return errors.Count == 0;
    }

    private bool Validate(int lineNumber, string wellId, double x, double y, double depthFrom, double depthTo, string rock, double porosity, Well well, out List<ValidationError> errors)
    {
        errors = new List<ValidationError>();

        if (well.X != x || well.Y != y)
        {
            errors.Add(new ValidationError(lineNumber, wellId, $"Координаты скважины не совпадают с предыдущими: ({well.X}, {well.Y})"));
        }

        if (!Validate(lineNumber, wellId, depthFrom, depthTo, rock, porosity, out var validationErrors))
        {
            errors.AddRange(validationErrors);
        }
        if (well.Intervals.Any(i => IntervalsOverlap(i, depthFrom, depthTo)))
        {
            errors.Add(new ValidationError(lineNumber, wellId,
                $"Интервал {depthFrom}-{depthTo} пересекается с ранее добавленым интервалом"));
        }

        return errors.Count == 0;
    }

    public async Task ExportSummaryToJsonAsync(string path, IEnumerable<WellAgregate> summaries)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, summaries, new JsonSerializerOptions { WriteIndented = true });
    }

    private WellAgregate Agregate(Well well)
    {
        var totalDepth = well.Intervals.Max(i => i.DepthTo);
        var intervalCount = well.Intervals.Count;
        var totalLength = well.Intervals.Sum(i => i.Length);
        var weightedPorosity = well.Intervals.Sum(i => i.Porosity * i.Length) / totalLength;
        var dominantRock = well.Intervals
            .GroupBy(i => i.Rock)
            .OrderByDescending(g => g.Sum(i => i.Length))
            .First().Key;

        return new WellAgregate(
            well.WellId,
            well.X,
            well.Y,
            totalDepth,
            intervalCount,
            weightedPorosity,
            dominantRock);
    }

    private bool IntervalsOverlap(Interval interval, double depthFrom, double depthTo) =>
        interval.DepthFrom < depthTo && interval.DepthTo > depthFrom;
}