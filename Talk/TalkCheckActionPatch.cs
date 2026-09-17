using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace HelloStardew.Talk;

/// <summary>
/// Holding the configured key while clicking a villager opens the reply box instead of the
/// vanilla dialogue.
/// </summary>
/// <remarks>
/// The spouse is a villager like any other here, so one rule covers both.
/// </remarks>
[HarmonyPatch(typeof(NPC), nameof(NPC.checkAction))]
internal static class TalkCheckActionPatch
{
	public static bool Prefix(NPC __instance, ref bool __result, Farmer who, GameLocation l)
	{
		if (!Context.IsWorldReady || !ModEntry.Config.EnableSpouseConversation)
			return true;

		// This patch sits on the base class, which monsters and other non-villagers also derive
		// from; without this they would answer the key too.
		if (!__instance.IsVillager)
			return true;

		// Mirror the guards vanilla checkAction applies before it will talk to an NPC.
		if (__instance.IsInvisible || __instance.isSleeping.Value || !who.CanMove)
			return true;

		if (!ModEntry.ModHelper.Input.IsDown(ModEntry.Config.InitiateTypedDialogueKey))
			return true;

		// Clicking a villager is the only way to begin a chat, so it always opens a new thread. That
		// also covers walking away mid-chat: whatever was left open is abandoned here.
		TalkSession.Instance.RequestTypedInput(__instance, startNewConversation: true);

		// Suppress vanilla so the two do not both push a dialogue.
		__result = false;
		return false;
	}
}
