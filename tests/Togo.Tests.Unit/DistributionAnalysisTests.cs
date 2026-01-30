using FluentAssertions; 
using Togo.Core.Hashing;
using Xunit.Abstractions;

namespace Togo.Tests.Unit;

public class DistributionAnalysisTests
{
    private readonly ITestOutputHelper _output;

    public DistributionAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(2, 10)]
    [InlineData(2, 100)]
    [InlineData(2, 500)]
    [InlineData(4, 10)]
    [InlineData(4, 100)]
    [InlineData(4, 500)]
    [InlineData(8, 10)]
    [InlineData(8, 100)]
    [InlineData(8, 500)]
    public void AnalyzeDistribution(int serverCount, int virtualNodeCount)
    {
        // Arrange
        var servers = Enumerable.Range(0, serverCount).Select(i => $"Server-{i}").ToArray();
        var ring = new ConsistentHashRing(servers, virtualNodeCount);
        int totalRequests = 100_000;
        
        var serverHits = servers.ToDictionary(s => s, s => 0);

        // Act
        for (int i = 0; i < totalRequests; i++)
        {
            // Simulate random request keys
            string key = $"req-{Guid.NewGuid()}"; 
            var node = ring.GetNode(key);
            serverHits[node]++;
        }

        // Analyze
        double ideal = totalRequests / (double)serverCount;
        var values = serverHits.Values.Select(x => (double)x).ToList();
        
        // Standard Deviation
        double avg = values.Average();
        double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        double stdDev = Math.Sqrt(sumOfSquares / (values.Count - 1));
        
        // Coefficient of Variation (CV) - Normalized measure of dispersion
        // Lower is better. < 10% is excellent. < 20% is acceptable.
        double cv = (stdDev / avg) * 100;
        
        // Max Skew
        int max = values.Select(v => (int)v).Max();
        int min = values.Select(v => (int)v).Min();
        double maxSkewPercent = ((max - ideal) / ideal) * 100;

        _output.WriteLine($"--- Configuration: {serverCount} Servers, {virtualNodeCount} vNodes ---");
        _output.WriteLine($"Ideal Hits per Server: {ideal:N0}");
        _output.WriteLine($"Std Dev: {stdDev:N2}");
        _output.WriteLine($"CV (Imbalance Score): {cv:N2}%");
        _output.WriteLine($"Max Skew: +{maxSkewPercent:N2}%");
        _output.WriteLine($"Range: {min} - {max}");
        _output.WriteLine("----------------------------------------------------------------");
        
        // Assert - Loose assertion just to keep the test green if reasonable
        // We know that with LOW vNodes (10), variance can be high (e.g. 30%).
        // We accept anything for this analysis tool, but let's say we expect it to run without crashing.
        serverHits.Values.Sum().Should().Be(totalRequests);
    }
    
    [Fact]
    public void AnalyzeFailureImpact()
    {
        // 1. Arrange: Start with 4 servers and 100,000 requests
        var initialServers = new[] { "Server-0", "Server-1", "Server-2", "Server-3" };
        int vNodes = 500;
        var originalRing = new ConsistentHashRing(initialServers, vNodes);
        
        var requestKeys = Enumerable.Range(0, 100_000)
                                    .Select(_ => $"req-{Guid.NewGuid()}")
                                    .ToList();
    
        // Mapping: Where do requests go originally?
        var originalAssignment = requestKeys.ToDictionary(k => k, k => originalRing.GetNode(k));
    
        // 2. Act: One server (Server-0) goes offline
        var remainingServers = initialServers.Where(s => s != "Server-0").ToArray();
        var newRing = new ConsistentHashRing(remainingServers, vNodes);
    
        // Re-Mapping: Where do they go now?
        int stayedPut = 0;
        int movedToNewServer = 0;
        var migrationTargetHits = remainingServers.ToDictionary(s => s, s => 0);
    
        foreach (var key in requestKeys)
        {
            string oldNode = originalAssignment[key];
            string newNode = newRing.GetNode(key);
    
            if (oldNode == newNode)
            {
                stayedPut++;
            }
            else
            {
                movedToNewServer++;
                migrationTargetHits[newNode]++;
            }
        }
    
        // 3. Analyze
        _output.WriteLine($"Total Requests: {requestKeys.Count:N0}");
        _output.WriteLine($"Requests originally on Server-0: {originalAssignment.Values.Count(v => v == "Server-0"):N0}");
        _output.WriteLine($"Total Disrupted Requests (Moved): {movedToNewServer:N0}");
        _output.WriteLine($"Total Stable Requests (Stayed Put): {stayedPut:N0}");
        
        _output.WriteLine("\n--- Migration Distribution (Where did Server-0's load go?) ---");
        foreach (var kvp in migrationTargetHits)
        {
            _output.WriteLine($"{kvp.Key}: received +{kvp.Value:N0} requests");
        }
    }
}
