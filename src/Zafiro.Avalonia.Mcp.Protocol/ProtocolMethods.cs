namespace Zafiro.Avalonia.Mcp.Protocol;

public static class ProtocolMethods
{
    // Tree inspection
    public const string GetTree = "get_tree";
    public const string Search = "search";
    public const string GetAncestors = "get_ancestors";
    public const string GetScreenText = "get_screen_text";
    public const string GetInteractables = "get_interactables";

    // Properties
    public const string GetProperties = "get_properties";
    public const string SetProperty = "set_property";
    public const string GetStyles = "get_styles";

    // Input
    public const string Click = "click";
    public const string Tap = "tap";
    public const string KeyDown = "key_down";
    public const string KeyUp = "key_up";
    public const string TextInput = "text_input";
    public const string PointerPress = "pointer_press";
    public const string PointerRelease = "pointer_release";
    public const string PointerMove = "pointer_move";

    // Actions
    public const string Action = "action";
    public const string Focus = "focus";
    public const string Enable = "enable";
    public const string Disable = "disable";
    public const string BringIntoView = "bring_into_view";

    // Pseudo-classes
    public const string GetPseudoClasses = "get_pseudo_classes";
    public const string SetPseudoClass = "set_pseudo_class";

    // Capture
    public const string Screenshot = "screenshot";
    public const string StartRecording = "start_recording";
    public const string StopRecording = "stop_recording";

    // Resources
    public const string GetResources = "get_resources";
    public const string ListAssets = "list_assets";
    public const string OpenAsset = "open_asset";

    // Interaction
    public const string SelectItem = "select_item";
    public const string Toggle = "toggle";
    public const string SetValue = "set_value";
    public const string Scroll = "scroll";

    // Wait
    public const string WaitFor = "wait_for";
    public const string ClickAndWait = "click_and_wait";
    public const string ClickByQuery = "click_by_query";

    // MVVM / Data inspection
    public const string GetDataContext = "get_datacontext";
    public const string GetBindings = "get_bindings";
    public const string FindViewSource = "find_view_source";
    public const string GetXaml = "get_xaml";
    public const string DiffTree = "diff_tree";

    // Connection
    public const string ListWindows = "list_windows";
    public const string Ping = "ping";
}
