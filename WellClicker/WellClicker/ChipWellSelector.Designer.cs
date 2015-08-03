namespace WellClicker
{
    partial class ChipWellSelector
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
            this.listBoxSelect = new System.Windows.Forms.ListBox();
            this.checkBox_ShowLoaded = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.button_ReadSelectedFile = new System.Windows.Forms.Button();
            this.button_FreeFileSelection = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listBoxSelect
            // 
            this.listBoxSelect.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxSelect.FormattingEnabled = true;
            this.listBoxSelect.ItemHeight = 16;
            this.listBoxSelect.Location = new System.Drawing.Point(12, 50);
            this.listBoxSelect.Name = "listBoxSelect";
            this.listBoxSelect.Size = new System.Drawing.Size(502, 196);
            this.listBoxSelect.TabIndex = 0;
            // 
            // checkBox_ShowLoaded
            // 
            this.checkBox_ShowLoaded.AutoSize = true;
            this.checkBox_ShowLoaded.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBox_ShowLoaded.Location = new System.Drawing.Point(12, 269);
            this.checkBox_ShowLoaded.Name = "checkBox_ShowLoaded";
            this.checkBox_ShowLoaded.Size = new System.Drawing.Size(237, 20);
            this.checkBox_ShowLoaded.TabIndex = 1;
            this.checkBox_ShowLoaded.Text = "Show also already loaded well files";
            this.checkBox_ShowLoaded.UseVisualStyleBackColor = true;
            this.checkBox_ShowLoaded.CheckedChanged += new System.EventHandler(this.checkBox_ShowLoaded_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(19, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(165, 16);
            this.label1.TabIndex = 2;
            this.label1.Text = "Select a well exclusion file:";
            // 
            // button_ReadSelectedFile
            // 
            this.button_ReadSelectedFile.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button_ReadSelectedFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button_ReadSelectedFile.Location = new System.Drawing.Point(12, 306);
            this.button_ReadSelectedFile.Name = "button_ReadSelectedFile";
            this.button_ReadSelectedFile.Size = new System.Drawing.Size(166, 23);
            this.button_ReadSelectedFile.TabIndex = 3;
            this.button_ReadSelectedFile.Text = "Read Selected File";
            this.button_ReadSelectedFile.UseVisualStyleBackColor = true;
            this.button_ReadSelectedFile.Click += new System.EventHandler(this.button_ReadSelectedFile_Click);
            // 
            // button_FreeFileSelection
            // 
            this.button_FreeFileSelection.Location = new System.Drawing.Point(337, 269);
            this.button_FreeFileSelection.Name = "button_FreeFileSelection";
            this.button_FreeFileSelection.Size = new System.Drawing.Size(109, 23);
            this.button_FreeFileSelection.TabIndex = 4;
            this.button_FreeFileSelection.Text = "Select Other File";
            this.button_FreeFileSelection.UseVisualStyleBackColor = true;
            this.button_FreeFileSelection.Click += new System.EventHandler(this.button_FreeFileSelection_Click);
            // 
            // ChipWellSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(536, 341);
            this.Controls.Add(this.button_FreeFileSelection);
            this.Controls.Add(this.button_ReadSelectedFile);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBox_ShowLoaded);
            this.Controls.Add(this.listBoxSelect);
            this.Name = "ChipWellSelector";
            this.Text = "ChipWellSelector";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxSelect;
        private System.Windows.Forms.CheckBox checkBox_ShowLoaded;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button_ReadSelectedFile;
        private System.Windows.Forms.Button button_FreeFileSelection;
    }
}