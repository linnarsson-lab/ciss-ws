namespace C1SeqPlateLoader
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
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.listBoxSelect = new System.Windows.Forms.ListBox();
            this.button1 = new System.Windows.Forms.Button();
            this.consoleBox1 = new Linnarsson.Utilities.ConsoleBox();
            this.SuspendLayout();
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBox1.Location = new System.Drawing.Point(36, 283);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(255, 20);
            this.checkBox1.TabIndex = 0;
            this.checkBox1.Text = "Show already loaded plates for reload";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(36, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(209, 18);
            this.label1.TabIndex = 1;
            this.label1.Text = "Select chip or mixplate to load:";
            // 
            // listBoxSelect
            // 
            this.listBoxSelect.FormattingEnabled = true;
            this.listBoxSelect.Location = new System.Drawing.Point(39, 55);
            this.listBoxSelect.Name = "listBoxSelect";
            this.listBoxSelect.Size = new System.Drawing.Size(243, 212);
            this.listBoxSelect.TabIndex = 2;
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(113, 315);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "Load";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // consoleBox1
            // 
            this.consoleBox1.Location = new System.Drawing.Point(11, 347);
            this.consoleBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.consoleBox1.Name = "consoleBox1";
            this.consoleBox1.Size = new System.Drawing.Size(336, 170);
            this.consoleBox1.TabIndex = 5;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(358, 528);
            this.Controls.Add(this.consoleBox1);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.listBoxSelect);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBox1);
            this.Name = "Form1";
            this.Text = "C1 Seq Plate Loader";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox listBoxSelect;
        private System.Windows.Forms.Button button1;
        private Linnarsson.Utilities.ConsoleBox consoleBox1;
    }
}

