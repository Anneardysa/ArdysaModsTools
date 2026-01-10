namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Install method selection options.
    /// </summary>
    public enum InstallMethod
    {
        None,
        AutoInstall,
        ManualInstall
    }

    /// <summary>
    /// Dialog to select install method (Auto or Manual).
    /// </summary>
    public partial class InstallMethodDialog : Form
    {
        public InstallMethod SelectedMethod { get; private set; } = InstallMethod.None;

        public InstallMethodDialog()
        {
            InitializeComponent();
            
            // Apply font fallback if JetBrains Mono not installed
            FontHelper.ApplyToForm(this);
        }

        private void BtnAutoInstall_Click(object? sender, EventArgs e)
        {
            SelectedMethod = InstallMethod.AutoInstall;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnManualInstall_Click(object? sender, EventArgs e)
        {
            SelectedMethod = InstallMethod.ManualInstall;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
