using System.ComponentModel;
using System.Globalization;
using Common.Localization.Languages;

namespace Common.Localization;

public sealed class Loc : INotifyPropertyChanged
{
    public static IReadOnlyList<LocalizedStrings> Available { get; } =
    [
        new Korean(),
        new English(),
        new Japanese(),
        new ChineseSimplified(),
        new ChineseTraditional(),
        new German(),
        new French(),
        new Spanish(),
        new Russian(),
    ];

    public static Loc Instance { get; } = new();

    public static LocalizedStrings S => Instance.Current;

    public LocalizedStrings Current { get; private set; } = GetInitialLanguage();
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Set(string? code)
    {
        var found = Available.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Available[0];

        if (ReferenceEquals(found, Current))
            return;

        Current = found;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
    }

    private static LocalizedStrings GetInitialLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;

        var matched = Available.FirstOrDefault(x => string.Equals(x.Code, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
            return matched;

        return Available.FirstOrDefault(x => string.Equals(x.Code, culture.Name, StringComparison.OrdinalIgnoreCase)) ?? Available[0];
    }
}