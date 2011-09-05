namespace IndranilsProject
{
    partial class P53MseI
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
            this.MseI = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // MseI
            // 
            this.MseI.Location = new System.Drawing.Point(74, 65);
            this.MseI.Name = "MseI";
            this.MseI.Size = new System.Drawing.Size(170, 34);
            this.MseI.TabIndex = 0;
            this.MseI.Text = "MseI ";
            this.MseI.UseVisualStyleBackColor = true;
            this.MseI.Click += new System.EventHandler(this.button1_Click);
            // 
            // P53MseI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(429, 366);
            this.Controls.Add(this.MseI);
            this.Name = "P53MseI";
            this.Text = "P53MseI";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button MseI;
    }
}