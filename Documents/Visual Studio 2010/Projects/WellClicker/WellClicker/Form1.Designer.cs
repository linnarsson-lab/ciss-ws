namespace WellClicker
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
            this.label1 = new System.Windows.Forms.Label();
            this.buttonSelectWellFile = new System.Windows.Forms.Button();
            this.buttonClickWells = new System.Windows.Forms.Button();
            this.linkSelectedWellFile = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(372, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "First select the well list, then Click! to select these wells.";
            // 
            // buttonSelectWellFile
            // 
            this.buttonSelectWellFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSelectWellFile.Location = new System.Drawing.Point(32, 49);
            this.buttonSelectWellFile.Name = "buttonSelectWellFile";
            this.buttonSelectWellFile.Size = new System.Drawing.Size(136, 23);
            this.buttonSelectWellFile.TabIndex = 1;
            this.buttonSelectWellFile.Text = "Select Well File";
            this.buttonSelectWellFile.UseVisualStyleBackColor = true;
            this.buttonSelectWellFile.Click += new System.EventHandler(this.buttonSelectWellFile_Click);
            // 
            // buttonClickWells
            // 
            this.buttonClickWells.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonClickWells.Location = new System.Drawing.Point(232, 49);
            this.buttonClickWells.Name = "buttonClickWells";
            this.buttonClickWells.Size = new System.Drawing.Size(102, 23);
            this.buttonClickWells.TabIndex = 2;
            this.buttonClickWells.Text = "Click Wells!";
            this.buttonClickWells.UseVisualStyleBackColor = true;
            this.buttonClickWells.Click += new System.EventHandler(this.buttonClickWells_Click);
            // 
            // linkSelectedWellFile
            // 
            this.linkSelectedWellFile.AutoSize = true;
            this.linkSelectedWellFile.Location = new System.Drawing.Point(29, 85);
            this.linkSelectedWellFile.Name = "linkSelectedWellFile";
            this.linkSelectedWellFile.Size = new System.Drawing.Size(84, 13);
            this.linkSelectedWellFile.TabIndex = 3;
            this.linkSelectedWellFile.TabStop = true;
            this.linkSelectedWellFile.Text = "(no file selected)";
            this.linkSelectedWellFile.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.linkSelectedWellFile.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkSelectedWellFile_LinkClicked);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(394, 111);
            this.Controls.Add(this.linkSelectedWellFile);
            this.Controls.Add(this.buttonClickWells);
            this.Controls.Add(this.buttonSelectWellFile);
            this.Controls.Add(this.label1);
            this.Name = "Form1";
            this.Text = "WellClicker";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonSelectWellFile;
        private System.Windows.Forms.Button buttonClickWells;
        private System.Windows.Forms.LinkLabel linkSelectedWellFile;
    }
}

