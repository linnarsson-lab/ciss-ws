namespace SilverBullet
{
	partial class MergeTool
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
			if(disposing && (components != null))
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
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tbMinimumReadsPerCell = new System.Windows.Forms.TextBox();
            this.cbRemoveLowCells = new System.Windows.Forms.CheckBox();
            this.cbNormalize = new System.Windows.Forms.CheckBox();
            this.cbIncludeRpts = new System.Windows.Forms.CheckBox();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(12, 34);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(303, 251);
            this.listBox1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(122, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Input files (\"_matrix.tab\")";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(321, 34);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 2;
            this.button1.Text = "Add";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(321, 63);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 3;
            this.button2.Text = "Remove";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tbMinimumReadsPerCell);
            this.groupBox1.Controls.Add(this.cbRemoveLowCells);
            this.groupBox1.Controls.Add(this.cbNormalize);
            this.groupBox1.Controls.Add(this.cbIncludeRpts);
            this.groupBox1.Location = new System.Drawing.Point(12, 291);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(384, 184);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Settings";
            // 
            // tbMinimumReadsPerCell
            // 
            this.tbMinimumReadsPerCell.Location = new System.Drawing.Point(192, 74);
            this.tbMinimumReadsPerCell.Name = "tbMinimumReadsPerCell";
            this.tbMinimumReadsPerCell.Size = new System.Drawing.Size(55, 20);
            this.tbMinimumReadsPerCell.TabIndex = 7;
            this.tbMinimumReadsPerCell.Text = "10000";
            // 
            // cbRemoveLowCells
            // 
            this.cbRemoveLowCells.AutoSize = true;
            this.cbRemoveLowCells.Checked = true;
            this.cbRemoveLowCells.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbRemoveLowCells.Location = new System.Drawing.Point(35, 76);
            this.cbRemoveLowCells.Name = "cbRemoveLowCells";
            this.cbRemoveLowCells.Size = new System.Drawing.Size(249, 17);
            this.cbRemoveLowCells.TabIndex = 6;
            this.cbRemoveLowCells.Text = "Remove cells with less than                      reads";
            this.cbRemoveLowCells.UseVisualStyleBackColor = true;
            // 
            // cbNormalize
            // 
            this.cbNormalize.AutoSize = true;
            this.cbNormalize.Checked = true;
            this.cbNormalize.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbNormalize.Location = new System.Drawing.Point(35, 99);
            this.cbNormalize.Name = "cbNormalize";
            this.cbNormalize.Size = new System.Drawing.Size(248, 17);
            this.cbNormalize.TabIndex = 5;
            this.cbNormalize.Text = "Normalize each feature to 1,000,000 t.p.m./cell";
            this.cbNormalize.UseVisualStyleBackColor = true;
            // 
            // cbIncludeRpts
            // 
            this.cbIncludeRpts.AutoSize = true;
            this.cbIncludeRpts.Location = new System.Drawing.Point(35, 45);
            this.cbIncludeRpts.Name = "cbIncludeRpts";
            this.cbIncludeRpts.Size = new System.Drawing.Size(99, 17);
            this.cbIncludeRpts.TabIndex = 3;
            this.cbIncludeRpts.Text = "Include repeats";
            this.cbIncludeRpts.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button3.Location = new System.Drawing.Point(321, 536);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 5;
            this.button3.Text = "OK";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button4
            // 
            this.button4.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button4.Location = new System.Drawing.Point(240, 536);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(75, 23);
            this.button4.TabIndex = 6;
            this.button4.Text = "Cancel";
            this.button4.UseVisualStyleBackColor = true;
            // 
            // MergeTool
            // 
            this.AcceptButton = this.button3;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.button4;
            this.ClientSize = new System.Drawing.Size(413, 571);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listBox1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "MergeTool";
            this.Text = "MergeTool";
            this.Load += new System.EventHandler(this.MergeTool_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ListBox listBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cbIncludeRpts;
		private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
		private System.Windows.Forms.TextBox tbMinimumReadsPerCell;
		private System.Windows.Forms.CheckBox cbRemoveLowCells;
        private System.Windows.Forms.CheckBox cbNormalize;
	}
}