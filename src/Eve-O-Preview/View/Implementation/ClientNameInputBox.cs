using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EveOPreview.View
{
    public partial class ClientNameInputBox
    {
        public ClientNameInputBox()
        {
            InitializeComponent();
        }

        public void LoadKnownClients(List<string> clientNames)
        {
            this.listOfAllClients.DataSource = clientNames;
        }

        private void acceptSelectionButton_Click(object sender, EventArgs e)
        {
            SetUserResponse();
        }

        private void SetUserResponse() 
        {
            SelectedClientName = selectedClientNameTextBox.Text;
            Close();
        }

        private void listOfAllClients_SelectedValueChanged(object sender, EventArgs e)
        {
            selectedClientNameTextBox.Text = listOfAllClients.SelectedItem.ToString();
        }

        private void listOfAllClients_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SetUserResponse();
        }
    }
}
