namespace Mud9Bot.Models;

public class GasPriceType
{
    public string Tc { get; set; } = string.Empty;
}

public class GasPriceVendor
{
    public string Tc { get; set; } = string.Empty;
}

public class GasPriceEntry
{
    public GasPriceVendor Vendor { get; set; } = new();
    public string Price { get; set; } = string.Empty;
}

public class GasPriceData
{
    public GasPriceType Type { get; set; } = new();
    public List<GasPriceEntry> Prices { get; set; } = new();
}