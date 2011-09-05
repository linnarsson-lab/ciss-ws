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
    public partial class BarcodeSetDialog : Form
    {
        public string barcodeSet;

        public BarcodeSetDialog()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            if (radioButtonV1.Checked) barcodeSet = "v1";
            else if (radioButtonV2.Checked) barcodeSet = "v2";
            else if (radioButtonV3.Checked) barcodeSet = "v3";
            else if (radioButtonV4.Checked) barcodeSet = "v4";
            else if (radioButtonNobarcodes.Checked) barcodeSet = "none";
        }
    }
}
