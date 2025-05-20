using Common;

var nodesBefore = new[] { "shard-a", "shard-b" };
var nodesAfter = new[] { "shard-a", "shard-b", "shard-c" };

var ringBefore = new ConsistentHashRing<string>(nodesBefore);
var ringAfter = new ConsistentHashRing<string>(nodesAfter);

int moved = 0;
int total = 100;

Console.WriteLine("Visual Hash Ring Before (hash → node):");
foreach (var kv in ringBefore.Circle.Take(20))
{
    Console.WriteLine($"{kv.Key} → {kv.Value}");
}

Console.WriteLine("\nVisual Hash Ring After (hash → node):");
foreach (var kv in ringAfter.Circle.Take(20))
{
    Console.WriteLine($"{kv.Key} → {kv.Value}");
}

Console.WriteLine("\nKey Reassignments After Adding shard-c:");
for (int i = 0; i < total; i++)
{
    var key = $"user:{i}";
    var before = ringBefore.GetNodeKeyValue(key);
    var after = ringAfter.GetNodeKeyValue(key);

    if (before.value != after.value)
    {
        Console.WriteLine($"{key} moved from {before} to {after}");
        moved++;
    }
}

Console.WriteLine($"\nMoved {moved} out of {total} keys ({(moved * 100.0 / total):F2}%)");