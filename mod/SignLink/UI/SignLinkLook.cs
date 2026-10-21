using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The window's settings as the kit's window reads them (IWindowLook): SignLink's config ("1 - Menu"), or the
    /// Settings tab's preview while its slider or an edge is held (shown at once, kept when let go).</summary>
    internal sealed class SignLinkLook : IWindowLook
    {
        public float TextScale => PluginConfig.MenuScale.Value;
        public float WidthShare => SettingsTab.PreviewWidth ?? PluginConfig.MenuWidth.Value;
        public float HeightShare => SettingsTab.PreviewHeight ?? PluginConfig.MenuHeight.Value;
        public float Opacity => SettingsTab.PreviewOpacity ?? PluginConfig.MenuOpacity.Value;   // a dragged Opacity shows at once
        public string ThemeName => PluginConfig.MenuTheme.Value;                                // Classic or Nordic (1.1.0)

        public float X
        {
            get => PluginConfig.WindowX.Value;
            set => PluginConfig.WindowX.Value = value;
        }

        public float Y
        {
            get => PluginConfig.WindowY.Value;
            set => PluginConfig.WindowY.Value = value;
        }

        public void Resizing(float widthShare, float heightShare) => SettingsTab.Resize(widthShare, heightShare);
        public void Resized() => SettingsTab.KeepSize();
    }
}
