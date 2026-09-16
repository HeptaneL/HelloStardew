using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace HelloStardew.Talk;

/// <summary>
/// Holding the configured key while clicking the spouse opens the reply box instead of the
/// vanilla dialogue.
/// </summary>
[HarmonyPatch(typeof(NPC), nameof(NPC.checkAction))]
internal static class SpouseCheckActionPatch
{
	public static bool Prefix(NPC __instance, ref bool __result, Farmer who, GameLocation l)
	{
		if (!Context.IsWorldReady || !ModEntry.Config.EnableSpouseConversation)
			return true;

		if (!SpouseTalkSession.IsSpouse(__instance))
			return true;

		// Mirror the guards vanilla checkAction applies before it will talk to an NPC.
		if (__instance.IsInvisible || __instance.isSleeping.Value || !who.CanMove)
			return true;

		if (!ModEntry.ModHelper.Input.IsDown(ModEntry.Config.InitiateTypedDialogueKey))
			return true;

		// Clicking the spouse is the only way to begin a chat, so it always opens a new thread. That
		// also covers walking away mid-chat: whatever was left open is abandoned here.
		SpouseTalkSession.Instance.RequestTypedInput(__instance, startNewConversation: true);

		// Suppress vanilla so the two do not both push a dialogue.
		__result = false;
		return false;
	}
}
