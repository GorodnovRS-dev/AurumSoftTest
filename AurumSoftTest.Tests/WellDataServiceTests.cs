using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AurumSoftTest.Models;
using AurumSoftTest.Services;
using Xunit;

namespace AurumSoftTest.Tests
{
    public class WellDataServiceTests
    {
        [Fact]
        public async Task LoadAsync_ReturnsAggregates_WhenDataValid()
        {
            var service = new WellDataService();
            var path = await CreateTempFileAsync(new[]
            {
                "WellId;X;Y;DepthFrom;DepthTo;Rock;Porosity",
                "A1;10;20;0;10;Sand;0.2",
                "A1;10;20;10;20;Clay;0.3",
                "B1;5;6;0;5;Sand;0.1"
            });

            try
            {
                var (summaries, errors) = await service.LoadAsync(path);

                Assert.Empty(errors);
                Assert.Equal(2, summaries.Count);

                var first = summaries.First(s => s.WellId == "A1");
                Assert.Equal(20, first.TotalDepth);
                Assert.Equal(2, first.IntervalCount);
                Assert.Equal(0.25, first.WeightedAvgPorosity, 5);
                Assert.Equal("Sand", first.DominantRock);

                var second = summaries.First(s => s.WellId == "B1");
                Assert.Equal(5, second.TotalDepth);
                Assert.Equal(1, second.IntervalCount);
                Assert.Equal(0.1, second.WeightedAvgPorosity, 5);
                Assert.Equal("Sand", second.DominantRock);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task LoadAsync_ReturnsErrors_ForInvalidRows()
        {
            var service = new WellDataService();
            var path = await CreateTempFileAsync(new[]
            {
                "WellId;X;Y;DepthFrom;DepthTo;Rock;Porosity",
                "A1;10;20;5;4;Sand;0.2",
                "A1;10;20;0;10;;0.2",
                "B1;5;6;0;5;Sand"
            });

            try
            {
                var (summaries, errors) = await service.LoadAsync(path);

                Assert.Empty(summaries);
                Assert.Equal(3, errors.Count);
                Assert.Contains(errors, e => e.LineNumber == 2 && e.Message.Contains("DepthFrom"));
                Assert.Contains(errors, e => e.LineNumber == 3 && e.Message.Contains("Rock"));
                Assert.Contains(errors, e => e.LineNumber == 4 && e.Message.Contains("Недостаточно"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task LoadAsync_ReturnsErrors_ForOverlappingIntervals()
        {
            var service = new WellDataService();
            var path = await CreateTempFileAsync(new[]
            {
                "WellId;X;Y;DepthFrom;DepthTo;Rock;Porosity",
                "A1;10;20;0;10;Sand;0.2",
                "A1;10;20;5;15;Clay;0.3"
            });

            try
            {
                var (summaries, errors) = await service.LoadAsync(path);

                Assert.Single(summaries);
                Assert.Single(errors);
                Assert.Contains("пересекается", errors[0].Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ExportSummaryToJsonAsync_WritesJson()
        {
            var service = new WellDataService();
            var summaries = new[]
            {
                new WellAgregate("A1", 10, 20, 20, 2, 0.25, "Sand")
            };

            var path = Path.Combine(Path.GetTempPath(), $"well-summary-{Guid.NewGuid():N}.json");

            try
            {
                await service.ExportSummaryToJsonAsync(path, summaries);

                await using var stream = File.OpenRead(path);
                var result = await JsonSerializer.DeserializeAsync<WellAgregate[]>(stream);

                Assert.NotNull(result);
                Assert.Single(result);
                Assert.Equal(summaries[0], result![0]);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static async Task<string> CreateTempFileAsync(string[] lines)
        {
            var path = Path.GetTempFileName();
            await File.WriteAllLinesAsync(path, lines);
            return path;
        }
    }
}
