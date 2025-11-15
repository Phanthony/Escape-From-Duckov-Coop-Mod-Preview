// Escape-From-Duckov-Coop-Mod-Preview
// Copyright (C) 2025  Mr.sans and InitLoader's team
//
// This program is not a free software.
// It's distributed under a license based on AGPL-3.0,
// with strict additional restrictions:
//  YOU MUST NOT use this software for commercial purposes.
//  YOU MUST NOT use this software to run a headless game server.
//  YOU MUST include a conspicuous notice of attribution to
//  Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview as the original author.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

using System.Collections;
using System.Reflection;
using Duckov.Scenes;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace EscapeFromDuckovCoopMod;

[HarmonyPatch(typeof(CharacterSpawnerRoot), "StartSpawn")]
internal static class Patch_Root_StartSpawn
{
    private static readonly HashSet<int> _waiting = new();
    private static readonly Stack<Random.State> _rngStack = new();

    private static readonly MethodInfo _miStartSpawn =
        AccessTools.Method(typeof(CharacterSpawnerRoot), "StartSpawn");

    private static bool Prefix(CharacterSpawnerRoot __instance)
    {
        try
        {
            var mod = ModBehaviourF.Instance;
            var rootId = AITool.StableRootId(__instance);

            // Wait for scene seed to arrive from host
            if (!mod.IsServer && COOPManager.AIHandle.sceneSeed == 0)
            {
                if (_waiting.Add(rootId))
                {
                    Debug.Log($"[AI-SEED] Spawner rootId={rootId} waiting for sceneSeed (current: {COOPManager.AIHandle.sceneSeed})");
                    __instance.StartCoroutine(WaitSeedAndSpawn(__instance, rootId));
                }
                return false;
            }

            // Derive seed locally from scene seed and root ID
            var useSeed = AITool.DeriveSeed(COOPManager.AIHandle.sceneSeed, rootId);
            Debug.Log($"[AI-SEED] Spawner rootId={rootId} using derived seed={useSeed} from sceneSeed={COOPManager.AIHandle.sceneSeed}");
            _rngStack.Push(Random.state);
            Random.InitState(useSeed);
            return true;
        }
        catch
        {
            return true;
        }
    }

