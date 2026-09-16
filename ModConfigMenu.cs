using GenericModConfigMenu;
using StardewModdingAPI;

namespace HelloStardew;

/// <summary>Exposes <see cref="ModConfig"/> through Generic Mod Config Menu, when it's installed.</summary>
/// <remarks>
/// Every getter and setter reads <c>ModEntry.Config</c> rather than a captured instance, because
/// GMCM's "reset to defaults" swaps the whole config object out.
/// </remarks>
internal static class ModConfigMenu
{
	private const string ModId = "spacechase0.GenericModConfigMenu";

	public static void Register(ModEntry mod)
	{
		IGenericModConfigMenuApi? menu = mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModId);
		if (menu is null)
		{
			mod.Monitor.Log("Generic Mod Config Menu isn't installed; edit config.json instead.", LogLevel.Info);
			return;
		}

		menu.Register(
			mod: mod.ModManifest,
			reset: () => ModEntry.Config = new ModConfig(),
			save: () =>
			{
				mod.Helper.WriteConfig(ModEntry.Config);

				// Push the new values into the running services. Called when the menu closes,
				// so changes take effect without restarting the game.
				mod.ApplyRuntimeConfig();
			}
		);

		RegisterConversationOptions(menu, mod);
		RegisterAgentOptions(menu, mod);
		RegisterApiOptions(menu, mod);
	}

	private static void RegisterConversationOptions(IGenericModConfigMenuApi menu, ModEntry mod)
	{
		menu.AddSectionTitle(
			mod: mod.ModManifest,
			text: () => "Conversation"
		);

		menu.AddBoolOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.EnableSpouseConversation,
			setValue: value => ModEntry.Config.EnableSpouseConversation = value,
			name: () => "Enable Spouse Conversation",
			tooltip: () => "Whether your spouse answers through the AI agent instead of vanilla dialogue.",
			fieldId: "EnableSpouseConversation"
		);

		menu.AddKeybind(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.InitiateTypedDialogueKey,
			setValue: value => ModEntry.Config.InitiateTypedDialogueKey = value,
			name: () => "Type To Spouse",
			tooltip: () => "Hold this key and click your spouse to write your own message.",
			fieldId: "InitiateTypedDialogueKey"
		);

		menu.AddBoolOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.OfferTypedResponse,
			setValue: value => ModEntry.Config.OfferTypedResponse = value,
			name: () => "Offer \"Something Else\"",
			tooltip: () => "Add a *Something else* option so you can type a reply instead of picking a suggestion.",
			fieldId: "OfferTypedResponse"
		);
	}

	private static void RegisterAgentOptions(IGenericModConfigMenuApi menu, ModEntry mod)
	{
		menu.AddSectionTitle(
			mod: mod.ModManifest,
			text: () => "AI Agent"
		);

		menu.AddTextOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.AgentEndpoint,
			setValue: value => ModEntry.Config.AgentEndpoint = value,
			name: () => "Agent URL",
			tooltip: () => "Full URL of the agent that writes your spouse's replies, including port. Default: http://127.0.0.1:8000/chat",
			allowedValues: null,
			formatAllowedValue: null,
			fieldId: "AgentEndpoint"
		);

		menu.AddNumberOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.AgentTimeoutSeconds,
			setValue: value => ModEntry.Config.AgentTimeoutSeconds = value,
			name: () => "Agent Timeout (seconds)",
			tooltip: () => "Give up on a reply after this long and show a fallback line.",
			min: 1,
			max: 600,
			interval: 1,
			formatValue: null,
			fieldId: "AgentTimeoutSeconds"
		);
	}

	private static void RegisterApiOptions(IGenericModConfigMenuApi menu, ModEntry mod)
	{
		menu.AddSectionTitle(
			mod: mod.ModManifest,
			text: () => "Calendar HTTP API"
		);

		menu.AddParagraph(
			mod: mod.ModManifest,
			text: () => "Changing the address or port rebinds the API as soon as this menu closes. If the new port is already in use the existing listener is kept."
		);

		menu.AddTextOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.BindAddress,
			setValue: value => ModEntry.Config.BindAddress = value,
			name: () => "Bind Address",
			tooltip: () => "Address the read-only calendar API listens on. Keep it on 127.0.0.1 unless you know what you're doing.",
			allowedValues: null,
			formatAllowedValue: null,
			fieldId: "BindAddress"
		);

		menu.AddNumberOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.Port,
			setValue: value => ModEntry.Config.Port = value,
			name: () => "Port",
			tooltip: () => "Port the read-only calendar API listens on.",
			min: 1,
			max: 65535,
			interval: 1,
			formatValue: null,
			fieldId: "Port"
		);
	}
}
