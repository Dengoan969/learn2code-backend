using System.Diagnostics;
using Learn2Code.Core.Models;

namespace Learn2Code.Services;

public class StateComparer
{
    public bool Compare(SceneState actual, SceneState expected)
    {
        if (actual == null || expected == null)
            return false;

        var actualCats = actual.Sprites.OfType<CatState>().ToList();
        var expectedCats = expected.Sprites.OfType<CatState>().ToList();
        
        var actualApples = actual.Sprites.OfType<AppleState>().ToList();
        var expectedApples = expected.Sprites.OfType<AppleState>().ToList();
        
        var actualWalls = actual.Sprites.OfType<WallState>().ToList();
        var expectedWalls = expected.Sprites.OfType<WallState>().ToList();
        
        if (actualCats.Count != expectedCats.Count)
        {
            Debug.WriteLine($"StateComparer: Cat count mismatch: actual={actualCats.Count}, expected={expectedCats.Count}");
            return false;
        }
        if (actualApples.Count != expectedApples.Count)
        {
            Debug.WriteLine($"StateComparer: Apple count mismatch: actual={actualApples.Count}, expected={expectedApples.Count}");
            return false;
        }
        if (actualWalls.Count != expectedWalls.Count)
        {
            Debug.WriteLine($"StateComparer: Wall count mismatch: actual={actualWalls.Count}, expected={expectedWalls.Count}");
            return false;
        }
        
        if (actualCats.Count == 1 && expectedCats.Count == 1)
        {
            var a = actualCats[0];
            var e = expectedCats[0];
            
            if (a.GridX != e.GridX)
            {
                Debug.WriteLine($"StateComparer: GridX mismatch: actual={a.GridX}, expected={e.GridX}");
                return false;
            }
            if (a.GridY != e.GridY)
            {
                Debug.WriteLine($"StateComparer: GridY mismatch: actual={a.GridY}, expected={e.GridY}");
                return false;
            }
            if (Math.Abs(a.Direction - e.Direction) > 0.001)
            {
                Debug.WriteLine($"StateComparer: Direction mismatch: actual={a.Direction}, expected={e.Direction}");
                return false;
            }
            if (!CompareDicts(a.SaidTexts, e.SaidTexts))
            {
                Debug.WriteLine($"StateComparer: SaidTexts mismatch");
                return false;
            }
            if (!CompareDicts(a.CollectedItems, e.CollectedItems))
            {
                Debug.WriteLine($"StateComparer: CollectedItems mismatch");
                return false;
            }
        }
        
        var actualApplePositions = actualApples.Select(x => (x.GridX, x.GridY)).ToHashSet();
        var expectedApplePositions = expectedApples.Select(x => (x.GridX, x.GridY)).ToHashSet();
        if (!actualApplePositions.SetEquals(expectedApplePositions))
        {
            Debug.WriteLine($"StateComparer: Apple positions mismatch");
            return false;
        }
        
        var actualWallPositions = actualWalls.Select(x => (x.GridX, x.GridY)).ToHashSet();
        var expectedWallPositions = expectedWalls.Select(x => (x.GridX, x.GridY)).ToHashSet();
        if (!actualWallPositions.SetEquals(expectedWallPositions))
        {
            Debug.WriteLine($"StateComparer: Wall positions mismatch");
            return false;
        }
        
        Debug.WriteLine($"StateComparer: All checks passed");
        return true;
    }
    
    private bool CompareDicts(Dictionary<string, int> d1, Dictionary<string, int> d2)
    {
        // Handle null cases
        if (d1 == null && d2 == null) return true;
        if (d1 == null || d2 == null) return false;
        
        if (d1.Count != d2.Count) return false;
        foreach (var kv in d1)
        {
            if (!d2.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
        }
        return true;
    }
}