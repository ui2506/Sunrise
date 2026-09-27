namespace Sunrise.Utility;

public abstract class PluginModule
{
    protected virtual List<PluginModule> SubModules { get; } = [];

    public bool IsEnabled { get; private set; }

    public void Enable()
    {
        if (IsEnabled)
            return;

        OnEnabled();
        IsEnabled = true;

        Handlers.ServerEvents.WaitingForPlayers += OnReset;

        foreach (PluginModule module in SubModules)
            module.Enable();
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        OnDisabled();
        IsEnabled = false;

        Handlers.ServerEvents.WaitingForPlayers -= OnReset;

        foreach (PluginModule module in SubModules)
            module.Disable();

        OnReset();
    }

    protected virtual void OnEnabled() { }
    protected virtual void OnDisabled() { }
    protected virtual void OnReset() { }
}
