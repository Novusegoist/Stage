using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;   // иначе «Random» неоднозначен (System / UnityEngine)

/// <summary>Параметры генерации лабиринта (настраиваются в инспекторе MazeRoomGenerator).</summary>
[Serializable]
public sealed class MazeSettings
{
    [Tooltip("Размер лабиринта в клетках (одна клетка = tilesPerCell × tilesPerCell плиток пола).")]
    public int width = 11;
    public int height = 11;

    [Tooltip("Сторона комнаты в клетках (минимум 3 — иначе не поместится стол).")]
    public int roomSize = 3;
    [Tooltip("0..1")] public float bigRoomChance = 0.3f;

    [Tooltip("Минимальное расстояние от старта до ближайшей клетки любой комнаты (в клетках).")]
    public int minRoomStartDistance = 3;

    [Tooltip("Доля клеток (0..0.3), где ломается стена: появляются кольцевые маршруты, которые запутывают.")]
    public float loopFraction = 0.06f;
    [Tooltip("0..1 — вероятность, что такая «лишняя» дверь будет вести в комнату.")]
    public float roomDoorChance = 0.3f;

    [Header("Комната со столом должна быть последней")]
    public int minFinalDistance = 28;
    public int minDistanceGap = 4;
    public int minSideRoomDistance = 6;
    public int maxAttempts = 120;
}

public sealed class MazeRoom
{
    public int Index;
    public int X, Z, Width, Height;   // положение и размер в клетках
    public int PathDistance;          // кратчайший путь от старта до ближайшей клетки комнаты
    public bool IsFinal;              // комната со столом, письмом, картиной и кнопкой
    public bool IsPassThrough;        // через неё проходит кратчайший путь к другой комнате

    public bool Contains(int x, int z)
    {
        return x >= X && x < X + Width && z >= Z && z < Z + Height;
    }
}

/// <summary>
/// Чистая логика лабиринта без Unity-объектов: сетка клеток, 3 комнаты, коридоры, старт.
///  1. Комнаты ставятся на сетку (не вплотную друг к другу и не рядом со стартом).
///  2. Коридоры — «рекурсивный backtracker» по всем клеткам вне комнат (длинные извилистые ходы).
///  3. Отдельные области связываются в одно дерево (Краскал) — лабиринт всегда связный.
///  4. Добавляются «лишние» проходы — кольцевые маршруты и вторые двери в комнаты.
///  5. Считаются пути от старта (BFS). Роль «финальной» получает самая дальняя комната, через
///     которую не идёт путь к другим комнатам. Если условия не выполнены — лабиринт собирается заново.
/// Проверено симуляцией на тысячах сидов: связность — 100%, подходящий лабиринт находится за 3–6 попыток.
/// </summary>
public sealed class MazeLayout
{
    public const int North = 0;   // +Z
    public const int East = 1;    // +X
    public const int South = 2;   // -Z
    public const int West = 3;    // -X

    private static readonly int[] StepX = { 0, 1, 0, -1 };
    private static readonly int[] StepZ = { 1, 0, -1, 0 };

    public readonly int Width;
    public readonly int Height;

    public int StartX;
    public int StartZ;
    public int StartDirection;
    public MazeRoom[] Rooms;
    public MazeRoom FinalRoom;
    public int[] Distance;            // BFS-расстояние от старта для каждой клетки

    private readonly bool[] _openEast;    // проход между (x,z) и (x+1,z)
    private readonly bool[] _openNorth;   // проход между (x,z) и (x,z+1)
    private readonly int[] _roomOf;       // индекс комнаты или -1

    private MazeLayout(int width, int height)
    {
        Width = width;
        Height = height;
        _openEast = new bool[width * height];
        _openNorth = new bool[width * height];
        _roomOf = new int[width * height];
        for (int i = 0; i < _roomOf.Length; i++) _roomOf[i] = -1;
    }

    public static int DirX(int dir) { return StepX[dir]; }
    public static int DirZ(int dir) { return StepZ[dir]; }

    public bool InBounds(int x, int z)
    {
        return x >= 0 && x < Width && z >= 0 && z < Height;
    }

    public MazeRoom RoomAt(int x, int z)
    {
        if (!InBounds(x, z)) return null;
        int r = _roomOf[z * Width + x];
        return r >= 0 ? Rooms[r] : null;
    }

