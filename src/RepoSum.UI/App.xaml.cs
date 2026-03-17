using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RepoSum.Application;
using RepoSum.Infrastructure;
using RepoSum.UI.ViewModels;
using Serilog;

namespace RepoSum.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private IHost? _host;

	private async void App_OnStartup(object sender, StartupEventArgs e)
	{
		_host = Host.CreateDefaultBuilder(e.Args)
			.ConfigureAppConfiguration(cfg =>
			{
				cfg.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
				cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			})
			.UseSerilog((context, services, loggerConfig) =>
				loggerConfig.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services))
			.ConfigureServices(services =>
			{
				services.AddRepoSumApplication();
				services.AddRepoSumInfrastructure();

				services.AddSingleton<MainViewModel>();
				services.AddSingleton<MainWindow>();
			})
			.Build();

		await _host.StartAsync();

		var mainWindow = _host.Services.GetRequiredService<MainWindow>();
		mainWindow.Show();
	}

	private async void App_OnExit(object sender, ExitEventArgs e)
	{
		if (_host is null)
		{
			return;
		}

		try
		{
			await _host.StopAsync(TimeSpan.FromSeconds(3));
		}
		finally
		{
			_host.Dispose();
			_host = null;
			Log.CloseAndFlush();
		}
	}
}

