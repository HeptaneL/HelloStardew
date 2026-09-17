using HarmonyLib;
using StardewValley;

namespace HelloStardew.Talk;

/// <summary>
/// Intercepts the option the player picks on one of our dialogue pages.
/// </summary>
/// <remarks>
/// Vanilla resolves a picked option inside <c>Dialogue.chooseResponse</c> by looking the option's
/// key up in <c>NPC.Dialogue</c>, which would fail for keys we invent. Returning false from this
/// prefix skips that lookup; the dialogue box still runs its normal closing transition, because it
/// set <c>transitioning</c> before ever calling us.
/// <para>
/// Which NPC is speaking does not have to be checked: the keys below say whose page this is.
/// </para>
/// </remarks>
[HarmonyPatch(typeof(Dialogue), nameof(Dialogue.chooseResponse))]
internal static class DialogueChooseResponsePatch
{
	public static bool Prefix(Dialogue __instance, ref bool __result, Response response)
	{
		NPC? speaker = __instance.speaker;
		if (speaker is null)
			return true;

		// Only take over a page where every option is ours. If anything else contributed an
		// option, hand the whole page back to vanilla rather than half-handling it.
		if (__instance.getResponseOptions().Any(r =>
				r.responseKey is null
				|| !r.responseKey.StartsWith(TalkScript.KeyPrefix, StringComparison.Ordinal)))
		{
			return true;
		}

		__result = true;

		if (response.responseKey == TalkScript.KeySilent)
		{
			TalkSession.Instance.End();
			return false;
		}

		if (response.responseKey == TalkScript.KeyTyped)
		{
			// Still the same chat, so this keeps the running thread rather than starting a new one.
			TalkSession.Instance.RequestTypedInput(speaker);
			return false;
		}

		// Every suggested reply shares one key and is told apart by its text.
		TalkSession.Instance.OnPlayerSpoke(speaker, response.responseText);
		return false;
	}
}
