using StardewValley;
using StardewValley.Menus;
using HelloStardew.UI;
using HelloStardew.Agent;

namespace HelloStardew.Spouse;

internal static class SpouseDialoguePatch
{
	private static readonly AgentClient Client = new();

	private static bool _awaitGeneration;

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
		
		if (_awaitGeneration)
			return;

		_awaitGeneration = true;

		int index = dialogue.currentDialogueIndex;
		string originText = dialogue.dialogues[index].Text;
		dialogue.dialogues[index].Text = "...";

		ModEntry.Dispatcher?.Enqueue(() =>
		{
			Game1.activeClickableMenu = new ThinkingWindow($"{speaker.Name} is thinking");
		});
		
		_ = Task.Run(async () =>
		{
			string response = originText;
			try
			{
				string result = await Client
					.ChatAsync(
						speaker.Name,
						"say something"
					);

				if (!string.IsNullOrWhiteSpace(response))
					response = result;

			} 
			catch (Exception ex)
			{
				ModEntry.Log?.Log(
					$"Spouse Agent failed: {ex}",
					StardewModdingAPI.LogLevel.Error
				);
			}

			dialogue.dialogues[dialogue.currentDialogueIndex].Text = response;
			ModEntry.Log?.Log(
				$"Spouse Agent response: {response}",
				StardewModdingAPI.LogLevel.Info
			);

			ModEntry.Dispatcher?.Enqueue(() =>
			{
				Game1.activeClickableMenu = new DialogueBox(dialogue);
			});
		});

	}
}
