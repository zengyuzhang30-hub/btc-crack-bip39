using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UserWinForms.Services;
using Timer = System.Windows.Forms.Timer;

namespace UserWinForms
{
    public partial class MainForm : Form
    {
        private readonly MiningEngine _miningEngine;
        private bool _isRunning;
        private bool _isPaused;
        private DateTime _sessionStartTime;
        private DateTime _pauseStartTime;
        private System.Windows.Forms.Timer _statusTimer;
        private System.Windows.Forms.Timer _mnemonicUpdateTimer;
        private enum MiningMode { None, Gold, Dragon }
        private MiningMode _currentMode = MiningMode.None;
        private Thread _miningThread;

        // 线程取消令牌源
        private CancellationTokenSource _threadCts;

        // 总运行时长（跨会话，持久化存储）
        private TimeSpan _totalElapsedTime = TimeSpan.Zero;
        // 当前会话有效运行时长（排除暂停时间）
        private TimeSpan _currentSessionElapsed = TimeSpan.Zero;
        // 存储总运行时长的文件路径
        private readonly string _timeDataPath;

        // 存储用户输入的原始提示词（用于恢复）
        private string _originalGoldMnemonics = "";
        private string _originalDragonMnemonics = "";

        // 存储匹配到地址时的助记词
        private string[] _matchedDragonMnemonics = null;

        // 加密货币类型选择控件
        private RadioButton _rbGoldETH;
        private RadioButton _rbGoldBTC;
        private RadioButton _rbGoldUSDT;
        private RadioButton _rbDragonETH;
        private RadioButton _rbDragonBTC;
        private RadioButton _rbDragonUSDT;

        // 原有控件声明
        private TextBox _txtGoldAddress;
        private TextBox _txtGoldMnemonics;
        private CheckBox _chkGoldCheckBalance;
        private CheckBox _chkGoldNotify;
        private TextBox _txtGoldNotifyAccount;
        private CheckBox _chkGoldTransfer;
        private TextBox _txtGoldTransferAddress;
        private Button _btnGoldStart;
        private Button _btnGoldPause;
        private Label _lblGoldStatus;

        private TextBox _txtDragonAddressPool;
        private Button _btnDragonImport;
        private Button[] _btnDragonPools;
        private TextBox _txtDragonAddress;
        private TextBox _txtDragonMnemonics;
        private CheckBox _chkDragonCheckBalance;
        private CheckBox _chkDragonNotify;
        private TextBox _txtDragonNotifyAccount;
        private CheckBox _chkDragonTransfer;
        private TextBox _txtDragonTransferAddress;
        private Button _btnDragonStart;
        private Button _btnDragonPause;
        private Label _lblDragonStatus;

        public MainForm()
        {
            // 设置窗体不可调整大小
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.ClientSize = new Size(740, 750);

            InitializeComponent();
            _miningEngine = new MiningEngine();
            _miningEngine.DebugLogCallback = LogDebug; // 设置日志回调
            InitializeTimers();

            // 初始化时间数据文件路径
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoMiningTool");
            Directory.CreateDirectory(appDataPath);
            _timeDataPath = Path.Combine(appDataPath, "total_time.dat");

            // 加载之前保存的总运行时长
            LoadTotalElapsedTime();

            this.Text = "加密货币挖掘工具";
        }

