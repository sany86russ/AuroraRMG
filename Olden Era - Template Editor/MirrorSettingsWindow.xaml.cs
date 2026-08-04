using System.Windows;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Options asked once when mirror mode is switched on: what to keep in sync with the twin half,
    /// and whether to rebuild the right half from the left right away.
    /// </summary>
    public partial class MirrorSettingsWindow : Window
    {
        public bool MirrorProperties { get; private set; }
        public bool MirrorConnections { get; private set; }
        public bool MakeSymmetricNow { get; private set; }

        public MirrorSettingsWindow(bool mirrorProperties, bool mirrorConnections)
        {
            InitializeComponent();
            ChkProps.IsChecked = mirrorProperties;
            ChkConns.IsChecked = mirrorConnections;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            MirrorProperties = ChkProps.IsChecked == true;
            MirrorConnections = ChkConns.IsChecked == true;
            MakeSymmetricNow = ChkSymmetrize.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
