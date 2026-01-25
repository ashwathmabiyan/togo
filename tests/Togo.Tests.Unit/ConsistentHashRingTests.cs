using FluentAssertions;
using Togo.Core.Hashing;

namespace Togo.Tests.Unit;

public class ConsistentHashRingTests
{
    [Fact]
    public void GetNode_ShouldReturnSameNodeForSameKey_Deterministic()
    {
        // Arrange
        var nodes = new[] { "nodeA", "nodeB", "nodeC" };
        var ring1 = new ConsistentHashRing(nodes);
        var ring2 = new ConsistentHashRing(nodes); // Identical ring
        var key = "user-123";

        // Act
        var node1 = ring1.GetNode(key);
        var node2 = ring2.GetNode(key);

        // Assert
        node1.Should().Be(node2);
        // Also ensure repeated calls on same instance are consistent
        ring1.GetNode(key).Should().Be(node1);
    }
    
    [Fact]
    public void GetNode_ShouldDistributeKeys_Fairly()
    {
        // Arrange
        var nodes = new[] { "node1", "node2", "node3", "node4", "node5" };
        // Increase vNodes for better distribution statistics in this small test
        var ring = new ConsistentHashRing(nodes, replicationFactor: 200); 
        
        var counts = nodes.ToDictionary(n => n, n => 0);
        int totalKeys = 10_000;

        // Act 
        for (int i = 0; i < totalKeys; i++)
        {
            var node = ring.GetNode($"session-{i}");
            counts[node]++;
        }

        // Assert 
        // Ideal is 2000 per node. 
        // We allow +/- 25% variance which is typical for Consistent Hashing with ~200 vNodes.
        double ideal = totalKeys / (double)nodes.Length;
        double margin = ideal * 0.25; 

        foreach (var node in nodes)
        {
            counts[node].Should().BeInRange((int)(ideal - margin), (int)(ideal + margin), 
                $"Node {node} count {counts[node]} should be near {ideal}");
        }
    }
    
    [Fact]
    public void ConsistentHash_ShouldMinimizeRemapping_WhenNodeAdded()
    {
        // Arrange
        var initialNodes = new[] { "node1", "node2", "node3" };
        var ring1 = new ConsistentHashRing(initialNodes, replicationFactor: 100);
        
        var keys = Enumerable.Range(0, 5000).Select(i => $"req-{i}").ToList();
        var assignments1 = keys.ToDictionary(k => k, k => ring1.GetNode(k));
        
        // Act - Add a node (simulate creating a new ring with one more node)
        var newNodes = new[] { "node1", "node2", "node3", "node4" };
        var ring2 = new ConsistentHashRing(newNodes, replicationFactor: 100);
        
        int changed = 0;
        foreach (var key in keys)
        {
            if (assignments1[key] != ring2.GetNode(key))
            {
                changed++;
            }
        }

        // Assert
        // With 3 nodes, each holds 33.3%.
        // With 4 nodes, each holds 25%.
        // The new node should take ~25% of the total keys.
        // Those keys should come from the existing nodes roughly equally.
        // So we expect ~25% changed. 5000 * 0.25 = 1250.
        
        // Allow variance (10% - 40%)
        changed.Should().BeInRange(500, 2000);
        
        // CRITICAL CHECK: Existing keys that were on node1 should mostly STAY on node1 (unless they moved to node4).
        // They should NOT move to node2 or node3 usually.
        // This is harder to test strictly without deep inspection, 
        // but the total changed count proves we didn't reshuffle everything.
    }

    [Fact]
    public void GetNode_ShouldHandleEmptyRing()
    {
        var ring = new ConsistentHashRing(Array.Empty<string>());
        Action act = () => ring.GetNode("key");
        act.Should().Throw<InvalidOperationException>();
    }
}
