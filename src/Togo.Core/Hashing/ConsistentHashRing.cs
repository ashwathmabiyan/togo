using System.Security.Cryptography;
using System.Text;
namespace Togo.Core.Hashing;

public class ConsistentHashRing
{
    private readonly SortedDictionary<uint, string> _ring = new();
    private uint[]? _sortedKeys;
    private readonly int _virtualNodeCount;

    public ConsistentHashRing(IEnumerable<string> servers, int virtualNodeCount = 100)
    {
        _virtualNodeCount = virtualNodeCount;
        foreach (var server in servers)
        {
            AddServer(server);
        }
    } 
    
    private void AddServer(string server)
    {
        for (int i = 0; i < _virtualNodeCount; i++)
        {
            string virtualNodeName = $"{server}vnode-{i}";
            uint hash = CalculateHash(virtualNodeName);
            _ring[hash] = server;
        }
        _sortedKeys = _ring.Keys.ToArray();
    }

    public string GetPrimary(string key)
    {
        if(_sortedKeys == null || _sortedKeys.Length == 0)
            throw new InvalidOperationException("Ring is empty.");
        
        uint hash = CalculateHash(key);
        int index = Array.BinarySearch(_sortedKeys, hash);
        if (index < 0)
        {
            index = ~index;
        }
        if (index >= _sortedKeys.Length)
        {
            index = 0;
        }
        return _ring[_sortedKeys[index]];
    }
    
    private uint CalculateHash(string key)
    { 
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}