using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    /// <summary>Feldraster der Karte. Gesperrte Felder sind nicht begehbar und verdecken die Sicht.</summary>
    public sealed class GameMap
    {
        private readonly bool[] _blocked;
        private readonly bool[] _opaque;

        public int Width { get; }
        public int Height { get; }

        public GameMap(int width, int height)
        {
            Width = width;
            Height = height;
            _blocked = new bool[width * height];
            _opaque = new bool[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsBlocked(int x, int y) => !InBounds(x, y) || _blocked[y * Width + x];

        public bool IsOpaque(int x, int y) => !InBounds(x, y) || _opaque[y * Width + x];

        public void Block(int x, int y, bool opaque)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            _blocked[y * Width + x] = true;
            _opaque[y * Width + x] |= opaque;
        }

        /// <summary>Macht ein Feld begehbar. Die Sichtsperre bleibt: Eine offene Tür zeigt nicht das ganze Haus.</summary>
        public void Unblock(int x, int y)
        {
            if (InBounds(x, y))
            {
                _blocked[y * Width + x] = false;
            }
        }

        /// <summary>Sichtlinie zwischen zwei Feldern (Bresenham). Start und Ziel zählen nicht.</summary>
        public bool HasLineOfSight(int x0, int y0, int x1, int y1)
        {
            // Immer vom selben Endpunkt aus ziehen: Wer gesehen wird, muss auch selbst sehen können.
            if (x0 > x1 || (x0 == x1 && y0 > y1))
            {
                (x0, y0, x1, y1) = (x1, y1, x0, y0);
            }
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, x = x0, y = y0;
            while (x != x1 || y != y1)
            {
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
                if ((x != x1 || y != y1) && IsOpaque(x, y))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A*-Suche mit acht Richtungen, ohne Ecken zu schneiden. Null, wenn kein Weg existiert.</summary>
        public List<(int X, int Y)>? FindPath(int sx, int sy, int tx, int ty)
        {
            if (IsBlocked(tx, ty))
            {
                return null;
            }
            if (sx == tx && sy == ty)
            {
                return new List<(int, int)>();
            }
            int n = Width * Height;
            var cost = new int[n];
            var from = new int[n];
            Array.Fill(cost, int.MaxValue);
            Array.Fill(from, -1);
            int start = sy * Width + sx, goal = ty * Width + tx;
            cost[start] = 0;
            var open = new PriorityQueue<int, (int, int)>();
            open.Enqueue(start, (Heuristic(sx, sy, tx, ty), 0));
            int order = 0;
            while (open.Count > 0)
            {
                int cur = open.Dequeue();
                if (cur == goal)
                {
                    break;
                }
                int cx = cur % Width, cy = cur / Width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }
                        int nx = cx + dx, ny = cy + dy;
                        if (IsBlocked(nx, ny))
                        {
                            continue;
                        }
                        if (dx != 0 && dy != 0 && (IsBlocked(cx + dx, cy) || IsBlocked(cx, cy + dy)))
                        {
                            continue;
                        }
                        int next = ny * Width + nx;
                        int newCost = cost[cur] + (dx != 0 && dy != 0 ? 14 : 10);
                        if (newCost < cost[next])
                        {
                            cost[next] = newCost;
                            from[next] = cur;
                            open.Enqueue(next, (newCost + Heuristic(nx, ny, tx, ty), ++order));
                        }
                    }
                }
            }
            if (from[goal] == -1)
            {
                return null;
            }
            var path = new List<(int, int)>();
            for (int c = goal; c != start; c = from[c])
            {
                path.Add((c % Width, c / Width));
            }
            path.Reverse();
            return path;
        }

        private static int Heuristic(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            return 10 * Math.Max(dx, dy) + 4 * Math.Min(dx, dy);
        }
    }
}
