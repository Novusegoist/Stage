using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Генерирует лабиринт заново при каждом входе в сцену (MazeScene):
///  • коридоры и три комнаты собираются из префабов-кита (Floor / Wall / Ceiling);
///  • комната со столом (письмо, картина, красная кнопка выхода) — самая дальняя от старта;
///  • две остальные комнаты заставлены сундуками и бочками (Chest / Barrel);
///  • освещение — факелы на стенах (Torch); свет включается только у ближайших (TorchLightCuller).
/// Размеры кита измеряются по Renderer.bounds, поэтому любые pivot и габариты префабов подойдут.
/// Если префаб не назначен — вместо него строится заглушка из примитива.
/// Сама логика лабиринта — в MazeLayout (без Unity-объектов).
/// </summary>
public class MazeRoomGenerator : MonoBehaviour
{
    public enum TorchMount { Auto, Floor, Wall }

    [Header("Лабиринт")]
    [SerializeField] private MazeSettings maze = new MazeSettings();
    [Tooltip("0 = при каждом входе новый случайный лабиринт. Любое другое число — всегда один и тот же.")]
    [SerializeField] private int fixedSeed = 0;
    [Tooltip("Сколько плиток пола по стороне одной клетки. 2 плитки по 2 м = коридор шириной 4 м.")]
    [SerializeField] private int tilesPerCell = 2;
    [Tooltip("Высота потолка в метрах (округляется вверх до целого числа рядов стен).")]
    [SerializeField] private float roomHeight = 5f;
    [Tooltip("Печатает карту лабиринта в Console.")]
    [SerializeField] private bool logLayout = false;

