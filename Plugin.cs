using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ThrowCrapOut;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly ArmouryScanner scanner;
    private readonly MainWindow window;
    private readonly ConfigWindow configWindow;
    private readonly IFontHandle uiFont;
    private readonly WindowSystem windows = new("ThrowCrapOut");

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands,
        IFramework framework, IClientState clientState, IPlayerState playerState, IDataManager data, IPluginLog log, IChatGui chat)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.framework = framework;
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.DisabledCategories ??= [];
        if (config.Version < 3)
        {
            config.DelayMilliseconds = config.EffectiveDelay;
            config.Version = 3;
            pluginInterface.SavePluginConfig(config);
        }
        scanner = new ArmouryScanner(clientState, playerState, data, log, config, chat);
        uiFont = pluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 17));
        var previousLanguage = config.Language;
        configWindow = new ConfigWindow(config, () =>
        {
            pluginInterface.SavePluginConfig(config);
            if (previousLanguage != config.Language)
            {
                previousLanguage = config.Language;
                scanner.LanguageChanged();
            }
            scanner.RequestRefresh();
        });
        window = new MainWindow(scanner, config, OpenConfigUi);
        windows.AddWindow(window);
        windows.AddWindow(configWindow);
        commands.AddHandler("/tco", new CommandInfo(OnCommand)
        {
            HelpMessage = "/tco | /tco config | /tco sort | /tco split | /tco clear | /tco stop",
        });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        framework.Update += OnUpdate;
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "config": OpenConfigUi(); return;
            case "sort": scanner.RequestAction(ArsenalAction.Sort); return;
            case "split": scanner.RequestAction(ArsenalAction.Split); return;
            case "clear": scanner.RequestAction(ArsenalAction.Clear); return;
            case "random": scanner.RequestAction(ArsenalAction.Random); return;
            case "stop": scanner.StopAction(); return;
            case "": break;
            default: return;
        }
        window.Toggle();
        if (window.IsOpen)
            scanner.RequestRefresh();
    }

    private void OnUpdate(IFramework _) => scanner.Update(window.IsOpen);

    private void Draw()
    {
        using (uiFont.Push()) windows.Draw();
    }

    private void OpenConfigUi() => configWindow.IsOpen = true;

    private void OpenMainUi()
    {
        window.IsOpen = true;
        scanner.RequestRefresh();
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        pluginInterface.UiBuilder.Draw -= Draw;
        commands.RemoveHandler("/tco");
        windows.RemoveAllWindows();
        uiFont.Dispose();
    }
}
