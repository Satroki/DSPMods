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
    /// 先按矿脉群估算一片可能的建造区域，在该区域内同时搜索“摆放位置 + 朝向”，
    /// 再用游戏原生的 BuildTool_Click.CheckBuildConditions 做碰撞、地形、建造范围、
    /// 资源类型和间距检查。这样位置和角度都与手动点击建造保持一致。
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
                AutoPlaceMinerPlugin.Tip("背包中没有大型采矿机");
                return;
            }

            if (!FindTargetGroup(factory, position, planet.realRadius,
                    out var group, out var type, out var groupCenter, out var groupRadius, out var groupVeinCount))
            {
                AutoPlaceMinerPlugin.Tip("附近没有可开采的固体矿脉");
                return;
            }

            // 先用矿脉群中心附近的一小圈候选试一下。如果这里已经能覆盖当前矿脉群
            // 的全部矿脉，就已经是最优解，不必再做整片网格搜索。
            var quick = BuildQuickCandidates(factory, planet, player, group, type, groupCenter, groupRadius,
                groupVeinCount, position, click.yaw);
            if (quick.Count > 0 && Place(player, planet, factory, physics, click, item, desc, quick,
                    groupCenter, groupRadius, groupVeinCount, reportFailure: false))
            {
                AutoPlaceMinerPlugin.Log?.LogInfo("大型采矿机使用中心快速候选完成放置");
                return;
            }

            var searchTimer = Stopwatch.StartNew();
            var candidates = BuildCandidates(factory, planet, player, group, type,
                groupCenter, groupRadius, groupVeinCount, position, click.yaw);
            searchTimer.Stop();
            if (candidates.Count == 0)
            {
                AutoPlaceMinerPlugin.Tip("当前矿脉附近没有可覆盖的建造位置");
                return;
            }

            AutoPlaceMinerPlugin.Log?.LogInfo(
                $"目标矿脉群 {group}（{type}），共 {groupVeinCount} 条，中心 {groupCenter}，半径 {groupRadius:0.0}m，" +
                $"候选最大覆盖 {candidates[0].TargetCount}/{groupVeinCount} 条，" +
                $"{candidates.Count} 个位置/角度候选（搜索 {searchTimer.ElapsedMilliseconds}ms），开始原生建造校验");
            Place(player, planet, factory, physics, click, item, desc, candidates,
                groupCenter, groupRadius, groupVeinCount, reportFailure: true);
        }

        private static bool Place(Player player, PlanetData planet, PlanetFactory factory, PlanetPhysics physics,
            BuildTool_Click click, ItemProto item, PrefabDesc desc, List<Candidate> candidates,
            Vector3 searchCenter, float searchRadius, int groupVeinCount, bool reportFailure)
        {
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
                    if (reportFailure) AutoPlaceMinerPlugin.Tip("无法初始化建造校验器，请查看日志");
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
                //AutoPlaceMinerPlugin.Log?.LogInfo(
                //    $"当前预览条件 {liveCondition}，previewIndex {livePreviewIndex}，隐藏预览模型 {hiddenPreviews.Count} 个");
                try
                {
                    // 一次性激活整个搜索区附近的碰撞体；否则 Physics.OverlapBox 可能漏掉
                    // 离当前镜头较远、但仍在候选区域内的建筑。
                    physics.RefreshColliders();
                    physics.nearColliderLogic.ActiveCollidersInArea(searchCenter,
                        Mathf.Max(64f, searchRadius + 52f));
                    Physics.SyncTransforms();

                    Candidate best = null;
                    var bestTotal = 0;
                    var rejected = new Dictionary<EBuildCondition, int>();
                    var examined = 0;

                    foreach (var candidate in candidates)
                    {
                        // 候选表按目标矿脉数、总矿脉数、离当前位置距离排序。
                        if (best != null &&
                            (candidate.TargetCount < best.TargetCount ||
                             candidate.TargetCount == best.TargetCount && candidate.TotalCount <= bestTotal)) break;

                        if (examined++ >= MaxNativeChecks) break;
                        if (!Validate(validator, preview, item, desc, candidate, livePreviewIndex,
                                out var total, out var condition))
                        {
                            rejected[condition] = rejected.TryGetValue(condition, out var count) ? count + 1 : 1;
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
                        AutoPlaceMinerPlugin.Log?.LogWarning(
                            $"大型采矿机候选位置被原生规则拒绝：{string.Join(", ", reasons)}" +
                            (reportFailure ? "" : "（快速候选，接着做完整搜索）"));
                        if (reportFailure) AutoPlaceMinerPlugin.Tip("未找到无碰撞的摆放位置");
                        return false;
                    }

                    // 选中布局后重新跑一次原生校验，确保预览数据与最终放置一致。
                    if (!Validate(validator, preview, item, desc, best, livePreviewIndex,
                            out bestTotal, out _))
                    {
                        if (reportFailure) AutoPlaceMinerPlugin.Tip("选中的位置已失效，请重试");
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
                        if (reportFailure) AutoPlaceMinerPlugin.Tip("放置失败：物品不足或位置无效");
                        return false;
                    }

                    AutoPlaceMinerPlugin.Log?.LogInfo(
                        $"已放置大型采矿机：预建造 {prebuildId}，位置 {best.Position}，角度 {best.Yaw:0.0}°，" +
                        $"矿脉群共 {groupVeinCount} 条，本机覆盖当前群 {best.TargetCount} 条，同类型总覆盖 {bestTotal} 条");
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

        private static bool PrepareValidator(BuildTool validator)
        {
            if (TmpPackageField == null || TmpInhandIdField == null || TmpInhandCountField == null)
            {
                AutoPlaceMinerPlugin.Log?.LogError(
                    "找不到 BuildTool 的 tmpPackage/tmpInhandId/tmpInhandCount 字段，游戏版本可能已变更");
                AutoPlaceMinerPlugin.Tip("游戏版本不兼容：找不到建造校验字段");
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
            Vector3 playerPosition, float maxDistanceSquared)
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

            // 覆盖计算只需要方向；高度查询放在确认有覆盖之后，减少 QueryModifiedHeight 次数。
            var position = positionDirection * QueryHeight(planet, positionDirection, fieldCenter.magnitude);
            if ((position - playerPosition).sqrMagnitude > maxDistanceSquared) return null;

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

        private static bool FindTargetGroup(PlanetFactory factory, Vector3 position, float realRadius,
            out int group, out EVeinType type, out Vector3 center, out float radius, out int veinCount)
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
                    vein.colliderId <= 0 || vein.pos.magnitude < minimumVisibleHeight) continue;

                var distance = (vein.pos - position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                group = vein.groupIndex;
                type = vein.type;
                found = true;
            }

            if (!found) return false;

            var sum = Vector3.zero;
            var count = 0;
            for (var i = 1; i < factory.veinCursor && i < veins.Length; i++)
            {
                ref var vein = ref veins[i];
                if (vein.id != i || vein.amount <= 0 || vein.groupIndex != group || vein.type != type ||
                    vein.colliderId <= 0 || vein.pos.magnitude < minimumVisibleHeight) continue;
                sum += vein.pos;
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
                    vein.colliderId <= 0 || vein.pos.magnitude < minimumVisibleHeight) continue;
                maxDistance = Mathf.Max(maxDistance, (vein.pos - center).sqrMagnitude);
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