    [Header("Префабы кита")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject ceilingPrefab;
    [SerializeField] private GameObject tablePrefab;
    [SerializeField] private GameObject torchPrefab;
    [Tooltip("Сундуки и бочки для боковых комнат. Если пусто — соберутся процедурно.")]
    [SerializeField] private GameObject[] decorPrefabs;

    [Header("Предметы на столе (если пусто — соберутся процедурно)")]
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private GameObject picturePrefab;
    [SerializeField] private GameObject exitButtonPrefab;

    [Header("Материалы заглушек (только если нет префаба)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material ceilingMaterial;

    [Header("Содержимое")]
    [SerializeField] private Texture2D pictureTexture;
    [SerializeField] private string letterTitle = "Письмо";
    [TextArea(10, 20)]
    [SerializeField] private string letterText = "Это секретное послание...";

    [Header("Факелы")]
    [Tooltip("Auto: высокие модели ставятся на пол у стены, низкие — вешаются на стену.")]
    [SerializeField] private TorchMount torchMount = TorchMount.Auto;
    [Tooltip("Высота центра факела над полом в режиме «на стене».")]
    [SerializeField] private float torchWallHeight = 2.4f;
    [Tooltip("Если факел повёрнут к стене не той стороной — подкрутите (например 180).")]
    [SerializeField] private float torchYawOffset = 0f;
    [Tooltip("Минимум клеток между факелами в коридорах.")]
    [SerializeField] private int torchSpacing = 2;
    [Range(0f, 1f)]
    [SerializeField] private float roomTorchChance = 0.6f;
    [Tooltip("Сколько ближайших факелов реально светят (остальные выключены — это главная экономия FPS).")]
    [SerializeField] private int maxActiveTorchLights = 10;
    [SerializeField] private float torchLightRadius = 26f;

    [Header("Боковые комнаты")]
    [SerializeField] private int propsPerRoomMin = 4;
    [SerializeField] private int propsPerRoomMax = 7;
    [Tooltip("Реквизит в комнате со столом (по желанию).")]
    [SerializeField] private int finalRoomPropCount = 0;

    [Header("Скримеры")]
    [Tooltip("Сколько картонных вырезов прячется в стенах (0 — без скримеров). Игрок может отключить их в настройках.")]
    [SerializeField] private int jumpScareCount = 5;
    [Tooltip("Не ближе скольких клеток пути от старта (чтобы не напугать сразу на входе).")]
    [SerializeField] private int minScarePathDistance = 6;
    [Tooltip("Минимум клеток между скримерами.")]
    [SerializeField] private int scareSpacing = 3;

    [Header("Атмосфера")]
    [SerializeField] private bool overrideAmbient = true;
    [SerializeField] private Color labyrinthAmbient = new Color(0.1f, 0.1f, 0.14f);
    [SerializeField] private bool useFog = true;
    [SerializeField] private Color fogColor = new Color(0.03f, 0.03f, 0.05f);
    [SerializeField] private float fogDensity = 0.02f;
    [SerializeField] private float lampIntensity = 1.2f;

    private sealed class TileTemplate
    {
        public GameObject Prefab;
        public string Name;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Scale = Vector3.one;      // множитель к масштабу префаба (для колонн)
        public Vector3 Size;                     // габариты
        public Vector3 CenterOffset;             // от pivot до центра габаритов
        public Vector3 FallbackSize;
        public Material FallbackMaterial;
    }

    private struct Slot
    {
        public int X;
        public int Z;
        public int Dir;
    }

    private Transform _root;
    private float _minX, _minZ, _cell, _floorTop, _wallThick, _ceilingY;
    private Vector3 _torchOutward;
    private bool _torchLogged;
    private readonly List<GameObject> _propPrefabs = new List<GameObject>();
    private readonly List<Vector3> _propPositions = new List<Vector3>();
    private readonly HashSet<int> _torchEdges = new HashSet<int>();

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Labyrinth")]
    public void Generate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[MazeRoomGenerator] Лабиринт собирается только в Play Mode.", this);
            return;
        }

        Clear();
        float startTime = Time.realtimeSinceStartup;

        int seed = fixedSeed != 0 ? fixedSeed : new System.Random().Next(1, int.MaxValue);
        MazeLayout layout = MazeLayout.Generate(maze, seed);
        if (layout == null)
        {
            Debug.LogError("[MazeRoomGenerator] Не удалось разместить комнаты. Увеличьте размер лабиринта (maze.width/height).", this);
            return;
        }
        if (logLayout) Debug.Log(layout.ToAscii());

        System.Random rng = new System.Random(seed ^ 0x2545F491);

        // Образец материала с рабочим шейдером (у вашего кита) — для предметов, собираемых кодом.
        MazeItemFactory.SetTemplateMaterial(FindTemplateMaterial());

        _root = new GameObject("Labyrinth").transform;
        _root.SetParent(transform, false);
        Transform geometry = NewChild("Geometry");
        Transform props = NewChild("Props");

        Vector3 origin = transform.position;
        _floorTop = origin.y;

        // ---------- Измеряем кит ----------
        Vector3 floorFallback = new Vector3(2f, 0.1f, 2f);
        Vector3 wallFallback = new Vector3(0.2f, 3f, 2f);

        TileTemplate floorT = MakeTemplate(floorPrefab, "Floor", floorFallback, floorMaterial, Quaternion.identity, Vector3.one);
        TileTemplate ceilingT = MakeTemplate(ceilingPrefab != null ? ceilingPrefab : floorPrefab, "Ceiling", floorFallback,
            ceilingMaterial != null ? ceilingMaterial : floorMaterial, Quaternion.identity, Vector3.one);

        float tileSize = Mathf.Max(0.5f, Mathf.Max(floorT.Size.x, floorT.Size.z));
        int perCell = Mathf.Max(1, tilesPerCell);
        _cell = tileSize * perCell;

        TileTemplate wallProbe = MakeTemplate(wallPrefab, "Wall", wallFallback, wallMaterial, Quaternion.identity, Vector3.one);
        bool longAlongX = wallProbe.Size.x >= wallProbe.Size.z;
        float wallLength = Mathf.Max(0.5f, Mathf.Max(wallProbe.Size.x, wallProbe.Size.z));
        _wallThick = Mathf.Min(wallProbe.Size.x, wallProbe.Size.z);
        float wallHeight = Mathf.Max(0.5f, wallProbe.Size.y);

        Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
        TileTemplate wallAlongX = MakeTemplate(wallPrefab, "Wall", wallFallback, wallMaterial, longAlongX ? Quaternion.identity : turn, Vector3.one);
        TileTemplate wallAlongZ = MakeTemplate(wallPrefab, "Wall", wallFallback, wallMaterial, longAlongX ? turn : Quaternion.identity, Vector3.one);

        // Колонна в узлах сетки закрывает «выемки» там, где стены сходятся под углом.
        float pillar = _wallThick * 1.12f;
        Vector3 pillarScale = longAlongX
            ? new Vector3(pillar / wallLength, 1f, pillar / _wallThick)
            : new Vector3(pillar / _wallThick, 1f, pillar / wallLength);
        TileTemplate pillarT = MakeTemplate(wallPrefab, "Pillar", wallFallback, wallMaterial, Quaternion.identity, pillarScale);

        int width = layout.Width;
        int height = layout.Height;
        _minX = origin.x - width * _cell * 0.5f;
        _minZ = origin.z - height * _cell * 0.5f;

        int rows = Mathf.Max(1, Mathf.CeilToInt(roomHeight / wallHeight));
        _ceilingY = _floorTop + rows * wallHeight;

        // ---------- Пол и потолок ----------
        for (int cx = 0; cx < width; cx++)
        {
            for (int cz = 0; cz < height; cz++)
            {
                for (int i = 0; i < perCell; i++)
                {
                    for (int j = 0; j < perCell; j++)
                    {
                        float px = _minX + cx * _cell + (i + 0.5f) * tileSize;
                        float pz = _minZ + cz * _cell + (j + 0.5f) * tileSize;
                        SpawnTile(floorT, new Vector3(px, _floorTop - floorT.Size.y * 0.5f, pz), geometry);
                        SpawnTile(ceilingT, new Vector3(px, _ceilingY + ceilingT.Size.y * 0.5f, pz), geometry);
                    }
                }
            }
        }

        // ---------- Стены ----------
        int segments = Mathf.Max(1, Mathf.RoundToInt(_cell / wallLength));
        float seg = _cell / segments;

        for (int ez = 0; ez <= height; ez++)
        {
            for (int ex = 0; ex < width; ex++)
            {
                if (!HorizontalWall(layout, ex, ez)) continue;
                for (int k = 0; k < segments; k++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        SpawnTile(wallAlongX, new Vector3(_minX + ex * _cell + (k + 0.5f) * seg, _floorTop + (r + 0.5f) * wallHeight, _minZ + ez * _cell), geometry);
                    }
                }
            }
        }

        for (int ex = 0; ex <= width; ex++)
        {
            for (int ez = 0; ez < height; ez++)
            {
                if (!VerticalWall(layout, ex, ez)) continue;
                for (int k = 0; k < segments; k++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        SpawnTile(wallAlongZ, new Vector3(_minX + ex * _cell, _floorTop + (r + 0.5f) * wallHeight, _minZ + ez * _cell + (k + 0.5f) * seg), geometry);
                    }
                }
            }
        }

