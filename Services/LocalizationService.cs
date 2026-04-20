using System.Globalization;
using System.Reflection;
using System.Resources;

namespace VortexModLists.Services;

public static class LocalizationService
{
    private static readonly ResourceManager ResourceManager = new("VortexModLists.Strings", Assembly.GetExecutingAssembly());

    public static void InitializeCulture()
    {
        var culture = CultureInfo.CurrentUICulture;
        if (!string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase))
        {
            culture = CultureInfo.GetCultureInfo("en");
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en"))
            ?? key;
    }
}
