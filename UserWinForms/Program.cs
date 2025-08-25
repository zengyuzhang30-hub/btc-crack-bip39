using UserWinForms.Services;

namespace UserWinForms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 检查激活状态
            var activationService = new AppActivationService();
            if (activationService.IsActivated())
            {
                // 已激活，直接进入主界面
                Application.Run(new MainForm());
            }
            else
            {
                // 未激活，显示激活界面
                var activationForm = new ActivationForm();
                if (activationForm.ShowDialog() == DialogResult.OK)
                {
                    // 激活成功或试用，进入主界面
                    Application.Run(new MainForm());
                }
                // 否则退出应用
            }

            //ApplicationConfiguration.Initialize();
            //Application.Run(new ActivationForm());
        }
    }
}