        for (int vx = 0; vx <= width; vx++)
        {
            for (int vz = 0; vz <= height; vz++)
            {
                bool horizontal = (vx < width && HorizontalWall(layout, vx, vz)) || (vx > 0 && HorizontalWall(layout, vx - 1, vz));
                bool vertical = (vz < height && VerticalWall(layout, vx, vz)) || (vz > 0 && VerticalWall(layout, vx, vz - 1));
                if (!horizontal || !vertical) continue;

                for (int r = 0; r < rows; r++)
                    SpawnTile(pillarT, new Vector3(_minX + vx * _cell, _floorTop + (r + 0.5f) * wallHeight, _minZ + vz * _cell), geometry);
            }
        }

        TryStaticBatch(geometry);

        // ---------- Реквизит, свет, предметы ----------
        _propPrefabs.Clear();
        if (decorPrefabs != null)
        {
            for (int i = 0; i < decorPrefabs.Length; i++)
                if (decorPrefabs[i] != null) _propPrefabs.Add(decorPrefabs[i]);
        }
        _propPositions.Clear();
        _torchEdges.Clear();

        TorchLightCuller culler = _root.gameObject.AddComponent<TorchLightCuller>();

        // Телефон: меньше одновременно горящих факелов и ближе радиус (свет и частицы — главная нагрузка).
        int lightLimit = maxActiveTorchLights;
        float lightRadius = torchLightRadius;
        if (PlatformUtil.IsMobile)
        {
            lightLimit = Mathf.Min(lightLimit, GraphicsQuality.Effective >= GraphicsQuality.Medium ? 6 : 4);
            lightRadius = Mathf.Min(lightRadius, 18f);
        }
        culler.Configure(lightRadius, lightLimit);

        ApplyAtmosphere();
        PlaceTorches(layout, props, rng, culler);

        for (int i = 0; i < layout.Rooms.Length; i++)
        {
            MazeRoom room = layout.Rooms[i];
            if (room.IsFinal)
            {
                BuildFinalRoom(layout, room, props, rng);
                if (finalRoomPropCount > 0) PlaceProps(layout, room, finalRoomPropCount, props, rng);
            }
            else
            {
                int count = rng.Next(Mathf.Max(0, propsPerRoomMin), Mathf.Max(propsPerRoomMin, propsPerRoomMax) + 1);
                PlaceProps(layout, room, count, props, rng);
            }
        }

        PlaceJumpScares(layout, props, rng);

        PlacePlayer(layout);

