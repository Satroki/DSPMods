using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace AutoConnectPower
{
    internal readonly struct ConnNode
    {
        public readonly Vector3 Dir;
        public readonly float Connect;
        public readonly float Cover;
        public readonly bool IsGhost;

        public ConnNode(Vector3 dir, float connect, float cover, bool isGhost = false)
        {
            Dir = dir;
            Connect = connect;
            Cover = cover;
            IsGhost = isGhost;
        }
    }

    internal sealed class PlannedTower
    {
        public int ProtoId;
        public Vector3 Dir;
        public float Connect;
        public float Cover;
        public int HashId = -1;
        public bool Skip;
    }

    internal sealed class PowerComponent
    {
        public int NetworkId;
        public readonly List<ConnNode> Nodes = new List<ConnNode>();
    }

    internal enum FoundationOutcome
    {
        Already,
        Paved,
        Failed,
    }

    /// <summary>
    /// 扫描当前星球的电力网络，规划最少数量的连接塔，并按配置建造或生成虚影。
    /// </summary>
    internal sealed class PowerGridConnector
    {
        public const int TeslaId = 2201;
        public const int WirelessId = 2202;

        private const float MinTowerSpacing = 3.6f;
        private const float ChainSafety = 1.2f;
        private const float MinStep = 1f;

        private const int FoundationSize = 3;
        private const int FoundationCells = 9;
        private const int FoundationBrushType = 1;
        private const float FoundationBrushRadius = 0.990946f * FoundationSize;

        private static readonly float[] StepFactors = { 1f, 0.85f, 0.7f, 0.55f };
        private static readonly float[] ChainLateralAngles = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
        private static readonly float[] CoverageLateralAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        private readonly PlanetData _planet;
        private readonly PlanetFactory _factory;
        private readonly PowerSystem _power;
        private readonly Player _player;
        private readonly float _radius;
        private readonly bool _hasWater;
        private readonly float _waterLevel;

        private readonly int _primaryId;
        private readonly int _secondaryId;
        private readonly bool _alternate;
        private readonly float _primaryCover;
        private readonly bool _autoFoundation;

        private static readonly Collider[] TmpColliders = new Collider[512];
        private const int BuildCollisionMask = 395264;

        private readonly List<ConnNode> _connected = new List<ConnNode>();
        private readonly List<PlannedTower> _plan = new List<PlannedTower>();
        private readonly List<PowerComponent> _remaining = new List<PowerComponent>();
        private readonly List<Vector3> _uncoveredDirs = new List<Vector3>();
        private readonly PointHash _powerPoints = new PointHash(8f);
        private readonly Vector3[] _foundationPoints = new Vector3[FoundationCells];
        private readonly int[] _foundationIndices = new int[FoundationCells];

        private int _networkCount;
        private int _ghostComponentCount;
        private int _generatorCount;
        private bool _hasGrid;
        private int _failedLinks;
        private int _failedCoverage;
        private int _foundationPaved;
        private int _foundationSkipped;
        private int _foundationItemsUsed;
        private int _foundationSandUsed;

        private PowerGridConnector(PlanetData planet, TowerMode mode)
        {
            _planet = planet;
            _factory = planet.factory;
            _power = _factory.powerSystem;
            _player = GameMain.mainPlayer;
            _radius = planet.realRadius + 0.2f;
            // waterItemId 为 0 表示没有液体球；>0 是水/硫酸等液体，<0 是熔岩等特殊海洋球。
            // 不能只看 waterHeight：海洋星球的水体可能正好位于 realRadius（waterHeight == 0）。
            _hasWater = planet.type == EPlanetType.Ocean
                || planet.type == EPlanetType.Vocano
                || planet.waterItemId != 0
                || Mathf.Abs(planet.waterHeight) > 0.01f;
            _waterLevel = planet.realRadius + planet.waterHeight;
            _autoFoundation = AutoConnectPowerPlugin.AutoFoundation?.Value ?? true;

            ItemProto tesla = LDB.items?.Select(TeslaId);
            ItemProto wireless = LDB.items?.Select(WirelessId);
            bool teslaOk = IsPowerTower(tesla) && IsItemUnlocked(TeslaId);
            bool wirelessOk = IsPowerTower(wireless) && IsItemUnlocked(WirelessId);

            if (mode == TowerMode.Alternate && teslaOk && wirelessOk)
            {
                _primaryId = TeslaId;
                _secondaryId = WirelessId;
                _alternate = true;
            }
            else
            {
                _primaryId = teslaOk ? TeslaId : WirelessId;
                _secondaryId = _primaryId;
                _alternate = false;
            }

            _primaryCover = TowerCover(_primaryId);
        }

        public static void Run()
        {
            PlanetData planet = GameMain.localPlanet;
            if (planet == null || planet.factory == null || !planet.factoryLoaded || planet.factory.powerSystem == null)
            {
                AutoConnectPowerPlugin.Tip("当前星球的电网尚未加载");
                return;
            }

            if (planet.type == EPlanetType.Gas)
            {
                AutoConnectPowerPlugin.Tip("气体行星不需要连接电网");
                return;
            }

            if (GameMain.mainPlayer == null || planet.aux == null || planet.data == null)
            {
                AutoConnectPowerPlugin.Tip("当前星球的建造数据尚未加载");
                return;
            }

            if (!IsUsableTower(TeslaId) && !IsUsableTower(WirelessId))
            {
                AutoConnectPowerPlugin.Tip("尚未解锁电力感应塔或无线输电塔");
                return;
            }

            var connector = new PowerGridConnector(planet, AutoConnectPowerPlugin.TowerModeConfig?.Value ?? TowerMode.Alternate);
            connector.Execute(AutoConnectPowerPlugin.PlacementModeConfig?.Value ?? PlacementMode.Ghost);
        }

        private static bool IsUsableTower(int itemId)
        {
            return IsPowerTower(LDB.items?.Select(itemId)) && IsItemUnlocked(itemId);
        }

        private static bool IsItemUnlocked(int itemId)
        {
            if (GameMain.sandboxToolsEnabled) return true;
            GameHistoryData history = GameMain.history;
            return history == null || history.ItemUnlocked(itemId);
        }

        private static bool IsPowerTower(ItemProto item)
        {
            return item != null && item.prefabDesc != null && item.prefabDesc.isPowerNode;
        }

        private float TowerConnect(int protoId)
        {
            ItemProto item = LDB.items.Select(protoId);
            return item != null && item.prefabDesc != null ? item.prefabDesc.powerConnectDistance : 0f;
        }

        private float TowerCover(int protoId)
        {
            ItemProto item = LDB.items.Select(protoId);
            return item != null && item.prefabDesc != null ? item.prefabDesc.powerCoverRadius : 0f;
        }

        private void Execute(PlacementMode placement)
        {
            var watch = Stopwatch.StartNew();
            Scan();

            if (!_hasGrid)
            {
                AutoConnectPowerPlugin.Tip(_uncoveredDirs.Count > 0
                    ? "本星球没有可用的电网，无法自动连接"
                    : "本星球没有需要连接的电力设备");
                return;
            }

            if (_networkCount <= 1 && _remaining.Count == 0 && _uncoveredDirs.Count == 0)
            {
                AutoConnectPowerPlugin.Tip(_generatorCount == 0
                    ? "电网已连通，但本星球没有发电机，无法供电"
                    : "电网已全部连通，无需处理");
                return;
            }

            if (_generatorCount == 0)
            {
                AutoConnectPowerPlugin.Log?.LogWarning("本星球没有发电机，只能合并现有电网，无法为无供电设备提供电力。");
            }

            if (_remaining.Count > 0)
            {
                ConnectNetworks();
            }

            PlanCoverage();

            if (_plan.Count == 0)
            {
                string message = "没有找到可放置连接塔的位置（可能被地形、水面/熔岩或建筑阻挡）";
                if (_failedLinks > 0 || _failedCoverage > 0)
                {
                    message = $"未能连接：{_failedLinks} 条线路、{_failedCoverage} 个设备被地形或建筑阻挡";
                }

                AutoConnectPowerPlugin.Tip(message);
                return;
            }

            Build(placement, out int built, out int ghosts);

            int planned = _plan.Count;
            int coverageTargets = CountPlannedCoverage();
            int networksAfter = CountNetworks();
            int uncoveredAfter = CountUncoveredConsumers();
            watch.Stop();

            string text;
            if (placement == PlacementMode.Direct && built > 0)
            {
                text = $"自动连接电网：建成 {built} 座，虚影 {ghosts} 座；电网 {_networkCount}→{networksAfter}，无供电设备 {_uncoveredDirs.Count}→{uncoveredAfter}";
            }
            else
            {
                text = $"自动连接电网：生成 {ghosts} 个虚影（共 {planned} 座），建成后合并 {_networkCount} 个电网";
                if (coverageTargets > 0) text += $"，覆盖 {coverageTargets} 个设备";
            }

            if (_ghostComponentCount > 0)
            {
                text += $"，虚影电网 {_ghostComponentCount} 个";
            }

            if (_autoFoundation)
            {
                if (_foundationPaved > 0) text += $"，铺地基 {_foundationPaved} 处（地基 {_foundationItemsUsed}，沙土 {_foundationSandUsed}）";
                if (_foundationSkipped > 0) text += $"，{_foundationSkipped} 座水面/熔岩塔因地基不足跳过";

                if (_foundationPaved == 0 && _foundationSkipped > 0)
                {
                    AutoConnectPowerPlugin.Log?.LogWarning("自动铺地基未成功：检查地基物品（1131）和沙土。");
                }
            }

            if (_failedLinks > 0 || _failedCoverage > 0)
            {
                text += $"；{_failedLinks} 条线路、{_failedCoverage} 个设备未能连接";
            }

            AutoConnectPowerPlugin.Tip(text);
            AutoConnectPowerPlugin.Log?.LogInfo(
                $"自动连接电网：建成 {built}，虚影 {ghosts}，电网 {_networkCount}→{networksAfter}，地基 {_foundationPaved}/{_foundationSkipped}，耗时 {watch.ElapsedMilliseconds}ms");
        }

        private void Scan()
        {
            var groups = new Dictionary<int, PowerComponent>();
            var realNodes = new List<ConnNode>();
            var ghostNodes = new List<ConnNode>();
            var ghostConsumers = new List<Vector3>();
            PowerNodeComponent[] nodes = _power.nodePool;
            EntityData[] entities = _factory.entityPool;

            if (nodes != null)
            {
                for (int i = 1; i < _power.nodeCursor; i++)
                {
                    if (nodes[i].id != i) continue;

                    int entityId = nodes[i].entityId;
                    if (entityId <= 0 || entityId >= entities.Length) continue;

                    EntityData entity = entities[entityId];
                    if (entity.id == 0) continue;

                    Vector3 dir = entity.pos.normalized;
                    if (dir.sqrMagnitude < 0.5f) continue;

                    int networkId = nodes[i].networkId;
                    if (networkId <= 0) continue;

                    if (!groups.TryGetValue(networkId, out PowerComponent component))
                    {
                        component = new PowerComponent { NetworkId = networkId };
                        groups[networkId] = component;
                    }

                    var node = new ConnNode(dir, nodes[i].connectDistance, nodes[i].coverRadius);
                    component.Nodes.Add(node);
                    realNodes.Add(node);
                    _powerPoints.Add(dir * _radius);
                }
            }

            _networkCount = groups.Count;

            PowerGeneratorComponent[] generators = _power.genPool;
            if (generators != null)
            {
                for (int i = 1; i < _power.genCursor; i++)
                {
                    if (generators[i].id == i) _generatorCount++;
                }
            }

            ScanPrebuilds(ghostNodes, ghostConsumers);

            var ghostComponents = BuildGhostComponents(ghostNodes);
            _ghostComponentCount = ghostComponents.Count;

            var components = new List<PowerComponent>(groups.Values);
            components.Sort((a, b) => b.Nodes.Count.CompareTo(a.Nodes.Count));

            // 虚影电力节点如果已经落在已建成电网的连接范围内，建成后就会自动并入，不需要额外铺线。
            if (components.Count > 0 && ghostComponents.Count > 0)
            {
                for (int gi = ghostComponents.Count - 1; gi >= 0; gi--)
                {
                    PowerComponent host = FindGhostHost(components, ghostComponents[gi]);
                    if (host == null) continue;

                    host.Nodes.AddRange(ghostComponents[gi].Nodes);
                    ghostComponents.RemoveAt(gi);
                }
            }

            if (components.Count > 0)
            {
                foreach (ConnNode node in components[0].Nodes)
                {
                    _connected.Add(node);
                }

                for (int i = 1; i < components.Count; i++)
                {
                    _remaining.Add(components[i]);
                }

                foreach (PowerComponent ghost in ghostComponents)
                {
                    _remaining.Add(ghost);
                }
            }
            else if (ghostComponents.Count > 0)
            {
                ghostComponents.Sort((a, b) => b.Nodes.Count.CompareTo(a.Nodes.Count));
                foreach (ConnNode node in ghostComponents[0].Nodes)
                {
                    _connected.Add(node);
                }

                for (int i = 1; i < ghostComponents.Count; i++)
                {
                    _remaining.Add(ghostComponents[i]);
                }
            }

            _hasGrid = components.Count > 0 || ghostComponents.Count > 0;

            PowerConsumerComponent[] consumers = _power.consumerPool;
            if (consumers != null)
            {
                for (int i = 1; i < _power.consumerCursor; i++)
                {
                    if (consumers[i].id != i) continue;
                    if (consumers[i].networkId != 0) continue;

                    Vector3 dir = consumers[i].plugPos.normalized;
                    if (dir.sqrMagnitude < 0.5f) continue;

                    // 已经落在虚影电力塔覆盖范围内的设备，等虚影建成后自然有电，不再重复铺塔。
                    if (IsCoveredByNodes(dir, ghostNodes)) continue;
                    _uncoveredDirs.Add(dir);
                }
            }

            foreach (Vector3 dir in ghostConsumers)
            {
                if (IsCoveredByNodes(dir, realNodes)) continue;
                if (IsCoveredByNodes(dir, ghostNodes)) continue;
                _uncoveredDirs.Add(dir);
            }
        }

        private void ScanPrebuilds(List<ConnNode> ghostNodes, List<Vector3> ghostConsumers)
        {
            PrebuildData[] prebuilds = _factory.prebuildPool;
            if (prebuilds == null) return;

            for (int i = 1; i < _factory.prebuildCursor; i++)
            {
                PrebuildData prebuild = prebuilds[i];
                if (prebuild.id != i || prebuild.isDestroyed) continue;

                ItemProto item = LDB.items?.Select(prebuild.protoId);
                if (item == null || item.prefabDesc == null) continue;

                Vector3 dir = prebuild.pos.normalized;
                if (dir.sqrMagnitude < 0.5f) continue;

                if (item.prefabDesc.isPowerNode)
                {
                    ghostNodes.Add(new ConnNode(
                        dir,
                        item.prefabDesc.powerConnectDistance,
                        item.prefabDesc.powerCoverRadius,
                        true));
                    _powerPoints.Add(dir * _radius);
                    if (item.prefabDesc.isPowerGen) _generatorCount++;
                }
                else if (item.prefabDesc.isPowerConsumer)
                {
                    ghostConsumers.Add(dir);
                }
            }
        }

        private List<PowerComponent> BuildGhostComponents(List<ConnNode> ghostNodes)
        {
            var components = new List<PowerComponent>();

            foreach (ConnNode node in ghostNodes)
            {
                var matches = new List<PowerComponent>();
                foreach (PowerComponent component in components)
                {
                    if (ComponentNear(component, node)) matches.Add(component);
                }

                PowerComponent host;
                if (matches.Count == 0)
                {
                    host = new PowerComponent { NetworkId = -1 - components.Count };
                    components.Add(host);
                }
                else
                {
                    host = matches[0];
                    for (int i = 1; i < matches.Count; i++)
                    {
                        host.Nodes.AddRange(matches[i].Nodes);
                        components.Remove(matches[i]);
                    }
                }

                host.Nodes.Add(node);
            }

            return components;
        }

        private bool ComponentNear(PowerComponent component, ConnNode node)
        {
            for (int i = 0; i < component.Nodes.Count; i++)
            {
                ConnNode other = component.Nodes[i];
                float distance = Chord(other.Dir, node.Dir);
                if (distance <= Mathf.Max(other.Connect, node.Connect) - 0.1f) return true;
            }

            return false;
        }

        private PowerComponent FindGhostHost(List<PowerComponent> components, PowerComponent ghost)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (IsReachable(ghost.Nodes, components[i].Nodes)) return components[i];
            }

            return null;
        }

        private bool IsCoveredByNodes(Vector3 dir, List<ConnNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                ConnNode node = nodes[i];
                if (SqrChord(dir, node.Dir) <= node.Cover * node.Cover) return true;
            }

            return false;
        }

        private void ConnectNetworks()
        {
            while (_remaining.Count > 0)
            {
                int bestComponent = -1;
                int bestNode = -1;
                int bestConnected = -1;
                float bestDistance = float.MaxValue;

                for (int ci = 0; ci < _remaining.Count; ci++)
                {
                    PowerComponent component = _remaining[ci];
                    for (int ni = 0; ni < component.Nodes.Count; ni++)
                    {
                        ConnNode node = component.Nodes[ni];
                        for (int gi = 0; gi < _connected.Count; gi++)
                        {
                            float distance = Chord(node.Dir, _connected[gi].Dir);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                bestComponent = ci;
                                bestNode = ni;
                                bestConnected = gi;
                            }
                        }
                    }
                }

                if (bestComponent < 0) break;

                PowerComponent target = _remaining[bestComponent];
                ConnNode start = _connected[bestConnected];
                ConnNode end = target.Nodes[bestNode];

                if (!TryPlanChain(start, end, out List<PlannedTower> chain))
                {
                    _failedLinks++;
                    AutoConnectPowerPlugin.Log?.LogWarning(
                        $"无法连接电网 {target.NetworkId}（{bestDistance:F0}m）");
                    _remaining.RemoveAt(bestComponent);
                    continue;
                }

                var added = new List<ConnNode>();
                foreach (PlannedTower tower in chain)
                {
                    AddPlanTower(tower);
                    var node = new ConnNode(tower.Dir, tower.Connect, tower.Cover);
                    _connected.Add(node);
                    added.Add(node);
                }

                foreach (ConnNode node in target.Nodes)
                {
                    _connected.Add(node);
                    added.Add(node);
                }

                _remaining.RemoveAt(bestComponent);
                MergeRemainingByPlan(added);
            }
        }

        private void MergeRemainingByPlan(List<ConnNode> added)
        {
            if (added.Count == 0 || _remaining.Count == 0) return;

            var frontier = new List<ConnNode>(added);
            bool changed = true;

            while (changed)
            {
                changed = false;
                for (int ci = _remaining.Count - 1; ci >= 0; ci--)
                {
                    PowerComponent component = _remaining[ci];
                    if (!IsReachable(component.Nodes, frontier)) continue;

                    foreach (ConnNode node in component.Nodes)
                    {
                        _connected.Add(node);
                        frontier.Add(node);
                    }

                    _remaining.RemoveAt(ci);
                    changed = true;
                }
            }
        }

        private bool IsReachable(List<ConnNode> nodes, List<ConnNode> frontier)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                ConnNode a = nodes[i];
                for (int j = 0; j < frontier.Count; j++)
                {
                    ConnNode b = frontier[j];
                    float distance = Chord(a.Dir, b.Dir);
                    float maxDistance = Mathf.Max(a.Connect, b.Connect);
                    if (distance <= maxDistance - 0.1f) return true;
                }
            }

            return false;
        }

        private void PlanCoverage()
        {
            if (_uncoveredDirs.Count == 0 || _connected.Count == 0) return;

            var covered = new bool[_uncoveredDirs.Count];
            foreach (PlannedTower planned in _plan)
            {
                MarkCoveredBy(planned, covered);
            }

            foreach (ConnNode node in _connected)
            {
                if (node.IsGhost) MarkCoveredBy(node.Dir, node.Cover, covered);
            }

            for (int i = 0; i < _uncoveredDirs.Count; i++)
            {
                if (covered[i]) continue;

                Vector3 consumerDir = _uncoveredDirs[i];
                int nearest = FindNearestConnected(consumerDir, out _);
                if (nearest < 0) continue;

                PlannedTower best = null;
                float bestScore = float.MinValue;

                foreach (Vector3 candidate in CoverageCandidates(consumerDir, _connected[nearest].Dir))
                {
                    if (!TryPlaceTower(candidate, _primaryId, false, default, 0f, 5f, CoverageLateralAngles, null, out PlannedTower tower)) continue;
                    if (Chord(tower.Dir, consumerDir) > tower.Cover - 0.05f) continue;

                    int count = CountUncoveredCovered(tower.Dir, tower.Cover, covered);
                    if (count <= 0) continue;

                    int linkTowers = EstimateLinkTowers(tower, _connected[nearest]);
                    float score = count * 100000f - linkTowers * 500f - Chord(tower.Dir, _connected[nearest].Dir);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = tower;
                    }
                }

                if (best == null)
                {
                    _failedCoverage++;
                    continue;
                }

                AddPlanTower(best);
                bool[] coveredBackup = (bool[])covered.Clone();
                MarkCoveredBy(best, covered);

                int gridIndex = FindNearestConnected(best.Dir, out float gridDistance);
                if (gridIndex >= 0)
                {
                    ConnNode grid = _connected[gridIndex];
                    float maxDistance = Mathf.Max(best.Connect, grid.Connect);
                    if (gridDistance > maxDistance - 0.05f)
                    {
                        if (TryPlanChain(new ConnNode(best.Dir, best.Connect, best.Cover), grid, out List<PlannedTower> chain))
                        {
                            foreach (PlannedTower tower in chain)
                            {
                                AddPlanTower(tower);
                                _connected.Add(new ConnNode(tower.Dir, tower.Connect, tower.Cover));
                                MarkCoveredBy(tower, covered);
                            }
                        }
                        else
                        {
                            RemovePlanTower(best);
                            covered = coveredBackup;
                            _failedCoverage++;
                            continue;
                        }
                    }

                    _connected.Add(new ConnNode(best.Dir, best.Connect, best.Cover));
                }
            }
        }

        private IEnumerable<Vector3> CoverageCandidates(Vector3 consumerDir, Vector3 gridDir)
        {
            Vector3 tangent = gridDir - consumerDir * Vector3.Dot(gridDir, consumerDir);
            if (tangent.sqrMagnitude < 1e-6f)
            {
                tangent = Vector3.Cross(consumerDir, Vector3.up);
                if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.Cross(consumerDir, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(consumerDir, tangent).normalized;
            float cover = Mathf.Max(1f, _primaryCover);
            float[] distances = { 0.98f, 0.8f, 0.6f };
            float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };

            for (int di = 0; di < distances.Length; di++)
            {
                for (int ai = 0; ai < angles.Length; ai++)
                {
                    float rad = angles[ai] * Mathf.Deg2Rad;
                    Vector3 offset = (tangent * Mathf.Cos(rad) + bitangent * Mathf.Sin(rad)) * (distances[di] * cover / _radius);
                    yield return (consumerDir + offset).normalized;
                }
            }
        }

        private int CountUncoveredCovered(Vector3 dir, float cover, bool[] covered)
        {
            int count = 0;
            float cover2 = cover * cover;

            for (int i = 0; i < _uncoveredDirs.Count; i++)
            {
                if (covered[i]) continue;
                if (SqrChord(dir, _uncoveredDirs[i]) <= cover2) count++;
            }

            return count;
        }

        private void MarkCoveredBy(PlannedTower tower, bool[] covered)
        {
            MarkCoveredBy(tower.Dir, tower.Cover, covered);
        }

        private void MarkCoveredBy(Vector3 dir, float cover, bool[] covered)
        {
            float cover2 = cover * cover;
            for (int i = 0; i < _uncoveredDirs.Count; i++)
            {
                if (covered[i]) continue;
                if (SqrChord(dir, _uncoveredDirs[i]) <= cover2) covered[i] = true;
            }
        }

        private int CountPlannedCoverage()
        {
            var covered = new bool[_uncoveredDirs.Count];
            foreach (PlannedTower tower in _plan)
            {
                MarkCoveredBy(tower, covered);
            }

            int count = 0;
            for (int i = 0; i < covered.Length; i++)
            {
                if (covered[i]) count++;
            }

            return count;
        }

        private int EstimateLinkTowers(PlannedTower tower, ConnNode grid)
        {
            float distance = Chord(tower.Dir, grid.Dir);
            float maxDistance = Mathf.Max(tower.Connect, grid.Connect);
            if (distance <= maxDistance) return 0;

            float step = Mathf.Max(MinStep, maxDistance - ChainSafety);
            return Mathf.Max(0, Mathf.CeilToInt(distance / step) - 1);
        }

        private int FindNearestConnected(Vector3 dir, out float distance)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _connected.Count; i++)
            {
                float current = Chord(dir, _connected[i].Dir);
                if (current < bestDistance)
                {
                    bestDistance = current;
                    best = i;
                }
            }

            distance = bestDistance;
            return best;
        }

        private bool TryPlanChain(ConnNode start, ConnNode target, out List<PlannedTower> chain)
        {
            chain = null;

            float direct = Chord(start.Dir, target.Dir);
            if (direct <= Mathf.Max(start.Connect, target.Connect) - 0.05f)
            {
                chain = new List<PlannedTower>();
                return true;
            }

            List<PlannedTower> best = null;

            foreach (int startType in StartTypes())
            {
                if (!TryPlanChainFrom(startType, start, target, out List<PlannedTower> candidate)) continue;
                if (best == null || candidate.Count < best.Count) best = candidate;
            }

            if (best == null) return false;

            chain = best;
            return true;
        }

        private IEnumerable<int> StartTypes()
        {
            yield return _primaryId;
            if (_alternate && _secondaryId != _primaryId)
            {
                yield return _secondaryId;
            }
        }

        private int NextTowerType(int startType, int index)
        {
            if (!_alternate) return _primaryId;
            if ((index & 1) == 0) return startType;
            return startType == TeslaId ? WirelessId : TeslaId;
        }

        private bool TryPlanChainFrom(int startType, ConnNode start, ConnNode target, out List<PlannedTower> chain)
        {
            chain = null;
            var planned = new List<PlannedTower>();
            Vector3 current = start.Dir;
            float currentConnect = start.Connect;

            for (int guard = 0; guard < 512; guard++)
            {
                float distance = Chord(current, target.Dir);
                if (distance <= Mathf.Max(currentConnect, target.Connect) - 0.05f)
                {
                    chain = planned;
                    return true;
                }

                int protoId = NextTowerType(startType, planned.Count);
                float maxDistance = Mathf.Max(currentConnect, TowerConnect(protoId));
                float step = maxDistance - ChainSafety;
                if (step < MinStep) break;

                bool placed = false;
                for (int fi = 0; fi < StepFactors.Length; fi++)
                {
                    Vector3 desired = StepTowards(current, target.Dir, step * StepFactors[fi]);
                    if (!TryPlaceTower(desired, protoId, true, current, maxDistance - 0.15f, 2f, ChainLateralAngles, planned, out PlannedTower tower)) continue;

                    planned.Add(tower);
                    current = tower.Dir;
                    currentConnect = tower.Connect;
                    placed = true;
                    break;
                }

                if (!placed) break;
            }

            chain = null;
            return false;
        }

        private bool TryPlaceTower(
            Vector3 desiredDir,
            int protoId,
            bool hasFrom,
            Vector3 fromDir,
            float maxFromDistance,
            float lateral,
            float[] lateralAngles,
            List<PlannedTower> local,
            out PlannedTower tower)
        {
            tower = null;

            for (int i = 0; i < lateralAngles.Length; i++)
            {
                Vector3 candidate = i == 0 ? desiredDir : RotateOnTangent(desiredDir, lateralAngles[i], lateral);
                if (!TryPlaceAt(candidate, protoId, out Vector3 dir)) continue;
                if (hasFrom && Chord(fromDir, dir) > maxFromDistance) continue;

                if (local != null)
                {
                    bool tooClose = false;
                    for (int j = 0; j < local.Count; j++)
                    {
                        if (Chord(local[j].Dir, dir) < MinTowerSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose) continue;
                }

                tower = new PlannedTower
                {
                    ProtoId = protoId,
                    Dir = dir,
                    Connect = TowerConnect(protoId),
                    Cover = TowerCover(protoId),
                };
                return true;
            }

            return false;
        }

        private bool TryPlaceAt(Vector3 desiredDir, int protoId, out Vector3 dir)
        {
            dir = desiredDir;

            Vector3 snapped = _planet.aux.Snap(desiredDir * _radius, false);
            if (snapped.sqrMagnitude < 0.5f) return false;

            dir = snapped.normalized;
            Vector3 position = dir * _radius;

            if (IsUnderwater(position))
            {
                if (!_autoFoundation) return false;
                if (!CanAffordFoundation()) return false;
            }

            if (_powerPoints.AnyWithin(position, MinTowerSpacing)) return false;

            Quaternion rotation = Maths.SphericalRotation(position, 0f);
            if (CollidesWithBuilding(position, rotation, protoId)) return false;

            return true;
        }

        private bool CollidesWithBuilding(Vector3 position, Quaternion rotation, int protoId)
        {
            if (_planet.physics == null) return false;

            ItemProto item = LDB.items.Select(protoId);
            if (item == null || item.prefabDesc == null) return false;

            ColliderData collider = item.prefabDesc.buildCollider;
            Vector3 center = position + rotation * collider.pos;
            Quaternion colliderRotation = rotation * collider.q;
            Vector3 ext = collider.ext;
            if (ext.x < 0.1f) ext.x = 1.0f;
            if (ext.y < 0.1f) ext.y = 1.2f;
            if (ext.z < 0.1f) ext.z = 1.0f;
            // 只留很小的余量：太大余量会把本来能放下的窄缝也判成阻挡。
            ext.x += 0.12f;
            ext.z += 0.12f;

            // 远处的 collider 可能没有在 Unity Physics 中激活；先激活附近 collider，
            // 让下面的 Physics.OverlapBox 能像手动建造一样检测熔炉、传送带等远处建筑。
            NearColliderLogic nearCollider = _planet.physics.nearColliderLogic;
            if (nearCollider != null)
            {
                nearCollider.ActiveCollidersInArea(position, 5f);
                Physics.SyncTransforms();
            }

            int count = Physics.OverlapBoxNonAlloc(
                center,
                ext,
                TmpColliders,
                colliderRotation,
                BuildCollisionMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collided = TmpColliders[i];
                if (collided == null) continue;

                // BuildingCollider 层就是生产设施、传送带、电力塔等建筑碰撞体，直接视为障碍。
                if (collided.gameObject.layer == (int)Layer.BuildingCollider) return true;

                // 矿脉不属于建筑阻挡；建在矿脉上会埋矿，但原生建造也允许。
                if (collided.gameObject.layer == (int)Layer.Vein) continue;

                bool hasData = _planet.physics.GetColliderData(collided, out ColliderData data);

                // 实体/虚影/残骸/敌人等真实对象一律视为障碍；不能只信 isForBuild。
                if (hasData && data.objType != EObjectType.None) return true;

                // 玩家当前拿着的建造预览忽略；虚影建筑没有 BuildPreviewModel，上面已经拦住。
                if (collided.gameObject.layer == 18 && collided.GetComponent<BuildPreviewModel>() != null) continue;
                if (hasData && data.isForBuild) return true;
            }

            return false;
        }

        private void AddPlanTower(PlannedTower tower)
        {
            _plan.Add(tower);
            tower.HashId = _powerPoints.Add(tower.Dir * _radius);
        }

        private void RemovePlanTower(PlannedTower tower)
        {
            _plan.Remove(tower);
            if (tower.HashId >= 0)
            {
                _powerPoints.Remove(tower.HashId);
                tower.HashId = -1;
            }
        }

        private bool IsUnderwater(Vector3 position)
        {
            if (!_hasWater) return false;
            return _planet.data.QueryHeight(position) < _waterLevel + 0.25f;
        }

        private bool CanAffordFoundation()
        {
            if (_player == null) return false;
            int haveItems = _player.package.GetItemCount(PlatformSystem.REFORM_ID) + _player.inhandItemCount;
            return haveItems >= FoundationCells && (_player.sandCount > 0 || HasInfiniteSand());
        }

        private static bool HasInfiniteSand()
        {
            return GameMain.sandboxToolsEnabled &&
                   GameMain.data?.history != null &&
                   GameMain.data.history.HasFeatureKey(FeatureID.SANDBOX_INFINITE_SAND);
        }

        private void PaveFoundations()
        {
            int waterTowers = 0;

            for (int i = 0; i < _plan.Count; i++)
            {
                PlannedTower tower = _plan[i];
                Vector3 position = tower.Dir * _radius;
                if (!IsUnderwater(position)) continue;
                waterTowers++;

                FoundationOutcome outcome;

                try
                {
                    outcome = TryPaveFoundation(position, out int itemsUsed, out int sandUsed);
                    if (outcome == FoundationOutcome.Paved)
                    {
                        _foundationPaved++;
                        _foundationItemsUsed += itemsUsed;
                        _foundationSandUsed += sandUsed;
                    }
                }
                catch (Exception exception)
                {
                    AutoConnectPowerPlugin.Log?.LogWarning("铺地基失败：" + exception);
                    outcome = FoundationOutcome.Failed;
                }

                if (outcome == FoundationOutcome.Failed)
                {
                    tower.Skip = true;
                    _foundationSkipped++;
                }
            }

            if (_foundationSkipped > 0)
            {
                for (int i = _plan.Count - 1; i >= 0; i--)
                {
                    if (_plan[i].Skip) _plan.RemoveAt(i);
                }
            }

            if (_foundationPaved > 0 || _foundationSkipped > 0)
            {
                AutoConnectPowerPlugin.Log?.LogInfo(
                    $"自动地基：水面/熔岩塔 {waterTowers}，成功 {_foundationPaved}，跳过 {_foundationSkipped}");
            }

            // 清掉可能残留的整平缓存，避免后续 BuildFinally 使用到上一次失败的地形改动。
            _factory.BeginFlattenTerrain();
        }

        private FoundationOutcome TryPaveFoundation(Vector3 position, out int itemsUsed, out int sandUsed)
        {
            itemsUsed = 0;
            sandUsed = 0;

            PlatformSystem platform = _factory.platformSystem;
            if (platform == null) return FoundationOutcome.Failed;

            platform.EnsureReformData();
            int count = _planet.aux.ReformSnap(
                position,
                0,
                FoundationSize,
                FoundationBrushType,
                0,
                _foundationPoints,
                _foundationIndices,
                platform,
                out Vector3 center);

            if (count <= 0)
            {
                // ReformSnap 会跳过已经是地基的格；全部已铺时会返回 0。
                int centerIndex = platform.GetReformIndexForPosition(position);
                if (centerIndex >= 0 && platform.IsTerrainReformed(platform.GetReformType(centerIndex)))
                {
                    return FoundationOutcome.Already;
                }

                return FoundationOutcome.Failed;
            }

            bool alreadyReformed = true;
            for (int i = 0; i < count; i++)
            {
                int index = _foundationIndices[i];
                if (index < 0 || platform.GetReformType(index) != FoundationBrushType)
                {
                    alreadyReformed = false;
                    break;
                }
            }

            if (alreadyReformed) return FoundationOutcome.Already;

            int haveItems = _player.package.GetItemCount(PlatformSystem.REFORM_ID) + _player.inhandItemCount;
            if (haveItems < count) return FoundationOutcome.Failed;

            int costSand = 0;
            int getSand = 0;
            _factory.ComputeFlattenTerrainReform(
                _foundationPoints,
                center,
                FoundationBrushRadius,
                count,
                ref costSand,
                ref getSand);

            int needSand = HasInfiniteSand() ? 0 : costSand - getSand;
            long remainingSand = _player.sandCount - needSand;
            if (remainingSand < 0)
            {
                _factory.BeginFlattenTerrain();
                return FoundationOutcome.Failed;
            }

            _factory.FlattenTerrainReform(center, FoundationBrushRadius, FoundationSize, false);
            if (needSand > 0) _player.SetSandCount(remainingSand, ESandSource.Reform);

            for (int i = 0; i < count; i++)
            {
                int index = _foundationIndices[i];
                if (index < 0) continue;
                if (platform.GetReformType(index) != FoundationBrushType) platform.SetReformType(index, FoundationBrushType);
                if (platform.GetReformColor(index) != 0) platform.SetReformColor(index, 0);
            }

            // 立即把改造数据推给 GPU，避免地基要等下一帧/重载才显示。
            if (platform.reformDataBuffer != null)
            {
                try
                {
                    platform.reformDataBuffer.SetData(platform.reformData);
                }
                catch (Exception)
                {
                    // 下一帧 PlanetSimulator 还会再同步一次，这里失败不影响地形数据本身。
                }
            }

            int remainingItems = count;
            if (_player.inhandItemId == PlatformSystem.REFORM_ID && _player.inhandItemCount > 0)
            {
                int take = Mathf.Min(remainingItems, _player.inhandItemCount);
                _player.UseHandItems(take, out _);
                remainingItems -= take;
            }

            if (remainingItems > 0)
            {
                int itemId = PlatformSystem.REFORM_ID;
                int takeCount = remainingItems;
                _player.package.TakeTailItems(ref itemId, ref takeCount, out _);
            }

            itemsUsed = count;
            sandUsed = Mathf.Max(0, needSand);
            if (GameMain.history != null) GameMain.history.MarkItemBuilt(PlatformSystem.REFORM_ID, count);

            return FoundationOutcome.Paved;
        }

        private void Build(PlacementMode placement, out int built, out int ghosts)
        {
            built = 0;
            ghosts = 0;

            if (_autoFoundation)
            {
                PaveFoundations();
            }

            bool direct = placement == PlacementMode.Direct;
            if (direct)
            {
                _factory.BeginFlattenTerrain();
                PlanetFactory.batchBuild = true;
            }

            try
            {
                foreach (PlannedTower tower in _plan)
                {
                    bool consumed = false;
                    if (direct)
                    {
                        consumed = TakeTowerItem(tower.ProtoId);
                    }

                    int itemRequired = direct && consumed ? 0 : 1;
                    PrebuildData prebuild = MakePrebuild(tower, itemRequired);
                    int prebuildId = _factory.AddPrebuildDataWithComponents(prebuild);
                    if (prebuildId <= 0) continue;

                    if (direct && consumed)
                    {
                        _factory.BuildFinally(_player, prebuildId, autoRefresh: false);
                        built++;
                    }
                    else
                    {
                        ghosts++;
                    }
                }
            }
            finally
            {
                if (direct)
                {
                    PlanetFactory.batchBuild = false;
                    _factory.EndFlattenTerrain();
                    _planet.physics?.SetPlanetPhysicsColliderDirty();
                    _planet.physics?.raycastLogic?.NotifyBatchObjectRemove();
                    _planet.audio?.SetPlanetAudioDirty();
                }
            }
        }

        private bool TakeTowerItem(int protoId)
        {
            if (_player.inhandItemId == protoId && _player.inhandItemCount > 0)
            {
                _player.UseHandItems(1, out _);
                return true;
            }

            int itemId = protoId;
            int count = 1;
            _player.package.TakeTailItems(ref itemId, ref count, out _);
            return count == 1 && itemId == protoId;
        }

        private PrebuildData MakePrebuild(PlannedTower tower, int itemRequired)
        {
            ItemProto item = LDB.items.Select(tower.ProtoId);
            Vector3 position = tower.Dir * _radius;
            Quaternion rotation = Maths.SphericalRotation(position, 0f);
            int modelIndex = item != null && item.prefabDesc != null && item.prefabDesc.modelIndex > 0
                ? item.prefabDesc.modelIndex
                : item?.ModelIndex ?? 0;

            return new PrebuildData
            {
                protoId = (short)tower.ProtoId,
                modelIndex = (short)modelIndex,
                pos = position,
                pos2 = position,
                rot = rotation,
                rot2 = rotation,
                isDestroyed = false,
                itemRequired = itemRequired,
                paramCount = 0,
            };
        }

        private int CountNetworks()
        {
            var ids = new HashSet<int>();
            PowerNodeComponent[] nodes = _power.nodePool;
            if (nodes != null)
            {
                for (int i = 1; i < _power.nodeCursor; i++)
                {
                    if (nodes[i].id != i) continue;
                    if (nodes[i].networkId > 0) ids.Add(nodes[i].networkId);
                }
            }

            return ids.Count;
        }

        private int CountUncoveredConsumers()
        {
            int count = 0;
            PowerConsumerComponent[] consumers = _power.consumerPool;
            if (consumers != null)
            {
                for (int i = 1; i < _power.consumerCursor; i++)
                {
                    if (consumers[i].id == i && consumers[i].networkId == 0) count++;
                }
            }

            return count;
        }

        private Vector3 RotateOnTangent(Vector3 dir, float angleDegrees, float distance)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector3 up = dir.normalized;
            Vector3 tangent = Vector3.Cross(up, Vector3.up);

            if (tangent.sqrMagnitude < 1e-4f)
            {
                tangent = Vector3.Cross(up, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(up, tangent).normalized;
            Vector3 offset = (tangent * Mathf.Cos(radians) + bitangent * Mathf.Sin(radians)) * (distance / _radius);
            return (up + offset).normalized;
        }

        private Vector3 StepTowards(Vector3 fromDir, Vector3 toDir, float chordDistance)
        {
            float dot = Mathf.Clamp(Vector3.Dot(fromDir, toDir), -1f, 1f);
            float angle = Mathf.Acos(dot);
            if (angle < 1e-5f) return toDir;

            float chordFraction = Mathf.Clamp(chordDistance / (2f * _radius), 0f, 1f);
            float stepAngle = 2f * Mathf.Asin(chordFraction);
            float t = Mathf.Clamp01(stepAngle / angle);
            return Vector3.Slerp(fromDir, toDir, t).normalized;
        }

        private float Chord(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(SqrChord(a, b));
        }

        private float SqrChord(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (dx * dx + dy * dy + dz * dz) * _radius * _radius;
        }

        /// <summary>按球面位置分桶的电力节点集合，用于快速检查 3.5 m 最小间距。</summary>
        private sealed class PointHash
        {
            private readonly float _cellSize;
            private readonly Dictionary<(int, int, int), List<int>> _buckets =
                new Dictionary<(int, int, int), List<int>>();
            private readonly List<Vector3> _points = new List<Vector3>();
            private readonly List<bool> _active = new List<bool>();

            public PointHash(float cellSize)
            {
                _cellSize = cellSize;
            }

            public int Add(Vector3 point)
            {
                (int, int, int) key = Key(point);
                if (!_buckets.TryGetValue(key, out List<int> bucket))
                {
                    bucket = new List<int>(4);
                    _buckets[key] = bucket;
                }

                int id = _points.Count;
                _points.Add(point);
                _active.Add(true);
                bucket.Add(id);
                return id;
            }

            public void Remove(int id)
            {
                if (id >= 0 && id < _active.Count) _active[id] = false;
            }

            public bool AnyWithin(Vector3 point, float radius)
            {
                float radius2 = radius * radius;
                (int x, int y, int z) = Key(point);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (!_buckets.TryGetValue((x + dx, y + dy, z + dz), out List<int> bucket)) continue;

                            for (int i = 0; i < bucket.Count; i++)
                            {
                                int id = bucket[i];
                                if (!_active[id]) continue;
                                if ((_points[id] - point).sqrMagnitude < radius2) return true;
                            }
                        }
                    }
                }

                return false;
            }

            private (int, int, int) Key(Vector3 point)
            {
                return (
                    Mathf.FloorToInt(point.x / _cellSize),
                    Mathf.FloorToInt(point.y / _cellSize),
                    Mathf.FloorToInt(point.z / _cellSize));
            }
        }
    }
}
