using System.Security.Cryptography;
using System.Text;

namespace Common;

public class ConsistentHashRing<T>
{
    private readonly SortedDictionary<int, T> _circle = [];
    private readonly int _replicas;
    private readonly MD5 _hash = MD5.Create();

    public SortedDictionary<int, T> Circle => _circle;

    /*
    * Each node gets 100 positions (virtual nodes) on the hash ring.
    * The ring becomes evenly populated - more balanced
    * Key movement Minimal (≈ 1/N keys move). It might be highly disruptive if we make it one point per node.
    */
    public ConsistentHashRing(IEnumerable<T> nodes, int replicas = 100)
    {
        _replicas = replicas;
        foreach (var node in nodes)
        {
            Add(node);
        }
    }

    public void Add(T node)
    {
        for (int i = 0; i < _replicas; i++)
        {
            var hash = Hash($"{node}-{i}");
            _circle[hash] = node!;
        }
    }

    public T GetNode(string key)
    {
        if (_circle.Count == 0)
            throw new InvalidOperationException("Hash ring is empty");

        var hash = Hash(key);
        foreach (var nodeHash in _circle.Keys)
        {
            if (hash <= nodeHash)
                return _circle[nodeHash];
        }

        return _circle.First().Value;
    }

    public (int key, T value) GetNodeKeyValue(string key)
    {
        if (_circle.Count == 0)
            throw new InvalidOperationException("Hash ring is empty");

        var hash = Hash(key);
        foreach (var nodeHash in _circle.Keys)
        {
            if (hash <= nodeHash)
                return (nodeHash, _circle[nodeHash]);
        }

        return (_circle.First().Key, _circle.First().Value);
    }

    private int Hash(string input)
    {
        var bytes = _hash.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
    }
}