        Debug.Log("[MazeRoomGenerator] Лабиринт " + width + "×" + height + " собран за " +
                  ((Time.realtimeSinceStartup - startTime) * 1000f).ToString("F0") + " мс (seed " + seed + "). Путь до комнат: " +
                  DescribeRooms(layout));
    }

    [ContextMenu("Clear Labyrinth")]
    public void Clear()
    {
        if (_root != null) MazeItemFactory.DestroyObject(_root.gameObject);
        _root = null;
    }

    // =====================================================================
    //  Стены
    // =====================================================================

    // Горизонтальный (вдоль X) отрезок стены на линии ez между ex и ex+1.
    private static bool HorizontalWall(MazeLayout layout, int ex, int ez)
    {
        if (ez <= 0 || ez >= layout.Height) return true;   // внешняя граница
        return !layout.IsOpen(ex, ez - 1, MazeLayout.North);
    }

    // Вертикальный (вдоль Z) отрезок стены на линии ex между ez и ez+1.
    private static bool VerticalWall(MazeLayout layout, int ex, int ez)
    {
        if (ex <= 0 || ex >= layout.Width) return true;
        return !layout.IsOpen(ex - 1, ez, MazeLayout.East);
    }

    // =====================================================================
    //  Факелы
    // =====================================================================

    private void PlaceTorches(MazeLayout layout, Transform props, System.Random rng, TorchLightCuller culler)
    {
        if (torchPrefab == null)
        {
            Debug.LogWarning("[MazeRoomGenerator] Не назначен Torch — лабиринт будет почти тёмным (останется только лампа над столом).", this);
            return;
        }

        _torchOutward = ComputeTorchOutward();

        List<int> torchCells = new List<int>();
        List<int> closed = new List<int>(4);
        int[] order = MazeLayout.Shuffled(layout.Width * layout.Height, rng);

        for (int o = 0; o < order.Length; o++)
        {
            int cx = order[o] % layout.Width;
            int cz = order[o] / layout.Width;

            closed.Clear();
            for (int d = 0; d < 4; d++)
                if (!layout.IsOpen(cx, cz, d)) closed.Add(d);
            if (closed.Count == 0) continue;

            if (layout.RoomAt(cx, cz) == null)
            {
                // Коридор: не чаще чем раз в torchSpacing клеток.
                bool tooClose = false;
                for (int i = 0; i < torchCells.Count; i++)
                {
                    int px = torchCells[i] % layout.Width;
                    int pz = torchCells[i] / layout.Width;
                    if (Mathf.Max(Mathf.Abs(px - cx), Mathf.Abs(pz - cz)) < torchSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                int dir = closed[rng.Next(closed.Count)];
                PlaceTorch(cx, cz, dir, 0f, props, culler);
                torchCells.Add(order[o]);
            }
            else
            {
                // Комната: на части стен, со случайным сдвигом вдоль стены.
                for (int i = 0; i < closed.Count; i++)
                {
                    if (rng.NextDouble() > roomTorchChance) continue;
                    float lateral = (float)(rng.NextDouble() * 1.4 - 0.7);
                    PlaceTorch(cx, cz, closed[i], lateral, props, culler);
                }
            }
        }
    }

    // Куда «смотрит» факел: от центра модели к источнику света (пламя наклонено от стены).
    private Vector3 ComputeTorchOutward()
    {
        GameObject probe = Instantiate(torchPrefab);
        Light light = probe.GetComponentInChildren<Light>();
        Vector3 v = Vector3.zero;
        if (light != null)
        {
            v = light.transform.position - GetMeshBounds(probe).center;
            v.y = 0f;
        }
        Object.DestroyImmediate(probe);

        return v.sqrMagnitude > 0.0025f ? v.normalized : Vector3.zero;
    }

    private void PlaceTorch(int cx, int cz, int dir, float lateral, Transform props, TorchLightCuller culler)
    {
        Vector3 n;
        Vector3 wallFace;
        EdgeFrame(cx, cz, dir, out n, out wallFace);

        GameObject torch = Instantiate(torchPrefab, props);
        torch.name = "Torch";

        float yaw = _torchOutward.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(_torchOutward, n, Vector3.up) : 0f;
        torch.transform.rotation = Quaternion.Euler(0f, yaw + torchYawOffset, 0f) * torch.transform.rotation;

        Bounds b = GetMeshBounds(torch);
        bool onFloor = torchMount == TorchMount.Floor || (torchMount == TorchMount.Auto && b.size.y >= 1.6f);
        if (!_torchLogged)
        {
            _torchLogged = true;
            Debug.Log("[MazeRoomGenerator] Факел: высота модели " + b.size.y.ToString("F2") + " м → " +
                      (onFloor ? "ставится на пол у стены" : "вешается на стену на высоте " + torchWallHeight.ToString("F1") + " м") +
                      ". Изменить: поле Torch Mount / Torch Wall Height / Torch Yaw Offset.");
        }

        float along = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.z) * b.extents.z;
        Vector3 lateralDir = Vector3.Cross(Vector3.up, n);
        Vector3 target = wallFace + n * (along + 0.02f) + lateralDir * lateral;
        float targetY = onFloor ? _floorTop + b.extents.y : _floorTop + torchWallHeight;

        torch.transform.position += new Vector3(target.x - b.center.x, targetY - b.center.y, target.z - b.center.z);

        // Теневые точечные источники в URP очень дороги — факелам тени не нужны.
        Light[] lights = torch.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++) lights[i].shadows = LightShadows.None;

        culler.Register(torch);
        _torchEdges.Add(EdgeKey(cx, cz, dir));
    }

    // =====================================================================
    //  Сундуки и бочки
    // =====================================================================

    private void PlaceProps(MazeLayout layout, MazeRoom room, int count, Transform props, System.Random rng)
    {
        List<Slot> slots = new List<Slot>();
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int z = room.Z; z < room.Z + room.Height; z++)
            {
                if (layout.CellHasDoor(x, z)) continue;   // у дверей не загромождаем проход
                for (int d = 0; d < 4; d++)
                {
                    if (layout.IsOpen(x, z, d)) continue;
                    if (_torchEdges.Contains(EdgeKey(x, z, d))) continue;
                    slots.Add(new Slot { X = x, Z = z, Dir = d });
                }
            }
        }

        int[] order = MazeLayout.Shuffled(slots.Count, rng);
        int placed = 0;
        for (int i = 0; i < order.Length && placed < count; i++)
        {
            Slot slot = slots[order[i]];
            if (PlaceProp(slot, props, rng)) placed++;
        }
    }

    private bool PlaceProp(Slot slot, Transform props, System.Random rng)
    {
        Vector3 n;
        Vector3 wallFace;
        EdgeFrame(slot.X, slot.Z, slot.Dir, out n, out wallFace);

        bool faceInward;
        GameObject prop = CreateProp(props, rng, out faceInward);

        float yaw = faceInward
            ? Vector3.SignedAngle(Vector3.forward, n, Vector3.up) + (float)(rng.NextDouble() * 20.0 - 10.0)
            : (float)(rng.NextDouble() * 360.0);
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prop.transform.rotation;

        Bounds b = GetMeshBounds(prop);
        float along = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.z) * b.extents.z;
        float sign = rng.Next(2) == 0 ? -1f : 1f;
        float lateral = sign * (float)(0.9 + rng.NextDouble() * 0.7);
        Vector3 target = wallFace + n * (along + 0.08f) + Vector3.Cross(Vector3.up, n) * lateral;

        // Не ставим предметы друг в друга.
        for (int i = 0; i < _propPositions.Count; i++)
        {
            Vector3 other = _propPositions[i];
            if ((new Vector2(other.x, other.z) - new Vector2(target.x, target.z)).sqrMagnitude < 2.4f * 2.4f)
            {
                Object.DestroyImmediate(prop);
                return false;
            }
        }

        prop.transform.position += new Vector3(target.x - b.center.x, _floorTop - b.min.y, target.z - b.center.z);
        _propPositions.Add(target);
        return true;
    }

    private GameObject CreateProp(Transform props, System.Random rng, out bool faceInward)
    {
        GameObject go;
        if (_propPrefabs.Count > 0)
        {
            GameObject prefab = _propPrefabs[rng.Next(_propPrefabs.Count)];
            go = Instantiate(prefab, props);
            faceInward = prefab.name.ToLowerInvariant().Contains("chest");
        }
        else
        {
            bool chest = rng.NextDouble() < 0.45;
            go = chest ? MazeItemFactory.CreateChest() : MazeItemFactory.CreateBarrel();
            go.transform.SetParent(props, false);
            faceInward = chest;
        }
        MazeItemFactory.RepairMaterials(go);
        return go;
    }

    // =====================================================================
    //  Скримеры
    // =====================================================================

    private void PlaceJumpScares(MazeLayout layout, Transform props, System.Random rng)
    {
        if (jumpScareCount <= 0 || !GameSettings.JumpScaresEnabled) return;

        // Кандидаты: свободные участки стен вне комнаты со столом, далеко от старта, без факелов и дверей.
        List<Slot> slots = new List<Slot>();
        for (int cx = 0; cx < layout.Width; cx++)
        {
            for (int cz = 0; cz < layout.Height; cz++)
            {
                MazeRoom room = layout.RoomAt(cx, cz);
                if (room != null && room.IsFinal) continue;
                if (layout.Distance[cz * layout.Width + cx] < minScarePathDistance) continue;
                if (room != null && layout.CellHasDoor(cx, cz)) continue;

                for (int d = 0; d < 4; d++)
                {
                    if (layout.IsOpen(cx, cz, d)) continue;
                    if (_torchEdges.Contains(EdgeKey(cx, cz, d))) continue;
                    slots.Add(new Slot { X = cx, Z = cz, Dir = d });
                }
            }
        }

        int[] order = MazeLayout.Shuffled(slots.Count, rng);
        List<Slot> placed = new List<Slot>();
        int variant = rng.Next(ScareAssets.VariantCount);
        int clip = rng.Next(ScareAssets.ClipCount);

        for (int i = 0; i < order.Length && placed.Count < jumpScareCount; i++)
        {
            Slot slot = slots[order[i]];

            bool tooClose = false;
            for (int p = 0; p < placed.Count; p++)
            {
                if (Mathf.Max(Mathf.Abs(placed[p].X - slot.X), Mathf.Abs(placed[p].Z - slot.Z)) < scareSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Vector3 n;
            Vector3 wallFace;
            EdgeFrame(slot.X, slot.Z, slot.Dir, out n, out wallFace);
            if (IsNearProp(wallFace, 2.6f)) continue;   // у сундуков и бочек вырез мог бы торчать из реквизита

            GameObject go = new GameObject("JumpScare");
            go.transform.SetParent(props, false);
            go.transform.position = wallFace;

            // Картинки и звуки создаются с задержкой в кадрах, чтобы не было одного большого фриза.
            go.AddComponent<JumpScare>().Setup(n, _wallThick, variant, clip, 3 + placed.Count * 4);

            variant++;
            clip++;
            placed.Add(slot);
        }
    }

    private bool IsNearProp(Vector3 point, float radius)
    {
        for (int i = 0; i < _propPositions.Count; i++)
        {
            Vector3 other = _propPositions[i];
            Vector2 delta = new Vector2(other.x - point.x, other.z - point.z);
            if (delta.sqrMagnitude < radius * radius) return true;
        }
        return false;
    }

    // =====================================================================
    //  Комната со столом
    // =====================================================================

    private void BuildFinalRoom(MazeLayout layout, MazeRoom room, Transform props, System.Random rng)
    {
        Vector3 center = new Vector3(
            _minX + (room.X + room.Width * 0.5f) * _cell,
            _floorTop,
            _minZ + (room.Z + room.Height * 0.5f) * _cell);

        // Поворачиваем «набор» так, чтобы лицевая сторона письма/картины смотрела на главный вход.
        float yaw = 0f;
        int doorDir;
        if (FindMainDoor(layout, room, out doorDir))
            yaw = Vector3.SignedAngle(Vector3.forward, new Vector3(-MazeLayout.DirX(doorDir), 0f, -MazeLayout.DirZ(doorDir)), Vector3.up);
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        // Лампа над столом (без теней — дёшево).
        GameObject lampGo = new GameObject("TableLamp");
        lampGo.transform.SetParent(props, false);
        lampGo.transform.position = new Vector3(center.x, _ceilingY - 0.8f, center.z);
        Light lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = _cell * 2.5f;
        lamp.intensity = lampIntensity;
        lamp.color = new Color(1f, 0.95f, 0.85f);
        lamp.shadows = LightShadows.None;

        // ---- Стол
        GameObject table;
        if (tablePrefab != null)
        {
            table = Instantiate(tablePrefab, props);
        }
        else
        {
            table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.SetParent(props, false);
            table.transform.localScale = new Vector3(2.1f, 1.05f, 1.2f);
            TintPrimitive(table, new Color(0.45f, 0.3f, 0.12f));
        }
        table.name = "Table";

        Bounds local = GetMeshBounds(table);          // размеры стола в его собственных осях
        float sx = local.size.x;
        float sz = local.size.z;

        table.transform.rotation = yawRot * table.transform.rotation;
        Bounds tb = GetMeshBounds(table);
        table.transform.position += new Vector3(center.x - tb.center.x, _floorTop - tb.min.y, center.z - tb.center.z);
        tb = GetMeshBounds(table);
        float top = tb.max.y;
        Vector3 tc = tb.center;

        // ---- Письмо — слева, лежит лицом вверх.
        GameObject letter = letterPrefab != null ? Instantiate(letterPrefab, props) : MazeItemFactory.CreateLetter();
        letter.transform.SetParent(props, true);
        MazeItemFactory.RepairMaterials(letter);
        LetterInteractable li = letter.GetComponent<LetterInteractable>();
        if (li == null) li = letter.AddComponent<LetterInteractable>();
        li.SetLetterTitle(letterTitle);
        li.SetLetterText(letterText, letterText);
        PlaceOnSurface(letter, yawRot * Quaternion.Euler(90f, 12f, 0f), tc + yawRot * new Vector3(-sx * 0.28f, 0f, -sz * 0.12f), top);

        // ---- Картина — по центру у дальнего края, чуть откинута назад.
        GameObject picture = picturePrefab != null ? Instantiate(picturePrefab, props) : MazeItemFactory.CreatePictureFrame();
        picture.transform.SetParent(props, true);
        MazeItemFactory.RepairMaterials(picture);
        PictureInteractable pi = picture.GetComponent<PictureInteractable>();
        if (pi == null) pi = picture.AddComponent<PictureInteractable>();
        pi.SetPictureTexture(pictureTexture);
        PlaceOnSurface(picture, yawRot * Quaternion.Euler(12f, 0f, 0f), tc + yawRot * new Vector3(0f, 0f, sz * 0.22f), top);

        // ---- Красная кнопка выхода — справа.
        GameObject button = exitButtonPrefab != null ? Instantiate(exitButtonPrefab, props) : MazeItemFactory.CreateExitButton();
        button.transform.SetParent(props, true);
        MazeItemFactory.RepairMaterials(button);
        if (button.GetComponent<ExitButtonInteractable>() == null) button.AddComponent<ExitButtonInteractable>();
        PlaceOnSurface(button, yawRot, tc + yawRot * new Vector3(sx * 0.30f, 0f, -sz * 0.10f), top);
    }

    // Вход, которым игрок скорее всего зайдёт: дверь, за которой клетка ближе всего к старту.
    private static bool FindMainDoor(MazeLayout layout, MazeRoom room, out int doorDir)
    {
        doorDir = 0;
        int bestDistance = int.MaxValue;
        bool found = false;

        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int z = room.Z; z < room.Z + room.Height; z++)
            {
                for (int d = 0; d < 4; d++)
                {
                    if (!layout.IsOpen(x, z, d)) continue;
                    int nx = x + MazeLayout.DirX(d);
                    int nz = z + MazeLayout.DirZ(d);
                    if (room.Contains(nx, nz)) continue;

                    int dist = layout.Distance[nz * layout.Width + nx];
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        doorDir = d;
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    // =====================================================================
    //  Атмосфера и игрок
    // =====================================================================

    private void ApplyAtmosphere()
    {
        if (overrideAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = labyrinthAmbient;
        }

        if (useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    private void PlacePlayer(MazeLayout layout)
    {
        // Берём игрока у PlayerManager: найденный по тегу дубликат мог ещё не успеть удалиться.
        GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.GetCurrentPlayer() : null;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[MazeRoomGenerator] Игрок не найден (нужен объект с тегом Player).", this);
            return;
        }

        Vector3 ground = CellCenter(layout.StartX, layout.StartZ);
        float yaw = Mathf.Atan2(MazeLayout.DirX(layout.StartDirection), MazeLayout.DirZ(layout.StartDirection)) * Mathf.Rad2Deg;
        PlayerManager.PlaceOnGround(player, ground, yaw);
    }

    // =====================================================================
    //  Геометрия: шаблоны и помощники
    // =====================================================================

    // Лучший образец: материал кита без текстур (его вариант шейдера точно есть в сборке).
    private Material FindTemplateMaterial()
    {
        Material best = null;
        int bestScore = int.MaxValue;

        List<Material> candidates = new List<Material>();
        GameObject[] prefabs = { tablePrefab, ceilingPrefab, floorPrefab, wallPrefab };
        for (int p = 0; p < prefabs.Length; p++)
        {
            if (prefabs[p] == null) continue;
            Renderer[] renderers = prefabs[p].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] is ParticleSystemRenderer) continue;
                Material[] mats = renderers[r].sharedMaterials;
                for (int m = 0; m < mats.Length; m++) candidates.Add(mats[m]);
            }
        }
        candidates.Add(floorMaterial);
        candidates.Add(wallMaterial);
        candidates.Add(ceilingMaterial);

        for (int i = 0; i < candidates.Count; i++)
        {
            Material c = candidates[i];
            if (c == null || MazeItemFactory.IsBroken(c)) continue;

            int score = MazeItemFactory.TextureCount(c);
            if (score < bestScore)
            {
                best = c;
                bestScore = score;
                if (score == 0) break;
            }
        }
        return best;
    }

    private Transform NewChild(string childName)
    {
        Transform t = new GameObject(childName).transform;
        t.SetParent(_root, false);
        return t;
    }

    private Vector3 CellCenter(int cx, int cz)
    {
        return new Vector3(_minX + (cx + 0.5f) * _cell, _floorTop, _minZ + (cz + 0.5f) * _cell);
    }

    // Нормаль внутрь клетки и точка на внутренней грани стены (на середине ребра).
    private void EdgeFrame(int cx, int cz, int dir, out Vector3 inward, out Vector3 wallFace)
    {
        float dx = MazeLayout.DirX(dir);
        float dz = MazeLayout.DirZ(dir);
        inward = new Vector3(-dx, 0f, -dz);
        wallFace = CellCenter(cx, cz) + new Vector3(dx, 0f, dz) * (_cell * 0.5f - _wallThick * 0.5f);
    }

    private static int EdgeKey(int cx, int cz, int dir)
    {
        return (cx * 1000 + cz) * 4 + dir;
    }

    private TileTemplate MakeTemplate(GameObject prefab, string templateName, Vector3 fallbackSize, Material fallbackMaterial, Quaternion rotation, Vector3 scale)
    {
        TileTemplate t = new TileTemplate
        {
            Prefab = prefab,
            Name = templateName,
            Rotation = rotation,
            Scale = scale,
            FallbackSize = fallbackSize,
            FallbackMaterial = fallbackMaterial
        };

        GameObject probe = SpawnTile(t, Vector3.zero, null);
        Bounds b = GetMeshBounds(probe);
        t.Size = b.size;
        t.CenterOffset = b.center - probe.transform.position;
        Object.DestroyImmediate(probe);
        return t;
    }

    // Создаёт плитку так, чтобы центр её габаритов оказался в точке center.
    private GameObject SpawnTile(TileTemplate t, Vector3 center, Transform parent)
    {
        Vector3 pos = center - t.CenterOffset;
        GameObject go;

        if (t.Prefab != null)
        {
            Quaternion rot = t.Rotation * t.Prefab.transform.rotation;
            go = parent != null ? Instantiate(t.Prefab, pos, rot, parent) : Instantiate(t.Prefab, pos, rot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, t.Rotation);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.localScale = t.FallbackSize;
            if (t.FallbackMaterial != null) go.GetComponent<Renderer>().sharedMaterial = t.FallbackMaterial;
            else MazeItemFactory.RepairMaterials(go);   // в сборке материал примитива розовый
        }

        if (t.Scale != Vector3.one) go.transform.localScale = Vector3.Scale(go.transform.localScale, t.Scale);
        go.name = t.Name;
        return go;
    }

    // Габариты только по мешам (частицы и трейлы не считаем — их bounds раздувают размер факела).
    private static Bounds GetMeshBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!has) b = new Bounds(go.transform.position, Vector3.one);
        return b;
    }

    private static void TintPrimitive(GameObject go, Color color)
    {
        MazeItemFactory.RepairMaterials(go);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) MazeItemFactory.SetMaterialColor(r.material, color);
    }

    /// <summary>Кладёт объект на поверхность: низ габаритов = surfaceY, центр по XZ = xz.</summary>
    private static void PlaceOnSurface(GameObject go, Quaternion rotation, Vector3 xz, float surfaceY)
    {
        go.transform.rotation = rotation;
        Bounds b = GetMeshBounds(go);
        go.transform.position += new Vector3(xz.x - b.center.x, surfaceY + 0.002f - b.min.y, xz.z - b.center.z);
    }

    // Статический батчинг склеивает ~1500 плиток в несколько draw call'ов.
    // Нужен Read/Write у мешей — если его нет, просто пропускаем (останется SRP Batcher).
    private static void TryStaticBatch(Transform root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null || !mesh.isReadable)
            {
                Debug.Log("[MazeRoomGenerator] Статический батчинг пропущен: у меша '" + (mesh != null ? mesh.name : "null") +
                          "' выключен Read/Write (Import Settings → Read/Write). Это не ошибка.");
                return;
            }
        }
        StaticBatchingUtility.Combine(root.gameObject);
    }

    private static string DescribeRooms(MazeLayout layout)
    {
        string s = "";
        for (int i = 0; i < layout.Rooms.Length; i++)
        {
            if (i > 0) s += ", ";
            s += (layout.Rooms[i].IsFinal ? "ФИНАЛ=" : "комната=") + layout.Rooms[i].PathDistance;
        }
        return s;
    }
}
