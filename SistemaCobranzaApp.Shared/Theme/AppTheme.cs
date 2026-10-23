using MudBlazor;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Theme;

public static class AppTheme
{
    public static string NombreAplicacion { get; } = "Nombre comercial";
    public static string TextoEmpresa { get; } = "Nombre comercial";
    public static string Version { get; } = "1.0.0";
    public static string IconoLogin { get; } = Icons.Material.Filled.AccountBalanceWallet;
    // Imagen fija de respaldo cuando no existe logo en la base; null utiliza IconoLogin.
    public static string? LogoLoginUrl { get; } = null;

    // La identidad se recibe por instancia: no compartir datos de empresa entre sesiones web.
    public static string ObtenerNombre(IdentidadEmpresaDto? empresa, bool corto = false)
        => (corto && !string.IsNullOrWhiteSpace(empresa?.NombreCorto))
            ? empresa.NombreCorto.Trim()
            : !string.IsNullOrWhiteSpace(empresa?.NombreComercial) ? empresa.NombreComercial.Trim() : NombreAplicacion;

    public static string? ObtenerLogo(IdentidadEmpresaDto? empresa, bool oscuro = false)
        => oscuro && !string.IsNullOrWhiteSpace(empresa?.LogoBlancoUrl) ? empresa.LogoBlancoUrl
            : !string.IsNullOrWhiteSpace(empresa?.LogoUrl) ? empresa.LogoUrl : LogoLoginUrl;

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2F7D5C",
            PrimaryDarken = "#256A4D",
            PrimaryLighten = "#4A9A76",

            Secondary = "#C99A3E",
            SecondaryDarken = "#B5862F",
            SecondaryLighten = "#DAB76B",

            // Colores de estado optimizados para alta legibilidad en pantallas móviles
            Success = "#2E7D32", // Verde más robusto para marcar "Pagado" o "Al corriente"
            Warning = "#D29A32",
            Error = "#C62828",   // Rojo más visible para "Moroso" o "Vencido" bajo la luz del sol
            Info = "#1976D2",    // Azul estándar accesible

            Background = "#F7F8F6",
            Surface = "#FFFFFF",

            TextPrimary = "#26332D",
            TextSecondary = "#6B756F",

            ActionDefault = "#6B756F",
            ActionDisabled = "#B7C0BB",

            Divider = "#DDE4DF",
            DividerLight = "#EBEFEC",

            TableLines = "#E1E6E2",
            LinesDefault = "#DDE4DF",
            LinesInputs = "#CCD5D0",

            DrawerBackground = "#2F7D5C",
            DrawerText = "#FFFFFF",
            DrawerIcon = "#FFFFFF",

            AppbarBackground = "#FFFFFF",
            AppbarText = "#26332D"
        },

        PaletteDark = new PaletteDark
        {
            Primary = "#4A9A76",
            PrimaryDarken = "#2F7D5C",
            PrimaryLighten = "#65AD8B",

            Secondary = "#DAB76B",
            SecondaryDarken = "#C99A3E",
            SecondaryLighten = "#E5CA8E",

            // Tonos pastel con mayor contraste sobre fondos oscuros (Evita la fatiga visual)
            Success = "#66BB6A", // Más encendido para que resalte sobre el gris oscuro
            Warning = "#FFA726",
            Error = "#EF5350",   // Más claro para que no se pierda en el fondo de la pantalla OLED
            Info = "#29B6F6",

            Background = "#1D2420",
            Surface = "#252D28",

            TextPrimary = "#F3F6F4",
            TextSecondary = "#B8C2BC",

            ActionDefault = "#B8C2BC",
            ActionDisabled = "#68736D",

            Divider = "#3B4640",
            DividerLight = "#323C37",

            TableLines = "#3B4640",
            LinesDefault = "#3B4640",
            LinesInputs = "#4B5750",

            DrawerBackground = "#256A4D",
            DrawerText = "#FFFFFF",
            DrawerIcon = "#FFFFFF",

            AppbarBackground = "#252D28",
            AppbarText = "#F3F6F4"
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px", // Esquinas más redondeadas al estilo Android moderno (Material You)
            DrawerWidthLeft = "280px",    // Ancho optimizado para navegación cómoda con el pulgar
            AppbarHeight = "56px"         // Estándar oficial de Android móvil para aprovechar espacio vertical
        }
    };
}

