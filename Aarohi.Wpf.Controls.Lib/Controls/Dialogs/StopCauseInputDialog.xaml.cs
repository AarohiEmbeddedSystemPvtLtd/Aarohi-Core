using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;

namespace Aarohi.Wpf.Controls.Lib.Controls.Dialogs
{
    public partial class StopCauseInputDialog : Window
    {
        public string StopCauseText { get; private set; } = string.Empty;

        public StopCauseInputDialog(string? initialText = null)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(initialText))
                SetDocumentText(initialText);
        }

        public static string Show(string? initialText = null)
        {
            StopCauseInputDialog dialog = new(initialText);

            if (Application.Current?.MainWindow != null)
                dialog.Owner = Application.Current.MainWindow;

            bool? result = dialog.ShowDialog();
            return result == true ? dialog.StopCauseText : string.Empty;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            StopCauseText = GetDocumentText();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            StopCauseText = string.Empty;
            DialogResult = false;
            Close();
        }

        private void StopCauseRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;
        }

        private string GetDocumentText()
        {
            TextRange range = new(StopCauseRichTextBox.Document.ContentStart, StopCauseRichTextBox.Document.ContentEnd);
            return range.Text.Trim();
        }

        private void SetDocumentText(string text)
        {
            StopCauseRichTextBox.Document.Blocks.Clear();
            StopCauseRichTextBox.Document.Blocks.Add(new Paragraph(new Run(text)));
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