        #region 持久化相关方法
        private void LoadTotalElapsedTime()
        {
            try
            {
                if (File.Exists(_timeDataPath))
                {
                    string timeString = File.ReadAllText(_timeDataPath);
                    if (long.TryParse(timeString, out long totalMilliseconds))
                    {
                        _totalElapsedTime = TimeSpan.FromMilliseconds(totalMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载总运行时长失败: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _totalElapsedTime = TimeSpan.Zero;
            }
        }

        private void SaveTotalElapsedTime()
        {
            try
            {
                File.WriteAllText(_timeDataPath, _totalElapsedTime.TotalMilliseconds.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存总运行时长失败: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        #endregion

        #region 组件设计器生成的代码
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 主窗体设置
            this.AutoScaleDimensions = new SizeF(7F, 17F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += new FormClosingEventHandler(this.MainForm_FormClosing);

            // 初始化主控件
            InitializeMainControls();

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        #region 初始化方法
        private void InitializeTimers()
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _statusTimer.Tick += StatusTimer_Tick;

            // 助记词更新定时器，每30ms更新一次
            _mnemonicUpdateTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _mnemonicUpdateTimer.Tick += MnemonicUpdateTimer_Tick;
        }

        private void InitializeMainControls()
        {
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 10)
            };

            var tabGold = new TabPage("淘金模式");
            InitializeGoldMiningControls(tabGold);

            var tabDragon = new TabPage("屠龙模式");
            InitializeDragonSlayingControls(tabDragon);

            // 标签页切换事件
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (_isRunning || _isPaused)
                {
                    if (tabControl.SelectedIndex == 0)
                        UpdateGoldStatus();
                    else
                        UpdateDragonStatus();
                }
            };

            tabControl.TabPages.Add(tabGold);
            tabControl.TabPages.Add(tabDragon);

            this.Controls.Add(tabControl);
        }

        private void InitializeGoldMiningControls(Control parent)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };
            parent.Controls.Add(panel);

            int yPos = 20;
            const int labelWidth = 100;
            const int controlWidth = 600;
            const int maxControlWidth = 600;

            // 加密货币类型选择
            panel.Controls.Add(new Label
            {
                Text = "币种选择:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            // 单选按钮组容器
            var currencyGroup = new FlowLayoutPanel
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                Height = 30,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                MinimumSize = new Size(300, 30)
            };
            panel.Controls.Add(currencyGroup);

            // ETH单选按钮
            _rbGoldETH = new RadioButton
            {
                Text = "ETH",
                Checked = true,
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbGoldETH);

            // BTC单选按钮
            _rbGoldBTC = new RadioButton
            {
                Text = "BTC",
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbGoldBTC);

            // USDT单选按钮
            _rbGoldUSDT = new RadioButton
            {
                Text = "USDT (TRC20)",
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbGoldUSDT);

            yPos += 40;

            // 地址栏
            panel.Controls.Add(new Label
            {
                Text = "当前地址:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            _txtGoldAddress = new TextBox
            {
                ReadOnly = true,
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtGoldAddress);

            yPos += 40;

            // 提示词
            panel.Controls.Add(new Label
            {
                Text = "提示词:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            _txtGoldMnemonics = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                PlaceholderText = "多个用空格分开（0-12个），输入12个有效词将生成唯一地址",
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Enabled = true,
                ReadOnly = false
            };
            panel.Controls.Add(_txtGoldMnemonics);

            // 添加提示标签
            panel.Controls.Add(new Label
            {
                Text = "(运行时将显示完整12词组合，包含您输入的提示词和随机补充词)",
                Location = new Point(labelWidth + 10, yPos + 25),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AutoSize = false,
                Width = controlWidth,
            });

            yPos += 60;

            // 查余额
            _chkGoldCheckBalance = new CheckBox
            {
                Text = "查余额",
                Checked = true,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkGoldCheckBalance.CheckedChanged += GoldCheckBalance_CheckedChanged;
            panel.Controls.Add(_chkGoldCheckBalance);

            yPos += 30;

            // 通知
            _chkGoldNotify = new CheckBox
            {
                Text = "通知",
                Checked = false,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkGoldNotify.CheckedChanged += GoldNotify_CheckedChanged;
            panel.Controls.Add(_chkGoldNotify);

            _txtGoldNotifyAccount = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = 300,
                Enabled = false,
                PlaceholderText = "TG账号",
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtGoldNotifyAccount);

            yPos += 30;

            // 转账
            _chkGoldTransfer = new CheckBox
            {
                Text = "转账",
                Checked = false,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkGoldTransfer.CheckedChanged += GoldTransfer_CheckedChanged;
            panel.Controls.Add(_chkGoldTransfer);

            _txtGoldTransferAddress = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                Enabled = false,
                PlaceholderText = "转入地址",
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtGoldTransferAddress);

            yPos += 50;

            // 开始按钮
            _btnGoldStart = new Button
            {
                Text = "开始",
                Location = new Point(0, yPos),
                Width = 100,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _btnGoldStart.Click += GoldStart_Click;
            panel.Controls.Add(_btnGoldStart);

            // 暂停按钮
            _btnGoldPause = new Button
            {
                Text = "暂停",
                Location = new Point(110, yPos),
                Width = 100,
                Enabled = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _btnGoldPause.Click += GoldPause_Click;
            panel.Controls.Add(_btnGoldPause);

            yPos += 50;

            // 状态栏标题
            panel.Controls.Add(new Label
            {
                Text = "状态信息:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            yPos += 25;

            // 状态栏
            _lblGoldStatus = new Label
            {
                Text = "当前状态：未运行",
                Location = new Point(0, yPos),
                Width = controlWidth + labelWidth + 10,
                MaximumSize = new Size(maxControlWidth + labelWidth + 10, 0),
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AutoEllipsis = false,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(5)
            };
            panel.Controls.Add(_lblGoldStatus);
        }

        private void InitializeDragonSlayingControls(Control parent)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };
            parent.Controls.Add(panel);

            int yPos = 20;
            const int labelWidth = 100;
            const int controlWidth = 600;
            const int maxControlWidth = 600;

            // 加密货币类型选择
            panel.Controls.Add(new Label
            {
                Text = "币种选择:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            // 单选按钮组容器
            var currencyGroup = new FlowLayoutPanel
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                Height = 30,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                MinimumSize = new Size(300, 30)
            };
            panel.Controls.Add(currencyGroup);

            // ETH单选按钮
            _rbDragonETH = new RadioButton
            {
                Text = "ETH",
                Checked = true,
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbDragonETH);

            // BTC单选按钮
            _rbDragonBTC = new RadioButton
            {
                Text = "BTC",
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbDragonBTC);

            // USDT单选按钮
            _rbDragonUSDT = new RadioButton
            {
                Text = "USDT (TRC20)",
                Margin = new Padding(0, 0, 20, 0)
            };
            currencyGroup.Controls.Add(_rbDragonUSDT);

            yPos += 40;

            // 地址池
            panel.Controls.Add(new Label
            {
                Text = "地址池:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            yPos += 25;

            _txtDragonAddressPool = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, yPos),
                Width = controlWidth + labelWidth + 10,
                MaximumSize = new Size(maxControlWidth + labelWidth + 10, 0),
                Height = 130,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Enabled = true
            };
            panel.Controls.Add(_txtDragonAddressPool);

            yPos += 140;

            // 导入按钮
            _btnDragonImport = new Button
            {
                Text = "导入",
                Location = new Point(0, yPos),
                Width = 70,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _btnDragonImport.Click += DragonImport_Click;
            panel.Controls.Add(_btnDragonImport);

            // 池1-6按钮
            _btnDragonPools = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                _btnDragonPools[i] = new Button
                {
                    Text = $"池{i + 1}",
                    Location = new Point(80 + i * 70, yPos),
                    Width = 60,
                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                };
                int poolNumber = i + 1;
                _btnDragonPools[i].Click += (s, e) => DragonLoadPool(poolNumber);
                panel.Controls.Add(_btnDragonPools[i]);
            }

            yPos += 45;

            // 地址栏
            panel.Controls.Add(new Label
            {
                Text = "当前地址:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            _txtDragonAddress = new TextBox
            {
                ReadOnly = true,
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtDragonAddress);

            yPos += 40;

            // 提示词
            panel.Controls.Add(new Label
            {
                Text = "提示词:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            _txtDragonMnemonics = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                PlaceholderText = "多个用空格分开（0-12个），输入12个有效词将生成唯一地址",
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Enabled = true,
                ReadOnly = false
            };
            panel.Controls.Add(_txtDragonMnemonics);

            // 添加提示标签
            panel.Controls.Add(new Label
            {
                Text = "(运行时将显示完整12词组合，包含您输入的提示词和随机补充词)",
                Location = new Point(labelWidth + 10, yPos + 25),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AutoSize = false,
                Width = controlWidth,
            });

            yPos += 60;

            // 查余额
            _chkDragonCheckBalance = new CheckBox
            {
                Text = "查余额",
                Checked = true,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkDragonCheckBalance.CheckedChanged += DragonCheckBalance_CheckedChanged;
            panel.Controls.Add(_chkDragonCheckBalance);

            yPos += 30;

            // 通知
            _chkDragonNotify = new CheckBox
            {
                Text = "通知",
                Checked = false,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkDragonNotify.CheckedChanged += DragonNotify_CheckedChanged;
            panel.Controls.Add(_chkDragonNotify);

            _txtDragonNotifyAccount = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = 300,
                Enabled = false,
                PlaceholderText = "TG账号",
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtDragonNotifyAccount);

            yPos += 30;

            // 转账
            _chkDragonTransfer = new CheckBox
            {
                Text = "转账",
                Checked = false,
                Location = new Point(0, yPos),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkDragonTransfer.CheckedChanged += DragonTransfer_CheckedChanged;
            panel.Controls.Add(_chkDragonTransfer);

            _txtDragonTransferAddress = new TextBox
            {
                Location = new Point(labelWidth + 10, yPos),
                Width = controlWidth,
                MaximumSize = new Size(maxControlWidth, 0),
                Enabled = false,
                PlaceholderText = "转入地址",
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            panel.Controls.Add(_txtDragonTransferAddress);

            yPos += 50;

            // 开始按钮
            _btnDragonStart = new Button
            {
                Text = "开始",
                Location = new Point(0, yPos),
                Width = 100,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _btnDragonStart.Click += DragonStart_Click;
            panel.Controls.Add(_btnDragonStart);

            // 暂停按钮
            _btnDragonPause = new Button
            {
                Text = "暂停",
                Location = new Point(110, yPos),
                Width = 100,
                Enabled = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _btnDragonPause.Click += DragonPause_Click;
            panel.Controls.Add(_btnDragonPause);

            yPos += 50;

            // 状态栏标题
            panel.Controls.Add(new Label
            {
                Text = "状态信息:",
                Location = new Point(0, yPos),
                Width = labelWidth,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            });

            yPos += 25;

            // 状态栏
            _lblDragonStatus = new Label
            {
                Text = "当前状态：未运行",
                Location = new Point(0, yPos),
                Width = controlWidth + labelWidth + 10,
                MaximumSize = new Size(maxControlWidth + labelWidth + 10, 0),
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AutoEllipsis = false,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(5)
            };
            panel.Controls.Add(_lblDragonStatus);
        }
        #endregion

        #region 助记词更新逻辑
        private void MnemonicUpdateTimer_Tick(object sender, EventArgs e)
        {
            // 关键修复：如果任务已停止，直接返回，不再更新助记词
            if (!_isRunning || _isPaused)
                return;

            // 检查是否为12个有效助记词，如果是则不更新
            string[] mnemonics = _currentMode == MiningMode.Gold
                ? _originalGoldMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                : _originalDragonMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool isFixed12Words = mnemonics.Length == 12 && _miningEngine.ValidateMnemonics(mnemonics);

            if (!isFixed12Words) // 只有非固定模式才更新助记词
            {
                if (_currentMode == MiningMode.Gold)
                {
                    UpdateGoldMnemonicsDisplay();
                }
                else if (_currentMode == MiningMode.Dragon)
                {
                    UpdateDragonMnemonicsDisplay();
                }
            }
        }

        private void UpdateGoldMnemonicsDisplay()
        {
            if (_txtGoldMnemonics.InvokeRequired)
            {
                _txtGoldMnemonics.Invoke(new Action(UpdateGoldMnemonicsDisplay));
                return;
            }

            string[] inputMnemonics = _originalGoldMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] combined = _miningEngine.CreateMnemonicCombination(inputMnemonics);
            _txtGoldMnemonics.Text = string.Join(" ", combined);
        }

        private void UpdateDragonMnemonicsDisplay()
        {
            if (_txtDragonMnemonics.InvokeRequired)
            {
                _txtDragonMnemonics.Invoke(new Action(UpdateDragonMnemonicsDisplay));
                return;
            }

            string[] inputMnemonics = _originalDragonMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] combined = _miningEngine.CreateMnemonicCombination(inputMnemonics);
            _txtDragonMnemonics.Text = string.Join(" ", combined);
        }
        #endregion

        #region 淘金模式事件处理
        private void GoldCheckBalance_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = _chkGoldCheckBalance.Checked;
            _chkGoldNotify.Enabled = enabled;
            _txtGoldNotifyAccount.Enabled = enabled && _chkGoldNotify.Checked;
            _chkGoldTransfer.Enabled = enabled;
            _txtGoldTransferAddress.Enabled = enabled && _chkGoldTransfer.Checked;
        }

        private void GoldNotify_CheckedChanged(object sender, EventArgs e)
        {
            _txtGoldNotifyAccount.Enabled = _chkGoldNotify.Checked;
        }

        private void GoldTransfer_CheckedChanged(object sender, EventArgs e)
        {
            _txtGoldTransferAddress.Enabled = _chkGoldTransfer.Checked;
        }

        private void GoldStart_Click(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                // 获取选中的加密货币类型
                CryptoCurrency selectedCurrency = CryptoCurrency.ETH;
                if (_rbGoldBTC.Checked)
                    selectedCurrency = CryptoCurrency.BTC;
                else if (_rbGoldUSDT.Checked)
                    selectedCurrency = CryptoCurrency.USDT;

                // 保存用户输入的原始提示词
                _originalGoldMnemonics = _txtGoldMnemonics.Text;

                // 验证提示词
                string[] mnemonics = _originalGoldMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (mnemonics.Length > 0)
                {
                    if (!_miningEngine.ValidatePartialMnemonics(mnemonics))
                    {
                        MessageBox.Show($"提示词包含不符合BIP39标准的单词: {_miningEngine.LastError}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (mnemonics.Length > 12)
                    {
                        MessageBox.Show("提示词数量不能超过12个，请减少输入的提示词数量！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 检查12个词是否有效
                    if (mnemonics.Length == 12)
                    {
                        string validation = _miningEngine.ValidateMnemonicsDetailed(mnemonics);
                        if (validation != "助记词有效")
                        {
                            MessageBox.Show($"输入的12个助记词无效: {validation}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }

                // 停止可能在运行的其他线程
                StopMiningThread();

                // 重置当前会话计时变量
                _currentSessionElapsed = TimeSpan.Zero;

                // 设置当前模式为淘金模式
                _currentMode = MiningMode.Gold;
                _isRunning = true;
                _isPaused = false;

                // 更新按钮状态
                _btnGoldStart.Text = "停止";
                _btnGoldStart.Enabled = true;

                // 如果是12个有效词，禁用暂停按钮
                bool isFixed12Words = mnemonics.Length == 12 && _miningEngine.ValidateMnemonics(mnemonics);
                _btnGoldPause.Enabled = !isFixed12Words;

                _btnDragonStart.Enabled = false;
                _btnDragonPause.Enabled = false;

                // 禁用提示词输入框（运行时不允许编辑）
                _txtGoldMnemonics.ReadOnly = true;
                _txtGoldMnemonics.Enabled = false;

                // 禁用币种选择
                _rbGoldETH.Enabled = false;
                _rbGoldBTC.Enabled = false;
                _rbGoldUSDT.Enabled = false;

                // 记录当前会话开始时间
                _sessionStartTime = DateTime.Now;

                // 创建新的取消令牌源
                _threadCts = new CancellationTokenSource();
                var token = _threadCts.Token;

                // 在新线程中开始淘金模式
                _miningThread = new Thread(() =>
                {
                    // 存储生成的地址，确保能在UI上显示
                    string generatedAddress = string.Empty;

                    _miningEngine.StartGoldMining(
                        mnemonics,
                        selectedCurrency,
                        _chkGoldCheckBalance.Checked,
                        _chkGoldNotify.Checked,
                        _txtGoldNotifyAccount.Text,
                        _chkGoldTransfer.Checked,
                        _txtGoldTransferAddress.Text,
                        address =>
                        {
                            generatedAddress = address;
                            if (_txtGoldAddress.InvokeRequired)
                            {
                                _txtGoldAddress.Invoke(new Action<string>(UpdateGoldAddress), address);
                            }
                            else
                            {
                                UpdateGoldAddress(address);
                            }

                            // 同步更新助记词显示
                            if (_currentMode == MiningMode.Gold && !_isPaused)
                            {
                                UpdateGoldMnemonicsDisplay();
                            }
                        },
                        token); // 传入取消令牌

                    // 如果是固定12词模式，生成后自动停止
                    if (isFixed12Words)
                    {
                        // 使用ManualResetEventSlim确保UI更新完成
                        var resetEvent = new ManualResetEventSlim(false);

                        this.Invoke(new Action(() =>
                        {
                            try
                            {
                                // 强制设置地址，确保显示
                                if (!string.IsNullOrEmpty(generatedAddress))
                                {
                                    _txtGoldAddress.Text = generatedAddress;
                                    LogDebug($"淘金模式固定12词地址生成: {generatedAddress}");
                                }
                                else
                                {
                                    _txtGoldAddress.Text = $"地址生成失败: {_miningEngine.LastError}";
                                    LogDebug($"淘金模式固定12词地址生成失败: {_miningEngine.LastError}");
                                }
                            }
                            finally
                            {
                                resetEvent.Set();
                            }
                        }));

                        // 等待UI更新完成
                        resetEvent.Wait(1000);

                        // 延迟停止，确保地址已显示
                        Task.Delay(500).ContinueWith(t =>
                        {
                            this.Invoke(new Action(() =>
                            {
                                StopMining();
                                _btnGoldStart.Text = "开始";
                                _btnGoldPause.Enabled = false;
                                _txtGoldMnemonics.Text = _originalGoldMnemonics;
                                _txtGoldMnemonics.ReadOnly = false;
                                _txtGoldMnemonics.Enabled = true;
                                _rbGoldETH.Enabled = true;
                                _rbGoldBTC.Enabled = true;
                                _rbGoldUSDT.Enabled = true;

                                UpdateGoldStatus();
                            }));
                        });
                    }
                });
                _miningThread.IsBackground = true; // 确保是后台线程
                _miningThread.Start();

                // 启动定时器
                _statusTimer.Start();
                _mnemonicUpdateTimer.Start();
                UpdateGoldStatus();
            }
            else if (_currentMode == MiningMode.Gold)
            {
                // 停止挖掘
                StopMining();

                // 重置按钮状态
                _btnGoldStart.Text = "开始";
                _btnGoldPause.Text = "暂停";
                _btnGoldPause.Enabled = false;
                _btnDragonStart.Enabled = true;

                // 恢复原始提示词显示，允许编辑
                _txtGoldMnemonics.Text = _originalGoldMnemonics;
                _txtGoldMnemonics.ReadOnly = false;
                _txtGoldMnemonics.Enabled = true;

                // 启用币种选择
                _rbGoldETH.Enabled = true;
                _rbGoldBTC.Enabled = true;
                _rbGoldUSDT.Enabled = true;
            }
        }

        private void GoldPause_Click(object sender, EventArgs e)
        {
            if (_isRunning && _currentMode == MiningMode.Gold)
            {
                if (!_isPaused)
                {
                    // 暂停挖掘
                    _isPaused = true;
                    _pauseStartTime = DateTime.Now;
                    _currentSessionElapsed += _pauseStartTime - _sessionStartTime;
                    _miningEngine.Pause();
                    _btnGoldPause.Text = "继续";

                    // 停止助记词更新定时器
                    _mnemonicUpdateTimer.Stop();

                    // 显示当前地址对应的助记词
                    string[] currentMnemonics = _miningEngine.CurrentAddressMnemonics;
                    if (currentMnemonics != null && currentMnemonics.Length > 0)
                    {
                        _txtGoldMnemonics.Text = string.Join(" ", currentMnemonics);
                    }

                    // 允许复制
                    _txtGoldMnemonics.Enabled = true;
                }
                else
                {
                    // 继续挖掘
                    _isPaused = false;
                    _sessionStartTime = DateTime.Now;
                    _miningEngine.Resume();
                    _btnGoldPause.Text = "暂停";

                    // 恢复时重新启动助记词更新，禁止编辑
                    _mnemonicUpdateTimer.Start();
                    _txtGoldMnemonics.Enabled = false;
                }
                UpdateGoldStatus();
            }
        }

        private void UpdateGoldAddress(string address)
        {
            if (_isRunning && !_isPaused)
            {
                _txtGoldAddress.Text = address;
            }
        }
        #endregion

        #region 屠龙模式事件处理
        private void DragonImport_Click(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        _txtDragonAddressPool.Lines = File.ReadAllLines(openFileDialog.FileName);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先停止当前运行的任务，再导入地址池", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DragonLoadPool(int poolNumber)
        {
            if (!_isRunning)
            {
                // 获取选中的加密货币类型
                CryptoCurrency selectedCurrency = CryptoCurrency.ETH;
                if (_rbDragonBTC.Checked)
                    selectedCurrency = CryptoCurrency.BTC;
                else if (_rbDragonUSDT.Checked)
                    selectedCurrency = CryptoCurrency.USDT;

                string[] addresses = _miningEngine.GetPresetAddressPool(poolNumber, selectedCurrency);
                _txtDragonAddressPool.Lines = addresses;
            }
            else
            {
                MessageBox.Show("请先停止当前运行的任务，再加载地址池", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DragonCheckBalance_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = _chkDragonCheckBalance.Checked;
            _chkDragonNotify.Enabled = enabled;
            _txtDragonNotifyAccount.Enabled = enabled && _chkDragonNotify.Checked;
            _chkDragonTransfer.Enabled = enabled;
            _txtDragonTransferAddress.Enabled = enabled && _chkDragonTransfer.Checked;
        }

        private void DragonNotify_CheckedChanged(object sender, EventArgs e)
        {
            _txtDragonNotifyAccount.Enabled = _chkDragonNotify.Checked;
        }

        private void DragonTransfer_CheckedChanged(object sender, EventArgs e)
        {
            _txtDragonTransferAddress.Enabled = _chkDragonTransfer.Checked;
        }

        private void DragonStart_Click(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                // 重置匹配助记词
                _matchedDragonMnemonics = null;

                // 获取选中的加密货币类型
                CryptoCurrency selectedCurrency = CryptoCurrency.ETH;
                if (_rbDragonBTC.Checked)
                    selectedCurrency = CryptoCurrency.BTC;
                else if (_rbDragonUSDT.Checked)
                    selectedCurrency = CryptoCurrency.USDT;

                // 保存用户输入的原始提示词
                _originalDragonMnemonics = _txtDragonMnemonics.Text;

                // 验证地址池（允许为空，使用默认地址池）
                List<string> targetAddresses = new List<string>();
                if (_txtDragonAddressPool.Lines != null)
                {
                    foreach (string line in _txtDragonAddressPool.Lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            targetAddresses.Add(line.Trim());
                    }
                }

                // 如果地址池为空，加载默认地址池
                if (targetAddresses.Count == 0)
                {
                    targetAddresses.AddRange(_miningEngine.GetPresetAddressPool(1, selectedCurrency));
                    this.Invoke(new Action(() =>
                    {
                        _txtDragonAddressPool.Lines = targetAddresses.ToArray();
                    }));
                }

                // 验证提示词
                string[] mnemonics = _originalDragonMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (mnemonics.Length > 0)
                {
                    if (!_miningEngine.ValidatePartialMnemonics(mnemonics))
                    {
                        MessageBox.Show($"提示词包含不符合BIP39标准的单词: {_miningEngine.LastError}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (mnemonics.Length > 12)
                    {
                        MessageBox.Show("提示词数量不能超过12个，请减少输入的提示词数量！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 检查12个词是否有效
                    if (mnemonics.Length == 12)
                    {
                        string validation = _miningEngine.ValidateMnemonicsDetailed(mnemonics);
                        if (validation != "助记词有效")
                        {
                            MessageBox.Show($"输入的12个助记词无效: {validation}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }

                // 停止可能在运行的其他线程
                StopMiningThread();

                // 重置当前会话计时变量
                _currentSessionElapsed = TimeSpan.Zero;

                // 设置当前模式为屠龙模式
                _currentMode = MiningMode.Dragon;
                _isRunning = true;
                _isPaused = false;

                // 更新按钮状态
                _btnDragonStart.Text = "停止";
                _btnDragonStart.Enabled = true;

                // 如果是12个有效词，禁用暂停按钮
                bool isFixed12Words = mnemonics.Length == 12 && _miningEngine.ValidateMnemonics(mnemonics);
                _btnDragonPause.Enabled = !isFixed12Words;

                _btnGoldStart.Enabled = false;
                _btnGoldPause.Enabled = false;

                // 禁用地址池和提示词输入框（运行时不允许编辑）
                _txtDragonAddressPool.Enabled = false;
                _txtDragonMnemonics.ReadOnly = true;
                _txtDragonMnemonics.Enabled = false;
                _btnDragonImport.Enabled = false;
                foreach (var btn in _btnDragonPools)
                {
                    btn.Enabled = false;
                }

                // 禁用币种选择
                _rbDragonETH.Enabled = false;
                _rbDragonBTC.Enabled = false;
                _rbDragonUSDT.Enabled = false;

                // 记录当前会话开始时间
                _sessionStartTime = DateTime.Now;

                // 创建新的取消令牌源
                _threadCts = new CancellationTokenSource();
                var token = _threadCts.Token;

                // 在新线程中开始屠龙模式
                _miningThread = new Thread(() =>
                {
                    // 存储生成的地址，确保能在UI上显示
                    string generatedAddress = string.Empty;
                    bool addressMatched = false; // 标记是否匹配到地址

                    _miningEngine.StartDragonSlaying(
                        targetAddresses.ToArray(),
                        mnemonics,
                        selectedCurrency,
                        _chkDragonCheckBalance.Checked,
                        _chkDragonNotify.Checked,
                        _txtDragonNotifyAccount.Text,
                        _chkDragonTransfer.Checked,
                        _txtDragonTransferAddress.Text,
                        address =>
                        {
                            generatedAddress = address;
                            if (_txtDragonAddress.InvokeRequired)
                            {
                                _txtDragonAddress.Invoke(new Action<string>(UpdateDragonAddress), address);
                            }
                            else
                            {
                                UpdateDragonAddress(address);
                            }

                            // 检查是否匹配到地址
                            bool isMatch = targetAddresses.Contains(address);
                            if (isMatch && !addressMatched)
                            {
                                addressMatched = true;
                                // 关键修复：立即保存匹配地址时的助记词
                                _matchedDragonMnemonics = _miningEngine.CurrentAddressMnemonics?.ToArray();

                                // 停止助记词更新，防止覆盖
                                if (_mnemonicUpdateTimer != null && _mnemonicUpdateTimer.Enabled)
                                {
                                    this.Invoke(new Action(() => _mnemonicUpdateTimer.Stop()));
                                }
                            }

                            // 同步更新助记词显示（仅在未匹配时）
                            if (_currentMode == MiningMode.Dragon && !_isPaused && !addressMatched)
                            {
                                UpdateDragonMnemonicsDisplay();
                            }
                        },
                        token); // 传入取消令牌

                    // 检查是否匹配到地址（由引擎内部标记）
                    addressMatched = addressMatched || _miningEngine.MatchedAddresses > 0;

                    // 如果匹配到地址或固定12词模式，自动停止
                    if (addressMatched || isFixed12Words)
                    {
                        var resetEvent = new ManualResetEventSlim(false);

                        this.Invoke(new Action(() =>
                        {
                            try
                            {
                                if (addressMatched)
                                {
                                    // 显示匹配地址对应的助记词
                                    if (_matchedDragonMnemonics != null && _matchedDragonMnemonics.Length > 0)
                                    {
                                        _txtDragonMnemonics.Text = string.Join(" ", _matchedDragonMnemonics);
                                    }
                                    else
                                    {
                                        // 备用方案：如果没有保存的助记词，尝试从引擎获取
                                        string[] matchedMnemonics = _miningEngine.CurrentAddressMnemonics;
                                        if (matchedMnemonics != null && matchedMnemonics.Length > 0)
                                        {
                                            _txtDragonMnemonics.Text = string.Join(" ", matchedMnemonics);
                                            _matchedDragonMnemonics = matchedMnemonics;
                                        }
                                    }
                                    LogDebug($"屠龙模式匹配到目标地址: {generatedAddress}");
                                    MessageBox.Show($"匹配到目标地址: {generatedAddress}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else if (!string.IsNullOrEmpty(generatedAddress))
                                {
                                    _txtDragonAddress.Text = generatedAddress;
                                    LogDebug($"屠龙模式固定12词地址生成: {generatedAddress}");
                                }
                                else
                                {
                                    _txtDragonAddress.Text = $"地址生成失败: {_miningEngine.LastError}";
                                    LogDebug($"屠龙模式地址生成失败: {_miningEngine.LastError}");
                                }
                            }
                            finally
                            {
                                resetEvent.Set();
                            }
                        }));

                        resetEvent.Wait(1000);

                        // 延迟停止，确保UI更新完成
                        Task.Delay(500).ContinueWith(t =>
                        {
                            this.Invoke(new Action(() =>
                            {
                                StopMining(); // 强制调用停止逻辑
                                _btnDragonStart.Text = "开始";
                                _btnDragonPause.Enabled = false;
                                _txtDragonMnemonics.ReadOnly = false;
                                _txtDragonMnemonics.Enabled = true;
                                _txtDragonAddressPool.Enabled = true;
                                _btnDragonImport.Enabled = true;
                                foreach (var btn in _btnDragonPools)
                                {
                                    btn.Enabled = true;
                                }
                                _rbDragonETH.Enabled = true;
                                _rbDragonBTC.Enabled = true;
                                _rbDragonUSDT.Enabled = true;

                                UpdateDragonStatus();
                            }));
                        });
                    }
                });
                _miningThread.IsBackground = true; // 确保是后台线程
                _miningThread.Start();

                // 启动定时器
                _statusTimer.Start();
                _mnemonicUpdateTimer.Start();
                UpdateDragonStatus();
            }
            else if (_currentMode == MiningMode.Dragon)
            {
                // 停止挖掘
                StopMining();

                // 重置按钮状态
                _btnDragonStart.Text = "开始";
                _btnDragonPause.Text = "暂停";
                _btnDragonPause.Enabled = false;
                _btnGoldStart.Enabled = true;

                // 恢复原始提示词显示，允许编辑
                _txtDragonMnemonics.Text = _originalDragonMnemonics;
                _txtDragonMnemonics.ReadOnly = false;
                _txtDragonMnemonics.Enabled = true;

                // 启用地址池和提示词输入框
                _txtDragonAddressPool.Enabled = true;
                _txtDragonMnemonics.Enabled = true;
                _btnDragonImport.Enabled = true;
                foreach (var btn in _btnDragonPools)
                {
                    btn.Enabled = true;
                }

                // 启用币种选择
                _rbDragonETH.Enabled = true;
                _rbDragonBTC.Enabled = true;
                _rbDragonUSDT.Enabled = true;
            }
        }

        private void DragonPause_Click(object sender, EventArgs e)
        {
            if (_isRunning && _currentMode == MiningMode.Dragon)
            {
                if (!_isPaused)
                {
                    // 暂停挖掘
                    _isPaused = true;
                    _pauseStartTime = DateTime.Now;
                    _currentSessionElapsed += _pauseStartTime - _sessionStartTime;
                    _miningEngine.Pause();
                    _btnDragonPause.Text = "继续";

                    // 停止助记词更新定时器
                    _mnemonicUpdateTimer.Stop();

                    // 显示当前地址对应的助记词
                    string[] currentMnemonics = _miningEngine.CurrentAddressMnemonics;
                    if (currentMnemonics != null && currentMnemonics.Length > 0)
                    {
                        _txtDragonMnemonics.Text = string.Join(" ", currentMnemonics);
                    }

                    // 允许复制
                    _txtDragonMnemonics.Enabled = true;
                }
                else
                {
                    // 继续挖掘
                    _isPaused = false;
                    _sessionStartTime = DateTime.Now;
                    _miningEngine.Resume();
                    _btnDragonPause.Text = "暂停";

                    // 恢复时重新启动助记词更新，禁止编辑
                    _mnemonicUpdateTimer.Start();
                    _txtDragonMnemonics.Enabled = false;
                }
                UpdateDragonStatus();
            }
        }

        private void UpdateDragonAddress(string address)
        {
            if (_isRunning && !_isPaused)
            {
                _txtDragonAddress.Text = address;
            }
        }
        #endregion

        #region 通用方法
        // 修复：移除Thread.Abort()调用，使用取消令牌机制
        private void StopMiningThread()
        {
            if (_miningThread != null && _miningThread.IsAlive)
            {
                // 1. 通过引擎的Stop方法触发取消逻辑
                _miningEngine.Stop();

                // 2. 如果有取消令牌源，主动取消
                if (_threadCts != null && !_threadCts.IsCancellationRequested)
                {
                    _threadCts.Cancel();
                }

                // 3. 等待线程自行结束（最多等待2秒）
                if (!_miningThread.Join(2000))
                {
                    LogDebug("警告：线程未能在规定时间内正常终止");
                }

                // 清理资源
                _threadCts?.Dispose();
                _threadCts = null;
                _miningThread = null;
            }
        }

        private void StopMining()
        {
            if (_isRunning)
            {
                // 计算当前会话最终有效时长
                TimeSpan finalSessionElapsed = _currentSessionElapsed;
                if (!_isPaused)
                {
                    finalSessionElapsed += DateTime.Now - _sessionStartTime;
                }

                // 总时长 = 历史累计 + 当前会话有效时长
                _totalElapsedTime += finalSessionElapsed;
                SaveTotalElapsedTime();
            }

            // 强制停止助记词更新定时器
            _mnemonicUpdateTimer.Stop();

            // 保存固定12词模式或匹配地址的助记词和地址
            string goldAddress = _txtGoldAddress.Text;
            string dragonAddress = _txtDragonAddress.Text;

            // 重置所有状态变量
            _isRunning = false;
            _isPaused = false;
            _currentMode = MiningMode.None;
            _currentSessionElapsed = TimeSpan.Zero;
            _sessionStartTime = DateTime.MinValue;
            _pauseStartTime = DateTime.MinValue;

            // 停止线程
            StopMiningThread();
            _statusTimer.Stop();

            // 保留固定词或匹配地址的信息
            string[] goldMnemonics = _originalGoldMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool isGoldFixed12Words = goldMnemonics.Length == 12 && _miningEngine.ValidateMnemonics(goldMnemonics);
            if (isGoldFixed12Words && !string.IsNullOrEmpty(goldAddress))
            {
                _txtGoldAddress.Text = goldAddress;
            }
            else
            {
                _txtGoldAddress.Text = string.Empty;
            }

            string[] dragonMnemonics = _originalDragonMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool isDragonFixed12Words = dragonMnemonics.Length == 12 && _miningEngine.ValidateMnemonics(dragonMnemonics);
            // 如果是匹配到的地址，保留地址和助记词
            if ((isDragonFixed12Words || _miningEngine.MatchedAddresses > 0) && !string.IsNullOrEmpty(dragonAddress))
            {
                _txtDragonAddress.Text = dragonAddress;
                // 显示匹配地址对应的助记词
                if (_matchedDragonMnemonics != null && _matchedDragonMnemonics.Length > 0)
                {
                    _txtDragonMnemonics.Text = string.Join(" ", _matchedDragonMnemonics);
                }
                else
                {
                    string[] matchedMnemonics = _miningEngine.CurrentAddressMnemonics;
                    if (matchedMnemonics != null && matchedMnemonics.Length > 0)
                    {
                        _txtDragonMnemonics.Text = string.Join(" ", matchedMnemonics);
                    }
                }
            }
            else
            {
                _txtDragonAddress.Text = string.Empty;
            }

            // 恢复按钮状态
            _btnGoldStart.Enabled = true;
            _btnDragonStart.Enabled = true;

            // 启用所有输入控件
            _txtGoldMnemonics.ReadOnly = false;
            _txtGoldMnemonics.Enabled = true;
            _rbGoldETH.Enabled = true;
            _rbGoldBTC.Enabled = true;
            _rbGoldUSDT.Enabled = true;

            _txtDragonAddressPool.Enabled = true;
            _txtDragonMnemonics.ReadOnly = false;
            _txtDragonMnemonics.Enabled = true;
            _btnDragonImport.Enabled = true;
            if (_btnDragonPools != null)
            {
                foreach (var btn in _btnDragonPools)
                {
                    btn.Enabled = true;
                }
            }
            _rbDragonETH.Enabled = true;
            _rbDragonBTC.Enabled = true;
            _rbDragonUSDT.Enabled = true;

            // 更新状态显示
            UpdateGoldStatus();
            UpdateDragonStatus();
        }

        // 辅助方法：更新按钮状态
        private void UpdateButtonStates(bool isRunning, MiningMode currentMode)
        {
            _btnGoldStart.Enabled = !isRunning || currentMode == MiningMode.Gold;
            _btnDragonStart.Enabled = !isRunning || currentMode == MiningMode.Dragon;
            _btnGoldPause.Enabled = isRunning && currentMode == MiningMode.Gold;
            _btnDragonPause.Enabled = isRunning && currentMode == MiningMode.Dragon;
        }

        // 日志辅助方法
        private void LogDebug(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        #endregion

        #region 状态更新与定时器
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                if (_currentMode == MiningMode.Gold)
                    UpdateGoldStatus();
                else if (_currentMode == MiningMode.Dragon)
                    UpdateDragonStatus();
            }
        }

        // 淘金模式状态栏
        private void UpdateGoldStatus()
        {
            if (_isRunning)
            {
                string currencyName = _rbGoldETH.Checked ? "ETH" :
                                    _rbGoldBTC.Checked ? "BTC" : "USDT (TRC20)";

                TimeSpan currentRealTime = _isPaused ?
                    TimeSpan.Zero :
                    DateTime.Now - _sessionStartTime;

                TimeSpan totalDuration = _totalElapsedTime + _currentSessionElapsed + currentRealTime;
                TimeSpan currentSessionTotal = _currentSessionElapsed + currentRealTime;

                string[] mnemonics = _originalGoldMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool isFixed12Words = mnemonics.Length == 12 && _miningEngine.ValidateMnemonics(mnemonics);

                string statusText = isFixed12Words ? "已生成固定地址" :
                                   (_isPaused ? "暂停中" : "运行中");

                var statusBuilder = new StringBuilder();
                statusBuilder.AppendLine($"当前状态：{statusText}");
                statusBuilder.AppendLine($"当前币种：{currencyName}");
                statusBuilder.AppendLine($"总运行时长：{totalDuration:hh\\:mm\\:ss}");
                statusBuilder.AppendLine($"本次运行时长：{currentSessionTotal:hh\\:mm\\:ss}");

                if (!isFixed12Words)
                {
                    statusBuilder.AppendLine($"总找到地址：{_miningEngine.TotalFoundAddresses}个");
                }

                if (!string.IsNullOrEmpty(_miningEngine.LastError))
                {
                    statusBuilder.AppendLine($"状态信息：{_miningEngine.LastError}");
                }

                _lblGoldStatus.Text = statusBuilder.ToString();
            }
            else
            {
                _lblGoldStatus.Text = $"当前状态：未运行\n" +
                                     $"总运行时长：{_totalElapsedTime:hh\\:mm\\:ss}";
            }
        }

        // 屠龙模式状态栏
        private void UpdateDragonStatus()
        {
            if (_isRunning)
            {
                string currencyName = _rbDragonETH.Checked ? "ETH" :
                                    _rbDragonBTC.Checked ? "BTC" : "USDT (TRC20)";

                TimeSpan currentRealTime = _isPaused ?
                    TimeSpan.Zero :
                    DateTime.Now - _sessionStartTime;

                TimeSpan totalDuration = _totalElapsedTime + _currentSessionElapsed + currentRealTime;
                TimeSpan currentSessionTotal = _currentSessionElapsed + currentRealTime;

                int totalAddresses = 0;
                if (_txtDragonAddressPool.Lines != null)
                {
                    totalAddresses = _txtDragonAddressPool.Lines.Count(line => !string.IsNullOrWhiteSpace(line));
                }

                string[] mnemonics = _originalDragonMnemonics.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool isFixed12Words = mnemonics.Length == 12 && _miningEngine.ValidateMnemonics(mnemonics);

                string statusText = isFixed12Words ? "已生成固定地址" :
                                   (_isPaused ? "暂停中" : "运行中");

                // 显示匹配状态
                if (_miningEngine.MatchedAddresses > 0)
                {
                    statusText = "已匹配地址";
                }

                var statusBuilder = new StringBuilder();
                statusBuilder.AppendLine($"当前状态：{statusText}");
                statusBuilder.AppendLine($"当前币种：{currencyName}");
                statusBuilder.AppendLine($"总运行时长：{totalDuration:hh\\:mm\\:ss}");
                statusBuilder.AppendLine($"本次运行时长：{currentSessionTotal:hh\\:mm\\:ss}");
                statusBuilder.AppendLine($"共有地址：{totalAddresses}个");
                statusBuilder.AppendLine($"总匹配地址：{_miningEngine.MatchedAddresses}个");

                if (!string.IsNullOrEmpty(_miningEngine.LastError))
                {
                    statusBuilder.AppendLine($"状态信息：{_miningEngine.LastError}");
                }

                _lblDragonStatus.Text = statusBuilder.ToString();
            }
            else
            {
                _lblDragonStatus.Text = $"当前状态：未运行\n" +
                                      $"总运行时长：{_totalElapsedTime:hh\\:mm\\:ss}";
            }
        }
        #endregion

        #region 窗体事件
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isRunning)
            {
                StopMining();
            }
            else
            {
                SaveTotalElapsedTime();
            }

            StopMiningThread();
            _statusTimer.Dispose();
            _mnemonicUpdateTimer.Dispose();
            _threadCts?.Dispose(); // 清理取消令牌源
        }
        #endregion
    }
}
