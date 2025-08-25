namespace ActivationCodeWinForms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        //private void InitializeComponent()
        //{
        //    SuspendLayout();
        //    // 
        //    // MainForm
        //    // 
        //    AutoScaleDimensions = new SizeF(7F, 17F);
        //    AutoScaleMode = AutoScaleMode.Font;
        //    ClientSize = new Size(800, 450);
        //    Name = "MainForm";
        //    Text = "Form1";
        //    ResumeLayout(false);
        //}
        private void InitializeComponent()
        {
            this.txtMachineCode = new System.Windows.Forms.TextBox();
            this.lblMachineCode = new System.Windows.Forms.Label();
            this.txtActivationCode = new System.Windows.Forms.TextBox();
            this.lblActivationCode = new System.Windows.Forms.Label();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.dtpDate = new System.Windows.Forms.DateTimePicker();
            this.lblDate = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // txtMachineCode
            this.txtMachineCode.Location = new System.Drawing.Point(12, 35);
            this.txtMachineCode.Name = "txtMachineCode";
            this.txtMachineCode.Size = new System.Drawing.Size(450, 23);
            this.txtMachineCode.TabIndex = 0;

            // lblMachineCode
            this.lblMachineCode.AutoSize = true;
            this.lblMachineCode.Location = new System.Drawing.Point(12, 17);
            this.lblMachineCode.Name = "lblMachineCode";
            this.lblMachineCode.Size = new System.Drawing.Size(59, 17);
            this.lblMachineCode.TabIndex = 1;
            this.lblMachineCode.Text = "机器码：";

            // txtActivationCode
            this.txtActivationCode.Location = new System.Drawing.Point(12, 125);
            this.txtActivationCode.Name = "txtActivationCode";
            this.txtActivationCode.ReadOnly = true;
            this.txtActivationCode.Size = new System.Drawing.Size(450, 23);
            this.txtActivationCode.TabIndex = 2;

            // lblActivationCode
            this.lblActivationCode.AutoSize = true;
            this.lblActivationCode.Location = new System.Drawing.Point(12, 107);
            this.lblActivationCode.Name = "lblActivationCode";
            this.lblActivationCode.Size = new System.Drawing.Size(59, 17);
            this.lblActivationCode.TabIndex = 3;
            this.lblActivationCode.Text = "激活码：";

            // btnGenerate
            this.btnGenerate.Location = new System.Drawing.Point(12, 175);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(100, 30);
            this.btnGenerate.TabIndex = 4;
            this.btnGenerate.Text = "生成激活码";
            this.btnGenerate.UseVisualStyleBackColor = true;
            this.btnGenerate.Click += new System.EventHandler(this.btnGenerate_Click);

            // btnCopy
            this.btnCopy.Location = new System.Drawing.Point(362, 175);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(100, 30);
            this.btnCopy.TabIndex = 5;
            this.btnCopy.Text = "复制激活码";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);

            // dtpDate
            this.dtpDate.Location = new System.Drawing.Point(12, 80);
            this.dtpDate.Name = "dtpDate";
            this.dtpDate.Size = new System.Drawing.Size(200, 23);
            this.dtpDate.TabIndex = 6;

            // lblDate
            this.lblDate.AutoSize = true;
            this.lblDate.Location = new System.Drawing.Point(12, 62);
            this.lblDate.Name = "lblDate";
            this.lblDate.Size = new System.Drawing.Size(101, 17);
            this.lblDate.TabIndex = 7;
            this.lblDate.Text = "激活码有效日期：";

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(474, 221);
            this.Controls.Add(this.lblDate);
            this.Controls.Add(this.dtpDate);
            this.Controls.Add(this.btnCopy);
            this.Controls.Add(this.btnGenerate);
            this.Controls.Add(this.lblActivationCode);
            this.Controls.Add(this.txtActivationCode);
            this.Controls.Add(this.lblMachineCode);
            this.Controls.Add(this.txtMachineCode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        private TextBox txtMachineCode;
        private Label lblMachineCode;
        private TextBox txtActivationCode;
        private Label lblActivationCode;
        private Button btnGenerate;
        private Button btnCopy;
        private DateTimePicker dtpDate;
        private Label lblDate;
        #endregion
    }
}
