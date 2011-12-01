namespace SilverBullet
{
    partial class BarcodeSetDialog
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
            this.radioButtonV1 = new System.Windows.Forms.RadioButton();
            this.radioButtonV2 = new System.Windows.Forms.RadioButton();
            this.button1 = new System.Windows.Forms.Button();
            this.radioButtonV4rnd = new System.Windows.Forms.RadioButton();
            this.radioButtonNobarcodes = new System.Windows.Forms.RadioButton();
            this.radioButtonV4 = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(91, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(146, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Select the BarcodeSet to use";
            // 
            // radioButtonV1
            // 
            this.radioButtonV1.AutoSize = true;
            this.radioButtonV1.Location = new System.Drawing.Point(46, 73);
            this.radioButtonV1.Name = "radioButtonV1";
            this.radioButtonV1.Size = new System.Drawing.Size(72, 17);
            this.radioButtonV1.TabIndex = 1;
            this.radioButtonV1.Text = "v1 (5-mer)";
            this.radioButtonV1.UseVisualStyleBackColor = true;
            // 
            // radioButtonV2
            // 
            this.radioButtonV2.AutoSize = true;
            this.radioButtonV2.Checked = true;
            this.radioButtonV2.Location = new System.Drawing.Point(142, 73);
            this.radioButtonV2.Name = "radioButtonV2";
            this.radioButtonV2.Size = new System.Drawing.Size(72, 17);
            this.radioButtonV2.TabIndex = 2;
            this.radioButtonV2.TabStop = true;
            this.radioButtonV2.Text = "v2 (6-mer)";
            this.radioButtonV2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(128, 139);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.OKButton_Click);
            // 
            // radioButtonV4rnd
            // 
            this.radioButtonV4rnd.AutoSize = true;
            this.radioButtonV4rnd.Location = new System.Drawing.Point(46, 96);
            this.radioButtonV4rnd.Name = "radioButtonV4rnd";
            this.radioButtonV4rnd.Size = new System.Drawing.Size(130, 17);
            this.radioButtonV4rnd.TabIndex = 4;
            this.radioButtonV4rnd.Text = "v4rnd (6-mer/48/random tag)";
            this.radioButtonV4rnd.UseVisualStyleBackColor = true;
            // 
            // radioButtonNobarcodes
            // 
            this.radioButtonNobarcodes.AutoSize = true;
            this.radioButtonNobarcodes.Location = new System.Drawing.Point(236, 73);
            this.radioButtonNobarcodes.Name = "radioButtonNobarcodes";
            this.radioButtonNobarcodes.Size = new System.Drawing.Size(49, 17);
            this.radioButtonNobarcodes.TabIndex = 5;
            this.radioButtonNobarcodes.Text = "none";
            this.radioButtonNobarcodes.UseVisualStyleBackColor = true;
            // 
            // radioButtonV4
            // 
            this.radioButtonV4.AutoSize = true;
            this.radioButtonV4.Location = new System.Drawing.Point(46, 116);
            this.radioButtonV4.Name = "radioButtonV4";
            this.radioButtonV4.Size = new System.Drawing.Size(89, 17);
            this.radioButtonV4.TabIndex = 6;
            this.radioButtonV4.Text = "v4 (6-mer/48)";
            this.radioButtonV4.UseVisualStyleBackColor = true;
            // 
            // BarcodeSetDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(339, 193);
            this.Controls.Add(this.radioButtonV4);
            this.Controls.Add(this.radioButtonNobarcodes);
            this.Controls.Add(this.radioButtonV4rnd);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.radioButtonV2);
            this.Controls.Add(this.radioButtonV1);
            this.Controls.Add(this.label1);
            this.Name = "BarcodeSetDialog";
            this.Text = "BarcodeSet";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioButtonV1;
        private System.Windows.Forms.RadioButton radioButtonV2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.RadioButton radioButtonV4rnd;
        private System.Windows.Forms.RadioButton radioButtonNobarcodes;
        private System.Windows.Forms.RadioButton radioButtonV4;
    }
}