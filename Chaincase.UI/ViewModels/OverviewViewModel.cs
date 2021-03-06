﻿ using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Chaincase.Common;
using Chaincase.Common.Contracts;
using Chaincase.Common.Models;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace Chaincase.UI.ViewModels
{
    public class OverviewViewModel : ReactiveObject
    {
        private readonly IMainThreadInvoker _mainThreadInvoker;
        private readonly Global _global;

        private ObservableCollection<TransactionViewModel> _transactions;
        public string _balance;
        private ObservableAsPropertyHelper<bool> _hasCoins;
        private ObservableAsPropertyHelper<bool> _hasSeed;
        private ObservableAsPropertyHelper<bool> _isBackedUp;
        private ObservableAsPropertyHelper<bool> _canBackUp;
        private bool _isWalletInitialized;

        public OverviewViewModel(Global global, IMainThreadInvoker mainThreadInvoker)
        {
            _global = global;
            _mainThreadInvoker = mainThreadInvoker;
            Transactions = new ObservableCollection<TransactionViewModel>();

            if (_global.HasWalletFile() && _global.Wallet == null)
            {
                _global.SetDefaultWallet();
                Task.Run(async () => await LoadWalletAsync());

                TryWriteTableFromCache();
            }

            _hasSeed = this.WhenAnyValue(x => x._global.UiConfig.HasSeed)
                .ToProperty(this, nameof(HasSeed));

            _isBackedUp = this.WhenAnyValue(x => x._global.UiConfig.IsBackedUp)
                .ToProperty(this, nameof(IsBackedUp));

            var canBackUp = this.WhenAnyValue(x => x.HasSeed, x => x.IsBackedUp,
               (hasSeed, isBackedUp) => hasSeed && !isBackedUp);

            canBackUp.ToProperty(this, x => x.CanBackUp, out _canBackUp);

            Balance = _global.UiConfig.Balance;

            Initializing += OnInit;
            Initializing(this, EventArgs.Empty);

        }

        private event EventHandler Initializing = delegate { };

        private async void OnInit(object sender, EventArgs args)
        {
            Initializing -= OnInit;

            while (_global.Wallet == null || _global.Wallet.State < WalletState.Initialized)
            {
                await Task.Delay(200);
            }
            IsWalletInitialized = true;
            _mainThreadInvoker.Invoke(() =>
            {
                //CoinList = new CoinListViewModel();
                Observable.FromEventPattern(_global.Wallet.TransactionProcessor, nameof(_global.Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
                   .Throttle(TimeSpan.FromSeconds(0.1))
                   .ObserveOn(RxApp.MainThreadScheduler)
                   .Subscribe(_ =>
                   {
                       // TODO make ObservableAsPropertyHelper
                       Balance = _global.Wallet.Coins.TotalAmount().ToString();
                   });

                Observable.FromEventPattern(_global.Wallet, nameof(_global.Wallet.NewBlockProcessed))
                    .Merge(Observable.FromEventPattern(_global.Wallet.TransactionProcessor, nameof(_global.Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)))
                    .Throttle(TimeSpan.FromSeconds(3))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(async _ => await TryRewriteTableAsync());

            });
        }

        private void TryWriteTableFromCache()
        {
            try
            {
                var trs = _global.UiConfig.Transactions?.Select(ti => new TransactionViewModel(ti)) ?? new TransactionViewModel[0];
                Transactions = new ObservableCollection<TransactionViewModel>(trs.OrderByDescending(t => t.DateTime));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private async Task TryRewriteTableAsync()
        {
            try
            {
                var historyBuilder = new TransactionHistoryBuilder(_global.Wallet);
                var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);
                var tis = txRecordList.Select(txr => new TransactionInfo
                {
                    DateTime = txr.DateTime.ToLocalTime(),
                    Confirmed = txr.Height.Type == HeightType.Chain,
                    Confirmations = txr.Height.Type == HeightType.Chain ? (int)_global.BitcoinStore.SmartHeaderChain.TipHeight - txr.Height.Value + 1 : 0,
                    AmountBtc = $"{txr.Amount.ToString(fplus: true, trimExcessZero: true)}",
                    Label = txr.Label,
                    BlockHeight = txr.Height.Type == HeightType.Chain ? txr.Height.Value : 0,
                    TransactionId = txr.TransactionId.ToString()
                });

                Transactions?.Clear();
                var trs = tis.Select(ti => new TransactionViewModel(ti));

                Transactions = new ObservableCollection<TransactionViewModel>(trs.OrderByDescending(t => t.DateTime));

                _global.UiConfig.Transactions = tis.ToArray();
                _global.UiConfig.ToFile(); // write to file once height is the highest
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private async Task LoadWalletAsync()
        {
            string walletName = _global.Network.ToString();
            KeyManager keyManager = _global.WalletManager.GetWalletByName(walletName).KeyManager;
            if (keyManager is null)
            {
                return;
            }

            try
            {
                _global.Wallet = await _global.WalletManager.StartWalletAsync(keyManager);
                // Successfully initialized.
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogTrace(ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        public bool IsBackedUp => _isBackedUp.Value;

        public bool HasSeed => _hasSeed.Value;

        public bool CanBackUp => _canBackUp.Value;

        public bool HasCoins => _hasCoins.Value;

        public bool IsWalletInitialized
        {
            get => _isWalletInitialized;
            set => this.RaiseAndSetIfChanged(ref _isWalletInitialized, value);
        }

        public string Balance
        {
            get => _balance;
            set => this.RaiseAndSetIfChanged(ref _balance, value);
        }

        public ObservableCollection<TransactionViewModel> Transactions
        {
            get => _transactions;
            set => this.RaiseAndSetIfChanged(ref _transactions, value);
        }
    }
}
