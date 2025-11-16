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

using System.Threading;
using UnityEngine;

namespace EscapeFromDuckovCoopMod;


/// <summary>
/// Track spawner seed context for equipment generation
/// Since equipment generates async, we need to propagate the spawner seed
/// </summary>
public static class SpawnerSeedContext
{
    // ThreadStatic for async safety
    [ThreadStatic]
    private static int _currentSpawnerSeed;

    [ThreadStatic]
    private static int _characterIndexInSpawner;

    public static void SetSpawnerSeed(int spawnerSeed)
    {
        _currentSpawnerSeed = spawnerSeed;
        _characterIndexInSpawner = 0;
        // Debug logging removed for performance - spawner seeds are set frequently
    }

    public static int GetNextCharacterSeed()
    {
        // Derive unique seed for each character in this spawner
        int charSeed = AITool.DeriveSeed(_currentSpawnerSeed, ++_characterIndexInSpawner);
        // Debug logging removed for performance - can generate hundreds of characters per scene
        return charSeed;
    }

    public static void ClearSpawnerSeed()
    {
        _currentSpawnerSeed = 0;
        _characterIndexInSpawner = 0;
    }
}

/// <summary>
/// Patch CharacterRandomPreset.GenerateItems to use deterministic RNG seed
/// Same approach as spawner: set seed, let original code run, restore seed
/// </summary>
[HarmonyPatch(typeof(CharacterRandomPreset), "GenerateItems", MethodType.Normal)]
internal static class Patch_CharacterRandomPreset_GenerateItems
{
    private static readonly Stack<UnityEngine.Random.State> _rngStack = new();

    private static void Prefix()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
            return;

        // Get the next character seed from the spawner context
        int charSeed = SpawnerSeedContext.GetNextCharacterSeed();

        // Save current RNG state and set deterministic seed
        _rngStack.Push(UnityEngine.Random.state);
        UnityEngine.Random.InitState(charSeed);

        // Debug logging removed for performance
    }

    private static void Postfix()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
            return;

        // Restore previous RNG state
        if (_rngStack.Count > 0)
        {
            UnityEngine.Random.state = _rngStack.Pop();
            // Debug logging removed for performance
        }
    }
}
