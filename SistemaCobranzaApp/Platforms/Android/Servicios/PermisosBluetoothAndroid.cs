#if ANDROID

using Android;
using Microsoft.Maui.ApplicationModel;

namespace SistemaCobranzaApp.Servicios;

public static class PermisosBluetoothAndroid
{
    public static async Task<bool> SolicitarAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var connect =
                await Permissions.CheckStatusAsync<BluetoothConnectPermission>();

            if (connect != PermissionStatus.Granted)
            {
                connect =
                    await Permissions.RequestAsync<BluetoothConnectPermission>();

                if (connect != PermissionStatus.Granted)
                    return false;
            }

            var scan =
                await Permissions.CheckStatusAsync<BluetoothScanPermission>();

            if (scan != PermissionStatus.Granted)
            {
                scan =
                    await Permissions.RequestAsync<BluetoothScanPermission>();

                if (scan != PermissionStatus.Granted)
                    return false;
            }
        }

        return true;
    }

    // CORREGIDO: Sintaxis de .NET MAUI moderna usando arreglos de strings
    private sealed class BluetoothConnectPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { (Manifest.Permission.BluetoothConnect, true) };
    }

    private sealed class BluetoothScanPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { (Manifest.Permission.BluetoothScan, true) };
    }
}

#endif