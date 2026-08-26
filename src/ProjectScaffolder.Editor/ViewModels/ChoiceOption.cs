namespace ProjectScaffolder.Editor.ViewModels;

internal sealed record ChoiceOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
