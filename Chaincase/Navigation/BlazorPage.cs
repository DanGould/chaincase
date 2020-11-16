using System;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.MobileBlazorBindings;
using ReactiveUI;
using ReactiveUI.Blazor;
using ReactiveUI.XamForms;
using Xamarin.Forms;

namespace Chaincase.Navigation
{
	public class BlazorPage<TComponent> : Page
		where TComponent : IComponent
	{
		public void InitializeComponent(IHost host) =>
			host.AddComponent<TComponent>(parent: this);
	}
}

