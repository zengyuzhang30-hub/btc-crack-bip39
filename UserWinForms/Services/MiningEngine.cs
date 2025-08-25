using NBitcoin;
using NBitcoin.DataEncoders;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace UserWinForms.Services
{
    // 加密货币类型枚举
    public enum CryptoCurrency
    {
        ETH,    // 以太坊
        BTC,    // 比特币
        USDT    // 泰达币(TRC-20)
    }

    // 地址和助记词的配对信息
    public class AddressMnemonicPair
    {
        public string Address { get; set; }
        public string[] Mnemonics { get; set; }
        public DateTime CreatedTime { get; set; }
    }


    public class MiningEngine
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Task _miningTask;
        private int _totalFoundAddresses;
        private decimal _totalBalance;
        private int _totalTargetAddresses;
        private int _matchedAddresses;
        private Action<string> _addressUpdateCallback;
        private CryptoCurrency _currentCurrency;

        // 用于存储地址和助记词的配对，确保同步
        private readonly ConcurrentQueue<AddressMnemonicPair> _addressMnemonicQueue = new ConcurrentQueue<AddressMnemonicPair>();
        private readonly object _currentPairLock = new object();
        private AddressMnemonicPair _currentPair = new AddressMnemonicPair();

        // 新增：用于调试的日志回调
        public Action<string> DebugLogCallback { get; set; }

        // 错误信息跟踪
        private string _lastError;
        public string LastError => _lastError;

        // BIP39英文词表缓存
        private static readonly string[] _bip39Wordlist = GetBip39Wordlist();
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        // 暂停相关字段
        private bool _isPaused = false;
        private readonly object _pauseLock = new object();
        private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);

        // 属性
        public string CurrentAddress
        {
            get
            {
                lock (_currentPairLock)
                {
                    return _currentPair?.Address;
                }
            }
        }

        public string[] CurrentAddressMnemonics
        {
            get
            {
                lock (_currentPairLock)
                {
                    return _currentPair?.Mnemonics ?? Array.Empty<string>();
                }
            }
        }

        public int TotalFoundAddresses => _totalFoundAddresses;
        public decimal TotalBalance => _totalBalance;
        public int TotalTargetAddresses => _totalTargetAddresses;
        public int MatchedAddresses => _matchedAddresses;
        public bool IsPaused => _isPaused;


        // 加载BIP39词表（适配NBitcoin 9.0.0）
        private static string[] GetBip39Wordlist()
        {
            try
            {
                return Wordlist.English.GetWords().ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("加载BIP39词表失败: " + ex.Message);
            }
        }


        // 生成随机助记词
        public string[] GenerateRandomMnemonics(int count = 12)
        {
            try
            {
                int validWordCount = Math.Clamp(count, 1, 12);
                var randomWords = new string[validWordCount];

                for (int i = 0; i < validWordCount; i++)
                {
                    byte[] randomBytes = new byte[2];
                    _rng.GetBytes(randomBytes);
                    ushort index = BitConverter.ToUInt16(randomBytes, 0);
                    index %= 2048; // BIP39词表固定包含2048个单词

                    randomWords[i] = _bip39Wordlist[index];
                }

                return randomWords;
            }
            catch (Exception ex)
            {
                _lastError = "生成随机助记词失败: " + ex.Message;
                LogDebug($"生成随机助记词失败: {ex.Message}");
                return Array.Empty<string>();
            }
        }


        /// <summary>
        /// 验证部分提示词有效性
        /// </summary>
        public bool ValidatePartialMnemonics(string[] mnemonics)
        {
            try
            {
                if (mnemonics == null || mnemonics.Length == 0)
                    return true;

                foreach (var word in mnemonics)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        _lastError = "提示词包含空值或空白";
                        LogDebug("提示词包含空值或空白");
                        return false;
                    }

                    // 不区分大小写检查
                    bool exists = _bip39Wordlist.Any(w =>
                        string.Equals(w, word, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        _lastError = $"提示词 '{word}' 不在BIP39标准词表中";
                        LogDebug($"提示词 '{word}' 不在BIP39标准词表中");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _lastError = "验证提示词失败: " + ex.Message;
                LogDebug($"验证提示词失败: {ex.Message}");
                return false;
            }
        }


        // 验证完整助记词并返回详细信息
        public string ValidateMnemonicsDetailed(string[] mnemonics)
        {
            try
            {
                if (mnemonics == null || mnemonics.Length == 0)
                {
                    return "助记词不能为空";
                }

                int wordCount = mnemonics.Length;
                // 有效的助记词长度必须是12, 15, 18, 21或24个单词
                if (wordCount != 12 && wordCount != 15 && wordCount != 18 &&
                    wordCount != 21 && wordCount != 24)
                {
                    return $"无效的助记词数量: {wordCount}，必须是12, 15, 18, 21或24个单词";
                }

                // 检查每个词是否在BIP39词表中
                foreach (var word in mnemonics)
                {
                    bool exists = _bip39Wordlist.Any(w =>
                        string.Equals(w, word, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        return $"助记词 '{word}' 不在BIP39标准词表中";
                    }
                }

                try
                {
                    // 将单词转换为小写，确保与词表匹配
                    string[] lowerCaseWords = mnemonics.Select(w => w.ToLowerInvariant()).ToArray();
                    string mnemonicString = string.Join(" ", lowerCaseWords);

                    // 显式验证助记词校验和
                    bool isValidChecksum = IsValidMnemonicChecksum(mnemonicString);
                    if (!isValidChecksum)
                    {
                        return "助记词校验和无效";
                    }

                    var mnemonic = new Mnemonic(mnemonicString, Wordlist.English);
                    return "助记词有效";
                }
                catch (Exception ex)
                {
                    return $"助记词校验和无效: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                return "验证助记词失败: " + ex.Message;
            }
        }


        // 验证助记词校验和（BIP39标准）
        private bool IsValidMnemonicChecksum(string mnemonic)
        {
            try
            {
                string[] words = mnemonic.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int wordCount = words.Length;

                // 计算熵长度（bits）
                int entropyLengthBits = wordCount * 11 - (wordCount * 11 / 33);
                byte[] entropy = new byte[entropyLengthBits / 8];

                // 将助记词转换为熵
                for (int i = 0; i < words.Length; i++)
                {
                    int index = Array.IndexOf(_bip39Wordlist, words[i]);
                    if (index == -1) return false;

                    for (int j = 0; j < 11; j++)
                    {
                        int bitPosition = i * 11 + j;
                        if (bitPosition >= entropyLengthBits) break;

                        int byteIndex = bitPosition / 8;
                        int bitIndex = 7 - (bitPosition % 8);

                        entropy[byteIndex] |= (byte)(((index >> (10 - j)) & 1) << bitIndex);
                    }
                }

                // 计算校验和
                byte[] hash = SHA256.Create().ComputeHash(entropy);
                int checksumLengthBits = wordCount * 11 % 33;

                // 验证校验和
                for (int i = 0; i < checksumLengthBits; i++)
                {
                    int bitPosition = entropyLengthBits + i;
                    int wordIndex = bitPosition / 11;
                    int bitInWord = bitPosition % 11;

                    int expectedBit = (hash[i / 8] >> (7 - (i % 8))) & 1;
                    int actualBit = (Array.IndexOf(_bip39Wordlist, words[wordIndex]) >> (10 - bitInWord)) & 1;

                    if (expectedBit != actualBit)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"校验和验证失败: {ex.Message}");
                return false;
            }
        }


        // 验证完整助记词
        public bool ValidateMnemonics(string[] mnemonics)
        {
            return ValidateMnemonicsDetailed(mnemonics) == "助记词有效";
        }


        /// <summary>
        /// 通过BIP39标准从助记词生成种子
        /// </summary>
        private byte[] GenerateSeedFromMnemonics(string[] mnemonics, string passphrase = "")
        {
            try
            {
                // 将单词转换为小写，确保正确生成种子
                string[] lowerCaseWords = mnemonics.Select(w => w.ToLowerInvariant()).ToArray();
                string mnemonicString = string.Join(" ", lowerCaseWords);

                LogDebug($"生成种子 - 助记词: {mnemonicString}");

                var mnemonic = new Mnemonic(mnemonicString, Wordlist.English);
                byte[] seed = mnemonic.DeriveSeed(passphrase);

                LogDebug($"种子生成成功，长度: {seed.Length}字节");
                return seed;
            }
            catch (Exception ex)
            {
                _lastError = $"生成种子失败: {ex.Message}";
                LogDebug($"生成种子失败: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// 生成以太坊风格地址 (ETH)
        /// </summary>
        private string GenerateEthereumStyleAddressFromSeed(byte[] seed)
        {
            if (seed == null)
            {
                _lastError = "种子为空，无法生成以太坊地址";
                LogDebug("种子为空，无法生成以太坊地址");
                return string.Empty;
            }

            try
            {
                // 以太坊标准BIP-44路径: m/44'/60'/0'/0/0
                var masterKey = ExtKey.CreateFromSeed(seed);
                var derivedKey = masterKey.Derive(new KeyPath("m/44'/60'/0'/0/0"));

                // 获取公钥并计算地址
                byte[] publicKey = derivedKey.PrivateKey.PubKey.ToBytes();

                // 以太坊地址生成
                byte[] publicKeyHash = ComputeKeccak256(publicKey.Skip(1).ToArray()); // 跳过0x04前缀
                byte[] addressBytes = publicKeyHash.Skip(12).Take(20).ToArray(); // 取后20字节
                string address = "0x" + Encoders.Hex.EncodeData(addressBytes);

                LogDebug($"生成以太坊地址: {address}");
                return address;
            }
            catch (Exception ex)
            {
                _lastError = $"生成以太坊地址失败: {ex.Message}";
                LogDebug($"生成以太坊地址失败: {ex.Message}");
                return string.Empty;
            }
        }


        /// <summary>
        /// 生成比特币风格地址
        /// </summary>
        private string GenerateBitcoinStyleAddressFromSeed(byte[] seed)
        {
            if (seed == null)
            {
                _lastError = "种子为空，无法生成比特币地址";
                LogDebug("种子为空，无法生成比特币地址");
                return string.Empty;
            }

            try
            {
                // 比特币标准BIP-44路径: m/44'/0'/0'/0/0
                var masterKey = ExtKey.CreateFromSeed(seed);
                var derivedKey = masterKey.Derive(new KeyPath("m/44'/0'/0'/0/0"));

                // 生成P2PKH地址
                var pubKeyHash = derivedKey.PrivateKey.PubKey.Hash;
                var address = pubKeyHash.GetAddress(Network.Main);
                string addressStr = address.ToString();

                LogDebug($"生成比特币地址: {addressStr}");
                return addressStr;
            }
            catch (Exception ex)
            {
                _lastError = $"生成比特币地址失败: {ex.Message}";
                LogDebug($"生成比特币地址失败: {ex.Message}");
                return string.Empty;
            }
        }


        /// <summary>
        /// 生成TRC-20风格地址 (USDT)
        /// </summary>
        private string GenerateTrc20StyleAddressFromSeed(byte[] seed)
        {
            if (seed == null)
            {
                _lastError = "种子为空，无法生成TRC-20地址";
                LogDebug("种子为空，无法生成TRC-20地址");
                return string.Empty;
            }

            try
            {
                // TRC-20标准BIP-44路径: m/44'/195'/0'/0/0
                var masterKey = ExtKey.CreateFromSeed(seed);
                var derivedKey = masterKey.Derive(new KeyPath("m/44'/195'/0'/0/0"));

                // 获取公钥并计算地址
                byte[] publicKey = derivedKey.PrivateKey.PubKey.ToBytes();

                // TRC-20地址生成 (波场地址)
                byte[] publicKeyHash = ComputeKeccak256(publicKey.Skip(1).ToArray()); // 跳过0x04前缀
                byte[] addressBytes = publicKeyHash.Skip(12).Take(20).ToArray(); // 取后20字节

                // 波场地址前缀为0x41(65)，对应字符'T'
                byte[] trcAddressBytes = new byte[21];
                trcAddressBytes[0] = 0x41; // TRC-20地址前缀
                Buffer.BlockCopy(addressBytes, 0, trcAddressBytes, 1, 20);

                // 计算校验和
                byte[] checksum = ComputeKeccak256(trcAddressBytes).Take(4).ToArray();

                // 组合地址和校验和，进行Base58编码
                byte[] allBytes = new byte[25];
                Buffer.BlockCopy(trcAddressBytes, 0, allBytes, 0, 21);
                Buffer.BlockCopy(checksum, 0, allBytes, 21, 4);

                string address = Encoders.Base58.EncodeData(allBytes);
                LogDebug($"生成TRC-20地址: {address}");
                return address;
            }
            catch (Exception ex)
            {
                _lastError = $"生成TRC-20地址失败: {ex.Message}";
                LogDebug($"生成TRC-20地址失败: {ex.Message}");
                return string.Empty;
            }
        }


        // 独立实现Keccak256哈希计算
        private byte[] ComputeKeccak256(byte[] data)
        {
            if (data == null)
            {
                _lastError = "输入数据为空，无法计算Keccak256哈希";
                LogDebug("输入数据为空，无法计算Keccak256哈希");
                return null;
            }

            var keccak = new KeccakDigest(256);
            try
            {
                keccak.BlockUpdate(data, 0, data.Length);
                byte[] result = new byte[32];
                keccak.DoFinal(result, 0);
                return result;
            }
            catch (Exception ex)
            {
                _lastError = $"计算Keccak256哈希失败: {ex.Message}";
                LogDebug($"计算Keccak256哈希失败: {ex.Message}");
                return null;
            }
            finally
            {
                (keccak as IDisposable)?.Dispose();
            }
        }


        // 根据当前加密货币类型生成地址
        private string GenerateAddressFromSeed(byte[] seed)
        {
            try
            {
                switch (_currentCurrency)
                {
                    case CryptoCurrency.ETH:
                        return GenerateEthereumStyleAddressFromSeed(seed);
                    case CryptoCurrency.BTC:
                        return GenerateBitcoinStyleAddressFromSeed(seed);
                    case CryptoCurrency.USDT:
                        return GenerateTrc20StyleAddressFromSeed(seed);
                    default:
                        _lastError = $"不支持的加密货币类型: {_currentCurrency}";
                        LogDebug($"不支持的加密货币类型: {_currentCurrency}");
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"根据种子生成地址失败: {ex.Message}";
                LogDebug($"根据种子生成地址失败: {ex.Message}");
                return string.Empty;
            }
        }


        // 从助记词生成地址 - 增加详细日志和错误处理
        public string GenerateAddressFromMnemonics(string[] mnemonics)
        {
            try
            {
                // 重置之前的错误
                _lastError = string.Empty;

                // 验证助记词有效性
                string validationResult = ValidateMnemonicsDetailed(mnemonics);
                LogDebug($"助记词验证结果: {validationResult}");

                if (validationResult != "助记词有效")
                {
                    _lastError = validationResult;
                    return string.Empty;
                }

                byte[] seed = GenerateSeedFromMnemonics(mnemonics);
                if (seed == null || seed.Length == 0)
                {
                    // GenerateSeedFromMnemonics已经设置了_lastError
                    return string.Empty;
                }

                string address = GenerateAddressFromSeed(seed);
                if (string.IsNullOrEmpty(address))
                {
                    if (string.IsNullOrEmpty(_lastError))
                        _lastError = "地址生成失败，结果为空";
                    LogDebug(_lastError);
                    return string.Empty;
                }

                LogDebug($"成功从助记词生成地址: {address}");
                return address;
            }
            catch (Exception ex)
            {
                _lastError = $"从助记词生成地址失败: {ex.Message}";
                LogDebug(_lastError);
                return string.Empty;
            }
        }


        // 开始淘金模式
        public void StartGoldMining(string[] inputMnemonics, CryptoCurrency currency,
                                   bool checkBalance, bool notify, string notifyAccount,
                                   bool transfer, string transferAddress,
                                   Action<string> addressUpdateCallback,
                                   CancellationToken cancellationToken)
        {
            try
            {
                _lastError = string.Empty;
                _currentCurrency = currency;
                ResetMiningState();

                _addressUpdateCallback = addressUpdateCallback;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _cancellationTokenSource.Token;

                // 判断是否为12个有效助记词
                bool isFixed12Words = inputMnemonics.Length == 12 && ValidateMnemonics(inputMnemonics);
                LogDebug($"固定12词模式: {isFixed12Words}");
                    try
                    {
                        // 固定12词模式：只生成一次地址后结束
                        if (isFixed12Words)
                        {
                            LogDebug("进入固定12词模式，开始生成地址");
                            ProcessAddressPair(inputMnemonics, checkBalance, notify, notifyAccount, transfer, transferAddress, token);
                            LogDebug("固定12词模式地址生成完成");
                            return; // 生成后立即结束任务
                        }

                        // 普通模式：持续生成地址
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                if (!_pauseEvent.Wait(100, token))
                                {
                                    continue;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (token.IsCancellationRequested) break;

                            string[] mnemonics = CreateMnemonicCombination(inputMnemonics);
                            ProcessAddressPair(mnemonics, checkBalance, notify, notifyAccount, transfer, transferAddress, token);

                            // 控制生成速度，减轻CPU负担
                            Thread.Sleep(30);
                        }
                    }
                    catch (Exception ex)
                    {
                        _lastError = $"淘金模式执行错误: {ex.Message}";
                        LogDebug(_lastError);
                    }
            }
            catch (Exception ex)
            {
                _lastError = $"启动淘金模式失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 开始屠龙模式
        public void StartDragonSlaying(string[] targetAddresses, string[] inputMnemonics,
                                      CryptoCurrency currency, bool checkBalance, bool notify,
                                      string notifyAccount, bool transfer, string transferAddress,
                                      Action<string> addressUpdateCallback,
                                      CancellationToken cancellationToken)
        {
            try
            {
                _lastError = string.Empty;
                _currentCurrency = currency;
                ResetMiningState();

                _addressUpdateCallback = addressUpdateCallback;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _cancellationTokenSource.Token;
                _totalTargetAddresses = targetAddresses.Length;
                _matchedAddresses = 0;

                // 判断是否为12个有效助记词
                bool isFixed12Words = inputMnemonics.Length == 12 && ValidateMnemonics(inputMnemonics);
                LogDebug($"固定12词模式: {isFixed12Words}");

                    try
                    {
                        // 固定12词模式：只生成一次地址后结束
                        if (isFixed12Words)
                        {
                            LogDebug("进入固定12词模式，开始生成地址");
                            string address = ProcessAddressPair(inputMnemonics, checkBalance, notify, notifyAccount, transfer, transferAddress, token);

                            // 检查是否匹配目标地址
                            bool isMatch = targetAddresses.Any(target =>
                                string.Equals(address, target.Trim(), StringComparison.OrdinalIgnoreCase));

                            if (isMatch)
                            {
                                _matchedAddresses++;
                                LogDebug($"匹配到目标{_currentCurrency}地址: {address}");

                                if (notify && !string.IsNullOrEmpty(notifyAccount))
                                {
                                    SendTelegramNotification(notifyAccount,
                                                           $"匹配到目标{_currentCurrency}地址: {address}");
                                }

                                Stop();
                            }

                            LogDebug("固定12词模式地址生成完成");
                            return; // 生成后立即结束任务
                        }

                        // 普通模式：持续生成地址
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                if (!_pauseEvent.Wait(100, token))
                                {
                                    continue;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (token.IsCancellationRequested) break;

                            string[] mnemonics = CreateMnemonicCombination(inputMnemonics);
                            string address = ProcessAddressPair(mnemonics, checkBalance, notify, notifyAccount, transfer, transferAddress, token);

                            // 检查是否匹配目标地址
                            bool isMatch = targetAddresses.Any(target =>
                                string.Equals(address, target.Trim(), StringComparison.OrdinalIgnoreCase));

                            if (isMatch)
                            {
                                _matchedAddresses++;
                                LogDebug($"匹配到目标{_currentCurrency}地址: {address}");

                                if (notify && !string.IsNullOrEmpty(notifyAccount))
                                {
                                    SendTelegramNotification(notifyAccount,
                                                           $"匹配到目标{_currentCurrency}地址: {address}");
                                }

                                if (transfer && !string.IsNullOrEmpty(transferAddress))
                                {
                                    TransferFunds(mnemonics, transferAddress, GetAddressBalance(address));
                                }

                                Stop();
                                break;
                            }

                            // 控制生成速度，减轻CPU负担
                            Thread.Sleep(30);
                        }
                    }
                    catch (Exception ex)
                    {
                        _lastError = $"屠龙模式执行错误: {ex.Message}";
                        LogDebug(_lastError);
                    }
            }
            catch (Exception ex)
            {
                _lastError = $"启动屠龙模式失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }

        /// <summary>
        /// 处理地址和助记词对，确保它们始终同步更新
        /// </summary>
        private string ProcessAddressPair(string[] mnemonics, bool checkBalance, bool notify,
                                         string notifyAccount, bool transfer, string transferAddress,
                                         CancellationToken token)
        {
            try
            {
                // 生成地址
                string address = GenerateAddressFromMnemonics(mnemonics);
                if (string.IsNullOrEmpty(address))
                {
                    LogDebug($"地址生成失败: {_lastError}");
                    return string.Empty;
                }

                // 创建配对并原子化更新当前配对
                var newPair = new AddressMnemonicPair
                {
                    Address = address,
                    Mnemonics = mnemonics.ToArray(), // 创建副本避免外部修改
                    CreatedTime = DateTime.Now
                };

                // 原子化更新当前配对，确保线程安全
                lock (_currentPairLock)
                {
                    _currentPair = newPair;
                }

                // 将新配对加入队列
                _addressMnemonicQueue.Enqueue(newPair);

                // 限制队列大小，防止内存溢出
                while (_addressMnemonicQueue.Count > 100)
                {
                    _addressMnemonicQueue.TryDequeue(out _);
                }

                // 更新计数器
                Interlocked.Increment(ref _totalFoundAddresses);

                // 触发地址更新回调
                if (!token.IsCancellationRequested && _addressUpdateCallback != null)
                {
                    try
                    {
                        _addressUpdateCallback.Invoke(address);
                    }
                    catch (Exception ex)
                    {
                        _lastError = $"地址更新回调错误: {ex.Message}";
                        LogDebug(_lastError);
                    }
                }

                // 检查余额（如果需要）
                if (checkBalance)
                {
                    decimal balance = GetAddressBalance(address);
                    if (balance > 0.0000001m)
                    {
                        //Interlocked.Add(ref _totalBalance, (decimal)balance);

                        if (notify && !string.IsNullOrEmpty(notifyAccount))
                        {
                            SendTelegramNotification(notifyAccount,
                                                   $"发现有余额的{_currentCurrency}地址: {address}, 余额: {balance:F8}");
                        }

                        if (transfer && !string.IsNullOrEmpty(transferAddress))
                        {
                            TransferFunds(mnemonics, transferAddress, balance);
                        }
                    }
                }

                LogDebug($"处理地址: {address}, 助记词数量: {mnemonics.Length}");
                return address;
            }
            catch (Exception ex)
            {
                _lastError = $"处理地址对失败: {ex.Message}";
                LogDebug(_lastError);
                return string.Empty;
            }
        }


        // 助记词组合生成逻辑
        public string[] CreateMnemonicCombination(string[] inputMnemonics)
        {
            try
            {
                var validInputs = inputMnemonics?
                    .Where(word => !string.IsNullOrWhiteSpace(word))
                    .Select(word => word.Trim())
                    .ToList() ?? new List<string>();

                // 如果输入正好12个词，直接返回（不补充随机词）
                if (validInputs.Count == 12)
                {
                    LogDebug("输入正好12个有效助记词，直接使用");
                    return validInputs.ToArray();
                }

                // 限制最多12个词
                if (validInputs.Count > 12)
                {
                    validInputs = validInputs.Take(12).ToList();
                    LogDebug($"输入助记词超过12个，截取前12个");
                }

                // 不足12个时补充随机词
                int remaining = 12 - validInputs.Count;
                if (remaining > 0)
                {
                    LogDebug($"输入助记词不足12个，补充{remaining}个随机词");
                    validInputs.AddRange(GenerateRandomMnemonics(remaining));
                }

                return validInputs.ToArray();
            }
            catch (Exception ex)
            {
                _lastError = $"生成助记词组合失败: {ex.Message}";
                LogDebug(_lastError);
                return Array.Empty<string>();
            }
        }


        // 暂停挖掘
        public void Pause()
        {
            try
            {
                lock (_pauseLock)
                {
                    if (!_isPaused)
                    {
                        _isPaused = true;
                        _pauseEvent.Reset();
                        LogDebug("挖掘已暂停");
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = $"暂停挖掘失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 继续挖掘
        public void Resume()
        {
            try
            {
                lock (_pauseLock)
                {
                    if (_isPaused)
                    {
                        _isPaused = false;
                        _pauseEvent.Set();
                        LogDebug("挖掘已恢复");
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = $"恢复挖掘失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 停止运算
        public void Stop()
        {
            try
            {
                _addressUpdateCallback = null;
                LogDebug("开始停止挖掘");

                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();

                    if (_miningTask != null)
                    {
                        try
                        {
                            _miningTask.Wait(_cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException) { }
                        catch (AggregateException ex)
                        {
                            ex.Handle(e => e is OperationCanceledException);
                        }
                    }
                }

                lock (_pauseLock)
                {
                    _isPaused = false;
                    _pauseEvent.Set();
                }

                ResetMiningState();
                LogDebug("挖掘已停止");
            }
            catch (Exception ex)
            {
                _lastError = $"停止挖掘失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 重置挖掘状态
        private void ResetMiningState()
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                _miningTask = null;

                if (_pauseEvent != null)
                {
                    _pauseEvent.Dispose();
                }
                _pauseEvent = new ManualResetEventSlim(true);
                _isPaused = false;

                _addressUpdateCallback = null;
                // 清空队列和当前配对
                _addressMnemonicQueue.Clear();
                lock (_currentPairLock)
                {
                    _currentPair = new AddressMnemonicPair();
                }

                LogDebug("挖掘状态已重置");
            }
            catch (Exception ex)
            {
                _lastError = $"重置挖掘状态失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 获取预设地址池（按加密货币类型区分）
        /// <summary>
        /// 获取预设地址池（从文件加载）
        /// </summary>
        public string[] GetPresetAddressPool(int poolNumber, CryptoCurrency currency)
        {
            try
            {
                // 构建文件名，例如 "BTC池1.txt", "ETH池2.txt"
                string currencyPrefix = currency.ToString();
                string fileName = $"{currencyPrefix}池{poolNumber}.txt";

                // 获取程序当前目录
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                LogDebug($"尝试加载地址池文件: {filePath}");

                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    LogDebug($"地址池文件不存在: {filePath}");
                    return Array.Empty<string>();
                }

                // 读取文件内容，每行一个地址
                List<string> addresses = new List<string>();
                foreach (string line in File.ReadAllLines(filePath))
                {
                    string address = line.Trim();
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        addresses.Add(address);
                    }
                }

                LogDebug($"成功加载地址池，共{addresses.Count}个地址");
                return addresses.ToArray();
            }
            catch (Exception ex)
            {
                _lastError = $"获取预设地址池失败: {ex.Message}";
                LogDebug(_lastError);
                return Array.Empty<string>();
            }
        }
        //public string[] GetPresetAddressPool(int poolNumber, CryptoCurrency currency)
        //{
        //    try
        //    {
        //        if (currency == CryptoCurrency.BTC)
        //        {
        //            return new Dictionary<int, string[]>
        //        {
        //            {1, new[] {"1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2"}},
        //            {2, new[] {"1DrPYi29M89XQxBV7UcR35y7g6iV8F88tR", "1K4n22P7VCZ2GfJfjpKQ1d95RwG956qYJw"}},
        //            {3, new[] {"16rCmCmbuWDhPjWTrpQGaU3EPdZF7MTdUk", "12c6DSiU4Rq3P4ZxziKxzrL5LmMBrzjrJX"}},
        //            {4, new[] {"1HLoD9E4SDFFPDiYfNYnkBLQ85Y51J3Zb1", "1P5ZEDWTKTFGxQjZphgWPQUpe554WKDfHQ"}},
        //            {5, new[] {"1FyxpwBjJ6rq3cGD6WFe4KTgQJ7sPbi1J7", "1L8meqXH4XqK8LJ8j7XvXwQa3h5Y9g8f1P"}},
        //            {6, new[] {"1EtheVv6Qj9G8tG8nLzrL5mX9y8d7k6j5T", "1Bd7DdJ4zQzX9sP5v7mK8j3nL9d2f1g4h6"}}
        //        }.TryGetValue(poolNumber, out var addresses) ? addresses : new string[0];
        //        }
        //        else if (currency == CryptoCurrency.ETH)
        //        {
        //            return new Dictionary<int, string[]>
        //        {
        //            {1, new[] {"0x742d35Cc6634C0532925a3b844Bc454e4438f44e", "0x5FbDB2315678afecb367f032d93F642f64180aa3"}},
        //            {2, new[] {"0xe7f1725E7734CE288F8367e1Bb143E90bb3F0512", "0x9fE46736679d2D9a65F0992F2272dE9f3c7fa6e0"}},
        //            {3, new[] {"0x2279B7A0a67DB372996a5FaB50D91eAA73d2eBe6", "0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65"}},
        //            {4, new[] {"0x09617F65550635F352092E2aD9d9A32D62A3689F5", "0x976EA74026E726554dB657fA54763abd0C3a0aa9"}},
        //            {5, new[] {"0x14dC79964da2C08b23698B3D3cc7Ca32193d9955", "0x23618e81E3f5cdF7f54C3d65f7FBc0aBf5B21E8f"}},
        //            {6, new[] {"0xa0Ee7A142d267C1f36714E4a8F75612F20a79720", "0xBcd4042DE499D14e55001CcbB24a551F3b954096"}}
        //        }.TryGetValue(poolNumber, out var addresses) ? addresses : new string[0];
        //        }
        //        else // USDT (TRC-20)
        //        {
        //            return new Dictionary<int, string[]>
        //        {
        //            {1, new[] {"THh9iD9aY3j7Vj8KzL6mN5oP4qR3sT2uW", "TBg4uJ3kL5mN6oP7qR8sT9uV0wX1yZ2aB"}},
        //            {2, new[] {"TCd5eF6gH7jK8lM9nO0pQ1rS2tU3vW4xY", "TDf6gH7jK8lM9nO0pQ1rS2tU3vW4xY5zA"}},
        //            {3, new[] {"TEg7hJ8kL9mN0oP1qR2sT3uV4wX5yZ6aB", "TFh8jK9lM0nO1pQ2rS3tU4vW5xY6zA7bC"}},
        //            {4, new[] {"TGj9kL0mN1oP2qR3sT4uV5wX6yZ7aB8cD", "THk0lM1nO2pQ3rS4tU5vW6xY7zA8bB9cE"}},
        //            {5, new[] {"TI1mM2nO3pQ4rS5tU6vW7xY8zA9bB0cF", "TJ2nN3oP4qR5sT6uU7vW8xY9zA0bB1cG"}},
        //            {6, new[] {"TK3oO4pQ5rS6tU7vV8wX9xY0zA1bB2cH", "TL4pP5qR6sT7uU8vV9wX0xY1zA2bB3cI"}}
        //        }.TryGetValue(poolNumber, out var addresses) ? addresses : new string[0];
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _lastError = $"获取预设地址池失败: {ex.Message}";
        //        LogDebug(_lastError);
        //        return Array.Empty<string>();
        //    }
        //}


        // 获取地址余额（模拟）
        private decimal GetAddressBalance(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address)) return 0;

                byte[] randomBytes = new byte[4];
                _rng.GetBytes(randomBytes);
                double randomValue = BitConverter.ToUInt32(randomBytes, 0) / (double)uint.MaxValue;

                // 根据不同币种调整余额范围
                switch (_currentCurrency)
                {
                    case CryptoCurrency.BTC:
                        return (decimal)(randomValue * 0.00001); // 比特币余额较小
                    case CryptoCurrency.ETH:
                        return (decimal)(randomValue * 0.001);  // 以太坊余额中等
                    case CryptoCurrency.USDT:
                        return (decimal)(randomValue * 10);     // USDT余额较大
                    default:
                        return (decimal)(randomValue * 0.001);
                }
            }
            catch (Exception ex)
            {
                _lastError = $"获取地址余额失败: {ex.Message}";
                LogDebug(_lastError);
                return 0;
            }
        }


        // 发送Telegram通知（模拟）
        private void SendTelegramNotification(string account, string message)
        {
            try
            {
                LogDebug($"发送通知到TG账号 {account}: {message}");
                Console.WriteLine($"发送通知到TG账号 {account}: {message}");
            }
            catch (Exception ex)
            {
                _lastError = $"发送Telegram通知失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 转账（模拟）
        private void TransferFunds(string[] mnemonics, string toAddress, decimal amount)
        {
            try
            {
                if (amount <= 0 || string.IsNullOrEmpty(toAddress)) return;

                string fromAddress = GenerateAddressFromMnemonics(mnemonics);
                LogDebug($"从{_currentCurrency}地址 {fromAddress} 转账 {amount:F8} 到 {toAddress}");
                Console.WriteLine($"从{_currentCurrency}地址 {fromAddress} 转账 {amount:F8} 到 {toAddress}");
            }
            catch (Exception ex)
            {
                _lastError = $"转账失败: {ex.Message}";
                LogDebug(_lastError);
            }
        }


        // 调试日志辅助方法
        private void LogDebug(string message)
        {
            DebugLogCallback?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
