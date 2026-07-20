using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Services;
using ScanProfinet.Views;

namespace ScanProfinet.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly SnapshotRepository _repo;

    [ObservableProperty] private string _statusText = "Pronto.";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isNpcapAvailable;
    [ObservableProperty] private int _selectedInterfaceIndex = -1;
    [ObservableProperty] private ProfinetDevice? _selectedDevice;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private int _scanSeconds = 5;

    // Campos de configuração (Set IP / Name)
    [ObservableProperty] private string _newIp = "";
    [ObservableProperty] private string _newMask = "255.255.255.0";
    [ObservableProperty] private string _newGateway = "0.0.0.0";
    [ObservableProperty] private string _newDeviceName = "";

    public ObservableCollection<NetworkInterfaceInfo> Interfaces { get; } = new();
    public ObservableCollection<ProfinetDevice> Devices { get; } = new();
    public ObservableCollection<ProfinetDevice> FilteredDevices { get; } = new();

    public bool HasDevices => Devices.Count > 0;

    /// <summary>Disparado quando uma rede é salva (para atualizar o painel de redes salvas).</summary>
    public event Action? SnapshotsChanged;

    public ScanViewModel(SnapshotRepository repo)
    {
        _repo = repo;
        RefreshInterfaces();
    }

    public void RefreshInterfaces()
    {
        try
        {
            IsNpcapAvailable = ProfinetDcpService.IsNpcapAvailable();
            if (IsNpcapAvailable)
            {
                Interfaces.Clear();
                foreach (var iface in ProfinetDcpService.GetNetworkInterfaces())
                    Interfaces.Add(iface);
                StatusText = $"{Interfaces.Count} interface(s) de rede disponível(is). Selecione uma e escaneie.";
            }
            else
            {
                StatusText = "Npcap não encontrado — instale para habilitar o scan.";
            }
        }
        catch (Exception ex)
        {
            IsNpcapAvailable = false;
            StatusText = $"Erro ao inicializar rede: {ex.Message}";
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDeviceChanged(ProfinetDevice? value)
    {
        if (value != null)
        {
            NewIp = value.IpAddress;
            NewMask = value.SubnetMask == "0.0.0.0" ? "255.255.255.0" : value.SubnetMask;
            NewGateway = value.Gateway;
            NewDeviceName = value.DeviceName;
        }
    }

    private void ApplyFilter()
    {
        FilteredDevices.Clear();
        var f = FilterText.Trim();
        foreach (var d in Devices)
        {
            if (string.IsNullOrEmpty(f)
                || d.DeviceName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || d.IpAddress.Contains(f, StringComparison.OrdinalIgnoreCase)
                || d.MacAddress.Contains(f, StringComparison.OrdinalIgnoreCase)
                || d.DeviceVendor.Contains(f, StringComparison.OrdinalIgnoreCase)
                || d.DeviceRole.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                FilteredDevices.Add(d);
            }
        }
    }

    [RelayCommand]
    private async Task ScanNetwork()
    {
        if (SelectedInterfaceIndex < 0)
        {
            MessageBox.Show("Selecione a placa de rede (interface) conectada à rede PROFINET.",
                "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsScanning = true;
        Devices.Clear();
        FilteredDevices.Clear();
        OnPropertyChanged(nameof(HasDevices));

        try
        {
            var iface = Interfaces[SelectedInterfaceIndex];
            var progress = new Progress<string>(m => StatusText = m);
            var found = await ProfinetDcpService.DiscoverAsync(iface.Index, ScanSeconds * 1000, progress);

            foreach (var d in found) Devices.Add(d);
            ApplyFilter();
            OnPropertyChanged(nameof(HasDevices));
            StatusText = found.Count == 0
                ? "Nenhum dispositivo respondeu. Verifique cabo, placa selecionada e alimentação."
                : $"{found.Count} dispositivo(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro no scan: {ex.Message}";
            MessageBox.Show($"Não foi possível escanear a rede:\n\n{ex.Message}",
                "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task SetIp()
    {
        if (SelectedDevice == null || SelectedInterfaceIndex < 0) return;
        if (!System.Net.IPAddress.TryParse(NewIp, out _) ||
            !System.Net.IPAddress.TryParse(NewMask, out _) ||
            !System.Net.IPAddress.TryParse(NewGateway, out _))
        {
            MessageBox.Show("IP, máscara ou gateway inválido.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Atribuir IP {NewIp} ao dispositivo\n'{SelectedDevice.DeviceName}' ({SelectedDevice.MacAddress})?",
                "Confirmar atribuição de IP", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var iface = Interfaces[SelectedInterfaceIndex];
        string oldIp = SelectedDevice.IpAddress, oldMask = SelectedDevice.SubnetMask, oldGw = SelectedDevice.Gateway;
        var progress = new Progress<string>(m => StatusText = m);
        bool ok = await ProfinetDcpService.SetIpAsync(iface.Index, SelectedDevice.MacAddress, NewIp, NewMask, NewGateway, progress);

        AppLog.Readdress($"[SET IP]  {(ok ? "OK " : "FALHA")}  MAC={SelectedDevice.MacAddress}  nome='{SelectedDevice.DeviceName}'  " +
                         $"IP: {oldIp} -> {NewIp}  |  Máscara: {oldMask} -> {NewMask}  |  Gateway: {oldGw} -> {NewGateway}");
        if (ok)
        {
            SelectedDevice.IpAddress = NewIp;
            SelectedDevice.SubnetMask = NewMask;
            SelectedDevice.Gateway = NewGateway;
        }
    }

    [RelayCommand]
    private async Task SetName()
    {
        if (SelectedDevice == null || SelectedInterfaceIndex < 0 || string.IsNullOrWhiteSpace(NewDeviceName)) return;

        if (MessageBox.Show(
                $"Atribuir o nome '{NewDeviceName.ToLowerInvariant()}' ao dispositivo {SelectedDevice.MacAddress}\n(atual: '{SelectedDevice.DeviceName}')?",
                "Confirmar nome do dispositivo", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var iface = Interfaces[SelectedInterfaceIndex];
        string oldName = SelectedDevice.DeviceName;
        var progress = new Progress<string>(m => StatusText = m);
        bool ok = await ProfinetDcpService.SetDeviceNameAsync(iface.Index, SelectedDevice.MacAddress, NewDeviceName, progress);

        AppLog.Readdress($"[SET NOME]  {(ok ? "OK " : "FALHA")}  MAC={SelectedDevice.MacAddress}  " +
                         $"IP={SelectedDevice.IpAddress}  Nome: '{oldName}' -> '{NewDeviceName.ToLowerInvariant()}'");
        if (ok) SelectedDevice.DeviceName = NewDeviceName.ToLowerInvariant();
    }

    [RelayCommand]
    private async Task BlinkDevice()
    {
        if (SelectedDevice == null || SelectedInterfaceIndex < 0) return;
        var iface = Interfaces[SelectedInterfaceIndex];
        var progress = new Progress<string>(m => StatusText = m);
        await ProfinetDcpService.BlinkAsync(iface.Index, SelectedDevice.MacAddress, progress);
    }

    [RelayCommand]
    private void SaveSnapshot()
    {
        if (Devices.Count == 0)
        {
            MessageBox.Show("Escaneie a rede antes de salvar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveSnapshotDialog { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;

        var name = dlg.SnapshotName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_repo.NameExists(name) &&
            MessageBox.Show($"Já existe uma rede salva com o nome '{name}'.\nDeseja salvar mesmo assim (cria uma nova versão)?",
                "Nome já existe", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            _repo.SaveSnapshot(name, dlg.Notes, Devices);
            StatusText = $"Rede salva como '{name}' ({Devices.Count} dispositivos).";
            SnapshotsChanged?.Invoke();
            MessageBox.Show($"Rede '{name}' salva com sucesso.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao salvar snapshot", ex);
            MessageBox.Show($"Erro ao salvar a rede:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
