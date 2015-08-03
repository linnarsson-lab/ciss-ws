namespace WellClicker
{
    partial class WellFileDisplay
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
            this.buttonClose = new System.Windows.Forms.Button();
            this.textBoxWellFile = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(143, 410);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(75, 23);
            this.buttonClose.TabIndex = 0;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // textBoxWellFile
            // 
            this.textBoxWellFile.Location = new System.Drawing.Point(62, 24);
            this.textBoxWellFile.Multiline = true;
            this.textBoxWellFile.Name = "textBoxWellFile";
            this.textBoxWellFile.ReadOnly = true;
            this.textBoxWellFile.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxWellFile.Size = new System.Drawing.Size(240, 361);
            this.textBoxWellFile.TabIndex = 1;
            // 
            // WellFileDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(367, 458);
            this.Controls.Add(this.textBoxWellFile);
            this.Controls.Add(this.buttonClose);
            this.Name = "WellFileDisplay";
            this.Text = "WellFileDisplay";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.TextBox textBoxWellFile;
    }
}