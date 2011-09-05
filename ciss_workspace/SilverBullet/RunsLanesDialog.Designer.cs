namespace SilverBullet
{
    partial class RunsLanesDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.RunNoBox1 = new System.Windows.Forms.TextBox();
            this.RunNoBox2 = new System.Windows.Forms.TextBox();
            this.RunNoBox3 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.LaneNoBox3 = new System.Windows.Forms.TextBox();
            this.LaneNoBox2 = new System.Windows.Forms.TextBox();
            this.LaneNoBox1 = new System.Windows.Forms.TextBox();
            this.buttonRunsLanesSelected = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.ProjectNameBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(47, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(248, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Specify Run numbers and lanes to extract:";
            // 
            // RunNoBox1
            // 
            this.RunNoBox1.Location = new System.Drawing.Point(100, 63);
            this.RunNoBox1.Name = "RunNoBox1";
            this.RunNoBox1.Size = new System.Drawing.Size(34, 20);
            this.RunNoBox1.TabIndex = 1;
            // 
            // RunNoBox2
            // 
            this.RunNoBox2.Location = new System.Drawing.Point(100, 89);
            this.RunNoBox2.Name = "RunNoBox2";
            this.RunNoBox2.Size = new System.Drawing.Size(34, 20);
            this.RunNoBox2.TabIndex = 3;
            // 
            // RunNoBox3
            // 
            this.RunNoBox3.Location = new System.Drawing.Point(100, 115);
            this.RunNoBox3.Name = "RunNoBox3";
            this.RunNoBox3.Size = new System.Drawing.Size(34, 20);
            this.RunNoBox3.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(100, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "RunNo";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(156, 44);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(73, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "LaneNumbers";
            // 
            // LaneNoBox3
            // 
            this.LaneNoBox3.Location = new System.Drawing.Point(160, 113);
            this.LaneNoBox3.Name = "LaneNoBox3";
            this.LaneNoBox3.Size = new System.Drawing.Size(68, 20);
            this.LaneNoBox3.TabIndex = 6;
            // 
            // LaneNoBox2
            // 
            this.LaneNoBox2.Location = new System.Drawing.Point(160, 87);
            this.LaneNoBox2.Name = "LaneNoBox2";
            this.LaneNoBox2.Size = new System.Drawing.Size(68, 20);
            this.LaneNoBox2.TabIndex = 4;
            // 
            // LaneNoBox1
            // 
            this.LaneNoBox1.Location = new System.Drawing.Point(160, 61);
            this.LaneNoBox1.Name = "LaneNoBox1";
            this.LaneNoBox1.Size = new System.Drawing.Size(68, 20);
            this.LaneNoBox1.TabIndex = 2;
            // 
            // buttonRunsLanesSelected
            // 
            this.buttonRunsLanesSelected.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonRunsLanesSelected.Location = new System.Drawing.Point(124, 241);
            this.buttonRunsLanesSelected.Name = "buttonRunsLanesSelected";
            this.buttonRunsLanesSelected.Size = new System.Drawing.Size(75, 23);
            this.buttonRunsLanesSelected.TabIndex = 8;
            this.buttonRunsLanesSelected.Text = "OK";
            this.buttonRunsLanesSelected.UseVisualStyleBackColor = true;
            this.buttonRunsLanesSelected.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(50, 153);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(141, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Specify a project name:";
            // 
            // ProjectNameBox
            // 
            this.ProjectNameBox.Location = new System.Drawing.Point(101, 183);
            this.ProjectNameBox.Name = "ProjectNameBox";
            this.ProjectNameBox.Size = new System.Drawing.Size(126, 20);
            this.ProjectNameBox.TabIndex = 7;
            // 
            // RunsLanesDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(353, 288);
            this.Controls.Add(this.ProjectNameBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.buttonRunsLanesSelected);
            this.Controls.Add(this.LaneNoBox3);
            this.Controls.Add(this.LaneNoBox2);
            this.Controls.Add(this.LaneNoBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.RunNoBox3);
            this.Controls.Add(this.RunNoBox2);
            this.Controls.Add(this.RunNoBox1);
            this.Controls.Add(this.label1);
            this.Name = "RunsLanesDialog";
            this.Text = "RunsLanesDialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox RunNoBox1;
        private System.Windows.Forms.TextBox RunNoBox2;
        private System.Windows.Forms.TextBox RunNoBox3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox LaneNoBox3;
        private System.Windows.Forms.TextBox LaneNoBox2;
        private System.Windows.Forms.TextBox LaneNoBox1;
        private System.Windows.Forms.Button buttonRunsLanesSelected;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox ProjectNameBox;
    }
}