namespace UserWinForms
{
    partial class ActivationForm
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
        private void InitializeComponent()
        {
            this.txtMachineCode = new System.Windows.Forms.TextBox();
            this.lblMachineCode = new System.Windows.Forms.Label();
            this.txtActivationCode = new System.Windows.Forms.TextBox();
            this.lblActivationCode = new System.Windows.Forms.Label();
            this.btnActivate = new System.Windows.Forms.Button();
            this.btnFreeTrial = new System.Windows.Forms.Button();
            this.btnCustomerService = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // txtMachineCode
            this.txtMachineCode.Location = new System.Drawing.Point(100, 20);
            this.txtMachineCode.Name = "txtMachineCode";
            this.txtMachineCode.ReadOnly = true;
            this.txtMachineCode.Size = new System.Drawing.Size(300, 23);
            this.txtMachineCode.TabIndex = 0;

            // lblMachineCode
            this.lblMachineCode.AutoSize = true;
            this.lblMachineCode.Location = new System.Drawing.Point(20, 20);
            this.lblMachineCode.Name = "lblMachineCode";
            this.lblMachineCode.Size = new System.Drawing.Size(59, 17);
            this.lblMachineCode.TabIndex = 1;
            this.lblMachineCode.Text = "机器码：";

            // txtActivationCode
            this.txtActivationCode.Location = new System.Drawing.Point(100, 60);
            this.txtActivationCode.Name = "txtActivationCode";
            this.txtActivationCode.Size = new System.Drawing.Size(300, 23);
            this.txtActivationCode.TabIndex = 2;

            // lblActivationCode
            this.lblActivationCode.AutoSize = true;
            this.lblActivationCode.Location = new System.Drawing.Point(20, 60);
            this.lblActivationCode.Name = "lblActivationCode";
            this.lblActivationCode.Size = new System.Drawing.Size(59, 17);
            this.lblActivationCode.TabIndex = 3;
            this.lblActivationCode.Text = "激活码：";

            // btnActivate
            this.btnActivate.Location = new System.Drawing.Point(100, 100);
            this.btnActivate.Name = "btnActivate";
            this.btnActivate.Size = new System.Drawing.Size(80, 30);
            this.btnActivate.TabIndex = 4;
            this.btnActivate.Text = "激活";
            this.btnActivate.UseVisualStyleBackColor = true;
            this.btnActivate.Click += new System.EventHandler(this.btnActivate_Click);

            // btnFreeTrial
            this.btnFreeTrial.Location = new System.Drawing.Point(200, 100);
            this.btnFreeTrial.Name = "btnFreeTrial";
            this.btnFreeTrial.Size = new System.Drawing.Size(100, 30);
            this.btnFreeTrial.TabIndex = 5;
            this.btnFreeTrial.UseVisualStyleBackColor = true;
            this.btnFreeTrial.Click += new System.EventHandler(this.btnFreeTrial_Click);

            // btnCustomerService
            this.btnCustomerService.Location = new System.Drawing.Point(320, 100);
            this.btnCustomerService.Name = "btnCustomerService";
            this.btnCustomerService.Size = new System.Drawing.Size(80, 30);
            this.btnCustomerService.TabIndex = 6;
            this.btnCustomerService.Text = "联系客服";
            this.btnCustomerService.UseVisualStyleBackColor = true;
            this.btnCustomerService.Click += new System.EventHandler(this.btnCustomerService_Click);

            // ActivationForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(450, 150);
            this.Controls.Add(this.btnCustomerService);
            this.Controls.Add(this.btnFreeTrial);
            this.Controls.Add(this.btnActivate);
            this.Controls.Add(this.lblActivationCode);
            this.Controls.Add(this.txtActivationCode);
            this.Controls.Add(this.lblMachineCode);
            this.Controls.Add(this.txtMachineCode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "ActivationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Label lblMachineCode;
        private Label lblActivationCode;
        private Button btnActivate;
        private Button btnCustomerService;

        #endregion
    }
}
