using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SilverBullet
{
    public partial class TextDialog : Form
    {
        public string value;

        public TextDialog()
        {
            InitializeComponent();
        }
        public TextDialog(string qText)
        {
            InitializeComponent();
            this.textEnterHeader.Text = qText;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.value = this.nameTextBox.Text;
        }
    }
}
