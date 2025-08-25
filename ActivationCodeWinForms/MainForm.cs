using System.Security.Cryptography;
using System.Text;

namespace ActivationCodeWinForms
{
    public partial class MainForm : Form
    {
        // 与主程序保持一致的密钥（必须相同才能生成有效激活码）
        private const string SecretKey = "ActivationCodeWinForms"; // 重要：需与主程序中的密钥完全一致

        public MainForm()
        {
            InitializeComponent();
            // 初始化界面默认值
            dtpDate.Value = DateTime.Now;
            txtMachineCode.Text = GenerateSampleMachineCode();
            this.Text = "激活码生成工具";
        }

        /// <summary>
        /// 生成示例机器码（用于测试）
        /// </summary>
        private string GenerateSampleMachineCode()
        {
            // 模拟主程序生成机器码的逻辑
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("SampleCPU111" + "SampleBoard222"));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
            }
        }

        /// <summary>
        /// 生成激活码（与主程序算法完全一致）
        /// </summary>
        private string GenerateActivationCode(string machineCode, DateTime date)
        {
            string timestamp = date.ToString("yyyyMMdd");
            using (SHA256 sha256 = SHA256.Create())
            {
                // 与主程序保持完全一致的哈希计算方式
                byte[] hashBytes = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(machineCode + timestamp + SecretKey));
                // 取前24位作为激活码
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 24);
            }
        }

        /// <summary>
        /// 生成激活码按钮点击事件
        /// </summary>
        private void btnGenerate_Click(object sender, EventArgs e)
        {
            string machineCode = txtMachineCode.Text.Trim();

            // 验证输入
            if (string.IsNullOrEmpty(machineCode))
            {
                MessageBox.Show("请输入机器码", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMachineCode.Focus();
                return;
            }

            if (machineCode.Length != 16)
            {
                MessageBox.Show("机器码格式不正确（应为16位字符）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMachineCode.Focus();
                return;
            }

            try
            {
                // 生成激活码
                string activationCode = GenerateActivationCode(machineCode, dtpDate.Value);
                txtActivationCode.Text = activationCode;
                MessageBox.Show("激活码生成成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 复制激活码按钮点击事件
        /// </summary>
        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtActivationCode.Text))
            {
                Clipboard.SetText(txtActivationCode.Text);
                MessageBox.Show("激活码已复制到剪贴板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("没有可复制的激活码，请先生成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}