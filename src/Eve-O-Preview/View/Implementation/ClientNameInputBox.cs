//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
