﻿using Gma.QrCodeNet.Encoding;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;
using Wasabi.Navigation;

namespace Wasabi.ViewModels
{
	public class AddressViewModel : ViewModelBase
	{
		private bool[,] _qrCode;

		public string Label => Model.Label;
		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();
		public string Pubkey => Model.PubKey.ToString();
		public string KeyPath => Model.FullKeyPath.ToString();
		public bool[,] QrCode
		{
			get => _qrCode;
			set
			{
				_qrCode = value;
				RaisePropertyChanged(() => _qrCode);
			}
		}
		public HdPubKey Model { get; }

		public AddressViewModel(INavigationService navigationService, HdPubKey model) : base(navigationService)
		{
			Model = model;

			// TODO fix this performance issue this should only be generated when accessed.
			Task.Run(() =>
			{
				var encoder = new QrEncoder(ErrorCorrectionLevel.M);
				encoder.TryEncode(Address, out var qrCode);

				return qrCode.Matrix.InternalArray;
			}).ContinueWith(x =>
			{
				QrCode = x.Result;
			});
		}
	}
}