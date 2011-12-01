namespace Junction_labels_simulator
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
            this.LoadGenome = new System.Windows.Forms.Button();
            this.num_molecule_TXB = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.mol_length_TXB = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.tsp_probablt_txt = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.Discardtxt = new System.Windows.Forms.TextBox();
            this.readLenTxt = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ReadmTxt = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.CompErrorbutton = new System.Windows.Forms.Button();
            this.UnConnButton = new System.Windows.Forms.Button();
            this.ButtonErrorCorr = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.maxreadtxt = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LoadGenome
            // 
            this.LoadGenome.Location = new System.Drawing.Point(74, 280);
            this.LoadGenome.Name = "LoadGenome";
            this.LoadGenome.Size = new System.Drawing.Size(243, 41);
            this.LoadGenome.TabIndex = 0;
            this.LoadGenome.Text = "Select a genome or Chromosome";
            this.LoadGenome.UseVisualStyleBackColor = true;
            this.LoadGenome.Click += new System.EventHandler(this.LoadGenome_Click);
            // 
            // num_molecule_TXB
            // 
            this.num_molecule_TXB.Location = new System.Drawing.Point(277, 25);
            this.num_molecule_TXB.Name = "num_molecule_TXB";
            this.num_molecule_TXB.Size = new System.Drawing.Size(41, 20);
            this.num_molecule_TXB.TabIndex = 1;
            this.num_molecule_TXB.Text = "400";
            this.num_molecule_TXB.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(73, 32);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Number of molecules";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(73, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(145, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Length of each molecule (kb)";
            // 
            // mol_length_TXB
            // 
            this.mol_length_TXB.Location = new System.Drawing.Point(277, 62);
            this.mol_length_TXB.Name = "mol_length_TXB";
            this.mol_length_TXB.Size = new System.Drawing.Size(40, 20);
            this.mol_length_TXB.TabIndex = 4;
            this.mol_length_TXB.Text = "100";
            this.mol_length_TXB.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 348);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(185, 23);
            this.button1.TabIndex = 5;
            this.button1.Text = "Fasta file generator";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // tsp_probablt_txt
            // 
            this.tsp_probablt_txt.Location = new System.Drawing.Point(277, 95);
            this.tsp_probablt_txt.Name = "tsp_probablt_txt";
            this.tsp_probablt_txt.Size = new System.Drawing.Size(41, 20);
            this.tsp_probablt_txt.TabIndex = 6;
            this.tsp_probablt_txt.Text = "200";
            this.tsp_probablt_txt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(73, 98);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(204, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Insert transposon with probability         =1/";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(73, 144);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(128, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Discard % of the fragment";
            // 
            // Discardtxt
            // 
            this.Discardtxt.Location = new System.Drawing.Point(275, 137);
            this.Discardtxt.Name = "Discardtxt";
            this.Discardtxt.Size = new System.Drawing.Size(42, 20);
            this.Discardtxt.TabIndex = 8;
            this.Discardtxt.Text = "5";
            this.Discardtxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // readLenTxt
            // 
            this.readLenTxt.Enabled = false;
            this.readLenTxt.Location = new System.Drawing.Point(275, 175);
            this.readLenTxt.Name = "readLenTxt";
            this.readLenTxt.Size = new System.Drawing.Size(43, 20);
            this.readLenTxt.TabIndex = 10;
            this.readLenTxt.Text = "100";
            this.readLenTxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(73, 182);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(92, 13);
            this.label5.TabIndex = 11;
            this.label5.Text = "Read lenght ( bp )";
            // 
            // ReadmTxt
            // 
            this.ReadmTxt.Location = new System.Drawing.Point(275, 216);
            this.ReadmTxt.Name = "ReadmTxt";
            this.ReadmTxt.Size = new System.Drawing.Size(43, 20);
            this.ReadmTxt.TabIndex = 12;
            this.ReadmTxt.Text = "400";
            this.ReadmTxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(73, 219);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(80, 13);
            this.label6.TabIndex = 13;
            this.label6.Text = "Reads in million";
            // 
            // CompErrorbutton
            // 
            this.CompErrorbutton.Location = new System.Drawing.Point(213, 387);
            this.CompErrorbutton.Name = "CompErrorbutton";
            this.CompErrorbutton.Size = new System.Drawing.Size(150, 23);
            this.CompErrorbutton.TabIndex = 14;
            this.CompErrorbutton.Text = "Compair Eror correction";
            this.CompErrorbutton.UseVisualStyleBackColor = true;
            this.CompErrorbutton.Click += new System.EventHandler(this.CompErrorbutton_Click);
            // 
            // UnConnButton
            // 
            this.UnConnButton.Location = new System.Drawing.Point(213, 348);
            this.UnConnButton.Name = "UnConnButton";
            this.UnConnButton.Size = new System.Drawing.Size(150, 23);
            this.UnConnButton.TabIndex = 15;
            this.UnConnButton.Text = "Unique connections";
            this.UnConnButton.UseVisualStyleBackColor = true;
            this.UnConnButton.Click += new System.EventHandler(this.UnConnButton_Click);
            // 
            // ButtonErrorCorr
            // 
            this.ButtonErrorCorr.Location = new System.Drawing.Point(13, 387);
            this.ButtonErrorCorr.Name = "ButtonErrorCorr";
            this.ButtonErrorCorr.Size = new System.Drawing.Size(184, 23);
            this.ButtonErrorCorr.TabIndex = 16;
            this.ButtonErrorCorr.Text = "Error Correction";
            this.ButtonErrorCorr.UseVisualStyleBackColor = true;
            this.ButtonErrorCorr.Click += new System.EventHandler(this.ButtonErrorCorr_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(13, 419);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(184, 23);
            this.button2.TabIndex = 17;
            this.button2.Text = "Record Count in FQ file";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // maxreadtxt
            // 
            this.maxreadtxt.Location = new System.Drawing.Point(275, 254);
            this.maxreadtxt.Name = "maxreadtxt";
            this.maxreadtxt.Size = new System.Drawing.Size(43, 20);
            this.maxreadtxt.TabIndex = 18;
            this.maxreadtxt.Text = "10";
            this.maxreadtxt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(73, 254);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(154, 13);
            this.label7.TabIndex = 19;
            this.label7.Text = "Max no of reads for a fragment ";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(571, 454);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.maxreadtxt);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.ButtonErrorCorr);
            this.Controls.Add(this.UnConnButton);
            this.Controls.Add(this.CompErrorbutton);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.ReadmTxt);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.readLenTxt);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.Discardtxt);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tsp_probablt_txt);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.mol_length_TXB);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.num_molecule_TXB);
            this.Controls.Add(this.LoadGenome);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button LoadGenome;
        private System.Windows.Forms.TextBox num_molecule_TXB;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox mol_length_TXB;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox tsp_probablt_txt;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox Discardtxt;
        private System.Windows.Forms.TextBox readLenTxt;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox ReadmTxt;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button CompErrorbutton;
        private System.Windows.Forms.Button UnConnButton;
        private System.Windows.Forms.Button ButtonErrorCorr;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TextBox maxreadtxt;
        private System.Windows.Forms.Label label7;
    }
}

