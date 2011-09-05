namespace SilverBullet
{
    partial class GeneVariantsDialog
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
            this.radioButtonMainGeneVariants = new System.Windows.Forms.RadioButton();
            this.radioButtonAllGeneVariants = new System.Windows.Forms.RadioButton();
            this.buttonGeneVariantsOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // radioButtonMainGeneVariants
            // 
            this.radioButtonMainGeneVariants.AutoSize = true;
            this.radioButtonMainGeneVariants.Location = new System.Drawing.Point(59, 31);
            this.radioButtonMainGeneVariants.Name = "radioButtonMainGeneVariants";
            this.radioButtonMainGeneVariants.Size = new System.Drawing.Size(176, 17);
            this.radioButtonMainGeneVariants.TabIndex = 0;
            this.radioButtonMainGeneVariants.TabStop = true;
            this.radioButtonMainGeneVariants.Text = "Analyze main gene variants only";
            this.radioButtonMainGeneVariants.UseVisualStyleBackColor = true;
            // 
            // radioButtonAllGeneVariants
            // 
            this.radioButtonAllGeneVariants.AutoSize = true;
            this.radioButtonAllGeneVariants.Location = new System.Drawing.Point(71, 76);
            this.radioButtonAllGeneVariants.Name = "radioButtonAllGeneVariants";
            this.radioButtonAllGeneVariants.Size = new System.Drawing.Size(142, 17);
            this.radioButtonAllGeneVariants.TabIndex = 1;
            this.radioButtonAllGeneVariants.TabStop = true;
            this.radioButtonAllGeneVariants.Text = "Analyze all gene variants";
            this.radioButtonAllGeneVariants.UseVisualStyleBackColor = true;
            // 
            // buttonGeneVariantsOK
            // 
            this.buttonGeneVariantsOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonGeneVariantsOK.Location = new System.Drawing.Point(106, 115);
            this.buttonGeneVariantsOK.Name = "buttonGeneVariantsOK";
            this.buttonGeneVariantsOK.Size = new System.Drawing.Size(75, 23);
            this.buttonGeneVariantsOK.TabIndex = 2;
            this.buttonGeneVariantsOK.Text = "OK";
            this.buttonGeneVariantsOK.UseVisualStyleBackColor = true;
            this.buttonGeneVariantsOK.Click += new System.EventHandler(this.buttonGeneVariantsOK_Click);
            // 
            // GeneVariantsDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(292, 169);
            this.Controls.Add(this.buttonGeneVariantsOK);
            this.Controls.Add(this.radioButtonAllGeneVariants);
            this.Controls.Add(this.radioButtonMainGeneVariants);
            this.Name = "GeneVariantsDialog";
            this.Text = "Gene Variant Selection";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radioButtonMainGeneVariants;
        private System.Windows.Forms.RadioButton radioButtonAllGeneVariants;
        private System.Windows.Forms.Button buttonGeneVariantsOK;
    }
}