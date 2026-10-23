namespace SistemaCobranzaApp.Domain.Entidades;

public class Empresa
{ 
    // Datos Identificativos y Fiscales
    public int Id { get; set; }
    public string RazonSocial { get; set; } 
    public string NombreComercial { get; set; } 
    public string NombreCorto { get; set; } // Ej: "TechSol" o "TS"
    public string Rfc_o_Nit { get; set; } 
    public string RegimenFiscal { get; set; }
    public int AnioFundacion { get; set; } // Ej: 2018 (Útil para el pie de página)

    // Identidad de Marca
    public string Lema { get; set; } // Ej: "Innovación a tu alcance"
    public string LogoUrl { get; set; } 
    public string LogoBlancoUrl { get; set; } // Para usar sobre fondos oscuros

    // Información de Contacto
    public string TelefonoPrincipal { get; set; } 
    public string TelefonoSecundario { get; set; }
    public string CorreoContacto { get; set; } 
    public string CorreoSoporte { get; set; } 
    public string SitioWeb { get; set; } 

    // Redes Sociales
    public string FacebookUrl { get; set; } 
    public string InstagramUrl { get; set; }
    public string LinkedinUrl { get; set; } 

    // Dirección Física
    public string Calle { get; set; }
    public string NumeroExterior { get; set; } 
    public string NumeroInterior { get; set; } 
    public string Colonia { get; set; } 
    public string CodigoPostal { get; set; } 
    public string Ciudad { get; set; } 
    public string Estado_o_Provincia { get; set; } 
    public string Pais { get; set; }

    // Configuración de Operación
    public string MonedaPorDefecto { get; set; } // Ej: "MXN", "USD"
    public string SimboloMoneda { get; set; } // Ej: "$"
    public decimal PorcentajeImpuesto { get; set; } 
    public string HorarioAtencion { get; set; } 
}