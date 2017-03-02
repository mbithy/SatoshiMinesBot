using System.Windows.Controls;

namespace SatoshiMinesBot
{
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : UserControl
    {
        public MessageDialog(string title, string message)
        {
            InitializeComponent();
            Title.Text = title;
            Message.Text = message;
        }
    }
}
