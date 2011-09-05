namespace IndranilsProject
{
    partial class Form1
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
            this.TmCalculation = new System.Windows.Forms.Button();
            this.UnSelector = new System.Windows.Forms.Button();
            this.RevCom = new System.Windows.Forms.Button();
            this.mseIpoly = new System.Windows.Forms.Button();
            this.ReadMe = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.CloseReadme = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // TmCalculation
            // 
            this.TmCalculation.Enabled = false;
            this.TmCalculation.Location = new System.Drawing.Point(39, 144);
            this.TmCalculation.Name = "TmCalculation";
            this.TmCalculation.Size = new System.Drawing.Size(189, 37);
            this.TmCalculation.TabIndex = 0;
            this.TmCalculation.Text = "TmCalculation";
            this.TmCalculation.UseVisualStyleBackColor = true;
            this.TmCalculation.Click += new System.EventHandler(this.TmCalculation_Click);
            // 
            // UnSelector
            // 
            this.UnSelector.Enabled = false;
            this.UnSelector.Location = new System.Drawing.Point(39, 209);
            this.UnSelector.Name = "UnSelector";
            this.UnSelector.Size = new System.Drawing.Size(189, 34);
            this.UnSelector.TabIndex = 1;
            this.UnSelector.Text = "Select Unique Selectors";
            this.UnSelector.UseVisualStyleBackColor = true;
            this.UnSelector.Click += new System.EventHandler(this.UnSelector_Click);
            // 
            // RevCom
            // 
            this.RevCom.Enabled = false;
            this.RevCom.Location = new System.Drawing.Point(39, 283);
            this.RevCom.Name = "RevCom";
            this.RevCom.Size = new System.Drawing.Size(189, 34);
            this.RevCom.TabIndex = 2;
            this.RevCom.Text = "Reverse Complement";
            this.RevCom.UseVisualStyleBackColor = true;
            this.RevCom.Click += new System.EventHandler(this.RevCom_Click);
            // 
            // mseIpoly
            // 
            this.mseIpoly.Enabled = false;
            this.mseIpoly.Location = new System.Drawing.Point(39, 86);
            this.mseIpoly.Name = "mseIpoly";
            this.mseIpoly.Size = new System.Drawing.Size(189, 36);
            this.mseIpoly.TabIndex = 3;
            this.mseIpoly.Text = " MseI poly A/C/G/T";
            this.mseIpoly.UseVisualStyleBackColor = true;
            this.mseIpoly.Click += new System.EventHandler(this.mseIpoly_Click);
            // 
            // ReadMe
            // 
            this.ReadMe.Location = new System.Drawing.Point(39, 27);
            this.ReadMe.Name = "ReadMe";
            this.ReadMe.Size = new System.Drawing.Size(191, 38);
            this.ReadMe.TabIndex = 4;
            this.ReadMe.Text = "Read Me first";
            this.ReadMe.UseVisualStyleBackColor = true;
            this.ReadMe.Click += new System.EventHandler(this.ReadMe_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 96);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(16, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "1.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(18, 142);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(19, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "2. ";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 218);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(19, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "3. ";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(18, 292);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(19, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "4. ";
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Right;
            this.richTextBox1.Location = new System.Drawing.Point(293, 0);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(794, 414);
            this.richTextBox1.TabIndex = 9;
            this.richTextBox1.Text = "";
            this.richTextBox1.Visible = false;
            // 
            // CloseReadme
            // 
            this.CloseReadme.Location = new System.Drawing.Point(39, 342);
            this.CloseReadme.Name = "CloseReadme";
            this.CloseReadme.Size = new System.Drawing.Size(189, 38);
            this.CloseReadme.TabIndex = 10;
            this.CloseReadme.Text = "Close Read Me ";
            this.CloseReadme.UseVisualStyleBackColor = true;
            this.CloseReadme.Visible = false;
            this.CloseReadme.Click += new System.EventHandler(this.CloseReadme_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1087, 414);
            this.Controls.Add(this.CloseReadme);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ReadMe);
            this.Controls.Add(this.mseIpoly);
            this.Controls.Add(this.RevCom);
            this.Controls.Add(this.UnSelector);
            this.Controls.Add(this.TmCalculation);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button TmCalculation;
        private System.Windows.Forms.Button UnSelector;
        private System.Windows.Forms.Button RevCom;
        private System.Windows.Forms.Button mseIpoly;
        private System.Windows.Forms.Button ReadMe;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button CloseReadme;
    }
}

