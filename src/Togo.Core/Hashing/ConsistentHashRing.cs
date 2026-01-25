using System.IO.Hashing;
using System.Text;

namespace Togo.Core.Hashing;

public class ConsistentHashRing
{
    private readonly ulong[] _sortedKeys;
    private readonly string[] _sortedValues;
    private readonly int _replicationFactor;

    public ConsistentHashRing(IEnumerable<string> nodes, int replicationFactor = 100)
    {
        _replicationFactor = replicationFactor;
        
        var keys = new List<ulong>();
        var values = new Dictionary<ulong, string>();

        foreach (var node in nodes)
        {
            for (int i = 0; i < _replicationFactor; i++)
            {
                // Create unique virtual node key
                string virtualNodeKey = $"{node}#{i}";
                ulong hash = CalculateHash(virtualNodeKey);

                // Collision Handling: Linear Probing
                // If a collision occurs (extremely rare in 64-bit space), 
                // we simply move to the next slot. This guarantees determinism and safety.
                while (values.ContainsKey(hash))
                {
                    hash++;
                }

                keys.Add(hash);
                values[hash] = node;
            }
        }

        keys.Sort();
        _sortedKeys = keys.ToArray();
        _sortedValues = new string[_sortedKeys.Length];

        // Map sorted keys to their corresponding nodes for O(1) access after binary search
        for (int i = 0; i < _sortedKeys.Length; i++)
        {
            _sortedValues[i] = values[_sortedKeys[i]];
        }
    }

    public string GetNode(string key)
    {
        if (_sortedKeys.Length == 0)
        {
            throw new InvalidOperationException("Ring is empty.");
        }

        ulong hash = CalculateHash(key);

        // Binary Search for O(log N) lookup
        int index = Array.BinarySearch(_sortedKeys, hash);

        if (index < 0)
        {
            // If exact match not found, get the index of the next larger element (Ceiling)
            index = ~index;
        }

        if (index >= _sortedKeys.Length)
        {
            // Wrap around to the start of the ring (Circle property)
            index = 0;
        }

        return _sortedValues[index];
    }

    private static ulong CalculateHash(string key)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(key);
        return XxHash64.HashToUInt64(bytes);
    }
}