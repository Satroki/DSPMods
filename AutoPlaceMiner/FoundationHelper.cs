using System;
using UnityEngine;

namespace AutoPlaceMiner
{
    /// <summary>
    /// 在大型采矿机的落点下方铺设地基。逻辑参考 AutoConnectPower 的水面电力塔处理：
    /// 先整平并铺地基，再消耗地基物品和沙土；已经是地基的格子不重复扣材料。
    /// </summary>
    internal static class FoundationHelper
    {
        private const int FoundationBrushType = 1;
        private const float FoundationCellSize = 1.3333333f;
        private const float FoundationBrushScale = 0.990946f;

        public static bool IsUnderwater(PlanetData planet, Vector3 position)
        {
            if (planet == null || planet.waterHeight <= 0.01f) return false;
            float waterLevel = planet.realRadius + planet.waterHeight;
            return planet.data.QueryHeight(position) < waterLevel + 0.25f;
        }

        /// <summary>
        /// 按建筑占地估算地基格数。大型采矿机比电力塔大，3×3 往往不够，
        /// 因此根据 buildColliders / landPoints 的实际半径计算，并保持奇数尺寸。
        /// </summary>
        public static int GetFoundationSize(PrefabDesc desc)
        {
            float radius = 0f;
            if (desc != null)
            {
                if (desc.buildColliders != null)
                {
                    foreach (var collider in desc.buildColliders)
                        radius = Mathf.Max(radius, collider.pos.magnitude + collider.ext.magnitude);
                }

                if (desc.landPoints != null)
                {
                    foreach (var point in desc.landPoints)
                        radius = Mathf.Max(radius, new Vector2(point.x, point.z).magnitude);
                }
            }

            int size = Mathf.CeilToInt(radius * 2f / FoundationCellSize) + 2;
            if ((size & 1) == 0) size++;
            return Mathf.Clamp(size, 5, 9);
        }

        public static bool HasInfiniteSand()
        {
            return GameMain.sandboxToolsEnabled &&
                   GameMain.data?.history != null &&
                   GameMain.data.history.HasFeatureKey(FeatureID.SANDBOX_INFINITE_SAND);
        }

        public static bool CanAttempt(Player player)
        {
            if (player == null) return false;
            int haveItems = player.package.GetItemCount(PlatformSystem.REFORM_ID) + player.inhandItemCount;
            return haveItems > 0 && (player.sandCount > 0 || HasInfiniteSand());
        }

        public static bool TryPave(PlanetData planet, PlanetFactory factory, Player player, Vector3 position,
            int size, out int itemsUsed, out int sandUsed, out string error)
        {
            itemsUsed = 0;
            sandUsed = 0;
            error = null;

            if (planet == null || factory == null || player == null || size <= 0)
            {
                error = "地基参数无效";
                return false;
            }

            PlatformSystem platform = factory.platformSystem;
            if (platform == null)
            {
                error = "当前星球没有地基系统";
                return false;
            }

            int cells = size * size;
            var points = new Vector3[cells];
            var indices = new int[cells];

            platform.EnsureReformData();
            int count = planet.aux.ReformSnap(
                position,
                0,
                size,
                FoundationBrushType,
                0,
                points,
                indices,
                platform,
                out Vector3 center);

            if (count <= 0)
            {
                int centerIndex = platform.GetReformIndexForPosition(position);
                if (centerIndex >= 0 && platform.IsTerrainReformed(platform.GetReformType(centerIndex)))
                    return true;

                error = "无法在该位置生成地基格";
                return false;
            }

            bool alreadyReformed = true;
            for (int i = 0; i < count; i++)
            {
                int index = indices[i];
                if (index < 0 || platform.GetReformType(index) != FoundationBrushType)
                {
                    alreadyReformed = false;
                    break;
                }
            }

            if (alreadyReformed) return true;

            int haveItems = player.package.GetItemCount(PlatformSystem.REFORM_ID) + player.inhandItemCount;
            if (haveItems < count)
            {
                error = $"地基不足（需要 {count}，现有 {haveItems}）";
                return false;
            }

            int costSand = 0;
            int getSand = 0;
            factory.ComputeFlattenTerrainReform(
                points,
                center,
                FoundationBrushScale * size,
                count,
                ref costSand,
                ref getSand);

            int needSand = HasInfiniteSand() ? 0 : costSand - getSand;
            long remainingSand = player.sandCount - needSand;
            if (remainingSand < 0)
            {
                factory.BeginFlattenTerrain();
                error = $"沙土不足（需要 {needSand}，现有 {player.sandCount}）";
                return false;
            }

            factory.FlattenTerrainReform(center, FoundationBrushScale * size, size, false);
            if (needSand > 0) player.SetSandCount(remainingSand, ESandSource.Reform);

            for (int i = 0; i < count; i++)
            {
                int index = indices[i];
                if (index < 0) continue;
                if (platform.GetReformType(index) != FoundationBrushType)
                    platform.SetReformType(index, FoundationBrushType);
                if (platform.GetReformColor(index) != 0)
                    platform.SetReformColor(index, 0);
            }

            if (platform.reformDataBuffer != null)
            {
                try
                {
                    platform.reformDataBuffer.SetData(platform.reformData);
                }
                catch (Exception)
                {
                    // 下一帧 PlanetSimulator 还会再同步一次。
                }
            }

            int remainingItems = count;
            if (player.inhandItemId == PlatformSystem.REFORM_ID && player.inhandItemCount > 0)
            {
                int take = Mathf.Min(remainingItems, player.inhandItemCount);
                player.UseHandItems(take, out _);
                remainingItems -= take;
            }

            if (remainingItems > 0)
            {
                int itemId = PlatformSystem.REFORM_ID;
                int takeCount = remainingItems;
                player.package.TakeTailItems(ref itemId, ref takeCount, out _);
            }

            itemsUsed = count;
            sandUsed = Mathf.Max(0, needSand);
            if (GameMain.history != null) GameMain.history.MarkItemBuilt(PlatformSystem.REFORM_ID, count);

            // 清掉整平缓存，避免后续原生建造校验使用到上一次失败的地形改动。
            factory.BeginFlattenTerrain();
            return true;
        }
    }
}
