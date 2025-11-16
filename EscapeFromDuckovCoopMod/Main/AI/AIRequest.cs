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

using EscapeFromDuckovCoopMod.Utils;

namespace EscapeFromDuckovCoopMod;

public class AIRequest : MonoBehaviour
{
    public static AIRequest Instance;

    private static NetService Service => NetService.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;

    public void Init()
    {
        Instance = this;
    }

    // DEPRECATED: No longer needed - clients derive seeds locally from sceneSeed
    // Keeping for backwards compatibility but this is now a no-op
    public void Server_SendRootSeedDelta(CharacterSpawnerRoot r, NetPeer target = null)
    {
        // No-op: Clients derive all spawn root seeds locally from sceneSeed
    }

    public void Server_TryRebroadcastIconLater(int aiId, CharacterMainControl cmc)
    {
        // DISABLED: Icon/Name rebroadcast no longer needed - deterministic generation handles this
        // Equipment and presets are generated identically on all clients via scene seed
        //if (!IsServer || aiId == 0 || !cmc) return;
        //if (!AIName._iconRebroadcastScheduled.Add(aiId)) return; // 只安排一次
        //
        //StartCoroutine(AIName.IconRebroadcastRoutine(aiId, cmc));
    }
}