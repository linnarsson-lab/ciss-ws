namespace SilverBullet
{
	partial class GenomeDialog
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
            this.rbMouse = new System.Windows.Forms.RadioButton();
            this.rbHuman = new System.Windows.Forms.RadioButton();
            this.button1 = new System.Windows.Forms.Button();
            this.rbChicken = new System.Windows.Forms.RadioButton();
            this.rbUCSC = new System.Windows.Forms.RadioButton();
            this.rbVEGA = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rbOldAnnot = new System.Windows.Forms.RadioButton();
            this.rbENSE = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // rbMouse
            // 
            this.rbMouse.AutoSize = true;
            this.rbMouse.Checked = true;
            this.rbMouse.Location = new System.Drawing.Point(31, 40);
            this.rbMouse.Name = "rbMouse";
            this.rbMouse.Size = new System.Drawing.Size(57, 17);
            this.rbMouse.TabIndex = 2;
            this.rbMouse.TabStop = true;
            this.rbMouse.Text = "Mouse";
            this.rbMouse.UseVisualStyleBackColor = true;
            // 
            // rbHuman
            // 
            this.rbHuman.AutoSize = true;
            this.rbHuman.Location = new System.Drawing.Point(112, 40);
            this.rbHuman.Name = "rbHuman";
            this.rbHuman.Size = new System.Drawing.Size(59, 17);
            this.rbHuman.TabIndex = 3;
            this.rbHuman.Text = "Human";
            this.rbHuman.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button1.Location = new System.Drawing.Point(112, 154);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 9;
            this.button1.Text = "Ok";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // rbChicken
            // 
            this.rbChicken.AutoSize = true;
            this.rbChicken.Location = new System.Drawing.Point(191, 40);
            this.rbChicken.Name = "rbChicken";
            this.rbChicken.Size = new System.Drawing.Size(64, 17);
            this.rbChicken.TabIndex = 10;
            this.rbChicken.Text = "Chicken";
            this.rbChicken.UseVisualStyleBackColor = true;
            // 
            // rbUCSC
            // 
            this.rbUCSC.AutoSize = true;
            this.rbUCSC.Location = new System.Drawing.Point(18, 30);
            this.rbUCSC.Name = "rbUCSC";
            this.rbUCSC.Size = new System.Drawing.Size(54, 17);
            this.rbUCSC.TabIndex = 11;
            this.rbUCSC.TabStop = true;
            this.rbUCSC.Text = "UCSC";
            this.rbUCSC.UseVisualStyleBackColor = true;
            // 
            // rbVEGA
            // 
            this.rbVEGA.AutoSize = true;
            this.rbVEGA.Location = new System.Drawing.Point(86, 30);
            this.rbVEGA.Name = "rbVEGA";
            this.rbVEGA.Size = new System.Drawing.Size(54, 17);
            this.rbVEGA.TabIndex = 12;
            this.rbVEGA.TabStop = true;
            this.rbVEGA.Text = "VEGA";
            this.rbVEGA.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rbENSE);
            this.groupBox1.Controls.Add(this.rbOldAnnot);
            this.groupBox1.Controls.Add(this.rbVEGA);
            this.groupBox1.Controls.Add(this.rbUCSC);
            this.groupBox1.Location = new System.Drawing.Point(6, 75);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(279, 73);
            this.groupBox1.TabIndex = 14;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Optionally specify annotations";
            // 
            // rbOldAnnot
            // 
            this.rbOldAnnot.AutoSize = true;
            this.rbOldAnnot.Location = new System.Drawing.Point(160, 30);
            this.rbOldAnnot.Name = "rbOldAnnot";
            this.rbOldAnnot.Size = new System.Drawing.Size(47, 17);
            this.rbOldAnnot.TabIndex = 13;
            this.rbOldAnnot.TabStop = true;
            this.rbOldAnnot.Text = "(Old)";
            this.rbOldAnnot.UseVisualStyleBackColor = true;
            // 
            // rbENSE
            // 
            this.rbENSE.AutoSize = true;
            this.rbENSE.Location = new System.Drawing.Point(213, 30);
            this.rbENSE.Name = "rbENSE";
            this.rbENSE.Size = new System.Drawing.Size(54, 17);
            this.rbENSE.TabIndex = 14;
            this.rbENSE.TabStop = true;
            this.rbENSE.Text = "ENSE";
            this.rbENSE.UseVisualStyleBackColor = true;
            // 
            // GenomeDialog
            // 
            this.AcceptButton = this.button1;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(325, 206);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.rbChicken);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.rbHuman);
            this.Controls.Add(this.rbMouse);
            this.Name = "GenomeDialog";
            this.Text = "Pick Genome";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.RadioButton rbMouse;
		private System.Windows.Forms.RadioButton rbHuman;
		private System.Windows.Forms.Button button1;
        private System.Windows.Forms.RadioButton rbChicken;
        private System.Windows.Forms.RadioButton rbUCSC;
        private System.Windows.Forms.RadioButton rbVEGA;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton rbOldAnnot;
        private System.Windows.Forms.RadioButton rbENSE;
	}
}