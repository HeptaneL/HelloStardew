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
			text: () => Text.ConfigConversationSection
		);

		menu.AddBoolOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.EnableSpouseConversation,
			setValue: value => ModEntry.Config.EnableSpouseConversation = value,
			name: () => Text.ConfigEnableSpouseName,
			tooltip: () => Text.ConfigEnableSpouseTooltip,
			fieldId: "EnableSpouseConversation"
		);

		menu.AddKeybind(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.InitiateTypedDialogueKey,
			setValue: value => ModEntry.Config.InitiateTypedDialogueKey = value,
			name: () => Text.ConfigTypeToSpouseName,
			tooltip: () => Text.ConfigTypeToSpouseTooltip,
			fieldId: "InitiateTypedDialogueKey"
		);

		menu.AddBoolOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.OfferTypedResponse,
			setValue: value => ModEntry.Config.OfferTypedResponse = value,
			name: () => Text.ConfigOfferTypedName,
			tooltip: () => Text.ConfigOfferTypedTooltip,
			fieldId: "OfferTypedResponse"
		);
	}

	private static void RegisterAgentOptions(IGenericModConfigMenuApi menu, ModEntry mod)
	{
		menu.AddSectionTitle(
			mod: mod.ModManifest,
			text: () => Text.ConfigAgentSection
		);

		menu.AddTextOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.AgentEndpoint,
			setValue: value => ModEntry.Config.AgentEndpoint = value,
			name: () => Text.ConfigAgentEndpointName,
			tooltip: () => Text.ConfigAgentEndpointTooltip,
			allowedValues: null,
			formatAllowedValue: null,
			fieldId: "AgentEndpoint"
		);

		menu.AddNumberOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.AgentTimeoutSeconds,
			setValue: value => ModEntry.Config.AgentTimeoutSeconds = value,
			name: () => Text.ConfigAgentTimeoutName,
			tooltip: () => Text.ConfigAgentTimeoutTooltip,
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
			text: () => Text.ConfigApiSection
		);

		menu.AddParagraph(
			mod: mod.ModManifest,
			text: () => Text.ConfigApiNote
		);

		menu.AddTextOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.BindAddress,
			setValue: value => ModEntry.Config.BindAddress = value,
			name: () => Text.ConfigBindAddressName,
			tooltip: () => Text.ConfigBindAddressTooltip,
			allowedValues: null,
			formatAllowedValue: null,
			fieldId: "BindAddress"
		);

		menu.AddNumberOption(
			mod: mod.ModManifest,
			getValue: () => ModEntry.Config.Port,
			setValue: value => ModEntry.Config.Port = value,
			name: () => Text.ConfigPortName,
			tooltip: () => Text.ConfigPortTooltip,
			min: 1,
			max: 65535,
			interval: 1,
			formatValue: null,
			fieldId: "Port"
		);
	}
}
