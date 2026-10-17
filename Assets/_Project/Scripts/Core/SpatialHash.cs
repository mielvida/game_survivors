using System;
using System.Collections.Generic;

namespace Survivors
{
    // Uniform grid for neighbour lookups. Rebuilt every frame; Query returns
    // candidates from overlapping cells, so callers do the exact distance test.
    public sealed class SpatialHash
    {
        readonly float _inv;
        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        readonly Stack<List<int>> _spare = new Stack<List<int>>();

        public SpatialHash(float cellSize)
        {
            _inv = 1f / cellSize;
        }

        static long Key(int cx, int cy)
        {
            return ((long)cx << 32) ^ (uint)cy;
        }

        int Cell(float v)
        {
            return (int)Math.Floor(v * _inv);
        }

        public void Clear()
        {
            foreach (var kv in _cells)
            {
                kv.Value.Clear();
                _spare.Push(kv.Value);
            }
            _cells.Clear();
        }

        public void Insert(int id, float x, float y)
        {
            long k = Key(Cell(x), Cell(y));
            List<int> list;
            if (!_cells.TryGetValue(k, out list))
            {
                list = _spare.Count > 0 ? _spare.Pop() : new List<int>(8);
                _cells[k] = list;
            }
            list.Add(id);
        }

        public void Query(float x, float y, float radius, List<int> results)
        {
            int x0 = Cell(x - radius), x1 = Cell(x + radius);
            int y0 = Cell(y - radius), y1 = Cell(y + radius);
            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cy = y0; cy <= y1; cy++)
                {
                    List<int> list;
                    if (_cells.TryGetValue(Key(cx, cy), out list))
                        results.AddRange(list);
                }
            }
        }
    }
}
