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
    public partial class RunsLanesDialog : Form
    {
        public List<string> laneArgs;
        public string projectName;

        public RunsLanesDialog()
        {
            InitializeComponent();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            projectName = ProjectNameBox.Text;
            laneArgs = new List<string>();
            int junk;
            if (int.TryParse(RunNoBox1.Text, out junk) && int.TryParse(LaneNoBox1.Text, out junk))
                laneArgs.Add(RunNoBox1.Text + ":" + LaneNoBox1.Text);
            if (int.TryParse(RunNoBox2.Text, out junk) && int.TryParse(LaneNoBox2.Text, out junk))
                laneArgs.Add(RunNoBox2.Text + ":" + LaneNoBox2.Text);
            if (int.TryParse(RunNoBox3.Text, out junk) && int.TryParse(LaneNoBox3.Text, out junk))
                laneArgs.Add(RunNoBox3.Text + ":" + LaneNoBox3.Text);
        }
    }
}
