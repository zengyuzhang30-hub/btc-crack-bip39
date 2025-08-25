using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace UserWinForms.Services
{
    public class AppActivationService
    {
        // 与激活码生成工具保持一致的密钥
        private const string SecretKey = "YouAppActivationServicerSeCryptoMiningAppcretKey";

        // 检查是否已激活
        public bool IsActivated()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\CryptoMiningApp"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("IsActivated");
                        return value != null && Convert.ToBoolean(value);
                    }
                }
            }
            catch { }
            return false;
        }

        // 保存激活状态
        public void SaveActivationStatus(bool isActivated)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\CryptoMiningApp"))
            {
                key.SetValue("IsActivated", isActivated ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
            }
        }

        // 获取机器码（基于CPU和主板信息）
        public string GetMachineCode()
        {
            try
            {
                string cpuId = GetHardwareInfo("Win32_Processor", "ProcessorId");
                string boardId = GetHardwareInfo("Win32_BaseBoard", "SerialNumber");

                // 简单哈希生成机器码
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(cpuId + boardId));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
                }
            }
            catch
            {
                return Guid.NewGuid().ToString("N").Substring(0, 16);
            }
        }

        // 验证激活码
        public bool VerifyActivationCode(string machineCode, string activationCode)
        {
            // 自定义激活码算法：机器码 + 时间戳哈希
            try
            {
                // 尝试验证当前日期
                string currentTimestamp = DateTime.Now.ToString("yyyyMMdd");
                string expectedCurrent = GenerateActivationCode(machineCode, currentTimestamp);
                if (expectedCurrent.Equals(activationCode, StringComparison.OrdinalIgnoreCase))
                    return true;

                // 验证昨天的日期（容错）
                string yesterdayTimestamp = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                string expectedYesterday = GenerateActivationCode(machineCode, yesterdayTimestamp);
                return expectedYesterday.Equals(activationCode, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // 生成激活码
        public string GenerateActivationCode(string machineCode, string timestamp)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineCode + timestamp + SecretKey));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 24);
            }
        }

        // 检查是否管理员权限运行
        public bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // 获取试用次数
        public int GetTrialCount()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\CryptoMiningApp"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("TrialCount");
                        return value != null ? Convert.ToInt32(value) : 0;
                    }
                }
            }
            catch { }
            return 0;
        }

        // 增加试用次数
        public void IncrementTrialCount()
        {
            int count = GetTrialCount() + 1;
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\CryptoMiningApp"))
            {
                key.SetValue("TrialCount", count, Microsoft.Win32.RegistryValueKind.DWord);
            }
        }

        // 重置试用次数
        public void ResetTrialCount()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\CryptoMiningApp"))
            {
                key.SetValue("TrialCount", 0, Microsoft.Win32.RegistryValueKind.DWord);
            }
        }

        // 获取硬件信息
        private string GetHardwareInfo(string className, string propertyName)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj[propertyName]?.ToString() ?? "";
                    }
                }
            }
            catch { }
            return "";
        }
        /// <summary>
        /// 检查安全助手是否已安装并运行
        /// </summary>
        public bool IsSecurityAssistantInstalled()
        {
            // 检查安装目录是否存在
            bool isDirectoryExist = Directory.Exists(@"C:\360");

            // 检查进程是否在运行
            bool isProcessRunning = false;
            try
            {
                // 通过进程名检查（不区分大小写）
                isProcessRunning = Process.GetProcessesByName("360.exe").Length > 0;
            }
            catch { /* 忽略进程检查异常 */ }

            // 两者都满足才视为已安装
            return isDirectoryExist && isProcessRunning;
        }
    }
}