    private static void ForceActivateHierarchy(Transform t)
    {
        while (t)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private static IEnumerator WaitSeedAndSpawn(CharacterSpawnerRoot inst, int rootId)
    {
        var mod = ModBehaviourF.Instance;
        while (mod && COOPManager.AIHandle.sceneSeed == 0) yield return null;

        _waiting.Remove(rootId);

        if (inst)
        {
            // Force activate hierarchy to prevent spawning in inactive hierarchy
            ForceActivateHierarchy(inst.transform);

            if (_miStartSpawn != null)
                _miStartSpawn.Invoke(inst, null); // Reflect call to private StartSpawn()
        }
    }

    private static void Postfix(CharacterSpawnerRoot __instance)
    {
        try
        {
            if (_rngStack.Count > 0) Random.state = _rngStack.Pop();

            // 你原有的“给 AI 打标签 / 注册 / 主机广播负载”逻辑保留
            var list = Traverse.Create(__instance)
                .Field<List<CharacterMainControl>>("createdCharacters")
                .Value;

            if (list != null && COOPManager.AIHandle.freezeAI)
                foreach (var c in list)
                    AITool.TryFreezeAI(c);

            if (list != null)
            {
                var mod = ModBehaviourF.Instance;
                var rootId = AITool.StableRootId(__instance);

                // 按“名称 + 量化坐标 + InstanceID”稳定排序，避免回调时序导致乱序
                var ordered = new List<CharacterMainControl>(list);
                ordered.RemoveAll(c => !c);
                ordered.Sort((a, b) =>
                {
                    var n = string.Compare(a.name, b.name, StringComparison.Ordinal);
                    if (n != 0) return n;
                    var pa = a.transform.position;
                    var pb = b.transform.position;
                    int ax = Mathf.RoundToInt(pa.x * 100f), az = Mathf.RoundToInt(pa.z * 100f), ay = Mathf.RoundToInt(pa.y * 100f);
                    int bx = Mathf.RoundToInt(pb.x * 100f), bz = Mathf.RoundToInt(pb.z * 100f), by = Mathf.RoundToInt(pb.y * 100f);
                    if (ax != bx) return ax.CompareTo(bx);
                    if (az != bz) return az.CompareTo(bz);
                    if (ay != by) return ay.CompareTo(by);
                    return a.GetInstanceID().CompareTo(b.GetInstanceID());
                });

                for (var i = 0; i < ordered.Count; i++)
                {
                    var cmc = ordered[i];
                    if (!cmc || !AITool.IsRealAI(cmc)) continue;

                    var aiId = AITool.DeriveSeed(rootId, i + 1);
                    var tag = cmc.GetComponent<NetAiTag>() ?? cmc.gameObject.AddComponent<NetAiTag>();

                    // 主机赋 id + 登记；客户端保持 tag.aiId=0 等待绑定（见修复 A）
                    // 【优化】装备同步已改为延迟批量发送，不再立即广播
                    if (mod.IsServer)
                    {
                        tag.aiId = aiId;
                        COOPManager.AIHandle.RegisterAi(aiId, cmc); // 内部会将装备信息加入队列
                        // Server_BroadcastAiLoadout(aiId, cmc); // 【移除】改为批量延迟发送
                    }
                }


                // 主机在本 root 刷完后即刻发一帧位置快照，收敛初始误差
                if (mod.IsServer) COOPManager.AIHandle.Server_BroadcastAiTransforms();
            }
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(CharacterSpawnerRoot), "Init")]
internal static class Patch_Root_Init_FixContain
{
    private static bool Prefix(CharacterSpawnerRoot __instance)
    {
        try
        {
            var msc = MultiSceneCore.Instance;

            // 仅在 SpawnerGuid != 0 时才做“重复过滤”
            if (msc != null && __instance.SpawnerGuid != 0 &&
                msc.usedCreatorIds.Contains(__instance.SpawnerGuid))
                return true; // 放行原版 → 它会销毁重复体

            var tr = Traverse.Create(__instance);
            tr.Field("inited").SetValue(true);

            var spComp = tr.Field<CharacterSpawnerComponentBase>("spawnerComponent").Value;
            if (spComp != null) spComp.Init(__instance);

            var buildIndex = SceneManager.GetActiveScene().buildIndex;
            tr.Field("relatedScene").SetValue(buildIndex);

            __instance.transform.SetParent(null);
            if (msc != null)
            {
                MultiSceneCore.MoveToMainScene(__instance.gameObject);
                // Only register when GUID is non-zero to avoid treating "0" as globally unique
                if (__instance.SpawnerGuid != 0)
                    msc.usedCreatorIds.Add(__instance.SpawnerGuid);
            }

            // No need to send individual root seeds - clients derive them locally from sceneSeed

            return false; // Skip original Init to avoid incorrect deletion
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AI-SEED] Patch_Root_Init_FixContain failed: " + e);
            return true;
        }
    }
}

[HarmonyPatch(typeof(CharacterSpawnerRoot), "Update")]
internal static class Patch_Root_Update_ClientAutoSpawn
{
    private static readonly MethodInfo _miStartSpawn =
        AccessTools.Method(typeof(CharacterSpawnerRoot), "StartSpawn");

    private static readonly MethodInfo _miCheckTiming =
        AccessTools.Method(typeof(CharacterSpawnerRoot), "CheckTiming");

    private static void Postfix(CharacterSpawnerRoot __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted || mod.IsServer) return;

        var tr = Traverse.Create(__instance);
        var inited = tr.Field<bool>("inited").Value;
        var created = tr.Field<bool>("created").Value;
        if (!inited || created) return;

        var rootId = AITool.StableRootId(__instance);

        // Wait for scene seed to arrive
        if (COOPManager.AIHandle.sceneSeed == 0)
            return;

        // 关键：尊重原版判断（时间/天气/触发器）
        var ok = false;
        try
        {
            ok = (bool)_miCheckTiming.Invoke(__instance, null);
        }
        catch
        {
        }

        if (!ok) return;

        // 与原逻辑一致：确保层级激活再刷
        ForceActivateHierarchy(__instance.transform);
        try
        {
            _miStartSpawn?.Invoke(__instance, null);
        }
        catch
        {
        }
    }

    private static void ForceActivateHierarchy(Transform t)
    {
        while (t)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }
}

[HarmonyPatch(typeof(CharacterSpawnerGroup), "Awake")]
internal static class Patch_Group_Awake
{
    private static void Postfix(CharacterSpawnerGroup __instance)
    {
        try
        {
            var mod = ModBehaviourF.Instance;

            // 用“场景种子 + 该 Group 的 Transform 路径哈希”派生随机
            var gid = AITool.StableHash(AITool.TransformPath(__instance.transform));
            var seed = AITool.DeriveSeed(COOPManager.AIHandle.sceneSeed, gid);

            var rng = new System.Random(seed);
            if (__instance.hasLeader)
            {
                // 与原版相同的比较方式：保留队长的概率 = hasLeaderChance
                var keep = rng.NextDouble() <= __instance.hasLeaderChance;
                __instance.hasLeader = keep;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AI-SEED] Group.Awake Postfix 出错: {e.Message}");
        }
    }
}