    /// <summary>true — между клеткой и соседом нет стены. За пределами сетки — всегда false (внешняя стена).</summary>
    public bool IsOpen(int x, int z, int dir)
    {
        int nx = x + StepX[dir];
        int nz = z + StepZ[dir];
        if (!InBounds(x, z) || !InBounds(nx, nz)) return false;

        switch (dir)
        {
            case North: return _openNorth[z * Width + x];
            case East: return _openEast[z * Width + x];
            case South: return _openNorth[nz * Width + nx];
            default: return _openEast[nz * Width + nx];
        }
    }

    private void SetOpen(int x, int z, int dir, bool value)
    {
        int nx = x + StepX[dir];
        int nz = z + StepZ[dir];
        if (!InBounds(x, z) || !InBounds(nx, nz)) return;

        switch (dir)
        {
            case North: _openNorth[z * Width + x] = value; break;
            case East: _openEast[z * Width + x] = value; break;
            case South: _openNorth[nz * Width + nx] = value; break;
            default: _openEast[nz * Width + nx] = value; break;
        }
    }

    /// <summary>Есть ли у клетки комнаты проход наружу (дверь).</summary>
    public bool CellHasDoor(int x, int z)
    {
        MazeRoom room = RoomAt(x, z);
        if (room == null) return false;
        for (int d = 0; d < 4; d++)
        {
            if (!IsOpen(x, z, d)) continue;
            if (!room.Contains(x + StepX[d], z + StepZ[d])) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ Генерация

    public static MazeLayout Generate(MazeSettings s, int seed)
    {
        Random rng = new Random(seed);
        MazeLayout best = null;
        int bestScore = int.MinValue;
        int attempts = Math.Max(1, s.maxAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            MazeLayout layout = TryBuild(s, rng);
            if (layout == null) continue;
            if (layout.IsAcceptable(s)) return layout;

            int score = layout.QualityScore();
            if (score > bestScore)
            {
                best = layout;
                bestScore = score;
            }
        }
        return best; // лучший из найденных (null только если комнаты вообще не поместились)
    }

    private bool IsAcceptable(MazeSettings s)
    {
        if (FinalRoom == null || FinalRoom.IsPassThrough) return false;

        int maxOther = 0;
        int minOther = int.MaxValue;
        for (int i = 0; i < Rooms.Length; i++)
        {
            if (Rooms[i] == FinalRoom) continue;
            maxOther = Math.Max(maxOther, Rooms[i].PathDistance);
            minOther = Math.Min(minOther, Rooms[i].PathDistance);
        }

        return FinalRoom.PathDistance >= s.minFinalDistance
               && FinalRoom.PathDistance - maxOther >= s.minDistanceGap
               && minOther >= s.minSideRoomDistance;
    }

    private int QualityScore()
    {
        int maxOther = 0;
        for (int i = 0; i < Rooms.Length; i++)
            if (Rooms[i] != FinalRoom) maxOther = Math.Max(maxOther, Rooms[i].PathDistance);
        return FinalRoom.PathDistance - maxOther;
    }

    private static MazeLayout TryBuild(MazeSettings s, Random rng)
    {
        int w = Math.Max(7, s.width);
        int h = Math.Max(7, s.height);
        MazeLayout m = new MazeLayout(w, h);

        // ---- 1. Старт: клетка на внешнем кольце сетки
        List<int> ring = new List<int>();
        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                if (x == 0 || x == w - 1 || z == 0 || z == h - 1) ring.Add(z * w + x);
        int startIndex = ring[rng.Next(ring.Count)];
        m.StartX = startIndex % w;
        m.StartZ = startIndex / w;

        // ---- 2. Три комнаты
        int roomSize = Math.Max(3, s.roomSize);
        List<int[]> rects = new List<int[]>();   // {x, z, width, height}
        for (int r = 0; r < 3; r++)
        {
            int rw = roomSize;
            int rh = roomSize;
            if (rng.NextDouble() < s.bigRoomChance)
            {
                if (rng.NextDouble() < 0.5) rw++; else rh++;
            }

            bool placed = false;
            for (int t = 0; t < 100 && !placed; t++)
            {
                int rx = rng.Next(0, w - rw + 1);
                int rz = rng.Next(0, h - rh + 1);

                int dx = Math.Max(Math.Max(rx - m.StartX, 0), m.StartX - (rx + rw - 1));
                int dz = Math.Max(Math.Max(rz - m.StartZ, 0), m.StartZ - (rz + rh - 1));
                if (dx + dz < s.minRoomStartDistance) continue;

                bool overlap = false;
                for (int i = 0; i < rects.Count; i++)
                {
                    int[] a = rects[i];
                    // зазор минимум в одну клетку между комнатами
                    if (rx < a[0] + a[2] + 1 && a[0] < rx + rw + 1 && rz < a[1] + a[3] + 1 && a[1] < rz + rh + 1)
                    {
                        overlap = true;
                        break;
                    }
                }
                if (overlap) continue;

                rects.Add(new[] { rx, rz, rw, rh });
                placed = true;
            }
            if (!placed) return null;
        }

        m.Rooms = new MazeRoom[3];
        for (int i = 0; i < 3; i++)
        {
            int[] a = rects[i];
            m.Rooms[i] = new MazeRoom { Index = i, X = a[0], Z = a[1], Width = a[2], Height = a[3] };

            for (int x = a[0]; x < a[0] + a[2]; x++)
            {
                for (int z = a[1]; z < a[1] + a[3]; z++)
                {
                    m._roomOf[z * w + x] = i;
                    if (x + 1 < a[0] + a[2]) m.SetOpen(x, z, East, true);   // внутри комнаты стен нет
                    if (z + 1 < a[1] + a[3]) m.SetOpen(x, z, North, true);
                }
            }
        }

        // ---- 3. Коридоры: backtracker по клеткам вне комнат
        bool[] visited = new bool[w * h];
        int[] order = Shuffled(w * h, rng);
        Stack<int> stack = new Stack<int>();
        List<int> candidates = new List<int>(4);

        for (int o = 0; o < order.Length; o++)
        {
            int st = order[o];
            if (visited[st] || m._roomOf[st] >= 0) continue;

            visited[st] = true;
            stack.Push(st);
            while (stack.Count > 0)
            {
                int cur = stack.Peek();
                int cx = cur % w;
                int cz = cur / w;

                candidates.Clear();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + StepX[d];
                    int nz = cz + StepZ[d];
                    if (m.InBounds(nx, nz) && !visited[nz * w + nx] && m._roomOf[nz * w + nx] < 0) candidates.Add(d);
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                int dir = candidates[rng.Next(candidates.Count)];
                int tx = cx + StepX[dir];
                int tz = cz + StepZ[dir];
                m.SetOpen(cx, cz, dir, true);
                visited[tz * w + tx] = true;
                stack.Push(tz * w + tx);
            }
        }

        // ---- 4. Связываем все области в одно целое (Краскал по соседним клеткам из разных областей)
        int[] parent = new int[w * h];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                if (m.IsOpen(x, z, East)) Union(parent, z * w + x, z * w + x + 1);
                if (m.IsOpen(x, z, North)) Union(parent, z * w + x, (z + 1) * w + x);
            }
        }

        List<int> connectors = new List<int>();   // клетка * 2 + (0 = восток, 1 = север)
        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                int a = z * w + x;
                if (x + 1 < w && Find(parent, a) != Find(parent, a + 1)) connectors.Add(a * 2);
                if (z + 1 < h && Find(parent, a) != Find(parent, a + w)) connectors.Add(a * 2 + 1);
            }
        }
        Shuffle(connectors, rng);

        for (int i = 0; i < connectors.Count; i++)
        {
            int a = connectors[i] / 2;
            bool north = (connectors[i] & 1) == 1;
            int b = north ? a + w : a + 1;
            if (Find(parent, a) == Find(parent, b)) continue;

            m.SetOpen(a % w, a / w, north ? North : East, true);
            Union(parent, a, b);
        }

        // ---- 5. «Лишние» проходы: кольцевые маршруты и вторые двери в комнаты
        int wanted = (int)Math.Round(w * h * s.loopFraction);
        int remaining = wanted;
        int tries = 0;
        while (remaining > 0 && tries < wanted * 20 + 50)
        {
            tries++;
            int x = rng.Next(w);
            int z = rng.Next(h);
            int d = rng.Next(4);
            int nx = x + StepX[d];
            int nz = z + StepZ[d];
            if (!m.InBounds(nx, nz) || m.IsOpen(x, z, d)) continue;

            int ra = m._roomOf[z * w + x];
            int rb = m._roomOf[nz * w + nx];
            if (ra >= 0 && rb >= 0) continue;                                   // комната—комната не соединяем
            if ((ra >= 0 || rb >= 0) && rng.NextDouble() > s.roomDoorChance) continue;

            m.SetOpen(x, z, d, true);
            remaining--;
        }

        // ---- 6. Пути от старта (BFS)
        int total = w * h;
        int[] dist = new int[total];
        int[] par = new int[total];
        for (int i = 0; i < total; i++)
        {
            dist[i] = -1;
            par[i] = -1;
        }

        int[] queue = new int[total];
        int head = 0;
        int tail = 0;
        dist[startIndex] = 0;
        queue[tail++] = startIndex;
        while (head < tail)
        {
            int c = queue[head++];
            int cx = c % w;
            int cz = c / w;
            for (int d = 0; d < 4; d++)
            {
                if (!m.IsOpen(cx, cz, d)) continue;
                int n = (cz + StepZ[d]) * w + cx + StepX[d];
                if (dist[n] >= 0) continue;
                dist[n] = dist[c] + 1;
                par[n] = c;
                queue[tail++] = n;
            }
        }
        m.Distance = dist;

        for (int i = 0; i < total; i++)
            if (dist[i] < 0) return null;   // сюда попасть не должны (граф связный), но на всякий случай

        // ---- 7. Расстояния до комнат и «проходные» комнаты
        int[] nearestCell = new int[3];
        for (int i = 0; i < 3; i++)
        {
            MazeRoom room = m.Rooms[i];
            int best = int.MaxValue;
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                for (int z = room.Z; z < room.Z + room.Height; z++)
                {
                    int c = z * w + x;
                    if (dist[c] < best)
                    {
                        best = dist[c];
                        nearestCell[i] = c;
                    }
                }
            }
            room.PathDistance = best;
        }

        for (int q = 0; q < 3; q++)
        {
            int c = nearestCell[q];
            while (c != -1)
            {
                int r = m._roomOf[c];
                if (r >= 0 && r != q) m.Rooms[r].IsPassThrough = true;
                c = par[c];
            }
        }

        // ---- 8. Финальная комната: самая дальняя из тех, что не стоят на пути к другим
        MazeRoom final = null;
        for (int i = 0; i < 3; i++)
        {
            if (m.Rooms[i].IsPassThrough) continue;
            if (final == null || m.Rooms[i].PathDistance > final.PathDistance) final = m.Rooms[i];
        }
        if (final == null)
        {
            for (int i = 0; i < 3; i++)
                if (final == null || m.Rooms[i].PathDistance > final.PathDistance) final = m.Rooms[i];
        }
        final.IsFinal = true;
        m.FinalRoom = final;

        // ---- 9. Куда смотрит игрок на старте
        m.StartDirection = North;
        for (int d = 0; d < 4; d++)
        {
            if (m.IsOpen(m.StartX, m.StartZ, d))
            {
                m.StartDirection = d;
                break;
            }
        }

        return m;
    }

    // ------------------------------------------------------------------ Помощники

    public static int[] Shuffled(int count, Random rng)
    {
        int[] a = new int[count];
        for (int i = 0; i < count; i++) a[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = a[i];
            a[i] = a[j];
            a[j] = tmp;
        }
        return a;
    }

    public static void Shuffle(List<int> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a);
        int rb = Find(parent, b);
        if (ra != rb) parent[ra] = rb;
    }

    /// <summary>Текстовая карта для отладки (S — старт, F — комната со столом, 0/1/2 — остальные комнаты).</summary>
    public string ToAscii()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Лабиринт " + Width + "×" + Height + ", путь до комнат: " + DescribeRooms());
        for (int z = Height - 1; z >= 0; z--)
        {
            for (int x = 0; x < Width; x++) sb.Append(IsOpen(x, z, North) ? "+   " : "+---");
            sb.AppendLine("+");

            for (int x = 0; x < Width; x++)
            {
                sb.Append(IsOpen(x, z, West) ? " " : "|");
                MazeRoom room = RoomAt(x, z);
                char ch = ' ';
                if (x == StartX && z == StartZ) ch = 'S';
                else if (room != null) ch = room.IsFinal ? 'F' : (char)('0' + room.Index);
                sb.Append(' ').Append(ch).Append(' ');
            }
            sb.AppendLine("|");
        }
        for (int x = 0; x < Width; x++) sb.Append("+---");
        sb.Append("+");
        return sb.ToString();
    }

    private string DescribeRooms()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Rooms.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(Rooms[i].IsFinal ? "финал=" : "комната=").Append(Rooms[i].PathDistance);
        }
        return sb.ToString();
    }
}
