namespace SilverBullet
{
    partial class TextDialog
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
            this.textEnterHeader = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.nameTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // textEnterHeader
            // 
            this.textEnterHeader.AutoSize = true;
            this.textEnterHeader.Location = new System.Drawing.Point(80, 29);
            this.textEnterHeader.Name = "textEnterHeader";
            this.textEnterHeader.Size = new System.Drawing.Size(108, 13);
            this.textEnterHeader.TabIndex = 0;
            this.textEnterHeader.Text = "Enter a Project Name";
            this.textEnterHeader.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button1.Location = new System.Drawing.Point(93, 108);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // nameTextBox
            // 
            this.nameTextBox.Location = new System.Drawing.Point(77, 70);
            this.nameTextBox.Name = "nameTextBox";
            this.nameTextBox.Size = new System.Drawing.Size(111, 20);
            this.nameTextBox.TabIndex = 2;
            // 
            // TextDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(269, 162);
            this.Controls.Add(this.nameTextBox);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.textEnterHeader);
            this.Name = "TextDialog";
            this.Text = "TextDialog";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label textEnterHeader;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox nameTextBox;
    }
}