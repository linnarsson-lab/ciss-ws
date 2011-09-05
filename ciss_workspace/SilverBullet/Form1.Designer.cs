namespace SilverBullet
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
			if(disposing && (components != null))
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
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.pipelineIIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runBowtieToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.mapPhaseTwoannotationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.annotateFromWiggleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.mergeAndNormalizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rerunAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.qCAnalysisToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buildIndexToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.peekAtgzFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitByBarcodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.barcodeStatsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.buildSplicedExonsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateAnnotationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importFastaFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.synthesizeReadsFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.testToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mySQLConnectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.projectFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.consoleBox1 = new Linnarsson.Utilities.ConsoleBox();
            this.statusStrip1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripProgressBar1,
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 516);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(594, 25);
            this.statusStrip1.TabIndex = 8;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripProgressBar1
            // 
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new System.Drawing.Size(100, 19);
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 20);
            this.lblStatus.Text = "Ready";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pipelineIIToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.testToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(594, 24);
            this.menuStrip1.TabIndex = 9;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // pipelineIIToolStripMenuItem
            // 
            this.pipelineIIToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.extractToolStripMenuItem,
            this.runBowtieToolStripMenuItem,
            this.toolStripMenuItem1,
            this.mapPhaseTwoannotationsToolStripMenuItem,
            this.annotateFromWiggleToolStripMenuItem,
            this.toolStripMenuItem2,
            this.mergeAndNormalizeToolStripMenuItem,
            this.rerunAllToolStripMenuItem});
            this.pipelineIIToolStripMenuItem.Name = "pipelineIIToolStripMenuItem";
            this.pipelineIIToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.pipelineIIToolStripMenuItem.Text = "Pipeline";
            // 
            // extractToolStripMenuItem
            // 
            this.extractToolStripMenuItem.Name = "extractToolStripMenuItem";
            this.extractToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.extractToolStripMenuItem.Text = "Extract...";
            this.extractToolStripMenuItem.Click += new System.EventHandler(this.extractToolStripMenuItem_Click);
            // 
            // runBowtieToolStripMenuItem
            // 
            this.runBowtieToolStripMenuItem.Name = "runBowtieToolStripMenuItem";
            this.runBowtieToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.runBowtieToolStripMenuItem.Text = "Run Bowtie...";
            this.runBowtieToolStripMenuItem.Click += new System.EventHandler(this.runBowtieToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(253, 6);
            // 
            // mapPhaseTwoannotationsToolStripMenuItem
            // 
            this.mapPhaseTwoannotationsToolStripMenuItem.Name = "mapPhaseTwoannotationsToolStripMenuItem";
            this.mapPhaseTwoannotationsToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.mapPhaseTwoannotationsToolStripMenuItem.Text = "Annotate from bowtie mappings...";
            this.mapPhaseTwoannotationsToolStripMenuItem.Click += new System.EventHandler(this.annotateFromBowtieToolStripMenuItem_Click);
            // 
            // annotateFromWiggleToolStripMenuItem
            // 
            this.annotateFromWiggleToolStripMenuItem.Name = "annotateFromWiggleToolStripMenuItem";
            this.annotateFromWiggleToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.annotateFromWiggleToolStripMenuItem.Text = "Annotate from Wiggle...";
            this.annotateFromWiggleToolStripMenuItem.Click += new System.EventHandler(this.annotateFromWiggleToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(253, 6);
            // 
            // mergeAndNormalizeToolStripMenuItem
            // 
            this.mergeAndNormalizeToolStripMenuItem.Name = "mergeAndNormalizeToolStripMenuItem";
            this.mergeAndNormalizeToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.mergeAndNormalizeToolStripMenuItem.Text = "Merge and Normalize...";
            this.mergeAndNormalizeToolStripMenuItem.Click += new System.EventHandler(this.mergeAndNormalizeToolStripMenuItem_Click);
            // 
            // rerunAllToolStripMenuItem
            // 
            this.rerunAllToolStripMenuItem.Name = "rerunAllToolStripMenuItem";
            this.rerunAllToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.rerunAllToolStripMenuItem.Text = "Rerun all...";
            this.rerunAllToolStripMenuItem.Click += new System.EventHandler(this.rerunAllToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.qCAnalysisToolStripMenuItem,
            this.buildIndexToolStripMenuItem,
            this.peekAtgzFileToolStripMenuItem,
            this.splitByBarcodeToolStripMenuItem,
            this.barcodeStatsToolStripMenuItem,
            this.buildSplicedExonsToolStripMenuItem,
            this.updateAnnotationsToolStripMenuItem,
            this.importFastaFileToolStripMenuItem,
            this.synthesizeReadsFileToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // qCAnalysisToolStripMenuItem
            // 
            this.qCAnalysisToolStripMenuItem.Name = "qCAnalysisToolStripMenuItem";
            this.qCAnalysisToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.qCAnalysisToolStripMenuItem.Text = "QC Annotation";
            this.qCAnalysisToolStripMenuItem.Click += new System.EventHandler(this.qCAnalysisToolStripMenuItem_Click);
            // 
            // buildIndexToolStripMenuItem
            // 
            this.buildIndexToolStripMenuItem.Name = "buildIndexToolStripMenuItem";
            this.buildIndexToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.buildIndexToolStripMenuItem.Text = "Build annotation/bowtie indices...";
            this.buildIndexToolStripMenuItem.Click += new System.EventHandler(this.buildIndexToolStripMenuItem_Click);
            // 
            // peekAtgzFileToolStripMenuItem
            // 
            this.peekAtgzFileToolStripMenuItem.Name = "peekAtgzFileToolStripMenuItem";
            this.peekAtgzFileToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.peekAtgzFileToolStripMenuItem.Text = "Peek at .gz file";
            this.peekAtgzFileToolStripMenuItem.Click += new System.EventHandler(this.peekAtgzFileToolStripMenuItem_Click);
            // 
            // splitByBarcodeToolStripMenuItem
            // 
            this.splitByBarcodeToolStripMenuItem.Name = "splitByBarcodeToolStripMenuItem";
            this.splitByBarcodeToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.splitByBarcodeToolStripMenuItem.Text = "Split by barcode...";
            this.splitByBarcodeToolStripMenuItem.Click += new System.EventHandler(this.splitByBarcodeToolStripMenuItem_Click);
            // 
            // barcodeStatsToolStripMenuItem
            // 
            this.barcodeStatsToolStripMenuItem.Name = "barcodeStatsToolStripMenuItem";
            this.barcodeStatsToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.barcodeStatsToolStripMenuItem.Text = "Barcode stats...";
            this.barcodeStatsToolStripMenuItem.Click += new System.EventHandler(this.barcodeStatsToolStripMenuItem_Click);
            // 
            // buildSplicedExonsToolStripMenuItem
            // 
            this.buildSplicedExonsToolStripMenuItem.Name = "buildSplicedExonsToolStripMenuItem";
            this.buildSplicedExonsToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.buildSplicedExonsToolStripMenuItem.Text = "Build spliced exons...";
            this.buildSplicedExonsToolStripMenuItem.Click += new System.EventHandler(this.buildSplicedExonsToolStripMenuItem_Click);
            // 
            // updateAnnotationsToolStripMenuItem
            // 
            this.updateAnnotationsToolStripMenuItem.Name = "updateAnnotationsToolStripMenuItem";
            this.updateAnnotationsToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.updateAnnotationsToolStripMenuItem.Text = "Update annotations...";
            this.updateAnnotationsToolStripMenuItem.Click += new System.EventHandler(this.updateAnnotationsToolStripMenuItem_Click);
            // 
            // importFastaFileToolStripMenuItem
            // 
            this.importFastaFileToolStripMenuItem.Name = "importFastaFileToolStripMenuItem";
            this.importFastaFileToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.importFastaFileToolStripMenuItem.Text = "Import fasta file...";
            this.importFastaFileToolStripMenuItem.Click += new System.EventHandler(this.importFastaFileToolStripMenuItem_Click);
            // 
            // synthesizeReadsFileToolStripMenuItem
            // 
            this.synthesizeReadsFileToolStripMenuItem.Name = "synthesizeReadsFileToolStripMenuItem";
            this.synthesizeReadsFileToolStripMenuItem.Size = new System.Drawing.Size(252, 22);
            this.synthesizeReadsFileToolStripMenuItem.Text = "Synthesize reads file...";
            this.synthesizeReadsFileToolStripMenuItem.Click += new System.EventHandler(this.synthesizeReadsFileToolStripMenuItem_Click);
            // 
            // testToolStripMenuItem
            // 
            this.testToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mySQLConnectionToolStripMenuItem,
            this.projectFileToolStripMenuItem});
            this.testToolStripMenuItem.Name = "testToolStripMenuItem";
            this.testToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
            this.testToolStripMenuItem.Text = "Test";
            // 
            // consoleBox1
            // 
            this.consoleBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.consoleBox1.Location = new System.Drawing.Point(0, 24);
            this.consoleBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.consoleBox1.Name = "consoleBox1";
            this.consoleBox1.Size = new System.Drawing.Size(594, 492);
            this.consoleBox1.TabIndex = 10;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(594, 541);
            this.Controls.Add(this.consoleBox1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "SilverBullet";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel lblStatus;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private Linnarsson.Utilities.ConsoleBox consoleBox1;
		private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
		private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem qCAnalysisToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem buildIndexToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem pipelineIIToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem extractToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem mapPhaseTwoannotationsToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem annotateFromWiggleToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem peekAtgzFileToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
		private System.Windows.Forms.ToolStripMenuItem mergeAndNormalizeToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem splitByBarcodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem barcodeStatsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runBowtieToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem buildSplicedExonsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateAnnotationsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importFastaFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem synthesizeReadsFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rerunAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem testToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mySQLConnectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem projectFileToolStripMenuItem;
	}
}