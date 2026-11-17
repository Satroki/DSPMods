using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace AutoPlaceMiner
{
    /// <summary>
    /// 大型采矿机（Advanced Mining Machine）的自动摆放。
    ///
    /// 先按矿脉群把目标矿点投影到局部平面，在每个角度用固定大小矩形的滑动扫描
    /// 直接求最大覆盖中心；几何候选坐标再交给游戏原生的
    /// BuildTool_Click.CheckBuildConditions 做碰撞、地形、建造范围、资源类型和间距检查。
    /// 复杂地形下几何候选全部失败时，才退回原有的网格搜索。
    /// </summary>
    internal static class MinerPlacer
    {
        // 大型采矿机的覆盖区是一个 12.5 x 15.5 的矩形，中心在矿机后方 10m。
        private const float CoarseAngleStep = 10f;
        private const float FineAngleStep = 0.5f;
        private const float CoarseCenterStep = 2f;
        private const float FineCenterStep = 0.5f;
        private const float RefineCenterRadius = 2.5f;
        private const float RefineAngleRadius = 5f;
        private const int RefineSeedCount = 8;
        private const int ExtendedSeedCount = 12;
        private const float ExtendedCenterRadius = 3f;
        private const float ExtendedCenterStep = 1f;
        private const float ExtendedAngleRadius = 10f;
        private const float ExtendedAngleStep = 1f;
        private const float NearbyRadius = 24f;
        private const float TargetGroupRadius = 35f;

        // 大型采矿机覆盖区：宽 12.5m、深 15.5m，中心在矿机后方 10m。
        private const float FieldHalfWidth = 6.25f;
        private const float FieldHalfDepth = 7.75f;
        private const float FieldCenterOffset = 10f;
        private const float DirectAngleStep = 0.5f;
        private const int DirectCentersPerAngle = 6;
        private const int MaxFullCandidates = 128;
        private const float DirectNearbyRadius = 36f;

        // 原生校验每次都会做 Physics.OverlapBox，限制次数避免在完全不可建造的位置卡住。
        private const int MaxNativeChecks = 512;
        private const int MaxSampledChecks = 256;

        // 大型采矿机的 PrebuildData.parameters 前 2048 项是预留槽，矿脉 id 从 2048 开始。
        private const int VeinParameterOffset = 2048;

        private static readonly FieldInfo TmpPackageField =
            typeof(BuildTool).GetField("tmpPackage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TmpInhandIdField =
            typeof(BuildTool).GetField("tmpInhandId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TmpInhandCountField =
            typeof(BuildTool).GetField("tmpInhandCount", BindingFlags.Instance | BindingFlags.NonPublic);

        // 建造模式下，当前手持建筑的预览模型本身带有 layer 18 的 BoxCollider。
        // 原生 CheckBuildConditions 只跳过 previewIndex 相同的预览；我们使用独立的
        // 校验器时 previewIndex 对不上，会把玩家自己的预览当成碰撞体。搜索期间先隐藏
        // 这些预览模型，结束后恢复。
        private static readonly FieldInfo PreviewModelsField =
            typeof(BuildModel).GetField("previewModels", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly struct NearbyVein
        {
            // 原生大型采矿机的范围判断先把矿点和矿机位置都投影到 200m 球面，
            // 因此这里预计算一次，后续每个候选角度只做点积，不再反复 Normalize。
            public readonly Vector3 Normalized;
            public readonly int Group;
            public readonly EVeinType Type;

            public NearbyVein(VeinData vein)
            {
                Normalized = vein.pos.normalized * 200f;
                Group = vein.groupIndex;
                Type = vein.type;
            }
        }

        private sealed class Candidate
        {
            public Vector3 FieldCenter;
            public Vector3 Position;
            public float Yaw;
            public int TargetCount;
            public int TotalCount;
        }

        private readonly struct AngleCenter
        {
            public readonly float U;
            public readonly float V;
            public readonly int Count;

            public AngleCenter(float u, float v, int count)
            {
                U = u;
                V = v;
                Count = count;
            }
        }

        internal static void TryPlace()
        {
            if (!GameMain.isRunning || GameMain.isPaused || DSPGame.IsMenuDemo) return;

            var player = GameMain.mainPlayer;
            var planet = GameMain.localPlanet;
            if (player == null || planet == null || !planet.factoryLoaded) return;

            var factory = planet.factory;
            var physics = planet.physics;
            if (factory == null || physics == null || physics.nearColliderLogic == null) return;

            var build = player.controller?.actionBuild;
            if (build == null || !build.active || build.activeTool != build.clickTool) return;

            var click = build.clickTool;
            if (!click.cursorValid) return;

            var item = LDB.items.Select(player.inhandItemId);
            if (item == null) return;

            var desc = click.handPrefabDesc ?? item.prefabDesc;
            if (desc == null || !desc.veinMiner || !desc.isVeinCollector) return;

            var position = click.castGroundPosSnapped;
            if (position.sqrMagnitude < 1f) position = player.buildTarget.position;
            if (position.sqrMagnitude < 1f) return;

            if (!HasItem(player, item.ID))
            {
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "背包中没有大型采矿机",
                    "No Advanced Mining Machine in the inventory."));
                return;
            }

            var autoFoundation = AutoPlaceMinerPlugin.AutoFoundation?.Value ?? true;
            var restoreBuried = AutoPlaceMinerPlugin.RestoreBuriedVeins?.Value ?? true;
            if (!FindTargetGroup(factory, position, planet.realRadius, restoreBuried,
                    out var group, out var type, out var groupCenter, out var groupRadius, out var groupVeinCount))
            {
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "附近没有可开采的固体矿脉",
                    "No minable solid veins nearby."));
                return;
            }

            if (restoreBuried)
            {
                var restored = RestoreTargetVeins(factory, planet, physics, group, type, groupRadius);
                if (!ComputeGroupStats(factory, group, type, planet.realRadius, false,
                        out groupCenter, out groupRadius, out groupVeinCount))
                {
                    AutoPlaceMinerPlugin.Tip(restored > 0
                        ? L10n.T("被掩埋的矿脉恢复后仍没有可用矿脉",
                            "No usable veins remain after restoring buried veins.")
                        : L10n.T("矿脉被完全掩埋，且无法安全恢复",
                            "The veins are fully buried and could not be safely restored."));
                    return;
                }

                if (restored > 0)
                    AutoPlaceMinerPlugin.VerboseInfo(
                        $"恢复被掩埋矿脉 {restored} 条，矿脉群共 {groupVeinCount} 条");
            }

            // 直接按角度做固定大小矩形的滑动扫描，求每个角度的几何最优覆盖中心。
            var searchTimer = Stopwatch.StartNew();
            var direct = BuildDirectCandidates(factory, planet, player, group, type, groupVeinCount,
                groupCenter, groupRadius, position, click.yaw, out var optimalOutOfRange);
            searchTimer.Stop();

            if (optimalOutOfRange)
            {
                AutoPlaceMinerPlugin.VerboseWarning(
                    $"几何最优位置超出建造范围（候选 {direct.Count} 个），不回退网格搜索");
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "最优摆放位置超出建造范围，请靠近矿脉后再试。",
                    "The best placement position is outside build range. Move closer to the veins and try again."));
                return;
            }

            var directSupplyMissing = false;
            if (direct.Count > 0)
            {
                var placeTimer = Stopwatch.StartNew();
                var placed = Place(player, planet, factory, physics, click, item, desc, direct,
                    groupCenter, groupRadius, groupVeinCount, autoFoundation, out directSupplyMissing,
                    reportFailure: false);
                placeTimer.Stop();
                if (placed)
                {
                    AutoPlaceMinerPlugin.VerboseInfo(
                        $"大型采矿机使用几何扫描候选完成放置（{direct.Count} 个候选）；" +
                        $"几何搜索 {searchTimer.ElapsedMilliseconds}ms，原生校验+放置 {placeTimer.ElapsedMilliseconds}ms");
                    return;
                }
            }

            // 已经找到满覆盖候选时不能退回低覆盖位置；否则日志会出现“找到 20 条、
            // 实际只放 18 条”的情况。让玩家清理碰撞/地形后再试。
            if (direct.Count > 0 && direct[0].TargetCount >= groupVeinCount)
            {
                if (directSupplyMissing)
                {
                    AutoPlaceMinerPlugin.VerboseWarning(
                        "满覆盖候选需要地基，但地基物品或沙土不足");
                    AutoPlaceMinerPlugin.Tip(L10n.T(
                        "地基物品或沙土不足，无法为水中矿脉铺设地基。",
                        "Not enough foundation items or soil to pave the underwater vein placement."));
                    return;
                }

                AutoPlaceMinerPlugin.VerboseWarning(
                    $"满覆盖候选均未通过原生建造校验（{direct.Count} 个候选），不回退低覆盖位置");
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "找到满覆盖位置，但原生建造校验未通过。请清理碰撞或换一个矿脉群边缘再试。",
                    "A full-coverage position was found but failed native build checks. Clear obstructions or try another edge of the vein group."));
                return;
            }

            // 没有满覆盖候选时，退回原来的网格搜索，保证复杂地形下仍有机会。
            var fallbackTimer = Stopwatch.StartNew();
            var candidates = BuildCandidates(factory, planet, player, group, type,
                groupCenter, groupRadius, groupVeinCount, position, click.yaw);
            fallbackTimer.Stop();
            if (candidates.Count == 0)
            {
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "当前矿脉附近没有可覆盖的建造位置",
                    "No valid placement position near the current veins."));
                return;
            }

            AutoPlaceMinerPlugin.VerboseInfo(
                $"目标矿脉群 {group}（{type}），共 {groupVeinCount} 条，中心 {groupCenter}，半径 {groupRadius:0.0}m，" +
                $"几何候选 {direct.Count} 个（{searchTimer.ElapsedMilliseconds}ms），" +
                $"兜底网格候选 {candidates.Count} 个（{fallbackTimer.ElapsedMilliseconds}ms），" +
                $"兜底最大覆盖 {candidates[0].TargetCount}/{groupVeinCount} 条");
            Place(player, planet, factory, physics, click, item, desc, candidates,
                groupCenter, groupRadius, groupVeinCount, autoFoundation, out _, reportFailure: true);
        }

        private static bool Place(Player player, PlanetData planet, PlanetFactory factory, PlanetPhysics physics,
            BuildTool_Click click, ItemProto item, PrefabDesc desc, List<Candidate> candidates,
            Vector3 searchCenter, float searchRadius, int groupVeinCount, bool autoFoundation,
            out bool foundationSupplyMissing, bool reportFailure)
        {
            foundationSupplyMissing = false;
            var validator = new BuildTool_Click();
            validator._Init(GameMain.data);
            try
            {
                validator.planet = planet;
                validator.factory = factory;
                validator.handItem = item;
                validator.handPrefabDesc = desc;
                validator.castGroundPosSnapped = candidates[0].Position;

                if (!PrepareValidator(validator))
                {
                    if (reportFailure) AutoPlaceMinerPlugin.Tip(L10n.T(
                        "无法初始化建造校验器，请查看日志",
                        "Could not initialize the build validator. Check the log."));
                    return false;
                }

                var preview = new BuildPreview();
                validator.buildPreviews.Add(preview);
                var livePreviewIndex = click.buildPreviews != null && click.buildPreviews.Count > 0
                    ? click.buildPreviews[0].previewIndex
                    : -1;
                var liveCondition = click.buildPreviews != null && click.buildPreviews.Count > 0
                    ? click.buildPreviews[0].condition
                    : EBuildCondition.Failure;

                var oldTest = player.controller.cmd.test;
                var hiddenPreviews = HideLivePreviewModels(click.actionBuild.model);
                try
                {
                    // 一次性激活整个搜索区附近的碰撞体；否则 Physics.OverlapBox 可能漏掉
                    // 离当前镜头较远、但仍在候选区域内的建筑。
                    physics.RefreshColliders();
                    physics.nearColliderLogic.ActiveCollidersInArea(searchCenter,
                        Mathf.Max(64f, searchRadius + 52f));
                    Physics.SyncTransforms();

                    var geometryObstacles = CollectPlacementObstacles(physics, searchCenter,
                        Mathf.Max(48f, searchRadius + 36f));
                    var geometrySkips = 0;

                    Candidate best = null;
                    var bestTotal = 0;
                    var rejected = new Dictionary<EBuildCondition, int>();
                    var examined = 0;
                    var foundationPlaced = 0;
                    var foundationItems = 0;
                    var foundationSand = 0;
                    var groundFailures = new HashSet<Candidate>();

                    foreach (var candidate in candidates)
                    {
                        // 候选表按目标矿脉数、总矿脉数、离当前位置距离排序。
                        if (best != null &&
                            (candidate.TargetCount < best.TargetCount ||
                             candidate.TargetCount == best.TargetCount && candidate.TotalCount <= bestTotal)) break;

                        if (examined++ >= MaxNativeChecks) break;

                        // 满覆盖候选经常只是底座压到矿脉。先用与原生相同的 Box/Sphere/Capsule
                        // 几何做一次碰撞预筛，避免为每个 Collide 候选执行 Physics.OverlapBox。
                        if (candidate.TargetCount >= groupVeinCount &&
                            GeometryCollides(desc, candidate, geometryObstacles))
                        {
                            geometrySkips++;
                            rejected[EBuildCondition.Collide] =
                                rejected.TryGetValue(EBuildCondition.Collide, out var skipped) ? skipped + 1 : 1;
                            continue;
                        }

                        if (!Validate(validator, preview, item, desc, candidate, livePreviewIndex,
                                out var total, out var condition))
                        {
                            rejected[condition] = rejected.TryGetValue(condition, out var count) ? count + 1 : 1;
                            if (condition == EBuildCondition.NeedGround || condition == EBuildCondition.NeedWater)
                                groundFailures.Add(candidate);
                            continue;
                        }

                        if (best == null || candidate.TargetCount > best.TargetCount ||
                            candidate.TargetCount == best.TargetCount && total > bestTotal)
                        {
                            best = candidate;
                            bestTotal = total;
                        }
                    }

                    // 最常见的高覆盖位置可能被建筑/地形挡住。再抽样检查后面的候选，
                    // 避免因为前 512 个候选全被拒绝而直接放弃。
                    if (best == null && candidates.Count > MaxNativeChecks)
                    {
                        var stride = Mathf.Max(1, candidates.Count / MaxSampledChecks);
                        for (var i = MaxNativeChecks; i < candidates.Count; i += stride)
                        {
                            var candidate = candidates[i];
                            if (!Validate(validator, preview, item, desc, candidate, livePreviewIndex,
                                    out var total, out var condition))
                            {
                                rejected[condition] = rejected.TryGetValue(condition, out var count) ? count + 1 : 1;
                                if (condition == EBuildCondition.NeedGround || condition == EBuildCondition.NeedWater)
                                    groundFailures.Add(candidate);
                                continue;
                            }

                            if (best == null || candidate.TargetCount > best.TargetCount ||
                                candidate.TargetCount == best.TargetCount && total > bestTotal)
                            {
                                best = candidate;
                                bestTotal = total;
                            }
                        }
                    }

                    // 水面矿脉：普通校验会因没有陆地支撑而失败。只在没有更好的陆地
                    // 位置时，尝试给最高覆盖的水面候选铺地基，再重新按原生规则校验。
                    if (autoFoundation && (best == null || best.TargetCount < groupVeinCount))
                    {
                        var foundationSize = FoundationHelper.GetFoundationSize(desc);
                        var attempts = 0;
                        AutoPlaceMinerPlugin.VerboseInfo(
                            $"自动地基检查：水面判定 {NeedsFoundation(planet, candidates[0].Position, candidates[0].Yaw, desc)}，" +
                            $"NeedGround/NeedWater 失败候选 {groundFailures.Count} 个，地基尺寸 {foundationSize}×{foundationSize}");
                        foreach (var candidate in candidates)
                        {
                            if (best != null && candidate.TargetCount <= best.TargetCount) break;
                            if (attempts >= 4) break;
                            // 只处理水面/水边候选；陆地上的 NeedGround（陡坡、悬崖）
                            // 不应被自动地基整平，否则会出现“地面下沉、矿机悬空”。
                            if (!NeedsFoundation(planet, candidate.Position, candidate.Yaw, desc)) continue;
                            if (!FoundationHelper.CanAttempt(player))
                            {
                                foundationSupplyMissing = true;
                                AutoPlaceMinerPlugin.VerboseWarning("自动地基材料不足：需要地基物品和沙土");
                                break;
                            }

                            attempts++;
                            if (!FoundationHelper.TryPave(planet, factory, player, candidate.Position, foundationSize,
                                    out var itemsUsed, out var sandUsed, out var supplyMissing, out var error))
                            {
                                if (supplyMissing) foundationSupplyMissing = true;
                                AutoPlaceMinerPlugin.VerboseWarning(
                                    $"水中矿脉铺地基失败：{error ?? "未知原因"}（位置 {candidate.Position}）");
                                if (supplyMissing) break;
                                continue;
                            }

                            foundationPlaced++;
                            foundationItems += itemsUsed;
                            foundationSand += sandUsed;
                            // FlattenTerrainReform 把地基整平到海平面附近；不能再使用
                            // 原高地地形的 QueryModifiedHeight，否则地形下沉后矿机会悬空。
                            candidate.Position = candidate.Position.normalized * (planet.realRadius + 0.2f);

                            physics.RefreshColliders();
                            physics.SetPlanetPhysicsColliderDirty();
                            physics.nearColliderLogic.ActiveCollidersInArea(candidate.Position, 48f);
                            Physics.SyncTransforms();

                            if (!Validate(validator, preview, item, desc, candidate, livePreviewIndex,
                                    out var total, out var condition))
                            {
                                rejected[condition] = rejected.TryGetValue(condition, out var count) ? count + 1 : 1;
                                AutoPlaceMinerPlugin.VerboseWarning(
                                    $"水中矿脉铺地基后仍无法建造：{condition}（位置 {candidate.Position}）");
                                continue;
                            }

                            if (best == null || candidate.TargetCount > best.TargetCount ||
                                candidate.TargetCount == best.TargetCount && total > bestTotal)
                            {
                                best = candidate;
                                bestTotal = total;
                            }
                        }
                    }

                    if (best == null)
                    {
                        var reasons = new List<string>();
                        foreach (var pair in rejected) reasons.Add($"{pair.Key}×{pair.Value}");
                        AutoPlaceMinerPlugin.VerboseWarning(
                            $"大型采矿机候选位置被原生规则拒绝：{string.Join(", ", reasons)}" +
                            (reportFailure ? "" : "（快速候选，接着做完整搜索）"));
                        if (reportFailure) AutoPlaceMinerPlugin.Tip(foundationSupplyMissing
                            ? L10n.T("地基物品或沙土不足，无法为水中矿脉铺设地基。",
                                "Not enough foundation items or soil to pave the underwater vein placement.")
                            : L10n.T("未找到无碰撞的摆放位置",
                                "No collision-free placement position was found."));
                        return false;
                    }

                    // 选中布局后重新跑一次原生校验，确保预览数据与最终放置一致。
                    if (!Validate(validator, preview, item, desc, best, livePreviewIndex,
                            out bestTotal, out _))
                    {
                        if (reportFailure) AutoPlaceMinerPlugin.Tip(L10n.T(
                            "选中的位置已失效，请重试",
                            "The selected position is no longer valid. Try again."));
                        return false;
                    }

                    click.yaw = best.Yaw;
                    click.castGroundPosSnapped = best.Position;
                    click.actionBuild.buildTargetPositionWanted = best.Position;
                    validator.castGroundPosSnapped = best.Position;
                    validator.CreatePrebuilds();

                    var prebuildId = preview.objId < 0 ? -preview.objId : 0;
                    if (prebuildId <= 0)
                    {
                        if (reportFailure) AutoPlaceMinerPlugin.Tip(L10n.T(
                            "放置失败：物品不足或位置无效",
                            "Placement failed: missing items or invalid position."));
                        return false;
                    }

                    if (geometrySkips > 0)
                        AutoPlaceMinerPlugin.VerboseInfo(
                            $"几何预筛跳过 {geometrySkips} 个与矿脉/建筑碰撞的满覆盖候选");
                    if (rejected.Count > 0)
                    {
                        var reasons = new List<string>();
                        foreach (var pair in rejected) reasons.Add($"{pair.Key}×{pair.Value}");
                        AutoPlaceMinerPlugin.VerboseInfo(
                            $"本次原生校验拒绝统计：{string.Join(", ", reasons)}");
                    }
                    AutoPlaceMinerPlugin.Log?.LogInfo(
                        $"已放置大型采矿机：预建造 {prebuildId}，位置 {best.Position}，角度 {best.Yaw:0.0}°，" +
                        $"矿脉群共 {groupVeinCount} 条，本机覆盖当前群 {best.TargetCount} 条，同类型总覆盖 {bestTotal} 条" +
                        (foundationPlaced > 0
                            ? $"，水中地基 {foundationPlaced} 处（地基 {foundationItems}，沙土 {foundationSand}）"
                            : ""));
                    //AutoPlaceMinerPlugin.Tip(
                    //    $"已自动定位并放置大型采矿机：矿脉群共 {groupVeinCount} 条，本机覆盖 {best.TargetCount} 条" +
                    //    (bestTotal > best.TargetCount ? $"，同类型共覆盖 {bestTotal} 条" : "") +
                    //    $"（{best.Yaw:0.0}°）");
                    return true;
                }
                finally
                {
                    player.controller.cmd.test = oldTest;
                    RestoreLivePreviewModels(hiddenPreviews);
                }
            }
            finally
            {
                validator._Free();
            }
        }

        private static bool NeedsFoundation(PlanetData planet, Vector3 position, float yaw, PrefabDesc desc)
        {
            if (FoundationHelper.IsUnderwater(planet, position)) return true;
            if (desc?.landPoints == null) return false;

            var rotation = Maths.SphericalRotation(position, yaw);
            foreach (var point in desc.landPoints)
            {
                var landPoint = position + rotation * new Vector3(point.x, 0f, point.z);
                if (FoundationHelper.IsUnderwater(planet, landPoint)) return true;
            }

            return false;
        }

        private static List<ColliderData> CollectPlacementObstacles(PlanetPhysics physics, Vector3 center,
            float radius)
        {
            var obstacles = new List<ColliderData>();
            if (physics?.colChunks == null) return obstacles;

            foreach (var chunk in physics.colChunks)
            {
                for (var i = 1; i < chunk.cursor; i++)
                {
                    var collider = chunk.colliderPool[i];
                    if (collider.idType == 0) continue;
                    if (!collider.isForBuild && collider.objType != EObjectType.Vein) continue;
                    var bound = radius + collider.ext.magnitude + collider.radius;
                    if ((collider.pos - center).sqrMagnitude <= bound * bound)
                        obstacles.Add(collider);
                }
            }

            return obstacles;
        }

        private static bool GeometryCollides(PrefabDesc desc, Candidate candidate,
            List<ColliderData> obstacles)
        {
            if (!desc.hasBuildCollider || desc.buildColliders == null) return false;

            var rotation = Maths.SphericalRotation(candidate.Position, candidate.Yaw);
            foreach (var source in desc.buildColliders)
            {
                var collider = source;
                collider.pos = candidate.Position + rotation * source.pos;
                collider.q = rotation * source.q;
                var box = new MinerPlacementGeometry.Box(collider);
                foreach (var obstacle in obstacles)
                    if (MinerPlacementGeometry.Intersects(box, obstacle)) return true;
            }

            return false;
        }

        private static bool PrepareValidator(BuildTool validator)
        {
            if (TmpPackageField == null || TmpInhandIdField == null || TmpInhandCountField == null)
            {
                AutoPlaceMinerPlugin.Log?.LogError(
                    "找不到 BuildTool 的 tmpPackage/tmpInhandId/tmpInhandCount 字段，游戏版本可能已变更");
                AutoPlaceMinerPlugin.Tip(L10n.T(
                    "游戏版本不兼容：找不到建造校验字段",
                    "Incompatible game version: build validator fields not found."));
                return false;
            }

            if (TmpPackageField.GetValue(validator) == null)
                TmpPackageField.SetValue(validator, new StorageComponent(1));
            return true;
        }

        private static bool Validate(BuildTool_Click validator, BuildPreview preview, ItemProto item,
            PrefabDesc desc, Candidate candidate, int livePreviewIndex, out int total,
            out EBuildCondition condition)
        {
            total = 0;
            condition = EBuildCondition.Failure;
            var position = candidate.Position;

            preview.ResetAll();
            preview.item = item;
            preview.desc = desc;
            preview.lpos = preview.lpos2 = position;
            preview.lrot = preview.lrot2 = Maths.SphericalRotation(position, candidate.Yaw);
            preview.previewIndex = livePreviewIndex;
            preview.condition = EBuildCondition.Ok;

            validator.castGroundPosSnapped = position;
            validator.controller.cmd.test = position;
            TmpInhandIdField.SetValue(validator, item.ID);
            TmpInhandCountField.SetValue(validator, 1);

            if (!validator.CheckBuildConditions())
            {
                condition = preview.condition;
                return false;
            }

            condition = preview.condition;
            if (preview.condition != EBuildCondition.Ok || preview.coverObjId != 0) return false;
            if (preview.parameters == null || preview.paramCount <= VeinParameterOffset) return false;

            for (var i = VeinParameterOffset; i < preview.paramCount; i++)
                if (preview.parameters[i] > 0) total++;

            return total > 0;
        }

        /// <summary>
        /// 固定角度下，把目标矿脉投影到局部 UV 平面，用固定大小矩形的滑动扫描
        /// 直接求最大覆盖中心；再对每个角度的前几个中心做原生合法性校验。
        /// </summary>
        private static List<Candidate> BuildDirectCandidates(PlanetFactory factory, PlanetData planet, Player player,
            int targetGroup, EVeinType targetType, int groupVeinCount, Vector3 groupCenter, float groupRadius,
            Vector3 currentPosition, float currentYaw, out bool optimalOutOfRange)
        {
            optimalOutOfRange = false;
            var result = new List<Candidate>();
            var nearby = CollectNearbyVeins(factory, groupCenter, groupRadius + DirectNearbyRadius, planet.realRadius);
            var targetPoints = new List<Vector2>();
            var centers = new List<AngleCenter>();
            var window = new List<Vector2>();
            var center200 = groupCenter.normalized * 200f;
            var maxDistance = player.mecha.buildArea + 1f;
            var maxDistanceSquared = maxDistance * maxDistance;
            var angleCount = Mathf.RoundToInt(360f / DirectAngleStep);
            var maxAnywhere = 0;
            var maxInRange = 0;
            var fullFound = false;
            var fullCandidates = 0;

            for (var ai = 0; ai < angleCount; ai++)
            {
                var yaw = ai * DirectAngleStep;
                var rotation = Maths.SphericalRotation(groupCenter, yaw);
                var right = rotation * Vector3.right;
                var forward = rotation * Vector3.forward;

                targetPoints.Clear();
                foreach (var vein in nearby)
                {
                    if (vein.Group != targetGroup || vein.Type != targetType) continue;
                    var offset = vein.Normalized - center200;
                    targetPoints.Add(new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward)));
                }
                if (targetPoints.Count == 0) continue;

                targetPoints.Sort((a, b) => a.y.CompareTo(b.y));
                centers.Clear();
                for (var i = 0; i < targetPoints.Count; i++)
                {
                    var left = targetPoints[i].x;
                    AddRectangleCenters(targetPoints, left, window, centers);
                    AddRectangleCenters(targetPoints, left - FieldHalfWidth * 2f, window, centers);
                }
                if (centers.Count == 0) continue;

                centers.Sort((a, b) => b.Count.CompareTo(a.Count));
                var used = new List<AngleCenter>();
                var added = 0;
                foreach (var center in centers)
                {
                    var duplicate = false;
                    foreach (var other in used)
                    {
                        if ((other.U - center.U) * (other.U - center.U) +
                            (other.V - center.V) * (other.V - center.V) < 0.25f)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate) continue;
                    used.Add(center);

                    var centerDirection =
                        (center200 + right * center.U + forward * center.V).normalized;
                    var fieldCenter = centerDirection * QueryHeight(planet, centerDirection, groupCenter.magnitude);
                    var candidate = Evaluate(planet, fieldCenter, yaw, nearby, targetGroup, targetType,
                        player.position, maxDistanceSquared, requireBuildRange: false);
                    if (candidate == null) continue;

                    if (candidate.TargetCount > maxAnywhere)
                    {
                        maxAnywhere = candidate.TargetCount;
                        maxInRange = 0;
                    }

                    var inRange = (candidate.Position - player.position).sqrMagnitude <= maxDistanceSquared;
                    if (inRange)
                    {
                        result.Add(candidate);
                        if (candidate.TargetCount == maxAnywhere) maxInRange = maxAnywhere;
                        if (candidate.TargetCount >= groupVeinCount)
                        {
                            fullFound = true;
                            fullCandidates++;
                            // 这个角度已经达到几何最优，不再评估同角度的次优中心；
                            // 其它角度仍会继续扫描，以取得不同朝向的满覆盖候选。
                            break;
                        }
                    }

                    if (++added >= DirectCentersPerAngle) break;
                }
            }

            // 把当前预览位置也作为候选，避免几何最优位置刚好不可建造。
            var currentForward = Maths.SphericalRotation(currentPosition, currentYaw) * Vector3.forward;
            var currentCandidate = Evaluate(planet, currentPosition - currentForward * FieldCenterOffset,
                currentYaw, nearby, targetGroup, targetType, player.position, maxDistanceSquared,
                requireBuildRange: false);
            if (currentCandidate != null)
            {
                if (currentCandidate.TargetCount > maxAnywhere)
                {
                    maxAnywhere = currentCandidate.TargetCount;
                    maxInRange = 0;
                }

                if ((currentCandidate.Position - player.position).sqrMagnitude <= maxDistanceSquared)
                {
                    result.Add(currentCandidate);
                    if (currentCandidate.TargetCount == maxAnywhere) maxInRange = maxAnywhere;
                }
            }

            optimalOutOfRange = maxAnywhere > 0 && maxInRange < maxAnywhere;
            if (fullFound)
            {
                // 存在满覆盖候选时只验证满覆盖候选，不能因为前几个被碰撞拒绝就直接降级。
                result.RemoveAll(candidate => candidate.TargetCount < groupVeinCount);
                result = SelectDiverseCandidates(result, currentPosition, currentYaw);
                AutoPlaceMinerPlugin.VerboseInfo(
                    $"几何扫描：收集到 {fullCandidates} 个满覆盖候选，选取 {result.Count} 个不同朝向的候选进行校验（目标 {groupVeinCount} 条）");
            }
            result.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));
            return result;
        }

        private static List<Candidate> SelectDiverseCandidates(List<Candidate> candidates,
            Vector3 currentPosition, float currentYaw)
        {
            if (candidates.Count <= MaxFullCandidates) return candidates;

            const int binCount = 36; // 10° 一个朝向区间
            var bestByBin = new Candidate[binCount];
            foreach (var candidate in candidates)
            {
                var bin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(candidate.Yaw, 360f) / 10f), 0, binCount - 1);
                var current = bestByBin[bin];
                if (current == null || Compare(candidate, current, currentPosition, currentYaw) < 0)
                    bestByBin[bin] = candidate;
            }

            var selected = new List<Candidate>(MaxFullCandidates);
            foreach (var candidate in bestByBin)
                if (candidate != null) selected.Add(candidate);

            candidates.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));
            foreach (var candidate in candidates)
            {
                if (selected.Count >= MaxFullCandidates) break;
                if (!selected.Contains(candidate)) selected.Add(candidate);
            }

            return selected;
        }

        private static void AddRectangleCenters(List<Vector2> points, float left,
            List<Vector2> window, List<AngleCenter> centers)
        {
            window.Clear();
            var right = left + FieldHalfWidth * 2f;
            foreach (var point in points)
                if (point.x >= left && point.x <= right) window.Add(point);
            if (window.Count == 0) return;

            var lo = 0;
            for (var hi = 0; hi < window.Count; hi++)
            {
                while (window[hi].y - window[lo].y > FieldHalfDepth * 2f) lo++;
                var count = hi - lo + 1;
                var centerU = left + FieldHalfWidth;
                var centerV = (window[lo].y + window[hi].y) * 0.5f;
                centers.Add(new AngleCenter(centerU, centerV, count));
            }
        }

        private static List<Candidate> BuildQuickCandidates(PlanetFactory factory, PlanetData planet, Player player,
            int targetGroup, EVeinType targetType, Vector3 groupCenter, float groupRadius, int groupVeinCount,
            Vector3 currentPosition, float currentYaw)
        {
            var result = new List<Candidate>();
            if (groupVeinCount <= 0) return result;

            var range = Mathf.Clamp(groupRadius + 8f, 8f, 32f);
            var nearby = CollectNearbyVeins(factory, groupCenter, range + NearbyRadius, planet.realRadius);
            var frame = Maths.SphericalRotation(groupCenter, 0f);
            var right = frame * Vector3.right;
            var forward = frame * Vector3.forward;
            var maxDistance = player.mecha.buildArea + 1f;
            var maxDistanceSquared = maxDistance * maxDistance;

            // 中心点以及上下左右 2m，通常已经能覆盖小型矿脉群的全部矿脉。
            for (var x = -2f; x <= 2f; x += 2f)
            for (var z = -2f; z <= 2f; z += 2f)
            {
                var centerDirection = (groupCenter + right * x + forward * z).normalized;
                var center = centerDirection * QueryHeight(planet, centerDirection, groupCenter.magnitude);
                for (var yaw = 0f; yaw < 360f; yaw += CoarseAngleStep)
                {
                    var candidate = Evaluate(planet, center, yaw, nearby, targetGroup, targetType,
                        player.position, maxDistanceSquared);
                    if (candidate != null && candidate.TargetCount >= groupVeinCount) result.Add(candidate);
                }
            }

            result.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));
            return result;
        }

        private static List<Candidate> BuildCandidates(PlanetFactory factory, PlanetData planet, Player player,
            int targetGroup, EVeinType targetType, Vector3 groupCenter, float groupRadius, int groupVeinCount,
            Vector3 currentPosition, float currentYaw)
        {
            var frame = Maths.SphericalRotation(groupCenter, 0f);
            var right = frame * Vector3.right;
            var forward = frame * Vector3.forward;
            var range = Mathf.Clamp(groupRadius + 8f, 8f, 32f);
            // 候选场地中心最远在 range 处，覆盖区半径约 18.8m，因此只需要收集
            // groupCenter 周围 range + 24m 的矿脉，不必把整颗行星的矿脉都放进来。
            var nearby = CollectNearbyVeins(factory, groupCenter, range + NearbyRadius, planet.realRadius);
            var maxDistance = player.mecha.buildArea + 1f;
            var maxDistanceSquared = maxDistance * maxDistance;

            var coarse = new List<Candidate>();
            // 把玩家当前预览位置也作为兜底候选，避免搜索区偏偏错过一个手动已经
            // 调好的合法位置。
            var currentForward = Maths.SphericalRotation(currentPosition, currentYaw) * Vector3.forward;
            var currentCandidate = Evaluate(planet, currentPosition - currentForward * 10f, currentYaw,
                nearby, targetGroup, targetType, player.position, maxDistanceSquared);
            if (currentCandidate != null) coarse.Add(currentCandidate);

            for (var x = -range; x <= range; x += CoarseCenterStep)
            for (var z = -range; z <= range; z += CoarseCenterStep)
            {
                var centerDirection = (groupCenter + right * x + forward * z).normalized;
                var center = centerDirection * QueryHeight(planet, centerDirection, groupCenter.magnitude);
                for (var yaw = 0f; yaw < 360f; yaw += CoarseAngleStep)
                {
                    var candidate = Evaluate(planet, center, yaw, nearby, targetGroup, targetType,
                        player.position, maxDistanceSquared);
                    if (candidate != null) coarse.Add(candidate);
                }
            }

            coarse.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));
            // 已经覆盖当前矿脉群全部矿脉时，覆盖数不可能再提高，省掉精细搜索。
            foreach (var candidate in coarse)
                if (candidate.TargetCount >= groupVeinCount) return coarse;

            var seeds = new List<Candidate>();
            foreach (var candidate in coarse)
            {
                if (seeds.Count >= RefineSeedCount) break;
                var duplicate = false;
                foreach (var seed in seeds)
                {
                    if ((seed.FieldCenter - candidate.FieldCenter).sqrMagnitude < 9f)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) seeds.Add(candidate);
            }

            var all = new List<Candidate>(coarse);
            foreach (var seed in seeds)
            {
                for (var x = -RefineCenterRadius; x <= RefineCenterRadius; x += FineCenterStep)
                for (var z = -RefineCenterRadius; z <= RefineCenterRadius; z += FineCenterStep)
                {
                    var centerDirection = (seed.FieldCenter + right * x + forward * z).normalized;
                    var center = centerDirection * QueryHeight(planet, centerDirection, seed.FieldCenter.magnitude);
                    for (var offset = -RefineAngleRadius; offset <= RefineAngleRadius; offset += FineAngleStep)
                    {
                        var candidate = Evaluate(planet, center, seed.Yaw + offset, nearby, targetGroup,
                            targetType, player.position, maxDistanceSquared);
                        if (candidate != null) all.Add(candidate);
                    }
                }
            }

            all.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));

            // 如果精扫后仍然没有覆盖满，说明最优区域可能落在某个次优粗候选附近。
            // 再围绕一批空间上分散的高分候选做一次更宽的角度搜索。
            if (all.Count > 0 && all[0].TargetCount < groupVeinCount)
            {
                var extendedSeeds = new List<Candidate>();
                foreach (var candidate in all)
                {
                    if (extendedSeeds.Count >= ExtendedSeedCount) break;
                    var duplicate = false;
                    foreach (var seed in extendedSeeds)
                    {
                        if ((seed.FieldCenter - candidate.FieldCenter).sqrMagnitude < 25f)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (!duplicate) extendedSeeds.Add(candidate);
                }

                foreach (var seed in extendedSeeds)
                {
                    for (var x = -ExtendedCenterRadius; x <= ExtendedCenterRadius; x += ExtendedCenterStep)
                    for (var z = -ExtendedCenterRadius; z <= ExtendedCenterRadius; z += ExtendedCenterStep)
                    {
                        var centerDirection = (seed.FieldCenter + right * x + forward * z).normalized;
                        var center = centerDirection * QueryHeight(planet, centerDirection, seed.FieldCenter.magnitude);
                        for (var offset = -ExtendedAngleRadius; offset <= ExtendedAngleRadius; offset += ExtendedAngleStep)
                        {
                            var candidate = Evaluate(planet, center, seed.Yaw + offset, nearby, targetGroup,
                                targetType, player.position, maxDistanceSquared);
                            if (candidate != null) all.Add(candidate);
                        }
                    }
                }

                all.Sort((a, b) => Compare(a, b, currentPosition, currentYaw));
            }

            return all;
        }

        private static Candidate Evaluate(PlanetData planet, Vector3 fieldCenter, float yaw,
            List<NearbyVein> nearby, int targetGroup, EVeinType targetType,
            Vector3 playerPosition, float maxDistanceSquared, bool requireBuildRange = true)
        {
            // 覆盖区中心在矿机后方 10m，因此由场地中心反推矿机位置。
            var forward = Maths.SphericalRotation(fieldCenter, yaw) * Vector3.forward;
            var positionDirection = (fieldCenter + forward * 10f).normalized;
            var rotation = Maths.SphericalRotation(positionDirection, yaw);
            var right = rotation * Vector3.right;
            var fieldForward = rotation * Vector3.forward;
            var center = positionDirection * 200f - fieldForward * 10f;
            var backward = -fieldForward;

            var targetCount = 0;
            var totalCount = 0;
            var mixed = false;

            foreach (var vein in nearby)
            {
                if (vein.Type == EVeinType.Oil) continue;
                var offset = vein.Normalized - center;
                if (offset.sqrMagnitude > 100f) continue;
                if (Mathf.Abs(Vector3.Dot(offset, backward)) > 7.75f ||
                    Mathf.Abs(Vector3.Dot(offset, right)) > 6.25f) continue;

                if (vein.Type != targetType)
                {
                    mixed = true;
                    break;
                }

                totalCount++;
                if (vein.Group == targetGroup) targetCount++;
            }

            if (mixed || targetCount == 0) return null;

            // 大型采矿机与手动建造一致：落点固定在水平面高度，由游戏在建造时
            // 下挖高地并返还沙土。不能使用高地地形的 QueryModifiedHeight，否则地形
            // 下挖后预建造矿机会悬浮在原高度。
            var position = positionDirection * (planet.realRadius + 0.2f);
            if (requireBuildRange && (position - playerPosition).sqrMagnitude > maxDistanceSquared) return null;

            return new Candidate
            {
                FieldCenter = fieldCenter,
                Position = position,
                Yaw = yaw,
                TargetCount = targetCount,
                TotalCount = totalCount
            };
        }

        private static List<NearbyVein> CollectNearbyVeins(PlanetFactory factory, Vector3 center, float radius,
            float realRadius)
        {
            var nearby = new List<NearbyVein>();
            var veins = factory.veinPool;
            var radiusSquared = radius * radius;
            var minimumVisibleHeight = realRadius - 40f;

            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.type == EVeinType.None || vein.colliderId <= 0) continue;
                if (vein.pos.magnitude < minimumVisibleHeight) continue;
                if ((vein.pos - center).sqrMagnitude > radiusSquared) continue;
                nearby.Add(new NearbyVein(vein));
            }

            return nearby;
        }

        private static int RestoreTargetVeins(PlanetFactory factory, PlanetData planet, PlanetPhysics physics,
            int group, EVeinType type, float groupRadius)
        {
            if (factory?.veinPool == null || planet?.data == null || physics == null) return 0;

            var center = Vector3.zero;
            var count = 0;
            var veins = factory.veinPool;
            var minimumVisibleHeight = planet.realRadius - 40f;

            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.groupIndex != group) continue;
                center += vein.pos.normalized;
                count++;
            }
            if (count == 0) return 0;
            center = center.normalized * (planet.realRadius + 0.2f);

            var obstacles = new List<ColliderData>();
            var radius = Mathf.Max(16f, groupRadius + 12f);
            foreach (var chunk in physics.colChunks)
            {
                for (var i = 1; i < chunk.cursor; i++)
                {
                    var collider = chunk.colliderPool[i];
                    var bound = radius + collider.ext.magnitude + collider.radius;
                    if (collider.idType != 0 && collider.isForBuild &&
                        (collider.pos - center).sqrMagnitude <= bound * bound)
                        obstacles.Add(collider);
                }
            }

            var restored = 0;
            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.groupIndex != group || vein.type != type ||
                    vein.colliderId <= 0 || vein.minerCount > 0 || vein.pos.sqrMagnitude < 1f) continue;
                if (vein.pos.magnitude >= minimumVisibleHeight) continue;

                var surface = vein.pos.normalized * (planet.data.QueryModifiedHeight(vein.pos) - 0.13f);
                var collider = physics.GetColliderData(vein.colliderId);
                collider.pos = surface + surface.normalized * 0.4f;

                var obstructed = false;
                foreach (var obstacle in obstacles)
                {
                    var bound = obstacle;
                    if (bound.shape != EColliderShape.Box)
                    {
                        bound.idType = (int)EColliderShape.Box << 24;
                        bound.q = Quaternion.identity;
                        bound.ext = new Vector3(Mathf.Abs(obstacle.ext.x), Mathf.Abs(obstacle.ext.y),
                            Mathf.Abs(obstacle.ext.z)) + Vector3.one * obstacle.radius;
                    }

                    if (MinerPlacementGeometry.Intersects(new MinerPlacementGeometry.Box(bound), collider))
                    {
                        obstructed = true;
                        break;
                    }
                }
                if (obstructed) continue;

                SetVeinPosition(factory, physics, i, surface);
                restored++;
            }

            if (restored > 0)
            {
                physics.RefreshColliders();
                if (GameMain.gpuiManager != null) GameMain.gpuiManager.SyncAllGPUBuffer();
                GameMain.gameScenario?.NotifyOnVeinReformed(2);
            }

            return restored;
        }

        private static void SetVeinPosition(PlanetFactory factory, PlanetPhysics physics, int id, Vector3 position)
        {
            ref var vein = ref factory.veinPool[id];
            vein.pos = position;

            ref var collider = ref physics.GetColliderDataRef(vein.colliderId);
            collider.pos = position + position.normalized * 0.4f;
            physics.SetPlanetPhysicsColliderDirty();

            if (GameMain.gpuiManager != null)
                GameMain.gpuiManager.AlterModel(vein.modelIndex, vein.modelId, id, position, setBuffer: false);

            var proto = LDB.veins.Select((int)vein.type);
            if (proto == null || GameMain.gpuiManager == null) return;

            var up = position.normalized;
            if (vein.minerId0 > 0)
            {
                GameMain.gpuiManager.AlterModel(proto.MinerBaseModelIndex, vein.minerBaseModelId, vein.minerId0,
                    position + up * 0.1f, setBuffer: false);
                GameMain.gpuiManager.AlterModel(proto.MinerCircleModelIndex, vein.minerCircleModelId0, vein.minerId0,
                    position + up * 0.4f, setBuffer: false);
            }

            if (vein.minerId1 > 0)
                GameMain.gpuiManager.AlterModel(proto.MinerCircleModelIndex, vein.minerCircleModelId1, vein.minerId1,
                    position + up * 0.6f, setBuffer: false);
            if (vein.minerId2 > 0)
                GameMain.gpuiManager.AlterModel(proto.MinerCircleModelIndex, vein.minerCircleModelId2, vein.minerId2,
                    position + up * 0.8f, setBuffer: false);
            if (vein.minerId3 > 0)
                GameMain.gpuiManager.AlterModel(proto.MinerCircleModelIndex, vein.minerCircleModelId3, vein.minerId3,
                    position + up, setBuffer: false);
        }

        private static bool FindTargetGroup(PlanetFactory factory, Vector3 position, float realRadius,
            bool includeBuried, out int group, out EVeinType type, out Vector3 center, out float radius,
            out int veinCount)
        {
            group = 0;
            type = EVeinType.None;
            center = position;
            radius = 0f;
            veinCount = 0;

            var found = false;
            var bestDistance = TargetGroupRadius * TargetGroupRadius;
            var minimumVisibleHeight = realRadius - 40f;
            var veins = factory.veinPool;

            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.type == EVeinType.None || vein.type == EVeinType.Oil ||
                    vein.colliderId <= 0 || vein.pos.sqrMagnitude < 1f) continue;
                if (!includeBuried && vein.pos.magnitude < minimumVisibleHeight) continue;

                var surface = vein.pos.normalized * realRadius;
                var distance = (surface - position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                group = vein.groupIndex;
                type = vein.type;
                found = true;
            }

            if (!found) return false;

            return ComputeGroupStats(factory, group, type, realRadius, includeBuried,
                out center, out radius, out veinCount);
        }

        private static bool ComputeGroupStats(PlanetFactory factory, int group, EVeinType type, float realRadius,
            bool includeBuried, out Vector3 center, out float radius, out int veinCount)
        {
            center = Vector3.zero;
            radius = 0f;
            veinCount = 0;

            var sum = Vector3.zero;
            var count = 0;
            var minimumVisibleHeight = realRadius - 40f;
            var veins = factory.veinPool;
            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.groupIndex != group || vein.type != type ||
                    vein.colliderId <= 0) continue;
                if (!includeBuried && vein.pos.magnitude < minimumVisibleHeight) continue;
                sum += vein.pos.normalized * realRadius;
                count++;
            }

            if (count == 0) return false;
            center = sum / count;
            veinCount = count;

            var maxDistance = 0f;
            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.groupIndex != group || vein.type != type ||
                    vein.colliderId <= 0) continue;
                if (!includeBuried && vein.pos.magnitude < minimumVisibleHeight) continue;
                maxDistance = Mathf.Max(maxDistance,
                    (vein.pos.normalized * realRadius - center).sqrMagnitude);
            }

            radius = Mathf.Sqrt(maxDistance);
            return true;
        }

        private static float QueryHeight(PlanetData planet, Vector3 direction, float fallback)
        {
            if (planet?.data != null)
            {
                var height = planet.data.QueryModifiedHeight(direction);
                if (height > 1f) return height + 0.1f;
            }
            return fallback + 0.1f;
        }

        private static int Compare(Candidate a, Candidate b, Vector3 currentPosition, float currentYaw)
        {
            var compare = b.TargetCount.CompareTo(a.TargetCount);
            if (compare != 0) return compare;
            compare = b.TotalCount.CompareTo(a.TotalCount);
            if (compare != 0) return compare;
            compare = (a.Position - currentPosition).sqrMagnitude.CompareTo(
                (b.Position - currentPosition).sqrMagnitude);
            if (compare != 0) return compare;
            compare = YawDistance(a.Yaw, currentYaw).CompareTo(YawDistance(b.Yaw, currentYaw));
            return compare != 0 ? compare : a.Yaw.CompareTo(b.Yaw);
        }

        private static bool HasItem(Player player, int itemId)
        {
            if (player.inhandItemId == itemId && player.inhandItemCount > 0) return true;
            return player.package != null && player.package.GetItemCount(itemId) > 0;
        }

        private static float YawDistance(float a, float b)
        {
            var difference = Mathf.Abs(Mathf.Repeat(a - b, 360f));
            return Mathf.Min(difference, 360f - difference);
        }

        private static List<GameObject> HideLivePreviewModels(BuildModel model)
        {
            var hidden = new List<GameObject>();
            if (model == null || PreviewModelsField == null) return hidden;

            try
            {
                var models = PreviewModelsField.GetValue(model) as List<BuildPreviewModel>;
                if (models == null) return hidden;

                foreach (var previewModel in models)
                {
                    if (previewModel == null || previewModel.gameObject == null ||
                        !previewModel.gameObject.activeSelf) continue;
                    previewModel.gameObject.SetActive(false);
                    hidden.Add(previewModel.gameObject);
                }
            }
            catch (Exception exception)
            {
                AutoPlaceMinerPlugin.Log?.LogWarning($"隐藏建造预览模型失败：{exception.Message}");
            }

            return hidden;
        }

        private static void RestoreLivePreviewModels(List<GameObject> hidden)
        {
            foreach (var gameObject in hidden)
            {
                if (gameObject == null) continue;
                try { gameObject.SetActive(true); }
                catch (Exception exception) { AutoPlaceMinerPlugin.Log?.LogWarning($"恢复建造预览模型失败：{exception.Message}"); }
            }
        }
    }
}
