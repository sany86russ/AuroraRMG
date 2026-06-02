using System.Windows;
using System.Windows.Input;

namespace Olden_Era___Template_Editor
{
    /// <summary>Small Aurora-styled modal that asks the user for a map name (used by "Save to game folder").</summary>
    public partial class NamePromptWindow : Window
    {
        /// <summary>The trimmed name the user entered (valid only when <see cref="Window.ShowDialog"/> returned true).</summary>
        public string MapName => TxtName.Text.Trim();

        public NamePromptWindow(string initialName)
        {
            InitializeComponent();
            TxtName.Text = initialName ?? string.Empty;
            Loaded += (_, _) =>
            {
                TxtName.Focus();
                TxtName.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (MapName.Length > 0)
                DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
