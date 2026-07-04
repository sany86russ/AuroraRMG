using System.Windows;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Read-only in-app reference for the visual zone editor: canvas, inspector, roads,
    /// content pools, toolbar tools and the colour legend. All text is localised (RU/EN)
    /// via <c>{DynamicResource S.HW.*}</c> so it switches with the app language.
    /// </summary>
    public partial class EditorHelpWindow : Window
    {
        public EditorHelpWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
