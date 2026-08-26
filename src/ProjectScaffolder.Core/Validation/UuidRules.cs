namespace ProjectScaffolder.Core.Validation;

public static class UuidRules
{
    public static bool IsVersion7(Guid value)
    {
        string text = value.ToString("D");
        return text[14] == '7' && text[19] is '8' or '9' or 'a' or 'b';
    }
}
