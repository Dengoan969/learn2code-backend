using System.Diagnostics;
using Learn2Code.Core.Models;

namespace Learn2Code.Services;

public class StateComparer
{
    public bool Compare(SceneState actual, SceneState expected, double tolerance = 0.0)
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
            Debug.WriteLine(
                $"StateComparer: Cat count mismatch: actual={actualCats.Count}, expected={expectedCats.Count}");
            return false;
        }

        if (actualApples.Count != expectedApples.Count)
        {
            Debug.WriteLine(
                $"StateComparer: Apple count mismatch: actual={actualApples.Count}, expected={expectedApples.Count}");
            return false;
        }

        if (actualWalls.Count != expectedWalls.Count)
        {
            Debug.WriteLine(
                $"StateComparer: Wall count mismatch: actual={actualWalls.Count}, expected={expectedWalls.Count}");
            return false;
        }

        if (actualCats.Count == 1 && expectedCats.Count == 1)
        {
            var a = actualCats[0];
            var e = expectedCats[0];

            // Compare X and Y with tolerance
            if (Math.Abs(a.X - e.X) > tolerance)
            {
                Debug.WriteLine($"StateComparer: X mismatch: actual={a.X}, expected={e.X}, tolerance={tolerance}");
                return false;
            }

            if (Math.Abs(a.Y - e.Y) > tolerance)
            {
                Debug.WriteLine($"StateComparer: Y mismatch: actual={a.Y}, expected={e.Y}, tolerance={tolerance}");
                return false;
            }

            if (Math.Abs(a.Direction - e.Direction) > 0.001)
            {
                Debug.WriteLine($"StateComparer: Direction mismatch: actual={a.Direction}, expected={e.Direction}");
                return false;
            }

            if (!CompareDicts(a.SaidTexts, e.SaidTexts))
            {
                Debug.WriteLine("StateComparer: SaidTexts mismatch");
                return false;
            }

            if (!CompareDicts(a.CollectedItems, e.CollectedItems))
            {
                Debug.WriteLine("StateComparer: CollectedItems mismatch");
                return false;
            }
        }

        // Compare apple positions with tolerance
        if (!CompareSpritePositions(actualApples, expectedApples, tolerance))
        {
            Debug.WriteLine("StateComparer: Apple positions mismatch");
            return false;
        }

        // Compare wall positions with tolerance
        if (!CompareSpritePositions(actualWalls, expectedWalls, tolerance))
        {
            Debug.WriteLine("StateComparer: Wall positions mismatch");
            return false;
        }

        Debug.WriteLine("StateComparer: All checks passed");
        return true;
    }

    private bool CompareSpritePositions<T>(List<T> actual, List<T> expected, double tolerance) where T : SpriteState
    {
        if (actual.Count != expected.Count) return false;

        // Sort by X then Y for consistent comparison
        var sortedActual = actual.OrderBy(s => s.X).ThenBy(s => s.Y).ToList();
        var sortedExpected = expected.OrderBy(s => s.X).ThenBy(s => s.Y).ToList();

        for (var i = 0; i < sortedActual.Count; i++)
        {
            var a = sortedActual[i];
            var e = sortedExpected[i];

            if (Math.Abs(a.X - e.X) > tolerance || Math.Abs(a.Y - e.Y) > tolerance)
            {
                Debug.WriteLine(
                    $"CompareSpritePositions: Position mismatch at index {i}: actual=({a.X}, {a.Y}), expected=({e.X}, {e.Y}), tolerance={tolerance}");
                return false;
            }
        }

        return true;
    }

    private bool CompareDicts(Dictionary<string, int> d1, Dictionary<string, int> d2)
    {
        // Handle null cases
        if (d1 == null && d2 == null) return true;
        if (d1 == null || d2 == null) return false;

        if (d1.Count != d2.Count) return false;
        foreach (var kv in d1)
            if (!d2.TryGetValue(kv.Key, out var v) || v != kv.Value)
                return false;
        return true;
    }
}