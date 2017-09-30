﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using Prime.Core;
using Prime.Core.Exchange.Rates;
using Prime.Utility;

namespace Prime.Ui.Wpf.ViewModel
{
    public class ExchangeRateViewModel : DocumentPaneViewModel
    {
        public ExchangeRateViewModel() { }

        public ExchangeRateViewModel(ScreenViewModel model)
        {
            _assetRight = UserContext.Current.BaseAsset;

            _dispatcher = Dispatcher.CurrentDispatcher;

            AllAssetsViewModel = new AllAssetsViewModel(model);

            foreach (var i in UserContext.Current.UserSettings.FavouritePairs)
                _requests.Add(_coord.AddRequest(i));

            foreach (var i in UserContext.Current.UserSettings.HistoricExchangeRates)
                _requests.Add(_coord.AddRequest(i));

            GoCommand = new RelayCommand(Go);
        }

        private readonly Dispatcher _dispatcher;
        private readonly List<ExchangeRateRequest> _requests = new List<ExchangeRateRequest>();
        private readonly ExchangeRatesCoordinator _coord = ExchangeRatesCoordinator.I;

        public AllAssetsViewModel AllAssetsViewModel { get; }

        public RelayCommand GoCommand { get; }

        private void NewRate(ExchangeRateCollected obj)
        {
            _dispatcher.Invoke(() =>
            {
                ExchangeRates.Clear();
                foreach (var er in _coord.Results())
                    ExchangeRates.Add(er);
            });
        }

        public override CommandContent Create()
        {
            return new SimpleContentCommand("exchange rates");
        }

        private double _convertLeft;
        public double ConvertLeft
        {
            get => _convertLeft;
            set => Set(ref _convertLeft, value);
        }

        private double _convertRight;
        public double ConvertRight
        {
            get => _convertRight;
            set => Set(ref _convertRight, value);
        }

        private Asset _assetLeft;
        public Asset AssetLeft
        {
            get => _assetLeft;
            set => Set(ref _assetLeft, value);
        }

        private Asset _assetRight;
        public Asset AssetRight
        {
            get => _assetRight;
            set => Set(ref _assetRight, value);
        }

        private void Go()
        {
            if (AssetRight == null || Equals(AssetRight, Asset.None))
                return;

            if (AssetLeft == null || Equals(AssetLeft, Asset.None))
                return;

            _coord.Messenger.Register<ExchangeRateCollected>(this, NewRate);
            _requests.Add(_coord.AddRequest(new AssetPair(AssetLeft, AssetRight)));
        }

        public BindingList<ExchangeRateCollected> ExchangeRates { get; } = new BindingList<ExchangeRateCollected>();

        public override void Dispose()
        {
            foreach (var r in _requests)
                _coord.RemoveRequest(r);

            _coord.Messenger.Unregister<ExchangeRateCollected>(this, NewRate);

            base.Dispose();
        }
    }
}
