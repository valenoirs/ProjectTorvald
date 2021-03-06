using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectTorvald
{
    public partial class AddNewFace : Form
    {
        public AddNewFace()
        {
            InitializeComponent();
        }

        private void btnAddFace_Click(object sender, EventArgs e)
        {
            if (txbNameLabel.Text.Length < 1)
            {
                MessageBox.Show("Name Cannot Empty ", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else if (txbNIMLabel.Text.Length < 1)
            {
                MessageBox.Show("NIM Cannot Empty ", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else if (txbNIMLabel.Text.Any(char.IsLetter))
            {
                MessageBox.Show("NIM Cannot Contain Letter", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else if (txbNIMLabel.Text.Length != 8)
            {
                MessageBox.Show("NIM Length Cannot More or Less Than 8 Digits", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                MainForm.addFace = true;
                if (!txbNameLabel.Text.Contains(" "))
                {
                    MainForm.NameLabel = txbNameLabel.Text + " ";
                }
                else
                {
                    MainForm.NameLabel = txbNameLabel.Text;
                }
                MainForm.NIMLabel = txbNIMLabel.Text;
                this.Dispose();
            }
        }
    }
}
