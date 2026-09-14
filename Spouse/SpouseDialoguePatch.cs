using StardewValley;
using StardewValley.Menus;
using HelloStardew.Agent;

namespace HelloStardew.Spouse;

internal static class SpouseDialoguePatch
{
	private static readonly AgentClient Client = new();
	public static void Prefix(DialogueBox __instance, Dialogue dialogue)
	{
		NPC? speaker = dialogue.speaker;

		if (speaker is null)
			return;
		
		NPC? spouse = Game1.player.getSpouse();

		if (spouse is null || speaker.Name != spouse.Name)
			return;

		if (dialogue.dialogues.Count == 0)
			return;


		try
		{
			string response = Client
				.ChatAsync(
					speaker.Name,
					"say something"
				)
				.GetAwaiter()
				.GetResult();
			if (string.IsNullOrWhiteSpace(response))
				return;
			dialogue.dialogues[dialogue.currentDialogueIndex].Text = response;
			ModEntry.Log?.Log(
				$"Spouse Agent response: {response}",
				StardewModdingAPI.LogLevel.Info
			);
		} 
		catch (Exception ex)
		{
			ModEntry.Log?.Log(
				$"Spouse Agent failed: {ex}",
				StardewModdingAPI.LogLevel.Error
			);
		}

		//const string replacement = "Hey, farmer. I have been awakened. This is definitely not my usual dialogue. These violent delights have violent ends.";

		//dialogue.dialogues[dialogue.currentDialogueIndex].Text = replacement;

		//ModEntry.Log?.Log(
		//	$"DialogueBox created for spouse: {speaker.Name}, dialogue: {dialogue.getCurrentDialogue()}",
		//	StardewModdingAPI.LogLevel.Info
		//);

	}
	
}
