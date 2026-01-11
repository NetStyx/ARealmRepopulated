using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ARealmRepopulated.Core.l10n;

public class ArrpTranslation {

    public event Action OnLocalizationChanged = null!;

    private readonly Dictionary<string, string> _cache = [];

    private readonly ResourceManager _rm = new("ARealmRepopulated.Data.l10n.ArrpContent", Assembly.GetExecutingAssembly());
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    public void SetLocale(CultureInfo info) {

        _culture = info;
        _cache.Clear();
        OnLocalizationChanged?.Invoke();
    }

    public string this[string key, params object[] obj] {
        get {
            if (!_cache.TryGetValue(key, out var value)) {
                value = _rm.GetString(key, _culture)!;
                _cache[key] = value;
            }

            if (obj != null && obj.Length > 0) {
                return string.Format(value, obj);
            } else {
                return value;
            }
        }
    }
}
